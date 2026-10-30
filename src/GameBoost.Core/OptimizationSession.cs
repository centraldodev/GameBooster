namespace GameBoost.Core;

// Persist the original value BEFORE each mutation; pending entries remain recoverable after a crash.
public sealed class OptimizationSession(IEnumerable<IOptimization> optimizations, ISnapshotStore store, IRestorePointService? restorePoints = null)
{
    private readonly Dictionary<string, IOptimization> catalog = optimizations.ToDictionary(x => x.Info.Id);
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<Snapshot> ApplyAsync(GameProfile profile)
    {
        await gate.WaitAsync();
        try
        {
            if ((await store.LoadAsync()).Any(x => x.Status == SnapshotStatus.Pending))
                throw new InvalidOperationException("Restaure a sessão pendente antes de ativar outra.");
            var actions = profile.OptimizationIds.Distinct().Select(id => catalog.TryGetValue(id, out var action)
                ? action : throw new InvalidOperationException($"Ajuste desconhecido: {id}")).ToList();
            if (actions.Count == 0) throw new InvalidOperationException("Selecione ao menos um ajuste.");
            if (profile.HasAntiCheat && actions.Any(x => !x.Info.SafeWithAntiCheat))
                throw new InvalidOperationException("Um ajuste selecionado não está disponível no Modo Seguro.");
            foreach (var action in actions)
                if (action is IProfileOptimization configurable) configurable.Configure(profile);
            var snapshot = new Snapshot { GameName = profile.Name, ProfileId = profile.Id };
            snapshot.Events.Add(new(DateTimeOffset.UtcNow, "created", "", "Sessão criada antes das alterações."));
            await store.SaveAsync(snapshot);
            try
            {
                if (profile.CreateRestorePoint && restorePoints is not null)
                {
                    var result = await restorePoints.CreateAsync($"GameBoost - {profile.Name}");
                    snapshot.RestorePointMessage = result.Message;
                    snapshot.Events.Add(new(DateTimeOffset.UtcNow, "restore-point", "", result.Message));
                    await store.SaveAsync(snapshot);
                }
                foreach (var action in actions)
                {
                    var original = await action.CaptureAsync();
                    snapshot.Entries.Add(new() { OptimizationId = action.Info.Id, Original = original });
                    await store.SaveAsync(snapshot);
                    await action.ApplyAsync();
                    snapshot.Events.Add(new(DateTimeOffset.UtcNow, "applied", action.Info.Id, "Ajuste aplicado."));
                    await store.SaveAsync(snapshot);
                }
                return snapshot;
            }
            catch (Exception applyError)
            {
                snapshot.Events.Add(new(DateTimeOffset.UtcNow, "apply-failed", "", applyError.Message));
                try { await RestoreInternalAsync(snapshot); }
                catch (Exception restoreError) { throw new AggregateException("Falha na aplicação e na reversão. O snapshot foi preservado para nova tentativa.", applyError, restoreError); }
                throw;
            }
        }
        finally { gate.Release(); }
    }

    public async Task RestoreAllAsync()
    {
        await gate.WaitAsync();
        try
        {
            var failures = new List<Exception>();
            foreach (var snapshot in (await store.LoadAsync()).Where(x => x.Status == SnapshotStatus.Pending).OrderByDescending(x => x.CreatedAt))
                try { await RestoreInternalAsync(snapshot); } catch (Exception ex) { failures.Add(ex); }
            if (failures.Count > 0) throw new AggregateException("Alguns ajustes não puderam ser restaurados. Tente novamente.", failures);
        }
        finally { gate.Release(); }
    }

    private async Task RestoreInternalAsync(Snapshot snapshot)
    {
        var failures = new List<Exception>();
        foreach (var entry in snapshot.Entries.AsEnumerable().Reverse().Where(x => !x.Restored))
        {
            try
            {
                if (!catalog.TryGetValue(entry.OptimizationId, out var action)) throw new InvalidOperationException($"Ajuste indisponível: {entry.OptimizationId}");
                await action.RevertAsync(entry.Original);
                entry.Restored = true;
                snapshot.Events.Add(new(DateTimeOffset.UtcNow, "restored", entry.OptimizationId, "Valor original restaurado."));
                await store.SaveAsync(snapshot);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
                snapshot.Events.Add(new(DateTimeOffset.UtcNow, "restore-failed", entry.OptimizationId, ex.Message));
                try { await store.SaveAsync(snapshot); } catch (Exception saveError) { failures.Add(saveError); }
            }
        }
        if (failures.Count > 0) throw new AggregateException(failures);
        snapshot.Status = SnapshotStatus.Restored;
        await store.SaveAsync(snapshot);
    }
}

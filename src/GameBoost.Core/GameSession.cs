namespace GameBoost.Core;

// Observe the configured executable through the OS management layer; never inspect game memory.
public sealed record GameWatchOptions(TimeSpan PollInterval, TimeSpan StartupTimeout, int MissingSamples)
{
    public static GameWatchOptions Default { get; } = new(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(2), 4);
}
public sealed class GameSession(OptimizationSession optimizations, IGameRuntime runtime, GameWatchOptions? options = null)
{
    private readonly GameWatchOptions watchOptions = options ?? GameWatchOptions.Default;
    public string Executable { get; private set; } = "";
    public bool Active { get; private set; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public async Task StartAsync(GameProfile profile)
    {
        if (Active) throw new InvalidOperationException("Já há um jogo sendo acompanhado.");
        var monitored = string.IsNullOrWhiteSpace(profile.MonitorExecutablePath) ? profile.ExecutablePath : profile.MonitorExecutablePath;
        if (await runtime.IsRunningAsync(monitored, CancellationToken.None))
            throw new InvalidOperationException("Esse jogo já está em execução. Feche-o antes de iniciar uma sessão controlada.");
        if (watchOptions.PollInterval <= TimeSpan.Zero || watchOptions.StartupTimeout <= TimeSpan.Zero || watchOptions.MissingSamples < 1) throw new ArgumentException("Configuração de acompanhamento inválida.");
        Active = true; Executable = monitored;
        var applied = false;
        try
        {
            if (profile.OptimizationIds.Count > 0) { await optimizations.ApplyAsync(profile); applied = true; }
            await runtime.LaunchAsync(profile.ExecutablePath, profile.Arguments);
            Completion = WatchAsync(monitored, applied && profile.AutoRestore);
        }
        catch (Exception launchError)
        {
            Active = false;
            if (applied)
                try { await optimizations.RestoreAllAsync(); }
                catch (Exception restoreError) { throw new AggregateException("Falha ao iniciar e ao restaurar a sessão.", launchError, restoreError); }
            throw;
        }
    }
    private async Task WatchAsync(string executable, bool restore)
    {
        var failed = false;
        try
        {
            var deadline = DateTimeOffset.UtcNow + watchOptions.StartupTimeout;
            while (!await runtime.IsRunningAsync(executable, CancellationToken.None))
            {
                if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException("O executável monitorado não apareceu em 2 minutos. Confira o caminho do processo real do jogo.");
                await Task.Delay(watchOptions.PollInterval);
            }
            // A short gap can occur while the launcher restarts the executable.
            var missing = 0;
            while (missing < watchOptions.MissingSamples)
            {
                await Task.Delay(watchOptions.PollInterval);
                missing = await runtime.IsRunningAsync(executable, CancellationToken.None) ? 0 : missing + 1;
            }
        }
        catch { failed = true; throw; }
        finally
        {
            try { if (restore || failed) await optimizations.RestoreAllAsync(); }
            finally { Active = false; }
        }
    }
}

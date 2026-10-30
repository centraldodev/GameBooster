using System.IO;
using System.Net;
using System.Windows;
using GameBoost.Core;
using GameBoost.Data;
using GameBoost.Monitoring;
using Microsoft.Win32;
namespace GameBoost.UI.ViewModels;

public sealed partial class MainViewModel
{
    private BenchmarkRun? baseline, optimized;
    private CancellationTokenSource? benchmarkCancellation;
    private string benchmarkStatus = "Salve o perfil. Meça 60 s sem ajustes, ative os ajustes e repita a mesma cena por 60 s.";
    public string BenchmarkStatus { get => benchmarkStatus; private set => Set(ref benchmarkStatus, value); }
    public AsyncCommand BeforeCommand { get; private set; } = null!;
    public AsyncCommand AfterCommand { get; private set; } = null!;
    public AsyncCommand ExportCommand { get; private set; } = null!;
    public AsyncCommand ImportBeforeFpsCommand { get; private set; } = null!;
    public AsyncCommand ImportAfterFpsCommand { get; private set; } = null!;
    public AsyncCommand CancelBenchmarkCommand { get; private set; } = null!;
    private void InitializeBenchmark()
    {
        BeforeCommand = Feature(() => CaptureBenchmarkAsync(false), () => !HasPending);
        AfterCommand = Feature(() => CaptureBenchmarkAsync(true), () => HasPending && baseline is not null);
        ExportCommand = Feature(ExportBenchmarkAsync, () => baseline is not null && optimized is not null);
        ImportBeforeFpsCommand = Feature(() => ImportFramesAsync(false), () => baseline is not null);
        ImportAfterFpsCommand = Feature(() => ImportFramesAsync(true), () => optimized is not null);
        CancelBenchmarkCommand = new(() => { benchmarkCancellation?.Cancel(); return Task.CompletedTask; }, () => benchmarkCancellation is not null);
        commands.Add(CancelBenchmarkCommand);
    }
    private async Task CaptureBenchmarkAsync(bool after)
    {
        if (SelectedProfile is null) throw new InvalidOperationException("Salve e selecione um perfil antes de medir.");
        var profile = ReadProfile();
        if (!IPAddress.TryParse(Target, out _)) throw new InvalidOperationException("Informe um IP de destino na aba Rede.");
        var active = (await snapshots.LoadAsync()).SingleOrDefault(x => x.Status == SnapshotStatus.Pending);
        if (after && (active is null || active.ProfileId != profile.Id || active.Entries.Any(x => x.Restored))) throw new InvalidOperationException("A sessão ativa deve ser do mesmo perfil selecionado.");
        if (!after && active is not null) throw new InvalidOperationException("Restaure todos os ajustes antes da medição base.");
        if (after && (baseline!.ProfileId != profile.Id || baseline.Target != Target || baseline.Executable != EffectiveExecutable)) throw new InvalidOperationException("Mantenha perfil, executável e destino iguais aos da medição base.");
        if (!await new GameBoost.Windows.WindowsGameRuntime(shell).IsRunningAsync(EffectiveExecutable, CancellationToken.None)) throw new InvalidOperationException("Inicie o executável real do jogo antes de medir.");
        if (MessageBox.Show("A coleta começa ao confirmar e dura 60 segundos. Deixe o jogo na mesma cena e volte para ele durante a coleta. CPU/RAM são do sistema; FPS requer CSV do PresentMon.", "Preparar benchmark", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        benchmarkCancellation = new(); CancelBenchmarkCommand.Refresh();
        var measuredTarget = Target; var measuredExecutable = EffectiveExecutable;
        try
        {
            var progress = new Progress<int>(value => { if (benchmarkCancellation is not null) BenchmarkStatus = $"Coleta {(after ? "depois" : "antes")}: {value}%"; });
            var result = await new BenchmarkRecorder(new WindowsMetrics()).RecordAsync(profile.Id, profile.Name, measuredExecutable, measuredTarget, active?.Entries.Select(x => x.OptimizationId).ToArray() ?? [], TimeSpan.FromSeconds(60), progress, benchmarkCancellation.Token);
            var end = (await snapshots.LoadAsync()).SingleOrDefault(x => x.Status == SnapshotStatus.Pending);
            if (!await new GameBoost.Windows.WindowsGameRuntime(shell).IsRunningAsync(measuredExecutable, CancellationToken.None)) throw new InvalidOperationException("O jogo encerrou durante a coleta. Repita a medição.");
            if (end?.Id != active?.Id || (end?.Entries.Any(x => x.Restored) ?? false)) throw new InvalidOperationException("A sessão mudou durante a coleta. Repita a medição.");
            if (after) { BenchmarkReport.Validate(baseline!, result); optimized = result; }
            else { baseline = result; optimized = null; }
            BenchmarkStatus = $"Coleta {(after ? "depois" : "antes")} concluída: {result.Samples.Count} amostras, CPU {result.AverageCpu:F1}%, RAM disponível {result.AverageAvailableRamMb:F0} MB, ping {result.Network.AverageMs:F1} ms. " + (after ? "Pronto para exportar relatório." : "Agora ative os ajustes e repita a coleta.");
        }
        catch (OperationCanceledException) { BenchmarkStatus = "Coleta cancelada. Amostras parciais descartadas."; }
        finally { benchmarkCancellation.Dispose(); benchmarkCancellation = null; CancelBenchmarkCommand.Refresh(); }
    }
    private Task ImportFramesAsync(bool after)
    {
        var run = after ? optimized! : baseline!;
        var dialog = new OpenFileDialog { Filter = "PresentMon CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog() == true)
        {
            run.Frames = PresentMonImport.Read(dialog.FileName, run.Executable);
            BenchmarkStatus = $"CSV {(after ? "depois" : "antes")}: {run.Frames.AverageFps:F1} FPS médio · {run.Frames.OnePercentLowFps:F1} FPS 1% low. Importação manual; confira se a captura corresponde à sessão correta.";
        }
        return Task.CompletedTask;
    }
    private async Task ExportBenchmarkAsync()
    {
        var report = BenchmarkReport.Markdown(baseline!, optimized!);
        var dialog = new SaveFileDialog { Filter = "Relatório Markdown (*.md)|*.md", FileName = "GameBoost-benchmark.md" };
        if (dialog.ShowDialog() != true) return;
        await File.WriteAllTextAsync(dialog.FileName, report);
        await AtomicJson.WriteAsync(Path.ChangeExtension(dialog.FileName, ".json"), new { Before = baseline, After = optimized });
        BenchmarkStatus = "Relatório Markdown e dados JSON exportados.";
    }
}

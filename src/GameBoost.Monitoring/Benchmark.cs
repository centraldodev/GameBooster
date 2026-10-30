using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualBasic.FileIO;
namespace GameBoost.Monitoring;

public sealed record FrameStatistics(int Frames, double AverageFps, double OnePercentLowFps, double DurationSeconds);
public sealed record BenchmarkRun(Guid ProfileId, string Game, string Executable, string Target, DateTimeOffset StartedAt, double DurationSeconds, List<SystemSample> Samples, string[] Actions)
{
    public FrameStatistics? Frames { get; set; }
    public double? AverageCpu => Samples.Where(x => x.CpuPercent.HasValue).Select(x => x.CpuPercent).DefaultIfEmpty().Average();
    public double? AverageAvailableRamMb => Samples.Count > 0 ? Samples.Average(x => x.AvailableRamMb) : null;
    public NetworkSummary Network => NetworkStatistics.Calculate(Samples.Select(x => x.PingMs).ToArray());
}
public sealed class BenchmarkRecorder(ISystemMetrics metrics)
{
    public async Task<BenchmarkRun> RecordAsync(Guid profile, string game, string executable, string target, string[] actions, TimeSpan duration, IProgress<int>? progress, CancellationToken token)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        token.ThrowIfCancellationRequested();
        var samples = new List<SystemSample>(); var watch = Stopwatch.StartNew();
        await metrics.SampleAsync(target); // warm up CPU deltas, outside the recorded interval
        var started = DateTimeOffset.UtcNow;
        watch.Restart();
        while (watch.Elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            samples.Add(await metrics.SampleAsync(target));
            progress?.Report(Math.Min(100, (int)(100 * watch.Elapsed.TotalSeconds / duration.TotalSeconds)));
            var remaining = duration - watch.Elapsed;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), token);
        }
        token.ThrowIfCancellationRequested();
        return new(profile, game, executable, target, started, watch.Elapsed.TotalSeconds, samples, actions);
    }
}
public static class PresentMonImport
{
    // Supports exported PresentMon CSV with MsBetweenPresents. Does not hook or inject into the game.
    public static FrameStatistics Read(string path, string executable)
    {
        using var parser = new TextFieldParser(path) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("CSV vazio.");
        var frameColumn = Array.FindIndex(header, x => x.Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase));
        var applicationColumn = Array.FindIndex(header, x => x.Equals("Application", StringComparison.OrdinalIgnoreCase));
        if (frameColumn < 0 || applicationColumn < 0) throw new InvalidDataException("CSV precisa das colunas Application e MsBetweenPresents do PresentMon.");
        var processColumn = Array.FindIndex(header, x => x.Equals("ProcessID", StringComparison.OrdinalIgnoreCase));
        var swapColumn = Array.FindIndex(header, x => x.Equals("SwapChainAddress", StringComparison.OrdinalIgnoreCase));
        var streams = new HashSet<string>();
        var frames = new List<double>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields()!;
            if (row.Length <= Math.Max(frameColumn, applicationColumn)) continue;
            if (!Path.GetFileName(row[applicationColumn].Replace('\\', '/')).Equals(Path.GetFileName(executable.Replace('\\', '/')), StringComparison.OrdinalIgnoreCase)) continue;
            if (double.TryParse(row[frameColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) && double.IsFinite(ms) && ms > 0)
            {
                var process = processColumn >= 0 && processColumn < row.Length ? row[processColumn] : "";
                var swap = swapColumn >= 0 && swapColumn < row.Length ? row[swapColumn] : "";
                streams.Add(process + ":" + swap);
                frames.Add(ms);
            }
        }
        if (streams.Count > 1) throw new InvalidDataException("O CSV contém múltiplos processos/swapchains. Filtre uma única sessão e swapchain antes de importar.");
        if (frames.Count < 100) throw new InvalidDataException("São necessários pelo menos 100 frametimes válidos do executável escolhido.");
        var slowest = frames.OrderByDescending(x => x).Take((int)Math.Ceiling(frames.Count * 0.01));
        return new(frames.Count, 1000 / frames.Average(), 1000 / slowest.Average(), frames.Sum() / 1000);
    }
}
public static class BenchmarkReport
{
    public static void Validate(BenchmarkRun before, BenchmarkRun after)
    {
        if (before.ProfileId != after.ProfileId || before.Executable != after.Executable || before.Target != after.Target) throw new InvalidOperationException("As medições devem usar o mesmo perfil, executável e destino de ping.");
        if (before.Actions.Length != 0 || after.Actions.Length == 0) throw new InvalidOperationException("A base deve estar sem ajustes e a comparação deve ter uma sessão ativa.");
    }
    public static string Markdown(BenchmarkRun before, BenchmarkRun after)
    {
        Validate(before, after);
        static string F(double? value) => value?.ToString("F2", CultureInfo.InvariantCulture) ?? "indisponível";
        var fps = before.Frames is not null && after.Frames is not null
            ? $"| FPS médio (CSV importado) | {F(before.Frames.AverageFps)} | {F(after.Frames.AverageFps)} |\n| 1% low (CSV importado) | {F(before.Frames.OnePercentLowFps)} | {F(after.Frames.OnePercentLowFps)} |\n"
            : "";
        return $"# Benchmark GameBoost\n\nJogo: {before.Game.Replace("\n", " ")}\n\nInícios (UTC): {before.StartedAt:O} / {after.StartedAt:O}\n\nDuração real: {F(before.DurationSeconds)} s / {F(after.DurationSeconds)} s. Amostras: {before.Samples.Count} / {after.Samples.Count}.\n\nDestino ICMP: {before.Target}\n\n| Métrica | Antes | Depois |\n| --- | ---: | ---: |\n| CPU do sistema (%) | {F(before.AverageCpu)} | {F(after.AverageCpu)} |\n| RAM disponível (MB) | {F(before.AverageAvailableRamMb)} | {F(after.AverageAvailableRamMb)} |\n| Ping médio (ms) | {F(before.Network.AverageMs)} | {F(after.Network.AverageMs)} |\n| Jitter ICMP (ms) | {F(before.Network.JitterMs)} | {F(after.Network.JitterMs)} |\n| Perda ICMP (%) | {F(before.Network.LossPercent)} | {F(after.Network.LossPercent)} |\n{fps}\nAções: {string.Join(", ", after.Actions)}\n\nCPU e RAM são do sistema inteiro, não do processo do jogo. Menor CPU não implica maior FPS. ICMP não equivale à latência do protocolo do jogo. Repita a mesma cena e carga de trabalho; o aplicativo não garante equivalência das sessões. FPS/1% low, quando presentes, são derivados de CSV externo importado manualmente, sem sincronização automática com a janela de coleta.\n";
    }
}

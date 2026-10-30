using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using GameBoost.Core;
namespace GameBoost.Optimizers;

[SupportedOSPlatform("windows")]
public sealed class PowerPlanOptimization : IOptimization
{
    public OptimizationInfo Info { get; } = new("power", "Plano de energia", "Ativa Alto Desempenho. Pode aumentar consumo, calor e ruído. O plano anterior será restaurado.", RiskLevel.Optional, true);
    public async Task<OriginalValue> CaptureAsync()
    {
        var output = await RunAsync("/getactivescheme");
        var match = Regex.Match(output, @"[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}");
        if (!match.Success) throw new InvalidOperationException("Não foi possível identificar o plano de energia atual.");
        return new("Guid", match.Value);
    }
    public async Task ApplyAsync() => _ = await RunAsync("/setactive", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public async Task RevertAsync(OriginalValue original)
    {
        if (!Guid.TryParse(original.Value, out var id)) throw new InvalidDataException("Plano original inválido.");
        await RunAsync("/setactive", id.ToString());
    }
    private static async Task<string> RunAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powercfg.exe")) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível executar powercfg.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("O plano de energia demorou demais para responder."); }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"O Windows recusou o plano de energia: {error} {output}");
        return output;
    }
}

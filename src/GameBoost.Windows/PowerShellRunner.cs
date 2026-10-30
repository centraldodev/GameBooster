using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using GameBoost.Core;
namespace GameBoost.Windows;

[SupportedOSPlatform("windows")]
public sealed class PowerShellRunner : IPowerShell
{
    public async Task<string> RunAsync(string script, int timeoutSeconds = 45, CancellationToken cancellationToken = default)
    {
        var preamble = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; ";
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-STA", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(preamble + script)) }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell não iniciou.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync();
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException("O Windows demorou demais para responder. Confira os snapshots pendentes.");
        }
        var result = await output; var diagnostic = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException(diagnostic.Trim());
        return result.Trim().TrimStart('\uFEFF');
    }
}

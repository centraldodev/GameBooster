using System.Diagnostics;
using System.Runtime.Versioning;
using GameBoost.Core;
namespace GameBoost.Windows;

public sealed record BackgroundApp(int ProcessId, string Name, string Title, DateTime StartedAt);
[SupportedOSPlatform("windows")]
public sealed class BackgroundApps
{
    public IReadOnlyList<BackgroundApp> List(string gameExecutable)
    {
        var result = new List<BackgroundApp>();
        using var self = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcesses())
            using (process)
                try
                {
                    if (ProtectionPolicy.CanCloseApp(process.ProcessName, gameExecutable) && process.SessionId == self.SessionId && process.MainWindowHandle != IntPtr.Zero)
                        result.Add(new(process.Id, process.ProcessName, process.MainWindowTitle, process.StartTime));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        return result;
    }
    public void RequestClose(BackgroundApp app, string gameExecutable)
    {
        if (!ProtectionPolicy.CanCloseApp(app.Name, gameExecutable)) throw new InvalidOperationException("Aplicativo protegido ou não permitido.");
        using var self = Process.GetCurrentProcess();
        using var process = Process.GetProcessById(app.ProcessId);
        if (!string.Equals(process.ProcessName, app.Name, StringComparison.OrdinalIgnoreCase) || process.StartTime != app.StartedAt || process.SessionId != self.SessionId)
            throw new InvalidOperationException("O processo mudou. Atualize a lista.");
        if (!process.CloseMainWindow()) throw new InvalidOperationException("O aplicativo não aceitou a solicitação de fechamento.");
    }
}

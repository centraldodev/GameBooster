using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Windows;

public sealed class WindowsGameRuntime(IPowerShell shell) : IGameRuntime
{
    public async Task<bool> IsRunningAsync(string executable, CancellationToken token)
    {
        // Query only by executable name, then compare full paths. No game process handles.
        var name = Path.GetFileName(executable).Replace("\\", "\\\\").Replace("'", "\\'");
        var output = await shell.RunAsync($"$p=@(Get-CimInstance Win32_Process -Filter {PowerShellLiteral.Quote("Name='" + name + "'")}); ConvertTo-Json -InputObject @($p | Select-Object ExecutablePath) -Compress", cancellationToken: token);
        return Paths(output).Any(x => string.Equals(x, executable, StringComparison.OrdinalIgnoreCase));
    }
    private static IEnumerable<string> Paths(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToArray() : [doc.RootElement];
        return rows.Select(x => x.GetProperty("ExecutablePath").GetString() ?? "").ToArray();
    }
    public Task LaunchAsync(string executable, string arguments)
    {
        if (!File.Exists(executable) || !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Executável de jogo inválido.");
        // Explorer's desktop shell executes at the user's normal integrity level.
        return shell.RunAsync($"$shell=New-Object -ComObject Shell.Application; $hwnd=0; $desktop=$shell.Windows().FindWindowSW(0,0,8,[ref]$hwnd,1); if($null -eq $desktop){{throw 'Explorer não está disponível para iniciar o jogo sem elevação.'}}; $desktop.Document.Application.ShellExecute({PowerShellLiteral.Quote(executable)},{PowerShellLiteral.Quote(arguments)},{PowerShellLiteral.Quote(Path.GetDirectoryName(executable)!)},'open',1)");
    }
}

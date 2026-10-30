using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Windows;

public sealed record NetworkAdapter(string Id, string Name);
public sealed class NetworkAdapters(IPowerShell shell)
{
    public async Task<IReadOnlyList<NetworkAdapter>> ListAsync()
    {
        var json = await shell.RunAsync("$items=@(Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | ForEach-Object {[pscustomobject]@{Id=$_.InterfaceGuid.ToString();Name=$_.Name}}); ConvertTo-Json -InputObject $items -Compress");
        return JsonSerializer.Deserialize<List<NetworkAdapter>>(json) ?? [];
    }
}

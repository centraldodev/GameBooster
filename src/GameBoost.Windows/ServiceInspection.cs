using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Windows;

public sealed record ServiceListing(string Name, string Status, string DisplayName);
public sealed class ServiceInspection(IPowerShell shell)
{
    public async Task<IReadOnlyList<ServiceListing>> ListAsync()
    {
        var names = string.Join(",", ProtectionPolicy.ManagedServices.Select(PowerShellLiteral.Quote));
        var json = await shell.RunAsync($"$rows=@(foreach($name in @({names})){{$s=Get-Service -Name $name -ErrorAction SilentlyContinue; if($s){{[pscustomobject]@{{Name=$s.Name;Status=$s.Status.ToString();DisplayName=$s.DisplayName}}}}else{{[pscustomobject]@{{Name=$name;Status='Unavailable';DisplayName='Não instalado'}}}}}}); ConvertTo-Json -InputObject $rows -Compress");
        return JsonSerializer.Deserialize<List<ServiceListing>>(json) ?? [];
    }
}

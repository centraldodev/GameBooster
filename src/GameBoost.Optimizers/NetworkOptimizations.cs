using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Optimizers;

public sealed record DnsState(string InterfaceId, bool Automatic, string[] Servers);
public sealed record QosState(string Name, string Executable);
public sealed class DnsOptimization(IPowerShell shell) : IOptimization, IProfileOptimization
{
    private string interfaceId = "";
    private string[] servers = [];
    public OptimizationInfo Info => new("dns", "DNS personalizado (IPv4)", "Altera somente o DNS IPv4 da interface escolhida. Pode afetar nomes internos/VPN. Restaura servidores estáticos ou o modo automático; não promete reduzir o ping do jogo.", RiskLevel.Optional, true);
    public void Configure(GameProfile profile)
    {
        interfaceId = Guid.Parse(profile.NetworkInterfaceId).ToString("B");
        servers = new[] { profile.DnsPrimary, profile.DnsSecondary }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (servers.Length == 0 || servers.Any(x => !IPAddress.TryParse(x, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)) throw new InvalidOperationException("Informe endereços DNS IPv4 válidos.");
    }
    internal static string Adapter(string id) => $"$a=Get-NetAdapter | Where-Object {{$_.InterfaceGuid -eq {PowerShellLiteral.Quote(Guid.Parse(id).ToString())}}}; if(@($a).Count -ne 1){{throw 'Interface de rede não encontrada.'}}; ";
    public async Task<OriginalValue> CaptureAsync()
    {
        var json = await shell.RunAsync(Adapter(interfaceId) + $@"$p=Get-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\' + {PowerShellLiteral.Quote(interfaceId)}); $manual=-not [string]::IsNullOrWhiteSpace($p.NameServer); $addresses=@((Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4).ServerAddresses); if($manual -and $addresses.Count -eq 0){{throw 'DNS estático sem endereços: operação bloqueada.'}}; [pscustomobject]@{{InterfaceId={PowerShellLiteral.Quote(interfaceId)};Automatic=(-not $manual);Servers=$addresses}} | ConvertTo-Json -Compress");
        var state = JsonSerializer.Deserialize<DnsState>(json) ?? throw new InvalidDataException("Não foi possível ler o DNS original.");
        if (!state.Automatic && state.Servers.Length == 0) throw new InvalidDataException("DNS original inválido.");
        return new("DnsV4", JsonSerializer.Serialize(state));
    }
    public async Task ApplyAsync() => await SetAsync(interfaceId, false, servers);
    public async Task RevertAsync(OriginalValue original)
    {
        if (original.Kind != "DnsV4") throw new InvalidDataException("Snapshot DNS inválido.");
        var state = JsonSerializer.Deserialize<DnsState>(original.Value!)!;
        await SetAsync(state.InterfaceId, state.Automatic, state.Servers);
    }
    private Task<string> SetAsync(string id, bool automatic, string[] addresses)
    {
        if (!automatic && (addresses.Length == 0 || addresses.Any(x => !IPAddress.TryParse(x, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork))) throw new InvalidDataException("Servidores DNS inválidos.");
        var verify = automatic
            ? $@"; $p=Get-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\' + {PowerShellLiteral.Quote(Guid.Parse(id).ToString("B"))}); if(-not [string]::IsNullOrWhiteSpace($p.NameServer)){{throw 'O Windows não restaurou o DNS automático.'}}"
            : $"; $actual=@((Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4).ServerAddresses); if(($actual -join ',') -ne {PowerShellLiteral.Quote(string.Join(",", addresses))}){{throw 'O Windows não confirmou os servidores DNS solicitados.'}}";
        return shell.RunAsync(Adapter(id) + "Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 | Set-DnsClientServerAddress " + (automatic ? "-ResetServerAddresses" : "-ServerAddresses @(" + string.Join(",", addresses.Select(PowerShellLiteral.Quote)) + ")") + verify);
    }
}
public sealed class QosOptimization(IPowerShell shell) : IOptimization, IProfileOptimization
{
    private QosState? state;
    public OptimizationInfo Info => new("qos", "QoS temporário para o jogo", "Cria política DSCP 46 apenas para o executável monitorado. Roteadores podem ignorar a marcação. Não reserva banda e não garante menor latência.", RiskLevel.Optional, true);
    public void Configure(GameProfile profile) => state = new("GameBoost-" + Guid.NewGuid().ToString("N"), string.IsNullOrWhiteSpace(profile.MonitorExecutablePath) ? profile.ExecutablePath : profile.MonitorExecutablePath);
    public Task<OriginalValue> CaptureAsync() => Task.FromResult(new OriginalValue("Qos", JsonSerializer.Serialize(state ?? throw new InvalidOperationException("Perfil não configurado."))));
    public async Task ApplyAsync()
    {
        var s = state!;
        await shell.RunAsync($"New-NetQosPolicy -Name {PowerShellLiteral.Quote(s.Name)} -AppPathNameMatchCondition {PowerShellLiteral.Quote(s.Executable)} -DSCPAction 46 -PolicyStore ActiveStore | Out-Null");
    }
    public async Task RevertAsync(OriginalValue original)
    {
        var s = JsonSerializer.Deserialize<QosState>(original.Value!)!;
        if (original.Kind != "Qos" || !s.Name.StartsWith("GameBoost-", StringComparison.Ordinal) || !Guid.TryParseExact(s.Name[10..], "N", out _)) throw new InvalidDataException("Snapshot QoS inválido.");
        await shell.RunAsync($"$p=Get-NetQosPolicy -PolicyStore ActiveStore | Where-Object {{$_.Name -eq {PowerShellLiteral.Quote(s.Name)}}}; if($p){{Remove-NetQosPolicy -Name {PowerShellLiteral.Quote(s.Name)} -PolicyStore ActiveStore -Confirm:$false}}");
    }
}

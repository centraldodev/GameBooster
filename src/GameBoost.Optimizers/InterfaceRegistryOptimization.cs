using System.Runtime.Versioning;
using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Optimizers;

[SupportedOSPlatform("windows")]
public sealed class InterfaceRegistryOptimization(string id, string valueName) : IOptimization, IProfileOptimization
{
    private string path = "";
    public OptimizationInfo Info => new(id, valueName + " (TCP experimental)", "Define 1 na interface selecionada. Afeta TCP, não UDP; pode não ajudar ou piorar. Algumas aplicações só observam a mudança após reiniciar a conexão. Restaura o registro original.", RiskLevel.Optional, true);
    public void Configure(GameProfile profile) => path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + Guid.Parse(profile.NetworkInterfaceId).ToString("B");
    private RegistryOptimization Action(string p) => new(Info, p, valueName, 1, true);
    public async Task<OriginalValue> CaptureAsync() => new("InterfaceRegistry", JsonSerializer.Serialize(new State(path, await Action(path).CaptureAsync())));
    public Task ApplyAsync() => Action(path).ApplyAsync();
    public Task RevertAsync(OriginalValue original)
    {
        var s = JsonSerializer.Deserialize<State>(original.Value!)!;
        if (original.Kind != "InterfaceRegistry" || !s.Path.StartsWith(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(s.Path.Split('\\').Last(), out _)) throw new InvalidDataException("Snapshot de interface inválido.");
        return Action(s.Path).RevertAsync(s.Original);
    }
    public sealed record State(string Path, OriginalValue Original);
}

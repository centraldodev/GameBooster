using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameBoost.Core;
using Microsoft.Win32;
namespace GameBoost.Optimizers;

[SupportedOSPlatform("windows")]
public sealed class GpuPreferenceOptimization : IOptimization, IProfileOptimization
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private string executable = "", target = "";
    public OptimizationInfo Info => new("gpu-preference", "Preferência de GPU de alto desempenho", "Define a preferência do Windows por executável. Só faz diferença quando há GPUs elegíveis; pode aumentar consumo. Não altera driver ou processo do jogo.", RiskLevel.Optional, true);
    public void Configure(GameProfile profile) => executable = string.IsNullOrWhiteSpace(profile.MonitorExecutablePath) ? profile.ExecutablePath : profile.MonitorExecutablePath;
    public Task<OriginalValue> CaptureAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        var original = key?.GetValue(executable, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (original is not null && key!.GetValueKind(executable) != RegistryValueKind.String) throw new InvalidOperationException("Preferência GPU tem um tipo inesperado.");
        var text = original as string;
        target = Regex.Replace(text ?? "", @"GpuPreference=\d+;?", "", RegexOptions.IgnoreCase).TrimEnd(';');
        if (target.Length > 0) target += ";";
        target += "GpuPreference=2;";
        return Task.FromResult(new OriginalValue("GpuPreference", JsonSerializer.Serialize(new State(executable, text))));
    }
    public Task ApplyAsync()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        key.SetValue(executable, target, RegistryValueKind.String); return Task.CompletedTask;
    }
    public Task RevertAsync(OriginalValue original)
    {
        if (original.Kind != "GpuPreference") throw new InvalidDataException("Snapshot GPU inválido.");
        var state = JsonSerializer.Deserialize<State>(original.Value!)!;
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        if (state.Value is null) key.DeleteValue(state.Executable, false);
        else key.SetValue(state.Executable, state.Value, RegistryValueKind.String);
        return Task.CompletedTask;
    }
    public sealed record State(string Executable, string? Value);
}

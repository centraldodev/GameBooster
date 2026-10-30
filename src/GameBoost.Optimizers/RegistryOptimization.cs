using System.Runtime.Versioning;
using Microsoft.Win32;
using GameBoost.Core;
namespace GameBoost.Optimizers;

[SupportedOSPlatform("windows")]
public sealed class RegistryOptimization(OptimizationInfo info, string path, string name, int target, bool machine = false) : IOptimization
{
    public OptimizationInfo Info => info;
    public Task<OriginalValue> CaptureAsync()
    {
        using var key = (machine ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(path);
        var value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is not null && key!.GetValueKind(name) != RegistryValueKind.DWord)
            throw new InvalidOperationException($"Tipo de registro inesperado para {name}; nenhuma alteração foi feita.");
        return Task.FromResult(new OriginalValue(value is null ? "Missing" : "DWord", value?.ToString()));
    }
    public Task ApplyAsync()
    {
        using var key = (machine ? Registry.LocalMachine : Registry.CurrentUser).CreateSubKey(path, true);
        key.SetValue(name, target, RegistryValueKind.DWord);
        return Task.CompletedTask;
    }
    public Task RevertAsync(OriginalValue original)
    {
        using var key = (machine ? Registry.LocalMachine : Registry.CurrentUser).CreateSubKey(path, true);
        if (original.Kind == "Missing") key.DeleteValue(name, false);
        else if (original.Kind == "DWord") key.SetValue(name, int.Parse(original.Value!), RegistryValueKind.DWord);
        else throw new InvalidDataException("Tipo de snapshot inválido.");
        return Task.CompletedTask;
    }
}

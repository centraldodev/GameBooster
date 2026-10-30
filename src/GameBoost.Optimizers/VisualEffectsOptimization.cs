using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GameBoost.Core;
namespace GameBoost.Optimizers;

[SupportedOSPlatform("windows")]
public sealed class VisualEffectsOptimization : IOptimization
{
    public OptimizationInfo Info => new("ui-effects", "Reduzir efeitos da interface", "Desativa efeitos visuais gerais pela API do Windows. Pode alterar a aparência da área de trabalho. Restaura a preferência original.", RiskLevel.Optional, true);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Get(uint action, uint param, out int value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Set(uint action, uint param, IntPtr value, uint flags);
    public Task<OriginalValue> CaptureAsync()
    {
        if (!Get(0x103E, 0, out var enabled, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return Task.FromResult(new OriginalValue("UiEffects", enabled == 0 ? "0" : "1"));
    }
    public Task ApplyAsync() => Change(false);
    public Task RevertAsync(OriginalValue original)
    {
        if (original.Kind != "UiEffects" || original.Value is not ("0" or "1")) throw new InvalidDataException("Snapshot de efeitos inválido.");
        return Change(original.Value == "1");
    }
    private static Task Change(bool value)
    {
        if (!Set(0x103F, 0, value ? new IntPtr(1) : IntPtr.Zero, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return Task.CompletedTask;
    }
}

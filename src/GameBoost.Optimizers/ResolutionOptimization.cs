using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Optimizers;

[SupportedOSPlatform("windows")]
public sealed class ResolutionOptimization(Func<Task<bool>>? confirm = null) : IOptimization, IProfileOptimization
{
    private DisplayState? captured;
    private int width, height;
    public OptimizationInfo Info => new("resolution", "Resolução do monitor principal", "Troca a resolução da área de trabalho, não a configuração interna do jogo. Pode reorganizar janelas. Só aceita modos suportados; restaura o monitor original quando disponível.", RiskLevel.Optional, true);
    public void Configure(GameProfile profile)
    {
        width = profile.ResolutionWidth; height = profile.ResolutionHeight;
        if (width < 640 || height < 480 || width > 16384 || height > 16384) throw new InvalidOperationException("Resolução alvo inválida.");
    }
    public Task<OriginalValue> CaptureAsync()
    {
        var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
        string? primary = null;
        for (uint index = 0; EnumDisplayDevices(null, index, ref device, 0); index++)
        {
            if ((device.Flags & 4) != 0) { primary = device.Name; break; }
            device.Size = Marshal.SizeOf<DisplayDevice>();
        }
        if (primary is null) throw new InvalidOperationException("Monitor principal não encontrado.");
        var mode = NewMode();
        if (!EnumDisplaySettings(primary, -1, ref mode)) throw new Win32Exception(Marshal.GetLastWin32Error());
        captured = new(primary, mode.Width, mode.Height, mode.Bits, mode.Frequency);
        return Task.FromResult(new OriginalValue("Display", JsonSerializer.Serialize(captured)));
    }
    public async Task ApplyAsync()
    {
        if (confirm is null) throw new InvalidOperationException("A troca de resolução exige confirmação com tempo limite.");
        await Change(captured! with { Width = (uint)width, Height = (uint)height });
        if (!await confirm()) throw new InvalidOperationException("Resolução não confirmada. A sessão será revertida.");
    }
    public Task RevertAsync(OriginalValue original)
    {
        if (original.Kind != "Display") throw new InvalidDataException("Snapshot de resolução inválido.");
        return Change(JsonSerializer.Deserialize<DisplayState>(original.Value!)!);
    }
    private static Task Change(DisplayState state)
    {
        if (state.Width < 640 || state.Height < 480) throw new InvalidDataException("Resolução inválida no snapshot.");
        var mode = NewMode();
        if (!EnumDisplaySettings(state.Device, -1, ref mode)) throw new InvalidOperationException("Monitor original indisponível. Reconecte-o e tente restaurar.");
        mode.Width = state.Width; mode.Height = state.Height; mode.Bits = state.Bits; mode.Frequency = state.Frequency;
        mode.Fields = 0x00080000 | 0x00100000 | 0x00040000 | 0x00400000;
        if (ChangeDisplaySettingsEx(state.Device, ref mode, IntPtr.Zero, 2, IntPtr.Zero) != 0) throw new InvalidOperationException("O monitor não aceita esse modo de resolução/frequência.");
        var result = ChangeDisplaySettingsEx(state.Device, ref mode, IntPtr.Zero, 0, IntPtr.Zero);
        if (result != 0) throw new InvalidOperationException($"O Windows recusou a resolução (código {result}).");
        return Task.CompletedTask;
    }
    private static DevMode NewMode() => new() { Size = (ushort)Marshal.SizeOf<DevMode>(), Device = "", Form = "" };
    public sealed record DisplayState(string Device, uint Width, uint Height, uint Bits, uint Frequency);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        public ushort SpecVersion, DriverVersion, Size, DriverExtra;
        public uint Fields;
        public int X, Y;
        public uint Orientation, FixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Form;
        public ushort LogPixels;
        public uint Bits, Width, Height, Flags, Frequency, IcmMethod, IcmIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice display, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplaySettings(string device, int mode, ref DevMode settings);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsEx(string device, ref DevMode mode, IntPtr hwnd, uint flags, IntPtr param);
}

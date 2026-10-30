using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
namespace GameBoost.Monitoring;

public sealed record SystemSample(DateTimeOffset Timestamp, double? CpuPercent, double AvailableRamMb, long? PingMs);
public interface ISystemMetrics { Task<SystemSample> SampleAsync(string pingTarget); }
[SupportedOSPlatform("windows")]
public sealed class WindowsMetrics : ISystemMetrics
{
    private ulong previousIdle, previousTotal;
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    public async Task<SystemSample> SampleAsync(string pingTarget)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var total = (ulong)(kernel + user); var idleValue = (ulong)idle;
        double? cpu = previousTotal != 0 && total > previousTotal ? Math.Clamp(100.0 * (1 - (double)(idleValue - previousIdle) / (total - previousTotal)), 0, 100) : null;
        previousTotal = total; previousIdle = idleValue;
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var ping = await new NetworkProbe().MeasureAsync(pingTarget);
        return new(DateTimeOffset.UtcNow, cpu, memory.AvailablePhysical / 1048576.0, ping.Milliseconds);
    }
}

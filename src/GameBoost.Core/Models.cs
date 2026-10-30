namespace GameBoost.Core;

public enum RiskLevel { Low, Optional }
public enum SnapshotStatus { Pending, Restored }
public sealed record OptimizationInfo(string Id, string Name, string Description, RiskLevel Risk, bool SafeWithAntiCheat);
public sealed record OriginalValue(string Kind, string? Value);
public sealed class SnapshotEntry
{
    public string OptimizationId { get; set; } = "";
    public OriginalValue Original { get; set; } = new("", null);
    public bool Restored { get; set; }
}
public sealed record SessionEvent(DateTimeOffset At, string Stage, string Action, string Detail);
public sealed class Snapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string GameName { get; set; } = "";
    public Guid ProfileId { get; set; }
    public string RestorePointMessage { get; set; } = "Não solicitado";
    public SnapshotStatus Status { get; set; }
    public List<SessionEvent> Events { get; set; } = [];
    public List<SnapshotEntry> Entries { get; set; } = [];
}
public sealed class GameProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public bool HasAntiCheat { get; set; } = true;
    public List<string> OptimizationIds { get; set; } = [];
    public string MonitorExecutablePath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool AutoRestore { get; set; } = true;
    public bool CreateRestorePoint { get; set; } = true;
    public string NetworkInterfaceId { get; set; } = "";
    public int ResolutionWidth { get; set; } = 1280;
    public int ResolutionHeight { get; set; } = 720;
    public string DnsPrimary { get; set; } = "1.1.1.1";
    public string DnsSecondary { get; set; } = "1.0.0.1";
}
public interface IOptimization
{
    OptimizationInfo Info { get; }
    Task<OriginalValue> CaptureAsync();
    Task ApplyAsync();
    Task RevertAsync(OriginalValue original);
}
public interface ISnapshotStore
{
    Task SaveAsync(Snapshot snapshot);
    Task<IReadOnlyList<Snapshot>> LoadAsync();
}
public interface IProfileStore
{
    Task<IReadOnlyList<GameProfile>> LoadAsync();
    Task SaveAsync(IReadOnlyList<GameProfile> profiles);
}

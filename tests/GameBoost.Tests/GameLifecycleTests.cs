using GameBoost.Core;
using Xunit;
namespace GameBoost.Tests;

public sealed class GameLifecycleTests
{
    private static GameWatchOptions Fast => new(TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1), 2);
    [Fact]
    public async Task ExitingGameRestoresAppliedSession()
    {
        var store = new Store(); var action = new Action();
        var runtime = new Runtime { Running = new([false, true, false, false]) };
        var session = new GameSession(new([action], store), runtime, Fast);
        await session.StartAsync(Profile()); await session.Completion;
        Assert.True(runtime.Launched); Assert.False(session.Active); Assert.True(action.Restored);
        Assert.Equal(SnapshotStatus.Restored, store.Snapshot!.Status);
    }
    [Fact]
    public async Task LaunchFailureRestoresEvenWhenAutoRestoreIsOff()
    {
        var store = new Store(); var action = new Action(); var runtime = new Runtime { LaunchFails = true };
        var session = new GameSession(new([action], store), runtime, Fast);
        var profile = Profile(); profile.AutoRestore = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(profile));
        Assert.True(action.Restored); Assert.False(session.Active);
    }
    [Fact]
    public async Task AlreadyRunningGameIsRejectedBeforeApplying()
    {
        var store = new Store();
        var session = new GameSession(new([new Action()], store), new Runtime { Running = new([true]) }, Fast);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(Profile()));
        Assert.Null(store.Snapshot);
    }
    [Fact]
    public async Task ManualRestoreOptionLeavesExitedSessionPending()
    {
        var store = new Store(); var action = new Action();
        var session = new GameSession(new([action], store), new Runtime { Running = new([false, true, false, false]) }, Fast);
        var profile = Profile(); profile.AutoRestore = false;
        await session.StartAsync(profile); await session.Completion;
        Assert.False(action.Restored); Assert.Equal(SnapshotStatus.Pending, store.Snapshot!.Status);
    }
    [Fact]
    public async Task MissingProcessTimesOutAndRollsBack()
    {
        var store = new Store(); var action = new Action();
        var session = new GameSession(new([action], store), new Runtime(), new(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(10), 2));
        await session.StartAsync(Profile());
        await Assert.ThrowsAsync<TimeoutException>(() => session.Completion);
        Assert.True(action.Restored); Assert.False(session.Active);
    }
    [Fact]
    public void ProtectionPolicyExcludesGameDependenciesAndAllUnlistedProcesses()
    {
        Assert.False(ProtectionPolicy.CanCloseApp("chrome", @"C:\Games\chrome.exe"));
        Assert.False(ProtectionPolicy.CanCloseApp("Discord", "game.exe", ["Discord.exe"]));
        foreach (var name in new[] { "svchost", "explorer", "vgc", "BEService", "Steam", "EpicGamesLauncher", "MsMpEng" }) Assert.False(ProtectionPolicy.CanCloseApp(name, "game.exe"));
        Assert.True(ProtectionPolicy.CanCloseApp("Spotify", "game.exe"));
    }
    private static GameProfile Profile() => new() { Name = "Game", ExecutablePath = "game.exe", OptimizationIds = ["one"] };
    private sealed class Store : ISnapshotStore
    {
        public Snapshot? Snapshot;
        public Task SaveAsync(Snapshot value) { Snapshot = value; return Task.CompletedTask; }
        public Task<IReadOnlyList<Snapshot>> LoadAsync() => Task.FromResult<IReadOnlyList<Snapshot>>(Snapshot is null ? [] : [Snapshot]);
    }
    private sealed class Action : IOptimization
    {
        public bool Restored;
        public OptimizationInfo Info => new("one", "One", "", RiskLevel.Low, true);
        public Task<OriginalValue> CaptureAsync() => Task.FromResult(new OriginalValue("Test", "old"));
        public Task ApplyAsync() => Task.CompletedTask;
        public Task RevertAsync(OriginalValue original) { Restored = true; return Task.CompletedTask; }
    }
    private sealed class Runtime : IGameRuntime
    {
        public Queue<bool> Running = new(); public bool LaunchFails, Launched;
        public Task<bool> IsRunningAsync(string executable, CancellationToken token) => Task.FromResult(Running.Count > 0 && Running.Dequeue());
        public Task LaunchAsync(string executable, string arguments) { if (LaunchFails) throw new InvalidOperationException("launch failed"); Launched = true; return Task.CompletedTask; }
    }
}

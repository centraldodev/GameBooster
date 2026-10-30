using GameBoost.Core;
using GameBoost.Restore;
using Xunit;
namespace GameBoost.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task PersistsBeforeMutationAndRestoresOriginalValue()
    {
        var store = new MemoryStore();
        var action = new FakeAction("one") { BeforeApply = () => Assert.Single(store.Items.Single().Entries) };
        var engine = new OptimizationSession([action], store);
        await engine.ApplyAsync(Profile("one"));
        Assert.Equal("optimized", action.Value);
        await engine.RestoreAllAsync();
        Assert.Equal("original", action.Value);
        Assert.Equal(SnapshotStatus.Restored, store.Items.Single().Status);
    }
    [Fact]
    public async Task PartialFailureRevertsInReverseOrderIncludingFailingAction()
    {
        var order = new List<string>();
        var first = new FakeAction("one") { OnRevert = () => order.Add("one") };
        var second = new FakeAction("two") { FailApply = true, OnRevert = () => order.Add("two") };
        var store = new MemoryStore();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new OptimizationSession([first, second], store).ApplyAsync(Profile("one", "two")));
        Assert.Equal(new[] { "two", "one" }, order);
        Assert.Equal("original", first.Value);
        Assert.Equal("original", second.Value);
        Assert.Equal(SnapshotStatus.Restored, store.Items.Single().Status);
    }
    [Fact]
    public async Task FailedRestoreStaysPendingAndCanBeRetriedAfterRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var action = new FakeAction("one");
            var store = new JsonSnapshotStore(directory);
            await new OptimizationSession([action], store).ApplyAsync(Profile("one"));
            action.FailRevert = true;
            await Assert.ThrowsAsync<AggregateException>(() => new OptimizationSession([action], new JsonSnapshotStore(directory)).RestoreAllAsync());
            Assert.Equal(SnapshotStatus.Pending, (await store.LoadAsync()).Single().Status);
            action.FailRevert = false;
            await new OptimizationSession([action], new JsonSnapshotStore(directory)).RestoreAllAsync();
            Assert.Equal("original", action.Value);
            Assert.Equal(SnapshotStatus.Restored, (await store.LoadAsync()).Single().Status);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task UnsafeActionIsBlockedBeforeAnyMutation()
    {
        var action = new FakeAction("unsafe", false);
        var store = new MemoryStore();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new OptimizationSession([action], store).ApplyAsync(Profile("unsafe")));
        Assert.Empty(store.Items);
        Assert.Equal("original", action.Value);
    }
    [Fact]
    public async Task PendingSessionBlocksAnotherActivation()
    {
        var store = new MemoryStore(); var action = new FakeAction("one");
        var engine = new OptimizationSession([action], store);
        await engine.ApplyAsync(Profile("one"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ApplyAsync(Profile("one")));
        Assert.Single(store.Items);
    }
    [Fact]
    public async Task FailedSnapshotWritePreventsMutation()
    {
        var store = new MemoryStore { FailOnEntry = true }; var action = new FakeAction("one");
        await Assert.ThrowsAnyAsync<Exception>(() => new OptimizationSession([action], store).ApplyAsync(Profile("one")));
        Assert.Equal("original", action.Value);
        Assert.False(action.Applied);
    }
    private static GameProfile Profile(params string[] ids) => new() { Name = "Test", HasAntiCheat = true, OptimizationIds = [.. ids] };
    private sealed class MemoryStore : ISnapshotStore
    {
        public List<Snapshot> Items { get; } = [];
        public bool FailOnEntry { get; init; }
        public Task SaveAsync(Snapshot snapshot)
        {
            if (FailOnEntry && snapshot.Entries.Count > 0) throw new IOException("disk full");
            if (!Items.Contains(snapshot)) Items.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<Snapshot>> LoadAsync() => Task.FromResult<IReadOnlyList<Snapshot>>(Items);
    }
    private sealed class FakeAction(string id, bool safe = true) : IOptimization
    {
        public OptimizationInfo Info => new(id, id, "", RiskLevel.Low, safe);
        public string Value { get; private set; } = "original";
        public bool Applied { get; private set; }
        public bool FailApply { get; init; }
        public bool FailRevert { get; set; }
        public Action? BeforeApply { get; init; }
        public Action? OnRevert { get; init; }
        public Task<OriginalValue> CaptureAsync() => Task.FromResult(new OriginalValue("test", Value));
        public Task ApplyAsync() { BeforeApply?.Invoke(); Applied = true; Value = "optimized"; if (FailApply) throw new InvalidOperationException("failed apply"); return Task.CompletedTask; }
        public Task RevertAsync(OriginalValue original) { if (FailRevert) throw new IOException("failed restore"); Value = original.Value!; OnRevert?.Invoke(); return Task.CompletedTask; }
    }
}

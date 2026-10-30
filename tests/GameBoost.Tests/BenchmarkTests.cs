using GameBoost.Monitoring;
using Xunit;
namespace GameBoost.Tests;

public sealed class BenchmarkTests
{
    [Fact]
    public void DifferentSwapchainsCannotBeMergedIntoOneFpsResult()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "Application,MsBetweenPresents,ProcessID,SwapChainAddress" }
                .Concat(Enumerable.Repeat("game.exe,10,100,0x1", 100)).Append("game.exe,10,100,0x2"));
            Assert.Throws<InvalidDataException>(() => PresentMonImport.Read(path, "game.exe"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NetworkSummaryExcludesLostPacketsAndComputesJitterOnlyAcrossAdjacentReplies()
    {
        var result = NetworkStatistics.Calculate(new long?[] { 10, 20, null, 100, 120 });
        Assert.Equal(5, result.Sent); Assert.Equal(4, result.Received);
        Assert.Equal(62.5, result.AverageMs); Assert.Equal(15, result.JitterMs); Assert.Equal(20, result.LossPercent);
    }
    [Fact]
    public void NoRepliesAreUnavailableRatherThanZeroLatency()
    {
        var result = NetworkStatistics.Calculate(new long?[] { null, null });
        Assert.Null(result.AverageMs); Assert.Null(result.JitterMs); Assert.Equal(100, result.LossPercent);
    }
    [Fact]
    public void FrameImportFiltersGameAndUsesSlowestOnePercent()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "Application,MsBetweenPresents" }.Concat(Enumerable.Repeat("game.exe,10", 99)).Append("game.exe,100").Append("other.exe,9999"));
            var result = PresentMonImport.Read(path, @"C:\Games\game.exe");
            Assert.Equal(100, result.Frames);
            Assert.Equal(1000.0 / 10.9, result.AverageFps, 6); Assert.Equal(10, result.OnePercentLowFps, 6);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void TooFewFramesDoNotProduceMisleadingFps()
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllText(path, "Application,MsBetweenPresents\ngame.exe,16\n"); Assert.Throws<InvalidDataException>(() => PresentMonImport.Read(path, "game.exe")); }
        finally { File.Delete(path); }
    }
    [Fact]
    public void ReportsRejectDifferentProfilesAndDoNotInventFps()
    {
        var before = Run(Guid.NewGuid(), []); var after = Run(before.ProfileId, ["power"]);
        var report = BenchmarkReport.Markdown(before, after);
        Assert.DoesNotContain("| FPS médio", report); Assert.Contains("CPU do sistema", report); Assert.Contains("power", report);
        Assert.Throws<InvalidOperationException>(() => BenchmarkReport.Validate(before, after with { ProfileId = Guid.NewGuid() }));
        Assert.Throws<InvalidOperationException>(() => BenchmarkReport.Validate(before, after with { Target = "8.8.8.8" }));
        Assert.Throws<InvalidOperationException>(() => BenchmarkReport.Validate(before, after with { Actions = [] }));
    }
    [Fact]
    public async Task CancelledRecordingDoesNotReturnPartialBenchmark()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BenchmarkRecorder(new Metrics()).RecordAsync(Guid.NewGuid(), "Game", "game.exe", "1.1.1.1", [], TimeSpan.FromSeconds(60), null, cancellation.Token));
    }
    private static BenchmarkRun Run(Guid id, string[] actions) => new(id, "Game", "game.exe", "1.1.1.1", DateTimeOffset.UtcNow, 60, [new(DateTimeOffset.UtcNow, 50, 2048, 10)], actions);
    private sealed class Metrics : ISystemMetrics
    {
        public Task<SystemSample> SampleAsync(string pingTarget) => Task.FromResult(new SystemSample(DateTimeOffset.UtcNow, 50, 2048, 10));
    }
}

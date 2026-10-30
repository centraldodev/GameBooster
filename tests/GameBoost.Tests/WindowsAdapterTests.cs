using System.Text.Json;
using GameBoost.Core;
using GameBoost.Optimizers;
using GameBoost.Restore;
using Xunit;
namespace GameBoost.Tests;

public sealed class WindowsAdapterTests
{
    [Fact]
    public async Task DnsStaticStateRestoresOriginalInterfaceAndServersWithFreshAdapter()
    {
        var id = Guid.NewGuid().ToString("B");
        var shell = new FakeShell { Result = JsonSerializer.Serialize(new DnsState(id, false, ["9.9.9.9", "149.112.112.112"])) };
        var action = new DnsOptimization(shell);
        action.Configure(new() { NetworkInterfaceId = id, DnsPrimary = "1.1.1.1", DnsSecondary = "1.0.0.1" });
        var original = await action.CaptureAsync(); await action.ApplyAsync();
        Assert.Contains("-ServerAddresses @('1.1.1.1','1.0.0.1')", shell.Scripts.Last());
        var restarted = new DnsOptimization(shell); await restarted.RevertAsync(original);
        Assert.Contains(Guid.Parse(id).ToString(), shell.Scripts.Last());
        Assert.Contains("-ServerAddresses @('9.9.9.9','149.112.112.112')", shell.Scripts.Last());
        Assert.Contains("-AddressFamily IPv4 |", shell.Scripts.Last());
        Assert.DoesNotContain("-ResetServerAddresses", shell.Scripts.Last());
    }
    [Fact]
    public async Task DnsAutomaticStateRestoresDhcpInsteadOfFreezingCurrentLeaseServers()
    {
        var shell = new FakeShell();
        var action = new DnsOptimization(shell);
        await action.RevertAsync(new("DnsV4", JsonSerializer.Serialize(new DnsState(Guid.NewGuid().ToString(), true, ["192.168.0.1"]))));
        Assert.Contains("-AddressFamily IPv4 | Set-DnsClientServerAddress -ResetServerAddresses", shell.Scripts.Single());
        Assert.DoesNotContain("192.168.0.1", shell.Scripts.Single());
    }
    [Fact]
    public void InvalidDnsCannotBecomePowerShellCode()
    {
        var shell = new FakeShell();
        Assert.Throws<InvalidOperationException>(() => new DnsOptimization(shell).Configure(new() { NetworkInterfaceId = Guid.NewGuid().ToString(), DnsPrimary = "1.1.1.1'; Stop-Computer; '" }));
        Assert.Empty(shell.Scripts);
        Assert.Equal("'a''; b'", PowerShellLiteral.Quote("a'; b"));
    }
    [Fact]
    public async Task QosRestoreRemovesOnlyItsUniqueEphemeralPolicy()
    {
        var shell = new FakeShell(); var action = new QosOptimization(shell);
        action.Configure(new() { ExecutablePath = @"C:\Games\Game's.exe" });
        var original = await action.CaptureAsync(); var state = JsonSerializer.Deserialize<QosState>(original.Value!)!;
        await action.ApplyAsync();
        Assert.Contains("-PolicyStore ActiveStore", shell.Scripts.Last());
        Assert.Contains("Game''s.exe", shell.Scripts.Last());
        await new QosOptimization(shell).RevertAsync(original);
        Assert.Contains(state.Name, shell.Scripts.Last());
        Assert.DoesNotContain("-All", shell.Scripts.Last());
        await Assert.ThrowsAsync<InvalidDataException>(() => action.RevertAsync(new("Qos", JsonSerializer.Serialize(new QosState("CorporatePolicy", "game.exe")))));
    }
    [Theory]
    [InlineData("vgc")]
    [InlineData("WinDefend")]
    [InlineData("Dhcp")]
    [InlineData("BEService")]
    [InlineData("XboxGipSvc")]
    [InlineData("AudioSrv")]
    public async Task ProtectedServicesAreRejectedBeforeInvokingTheOs(string service)
    {
        var shell = new FakeShell();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ServiceOptimization(shell, service, "").CaptureAsync());
        Assert.Empty(shell.Scripts);
    }
    [Theory]
    [InlineData("Stopped", 0)]
    [InlineData("Running", 2)]
    public async Task ServiceStateSurvivesRestartAndNeverChangesStartupType(string status, int calls)
    {
        var shell = new FakeShell { Result = status }; var action = new ServiceOptimization(shell, "WSearch", "");
        var original = await action.CaptureAsync(); shell.Scripts.Clear(); await action.ApplyAsync();
        await new ServiceOptimization(shell, "WSearch", "").RevertAsync(original);
        Assert.Equal(calls, shell.Scripts.Count);
        Assert.All(shell.Scripts, script => { Assert.DoesNotContain("-Force", script); Assert.DoesNotContain("StartupType", script); });
    }
    [Theory]
    [InlineData("CREATED", true)]
    [InlineData("NOT_CREATED", false)]
    public async Task RestorePointResultUsesVerifiedCreationRatherThanSuccessfulCommandExit(string output, bool created)
    {
        var shell = new FakeShell { Result = output };
        var result = await new WindowsRestorePointService(shell).CreateAsync("Game's checkpoint");
        Assert.Equal(created, result.Created);
        Assert.Contains("SequenceNumber -notin $before", shell.Scripts.Single());
        Assert.Contains("Game''s checkpoint", shell.Scripts.Single());
    }
    [Fact]
    public async Task UnavailableRestorePointIsReportedAsFallback()
    {
        var result = await new WindowsRestorePointService(new FakeShell { Error = new InvalidOperationException("System protection disabled") }).CreateAsync("test");
        Assert.False(result.Created); Assert.Contains("Snapshot próprio", result.Message);
    }
    private sealed class FakeShell : IPowerShell
    {
        public string Result { get; init; } = "";
        public Exception? Error { get; init; }
        public List<string> Scripts { get; } = [];
        public Task<string> RunAsync(string script, int timeoutSeconds = 45, CancellationToken cancellationToken = default)
        {
            Scripts.Add(script); if (Error is not null) throw Error; return Task.FromResult(Result);
        }
    }
}

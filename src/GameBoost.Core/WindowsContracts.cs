namespace GameBoost.Core;

public interface IPowerShell
{
    Task<string> RunAsync(string script, int timeoutSeconds = 45, CancellationToken cancellationToken = default);
}
public interface IProfileOptimization { void Configure(GameProfile profile); }
public sealed record RestorePointResult(bool Created, string Message);
public interface IRestorePointService { Task<RestorePointResult> CreateAsync(string description); }
public interface IGameRuntime
{
    Task<bool> IsRunningAsync(string executable, CancellationToken token);
    Task LaunchAsync(string executable, string arguments);
}
public sealed record InstalledGame(string Name, string Directory, string Source);
public static class PowerShellLiteral
{
    public static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}

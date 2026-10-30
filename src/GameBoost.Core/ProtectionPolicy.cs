namespace GameBoost.Core;

public static class ProtectionPolicy
{
    // Positive lists intentionally exclude Windows/security/anti-cheat services and all launchers.
    public static IReadOnlyList<string> ManagedServices { get; } = Array.AsReadOnly(new[] { "WSearch", "WerSvc", "MapsBroker", "SysMain", "Spooler", "wuauserv", "DoSvc", "BITS", "DiagTrack" });
    public static IReadOnlyList<string> ClosableApps { get; } = Array.AsReadOnly(new[] { "chrome", "msedge", "firefox", "Spotify", "Discord", "notepad" });
    public static bool CanManageService(string name) => ManagedServices.Contains(name, StringComparer.OrdinalIgnoreCase);
    public static bool CanCloseApp(string name, string gameExecutable, IEnumerable<string>? dependencies = null)
    {
        var normalized = Path.GetFileNameWithoutExtension(name.Replace('\\', '/'));
        var game = Path.GetFileNameWithoutExtension(gameExecutable.Replace('\\', '/'));
        return ClosableApps.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            && !normalized.Equals(game, StringComparison.OrdinalIgnoreCase)
            && !(dependencies ?? []).Any(x => Path.GetFileNameWithoutExtension(x.Replace('\\', '/')).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}

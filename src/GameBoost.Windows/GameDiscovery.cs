using System.Text.Json;
using System.Text.RegularExpressions;
using GameBoost.Core;
namespace GameBoost.Windows;

public sealed class GameDiscovery(IPowerShell shell)
{
    public async Task<IReadOnlyList<InstalledGame>> DiscoverAsync()
    {
        var result = new List<InstalledGame>();
        var steam = await shell.RunAsync(@"$p=Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue; if($p){$p.SteamPath}");
        if (Directory.Exists(steam))
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };
            var libraryFile = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFile))
                foreach (Match match in Regex.Matches(await File.ReadAllTextAsync(libraryFile), "\"path\"\\s+\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            foreach (var library in libraries)
            {
                var folder = Path.Combine(library, "steamapps");
                if (!Directory.Exists(folder)) continue;
                foreach (var manifest in Directory.EnumerateFiles(folder, "appmanifest_*.acf"))
                {
                    var content = await File.ReadAllTextAsync(manifest);
                    var name = Field(content, "name"); var install = Field(content, "installdir");
                    var location = Path.Combine(folder, "common", install);
                    if (name.Length > 0 && install.Length > 0 && Directory.Exists(location)) result.Add(new(name, location, "Steam"));
                }
            }
        }
        var epic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (Directory.Exists(epic))
            foreach (var manifest in Directory.EnumerateFiles(epic, "*.item"))
            {
                using var json = JsonDocument.Parse(await File.ReadAllTextAsync(manifest));
                var data = json.RootElement;
                if (data.TryGetProperty("DisplayName", out var name) && data.TryGetProperty("InstallLocation", out var folder) && Directory.Exists(folder.GetString()))
                    result.Add(new(name.GetString()!, folder.GetString()!, "Epic"));
            }
        var gog = await shell.RunAsync(@"$paths=@('HKLM:\SOFTWARE\WOW6432Node\GOG.com\Games','HKLM:\SOFTWARE\GOG.com\Games'); $games=@(foreach($path in $paths){if(Test-Path $path){Get-ChildItem $path | Get-ItemProperty | Select-Object gameName,path}}); ConvertTo-Json -InputObject $games -Compress");
        if (!string.IsNullOrWhiteSpace(gog))
        {
            using var json = JsonDocument.Parse(gog);
            foreach (var item in json.RootElement.EnumerateArray())
                if (item.TryGetProperty("gameName", out var name) && item.TryGetProperty("path", out var folder) && name.ValueKind == JsonValueKind.String && folder.ValueKind == JsonValueKind.String && Directory.Exists(folder.GetString())) result.Add(new(name.GetString()!, folder.GetString()!, "GOG"));
        }
        return result.DistinctBy(x => x.Directory, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Name).ToList();
    }
    private static string Field(string text, string field) => Regex.Match(text, "\"" + field + "\"\\s+\"([^\"]+)\"").Groups[1].Value;
}

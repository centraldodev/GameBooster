using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Data;

public sealed class JsonProfileStore(string directory) : IProfileStore
{
    private readonly string path = Path.Combine(directory, "profiles.json");
    public async Task<IReadOnlyList<GameProfile>> LoadAsync() => File.Exists(path)
        ? await AtomicJson.ReadAsync<List<GameProfile>>(path) : [];
    public Task SaveAsync(IReadOnlyList<GameProfile> profiles) => AtomicJson.WriteAsync(path, profiles);
}
public static class AtomicJson
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static async Task<T> ReadAsync<T>(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options) ?? throw new InvalidDataException($"Arquivo inválido: {path}");
    }
    public static async Task WriteAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options);
            await stream.FlushAsync();
            stream.Flush(true);
        }
        File.Move(temporary, path, true);
    }
}

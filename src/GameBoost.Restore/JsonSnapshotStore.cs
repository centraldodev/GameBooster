using GameBoost.Core;
using GameBoost.Data;
namespace GameBoost.Restore;

public sealed class JsonSnapshotStore(string directory) : ISnapshotStore
{
    public Task SaveAsync(Snapshot snapshot) => AtomicJson.WriteAsync(Path.Combine(directory, $"{snapshot.Id}.json"), snapshot);
    public async Task<IReadOnlyList<Snapshot>> LoadAsync()
    {
        if (!Directory.Exists(directory)) return [];
        var results = new List<Snapshot>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json")) results.Add(await AtomicJson.ReadAsync<Snapshot>(path));
        return results.OrderByDescending(x => x.CreatedAt).ToList();
    }
}

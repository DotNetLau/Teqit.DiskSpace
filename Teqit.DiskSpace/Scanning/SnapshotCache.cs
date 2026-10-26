using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Teqit.DiskSpace.Scanning;

public sealed record Snapshot(DirNode Root, DateTime ScannedAtUtc);

/// <summary>
/// Stores the last finished scan per root as JSON so the next run can show something immediately.
/// The tree is stored flat (parent index per entry) so very deep folder structures cannot overflow the serializer.
/// </summary>
public sealed class SnapshotCache
{
    private const int Version = 1;
    private readonly string _directory;

    public SnapshotCache(string? directory = null)
    {
        _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Teqit.DiskSpace");
    }

    public Snapshot? TryLoad(string root)
    {
        try
        {
            var file = FileFor(root);
            if (!File.Exists(file)) return null;

            using var stream = File.OpenRead(file);
            var dto = JsonSerializer.Deserialize<SnapshotDto>(stream);
            if (dto is null || dto.Version != Version || dto.Entries.Length == 0) return null;

            var nodes = new DirNode[dto.Entries.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var e = dto.Entries[i];
                if (i == 0)
                {
                    nodes[0] = new DirNode(dto.Root, dto.Root, null);
                }
                else
                {
                    if (e.P < 0 || e.P >= i) return null; // corrupt
                    var parent = nodes[e.P];
                    nodes[i] = new DirNode(e.N, Path.Combine(parent.FullPath, e.N), parent);
                }
                nodes[i].SetTotals(e.S, e.F);
                nodes[i].OwnFilesSize = e.O;
                nodes[i].HadErrors = e.E;
                nodes[i].IsComplete = true;
            }

            var kids = new List<DirNode>[nodes.Length];
            for (int i = 1; i < nodes.Length; i++)
                (kids[dto.Entries[i].P] ??= new List<DirNode>()).Add(nodes[i]);
            for (int i = 0; i < nodes.Length; i++)
                if (kids[i] != null) nodes[i].Children = kids[i].ToArray();

            return new Snapshot(nodes[0], dto.ScannedAtUtc);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return null; // a broken cache must never break the tool
        }
    }

    public void Save(DirNode root)
    {
        try
        {
            Directory.CreateDirectory(_directory);

            var entries = new List<EntryDto>();
            var queue = new Queue<(DirNode Node, int Parent)>();
            queue.Enqueue((root, -1));
            while (queue.Count > 0)
            {
                var (n, parent) = queue.Dequeue();
                int index = entries.Count;
                entries.Add(new EntryDto(parent, n.Name, n.Size, n.FileCount, n.OwnFilesSize, n.HadErrors));
                foreach (var c in n.Children) queue.Enqueue((c, index));
            }

            var dto = new SnapshotDto(Version, root.FullPath, DateTime.UtcNow, entries.ToArray());
            var file = FileFor(root.FullPath);
            var temp = file + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, dto);
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // caching is best effort
        }
    }

    private string FileFor(string root)
    {
        var comparable = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(comparable)))[..16];
        return Path.Combine(_directory, $"scan-{hash}.json");
    }

    // Short property names keep the file small on large trees.
    private sealed record SnapshotDto(int Version, string Root, DateTime ScannedAtUtc, EntryDto[] Entries);

    private sealed record EntryDto(int P, string N, long S, long F, long O, bool E);
}

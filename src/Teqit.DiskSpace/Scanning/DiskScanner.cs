using System.IO.Enumeration;

namespace Teqit.DiskSpace.Scanning;

public sealed class ScanOptions
{
    public bool CrossMounts { get; init; }
    public IReadOnlyCollection<string> Excludes { get; init; } = Array.Empty<string>();

    /// <summary>Directories smaller than this drop their child nodes to save memory.</summary>
    public long PruneBelowBytes { get; init; } = 1024 * 1024;
}

public sealed class ScanProgress
{
    public long Files;
    public long Dirs;
    public long Bytes;
    public long Errors;
    public volatile string Current = "";
}

public sealed class DiskScanner
{
    private const FileAttributes CloudPlaceholder =
        FileAttributes.Offline | (FileAttributes)0x40000 /* RecallOnOpen */ | (FileAttributes)0x400000 /* RecallOnDataAccess */;

    private readonly ScanOptions _options;
    private readonly HashSet<string> _skip;
    private readonly int _parallelism = Math.Max(4, Environment.ProcessorCount * 2);

    public DiskScanner(ScanOptions? options = null)
    {
        _options = options ?? new ScanOptions();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _skip = new HashSet<string>(_options.Excludes.Select(Normalize), comparer);

        if (OperatingSystem.IsLinux())
        {
            foreach (var p in new[] { "/proc", "/sys", "/dev", "/run" }) _skip.Add(p);
        }
    }

    public ScanProgress Progress { get; } = new();

    /// <summary>The tree being built; available as soon as <see cref="Scan"/> starts and filled in while it runs.</summary>
    public DirNode? Root { get; private set; }

    /// <summary>The canonical form of a scan root (also the key used by the snapshot cache).</summary>
    public static string NormalizeRoot(string path) => Normalize(Path.GetFullPath(path));

    public DirNode Scan(string root, CancellationToken ct = default)
    {
        root = NormalizeRoot(root);
        if (!_options.CrossMounts && OperatingSystem.IsLinux())
        {
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    var mount = Normalize(d.Name);
                    if (mount != root) _skip.Add(mount);
                }
            }
            catch (IOException) { }
        }

        var node = new DirNode(root, root, null);
        Root = node;
        ScanDir(node, 0, ct);
        return node;
    }

    private void ScanDir(DirNode node, int depth, CancellationToken ct)
    {
        Progress.Current = node.FullPath;
        Interlocked.Increment(ref Progress.Dirs);

        var subDirs = new List<DirNode>();
        long own = 0, files = 0;
        try
        {
            foreach (var e in Enumerate(node.FullPath))
            {
                if (ct.IsCancellationRequested) break;
                if (e.IsDirectory)
                {
                    if ((e.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    var path = Path.Combine(node.FullPath, e.Name);
                    if (_skip.Contains(Normalize(path))) continue;
                    subDirs.Add(new DirNode(e.Name, path, node));
                }
                else
                {
                    if ((e.Attributes & CloudPlaceholder) != 0) continue; // not occupying local disk
                    own += e.Length;
                    files++;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            node.HadErrors = true;
            Interlocked.Increment(ref Progress.Errors);
        }

        Interlocked.Add(ref Progress.Files, files);
        Interlocked.Add(ref Progress.Bytes, own);

        // Publish immediately so a UI reading the tree sees this folder's totals and subfolders while the scan runs.
        node.OwnFilesSize = own;
        node.AddTotals(own, files);
        node.Children = subDirs.ToArray();

        if (depth < 4 && subDirs.Count > 1)
        {
            Parallel.ForEach(subDirs, new ParallelOptions { MaxDegreeOfParallelism = _parallelism },
                d => { if (!ct.IsCancellationRequested) ScanDir(d, depth + 1, ct); });
        }
        else
        {
            foreach (var d in subDirs)
            {
                if (ct.IsCancellationRequested) break;
                ScanDir(d, depth + 1, ct);
            }
        }

        if (node.Size < _options.PruneBelowBytes) node.Children = Array.Empty<DirNode>();
        node.IsComplete = true;
    }

    private readonly record struct Entry(string Name, bool IsDirectory, long Length, FileAttributes Attributes);

    private static IEnumerable<Entry> Enumerate(string path)
    {
        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = 0, // include hidden + system: that's where disk space often hides
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };
        return new FileSystemEnumerable<Entry>(path,
            (ref FileSystemEntry e) => new Entry(e.FileName.ToString(), e.IsDirectory, e.IsDirectory ? 0 : e.Length, e.Attributes),
            opts);
    }

    private static string Normalize(string path)
    {
        var root = Path.GetPathRoot(path);
        return path.Length > (root?.Length ?? 0) ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
    }
}

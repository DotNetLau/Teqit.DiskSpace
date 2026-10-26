namespace Teqit.DiskSpace.Scanning;

/// <summary>
/// A folder in the scan tree. Safe to read from the UI thread while a scan is still filling it in:
/// sizes are updated with interlocked adds and <see cref="Children"/> is swapped atomically.
/// </summary>
public sealed class DirNode
{
    private long _size;
    private long _fileCount;
    private DirNode[] _children = Array.Empty<DirNode>();
    private volatile bool _isComplete;
    private volatile bool _hadErrors;

    public DirNode(string name, string fullPath, DirNode? parent)
    {
        Name = name;
        FullPath = fullPath;
        Parent = parent;
    }

    public string Name { get; }
    public string FullPath { get; }
    public DirNode? Parent { get; }

    /// <summary>Recursive total size in bytes (own files + all children). Grows while scanning.</summary>
    public long Size => Interlocked.Read(ref _size);

    /// <summary>Recursive file count. Grows while scanning.</summary>
    public long FileCount => Interlocked.Read(ref _fileCount);

    /// <summary>Size of the files directly inside this directory.</summary>
    public long OwnFilesSize { get; set; }

    public bool HadErrors
    {
        get => _hadErrors;
        set => _hadErrors = value;
    }

    /// <summary>False while this folder (or anything below it) is still being scanned.</summary>
    public bool IsComplete
    {
        get => _isComplete;
        set => _isComplete = value;
    }

    public DirNode[] Children
    {
        get => Volatile.Read(ref _children);
        set => Volatile.Write(ref _children, value);
    }

    /// <summary>Adds to this node and every ancestor.</summary>
    public void AddTotals(long bytes, long files)
    {
        for (var n = this; n != null; n = n.Parent)
        {
            Interlocked.Add(ref n._size, bytes);
            Interlocked.Add(ref n._fileCount, files);
        }
    }

    /// <summary>Restores totals when loading a snapshot (no ancestors involved).</summary>
    public void SetTotals(long bytes, long files)
    {
        Interlocked.Exchange(ref _size, bytes);
        Interlocked.Exchange(ref _fileCount, files);
    }

    public IEnumerable<DirNode> Descendants()
    {
        var stack = new Stack<DirNode>(Children);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            foreach (var c in n.Children) stack.Push(c);
        }
    }
}

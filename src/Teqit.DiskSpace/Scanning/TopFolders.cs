namespace Teqit.DiskSpace.Scanning;

public static class TopFolders
{
    /// <summary>
    /// Largest folders anywhere below <paramref name="root"/>. A folder is skipped when a single child holds
    /// at least <paramref name="dominance"/> of its size, so the more specific child is reported instead
    /// (e.g. AppData rather than Users).
    /// </summary>
    public static List<DirNode> Find(DirNode root, int count, double dominance = 0.8)
    {
        return root.Descendants()
            .Where(n => n.Size > 0 && !IsDominated(n, dominance))
            .OrderByDescending(n => n.Size)
            .Take(count)
            .ToList();
    }

    private static bool IsDominated(DirNode n, double dominance)
    {
        var children = n.Children; // snapshot: the array may be swapped by a running scan
        if (children.Length == 0) return false;
        return children.Max(c => c.Size) >= n.Size * dominance;
    }
}

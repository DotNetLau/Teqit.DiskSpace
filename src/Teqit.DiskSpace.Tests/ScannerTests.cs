using Teqit.DiskSpace.Scanning;
using Teqit.DiskSpace.Ui;
using Xunit;

namespace Teqit.DiskSpace.Tests;

public sealed class ScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "diskspace-" + Guid.NewGuid().ToString("N"));

    public ScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private void File(string rel, int bytes)
    {
        var p = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        System.IO.File.WriteAllBytes(p, new byte[bytes]);
    }

    private static DiskScanner NewScanner(ScanOptions? o = null) => new(o ?? new ScanOptions { PruneBelowBytes = 0 });

    [Fact]
    public void Sums_sizes_recursively()
    {
        File("a/one.bin", 1000);
        File("a/deep/two.bin", 2000);
        File("b/three.bin", 500);
        File("root.bin", 10);

        var root = NewScanner().Scan(_root);

        Assert.Equal(3510, root.Size);
        Assert.Equal(4, root.FileCount);
        Assert.Equal(10, root.OwnFilesSize);
        Assert.Equal(3000, root.Children.Single(c => c.Name == "a").Size);
    }

    [Fact]
    public void Symlinked_folders_are_not_followed()
    {
        File("real/data.bin", 1000);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_root, "link"), Path.Combine(_root, "real"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return; // creating symlinks needs elevated rights on some Windows setups
        }

        var root = NewScanner().Scan(_root);

        Assert.Equal(1000, root.Size);
        Assert.DoesNotContain(root.Children, c => c.Name == "link");
    }

    [Fact]
    public void Hidden_files_are_counted()
    {
        File("hidden.bin", 700);
        var path = Path.Combine(_root, "hidden.bin");
        System.IO.File.SetAttributes(path, System.IO.File.GetAttributes(path) | FileAttributes.Hidden);

        Assert.Equal(700, NewScanner().Scan(_root).Size);
    }

    [Fact]
    public void Top_folders_prefers_the_dominant_child_over_its_parent()
    {
        File("users/laure/AppData/big.bin", 9000);
        File("users/laure/small.bin", 100);
        File("users/other/x.bin", 200);

        var root = NewScanner().Scan(_root);
        var top = TopFolders.Find(root, 10).Select(n => n.Name).ToList();

        Assert.Contains("AppData", top);
        Assert.DoesNotContain("laure", top); // 98% of laure is AppData
        Assert.DoesNotContain("users", top); // 98% of users is laure -> dominated too
        Assert.Equal("AppData", top[0]);
    }

    [Fact]
    public void Excluded_folders_are_skipped()
    {
        File("keep/a.bin", 100);
        File("skip/b.bin", 900);

        var root = NewScanner(new ScanOptions { PruneBelowBytes = 0, Excludes = new[] { Path.Combine(_root, "skip") } }).Scan(_root);

        Assert.Equal(100, root.Size);
    }

    [Fact]
    public void Small_folders_are_pruned_from_the_tree()
    {
        File("tiny/deep/a.bin", 10);

        var root = NewScanner(new ScanOptions { PruneBelowBytes = 1024 }).Scan(_root);

        Assert.Equal(10, root.Size);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Cancelled_scan_returns_without_throwing()
    {
        File("a/a.bin", 10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var root = NewScanner().Scan(_root, cts.Token);

        Assert.NotNull(root);
    }

    [Fact]
    public void Snapshot_round_trips_the_tree()
    {
        File("a/one.bin", 1000);
        File("a/deep/two.bin", 2000);
        File("b/three.bin", 500);
        var scanned = NewScanner().Scan(_root);
        var cache = new SnapshotCache(Path.Combine(_root, ".cache"));

        Assert.Null(cache.TryLoad(scanned.FullPath));
        cache.Save(scanned);
        var loaded = cache.TryLoad(scanned.FullPath);

        Assert.NotNull(loaded);
        Assert.Equal(scanned.Size, loaded!.Root.Size);
        Assert.Equal(scanned.FileCount, loaded.Root.FileCount);
        var deep = loaded.Root.Descendants().Single(n => n.Name == "deep");
        Assert.Equal(Path.Combine(_root, "a", "deep"), deep.FullPath);
        Assert.Equal(2000, deep.Size);
        Assert.True(deep.IsComplete);
    }

    [Fact]
    public void Corrupt_snapshot_is_ignored()
    {
        var dir = Path.Combine(_root, ".cache");
        var cache = new SnapshotCache(dir);
        cache.Save(NewScanner().Scan(_root));
        foreach (var f in Directory.GetFiles(dir)) System.IO.File.WriteAllText(f, "{ not json");

        Assert.Null(cache.TryLoad(DiskScanner.NormalizeRoot(_root)));
    }

    [Fact]
    public void Tree_is_complete_after_scan_and_root_is_available_during_it()
    {
        File("a/one.bin", 10);
        var scanner = NewScanner();
        Assert.Null(scanner.Root);

        var root = scanner.Scan(_root);

        Assert.Same(root, scanner.Root);
        Assert.All(root.Descendants().Append(root), n => Assert.True(n.IsComplete));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5 GB")]
    public void Formats_sizes(long bytes, string expected) => Assert.Equal(expected, SizeFormat.Format(bytes));
}

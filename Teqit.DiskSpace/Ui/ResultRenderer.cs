using Spectre.Console;
using Teqit.DiskSpace.Scanning;

namespace Teqit.DiskSpace.Ui;

public static class ResultRenderer
{
    public static void RenderHeader(DirNode root, TimeSpan elapsed, ScanProgress progress, bool cancelled) =>
        AnsiConsole.Write(BuildHeader(root, elapsed, progress, cancelled));

    public static Panel BuildHeader(DirNode root, TimeSpan elapsed, ScanProgress progress, bool cancelled)
    {
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Scan root[/]", $"[bold]{Markup.Escape(root.FullPath)}[/]");
        grid.AddRow("[grey]Scanned[/]", $"[bold]{SizeFormat.Format(root.Size)}[/] in {root.FileCount:N0} files, {progress.Dirs:N0} folders ({elapsed.TotalSeconds:0.#}s)");
        try
        {
            var d = new DriveInfo(Path.GetPathRoot(root.FullPath)!);
            if (d.IsReady)
            {
                var used = d.TotalSize - d.AvailableFreeSpace;
                grid.AddRow("[grey]Drive[/]", $"{SizeFormat.Format(used)} used of {SizeFormat.Format(d.TotalSize)}, [bold]{SizeFormat.Format(d.AvailableFreeSpace)} free[/] ({Percent(used, d.TotalSize)} used)");
            }
        }
        catch (Exception) { /* drive info is a nicety */ }

        if (progress.Errors > 0)
            grid.AddRow("[yellow]Skipped[/]", $"[yellow]{progress.Errors:N0} folders were not accessible - numbers may be too low. Run as Administrator/root for a full picture.[/]");
        if (cancelled)
            grid.AddRow("[red]Cancelled[/]", "[red]Partial results only.[/]");

        return new Panel(grid).Header("[bold] Teqit DiskSpace [/]").Border(BoxBorder.Rounded);
    }

    /// <summary>Direct children of a node, biggest first, with bars.</summary>
    public static void RenderChildren(DirNode node, string title, int max = 40, bool scanning = false) =>
        AnsiConsole.Write(BuildChildren(node, title, max, scanning));

    public static Table BuildChildren(DirNode node, string title, int max = 40, bool scanning = false)
    {
        var table = NewTable(title);
        table.AddColumn("Folder");
        table.AddColumn(new TableColumn("Size").RightAligned());
        table.AddColumn(new TableColumn("% of folder").RightAligned());
        var diskTotal = DiskTotal(node.FullPath);
        if (diskTotal > 0) table.AddColumn(new TableColumn("% of disk").RightAligned());
        table.AddColumn(new TableColumn("").NoWrap());
        table.AddColumn(new TableColumn("Files").RightAligned());

        void Row(string name, long size, string bar, string files)
        {
            var cells = new List<string> { name, SizeFormat.Format(size), Percent(size, node.Size) };
            if (diskTotal > 0) cells.Add(Percent(size, diskTotal));
            cells.Add(bar);
            cells.Add(files);
            table.AddRow(cells.ToArray());
        }

        var sorted = node.Children.OrderByDescending(c => c.Size).ToList();
        foreach (var c in sorted.Take(max))
            Row(Markup.Escape(c.Name) + (c.HadErrors ? " [yellow]![/]" : "") + (scanning && !c.IsComplete ? " [blue]⟳[/]" : ""), c.Size, Bar(c.Size, node.Size, 14), c.FileCount.ToString("N0"));

        if (sorted.Count > max)
            Row($"[grey]... {sorted.Count - max} more folders[/]", sorted.Skip(max).Sum(c => c.Size), "", "");
        if (node.OwnFilesSize > 0)
            Row("[grey](files directly in this folder)[/]", node.OwnFilesSize, Bar(node.OwnFilesSize, node.Size, 14), "");

        if (diskTotal > 0)
            table.Caption($"[grey]This folder: [bold]{SizeFormat.Format(node.Size)}[/] of a [bold]{SizeFormat.Format(diskTotal)}[/] disk ({Percent(node.Size, diskTotal)})[/]");

        return table;
    }

    public static void RenderTop(DirNode root, IReadOnlyList<DirNode> top)
    {
        AnsiConsole.Write(BuildTop(root, top));
        AnsiConsole.MarkupLine("[grey]Folders that are mostly one big subfolder are replaced by that subfolder; remaining entries may still overlap.[/]");
    }

    public static Table BuildTop(DirNode root, IReadOnlyList<DirNode> top, bool scanning = false)
    {
        var table = NewTable($"Top {top.Count} largest folders (drilled down)");
        table.AddColumn(new TableColumn("#").RightAligned());
        table.AddColumn(new TableColumn("Path").NoWrap());
        table.AddColumn(new TableColumn("Size").RightAligned());
        table.AddColumn(new TableColumn("% of scan").RightAligned());
        var diskTotal = DiskTotal(root.FullPath);
        if (diskTotal > 0) table.AddColumn(new TableColumn("% of disk").RightAligned());
        table.AddColumn("");

        const int barWidth = 12;
        int pathWidth = Math.Max(20, SafeWidth() - (diskTotal > 0 ? 70 : 58));
        int i = 1;
        foreach (var n in top)
        {
            var cells = new List<string> { i++.ToString(), PathMarkup(n.FullPath, pathWidth) + (scanning && !n.IsComplete ? " [blue]⟳[/]" : ""), SizeFormat.Format(n.Size), Percent(n.Size, root.Size) };
            if (diskTotal > 0) cells.Add(Percent(n.Size, diskTotal));
            cells.Add(Bar(n.Size, root.Size, barWidth));
            table.AddRow(cells.ToArray());
        }

        return table;
    }

    private static Table NewTable(string title) =>
        new Table().Title($"[bold]{Markup.Escape(title)}[/]").Border(TableBorder.Rounded).BorderColor(Color.Grey);

    public static string Bar(long value, long total, int width = 20)
    {
        double ratio = total <= 0 ? 0 : Math.Clamp((double)value / total, 0, 1);
        int filled = (int)Math.Round(ratio * width);
        if (filled == 0 && value > 0) filled = 1;
        var color = ratio >= 0.25 ? "red" : ratio >= 0.10 ? "yellow" : "green";
        return $"[{color}]{new string('█', filled)}[/][grey23]{new string('░', width - filled)}[/]";
    }

    public static string Percent(long value, long total) =>
        total <= 0 ? "-" : $"{100.0 * value / total:0.#}%";

    public static string PathMarkup(string path, int max)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileName(path);
        if (name.Length == 0) return Markup.Escape(path);
        var prefix = dir.Length == 0 ? "" : dir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        int room = max - name.Length;
        if (prefix.Length > room)
        {
            int keep = Math.Max(4, room - 1);
            if (keep < prefix.Length) prefix = "…" + prefix[^keep..];
        }
        return $"[grey]{Markup.Escape(prefix)}[/][bold]{Markup.Escape(name)}[/]";
    }

    private static long DiskTotal(string path)
    {
        try
        {
            var d = new DriveInfo(Path.GetPathRoot(path)!);
            return d.IsReady ? d.TotalSize : 0;
        }
        catch (Exception) { return 0; }
    }

    private static int SafeWidth()
    {
        try { return AnsiConsole.Profile.Width; } catch (Exception) { return 120; }
    }
}

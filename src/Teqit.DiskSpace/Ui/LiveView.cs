using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Rendering;
using Teqit.DiskSpace.Scanning;

namespace Teqit.DiskSpace.Ui;

/// <summary>The screen shown while the scan runs in the background; redraws in place until the scan ends.</summary>
public static class LiveView
{
    private static readonly string[] Frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    public static bool Run(Task scan, DiskScanner scanner, Snapshot? stale, Stopwatch clock, int topCount, bool allowKeys)
    {
        bool browseRequested = false;
        var topCache = new List<DirNode>();
        var topAt = DateTime.MinValue;
        int frame = 0;

        IRenderable Build()
        {
            var view = stale?.Root ?? scanner.Root;
            var rows = new List<IRenderable> { BuildStatus(scanner, stale, clock, Frames[frame++ % Frames.Length], allowKeys) };
            if (view != null)
            {
                int budget = Math.Clamp((WindowHeight() - 12) / 2, 3, 12);
                rows.Add(ResultRenderer.BuildChildren(view, "Overview: " + view.FullPath, budget, scanning: stale == null));

                if (DateTime.UtcNow - topAt > TimeSpan.FromSeconds(1)) // the top list walks the whole tree
                {
                    topCache = TopFolders.Find(view, Math.Min(topCount, budget));
                    topAt = DateTime.UtcNow;
                }
                rows.Add(ResultRenderer.BuildTop(view, topCache, scanning: stale == null));
            }
            return new Rows(rows);
        }

        AnsiConsole.Live(Build()).Overflow(VerticalOverflow.Ellipsis).Start(ctx =>
        {
            while (!scan.IsCompleted)
            {
                if (allowKeys && ReadBrowseKey() && (stale?.Root ?? scanner.Root) != null)
                {
                    browseRequested = true;
                    return;
                }
                ctx.UpdateTarget(Build());
                ctx.Refresh();
                scan.Wait(250);
            }
        });
        return browseRequested;
    }

    private static IRenderable BuildStatus(DiskScanner scanner, Snapshot? stale, Stopwatch clock, string spinner, bool allowKeys)
    {
        var p = scanner.Progress;
        long files = Volatile.Read(ref p.Files);
        var lines = new List<string>
        {
            $"[blue]{spinner}[/] [bold]Scanning in background[/]  {files:N0} files · {SizeFormat.Format(Volatile.Read(ref p.Bytes))} · {clock.Elapsed:mm\\:ss}",
        };

        if (stale != null)
        {
            long expected = Math.Max(1, stale.Root.FileCount);
            var pct = Math.Min(99, 100.0 * files / expected);
            lines.Add($"[yellow]Showing snapshot from {Age(DateTime.UtcNow - stale.ScannedAtUtc)} ago (may be out of date)[/] - fresh scan {pct:0}% {ResultRenderer.Bar((long)pct, 100, 16)}");
        }
        else
        {
            lines.Add("[yellow]Results are provisional[/] and update live; [blue]⟳[/] marks folders still being scanned.");
        }

        var current = p.Current;
        if (current.Length > 90) current = "…" + current[^89..];
        lines.Add($"[grey]{Markup.Escape(current)}[/]");
        lines.Add(allowKeys
            ? "[grey]Press [bold]b[/] to browse the results now · [bold]Ctrl+C[/] to stop and keep partial results[/]"
            : "[grey][bold]Ctrl+C[/] to stop and keep partial results[/]");

        return new Panel(new Markup(string.Join('\n', lines))).Header("[bold] Teqit DiskSpace [/]").Border(BoxBorder.Rounded);
    }

    private static bool ReadBrowseKey()
    {
        bool hit = false;
        try
        {
            while (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true).Key;
                if (k is ConsoleKey.B or ConsoleKey.Enter) hit = true;
            }
        }
        catch (InvalidOperationException) { /* no console input */ }
        return hit;
    }

    public static string Age(TimeSpan span) =>
        span.TotalMinutes < 1 ? "moments" :
        span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min" :
        span.TotalDays < 1 ? $"{(int)span.TotalHours} h" :
        $"{(int)span.TotalDays} d";

    private static int WindowHeight()
    {
        try { return Console.WindowHeight; } catch (IOException) { return 40; }
    }
}

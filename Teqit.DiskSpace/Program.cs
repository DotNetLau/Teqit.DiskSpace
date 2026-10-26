using System.Diagnostics;
using Spectre.Console;
using Teqit.DiskSpace;
using Teqit.DiskSpace.Scanning;
using Teqit.DiskSpace.Ui;

Options options;
try
{
    options = Options.Parse(args);
}
catch (Exception ex) when (ex is ArgumentException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

if (options.Help)
{
    Console.WriteLine(Options.HelpText);
    return 0;
}

if (!Directory.Exists(options.Path))
{
    AnsiConsole.MarkupLine($"[red]Folder not found:[/] {Markup.Escape(options.Path)}");
    return 2;
}

using var cts = new CancellationTokenSource();
bool scanRunning = true;
Console.CancelKeyPress += (_, e) =>
{
    if (!scanRunning || cts.IsCancellationRequested) return; // outside the scan (or a second Ctrl+C) exit normally
    e.Cancel = true;
    cts.Cancel();
};

bool liveUi = !Console.IsOutputRedirected;
bool keys = liveUi && !Console.IsInputRedirected;
var cache = options.NoCache ? null : new SnapshotCache();
var rootPath = DiskScanner.NormalizeRoot(options.Path);
var stale = liveUi ? cache?.TryLoad(rootPath) : null;

var scanner = new DiskScanner(new ScanOptions { CrossMounts = options.CrossMounts, Excludes = options.Excludes });
var clock = Stopwatch.StartNew();
var elapsed = TimeSpan.Zero;
var scan = Task.Run(() =>
{
    var result = scanner.Scan(options.Path, cts.Token);
    elapsed = clock.Elapsed;
    scanRunning = false;
    return result;
});

while (liveUi && !scan.IsCompleted)
{
    bool browse = LiveView.Run(scan, scanner, stale, clock, options.Top, keys);
    if (browse)
        Interactive.Run(stale?.Root ?? scanner.Root!, options.Top, () => !scan.IsCompleted);
}

var root = await scan;
bool cancelled = cts.IsCancellationRequested;
if (!cancelled) cache?.Save(root);

var top = TopFolders.Find(root, options.Top);

if (liveUi) AnsiConsole.Clear();
ResultRenderer.RenderHeader(root, elapsed, scanner.Progress, cancelled);
ResultRenderer.RenderChildren(root, $"Overview: {root.FullPath}");
ResultRenderer.RenderTop(root, top);

if (options.Interactive && keys)
    Interactive.Run(root, options.Top);

return 0;

using System.Diagnostics;
using Spectre.Console;
using Teqit.DiskSpace.Scanning;

namespace Teqit.DiskSpace.Ui;

public static class Interactive
{
    private enum Kind { Folder, Up, Root, Top, Open, Quit }

    private sealed record Choice(Kind Kind, DirNode? Node, string Label);

    /// <param name="isScanning">While true the scan is still running: sizes are provisional and Quit returns to the live view.</param>
    public static void Run(DirNode root, int topCount, Func<bool>? isScanning = null)
    {
        var current = root;
        while (true)
        {
            bool scanning = isScanning?.Invoke() ?? false;
            AnsiConsole.WriteLine();
            if (scanning)
                AnsiConsole.MarkupLine("[blue]⟳ Scan still running in the background - sizes are provisional (⟳ = folder not finished).[/]");
            ResultRenderer.RenderChildren(current, current.FullPath, scanning: scanning);

            var choices = new List<Choice>();
            foreach (var c in current.Children.OrderByDescending(c => c.Size).Take(25))
                choices.Add(new Choice(Kind.Folder, c, $"{Markup.Escape(c.Name)}  [grey]{SizeFormat.Format(c.Size)}[/]" + (scanning && !c.IsComplete ? " [blue]⟳[/]" : "")));
            if (current.Parent != null) choices.Add(new Choice(Kind.Up, current.Parent, "[blue]^ Up[/]"));
            if (current != root) choices.Add(new Choice(Kind.Root, root, "[blue]<< Back to scan root[/]"));
            choices.Add(new Choice(Kind.Top, null, "[blue]* Show top folders again[/]"));
            choices.Add(new Choice(Kind.Open, current, "[blue]Open this folder in file manager[/]"));
            choices.Add(new Choice(Kind.Quit, null, scanning ? "[red]Back to live scan view[/]" : "[red]Quit[/]"));

            var pick = AnsiConsole.Prompt(new SelectionPrompt<Choice>()
                .Title("Drill down into a folder:")
                .PageSize(18)
                .UseConverter(c => c.Label)
                .AddChoices(choices));

            switch (pick.Kind)
            {
                case Kind.Folder:
                case Kind.Up:
                case Kind.Root:
                    current = pick.Node!;
                    break;
                case Kind.Top:
                    ResultRenderer.RenderTop(root, TopFolders.Find(root, topCount));
                    break;
                case Kind.Open:
                    OpenInFileManager(pick.Node!.FullPath);
                    break;
                case Kind.Quit:
                    return;
            }
        }
    }

    private static void OpenInFileManager(string path)
    {
        try
        {
            var cmd = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            Process.Start(new ProcessStartInfo(cmd) { ArgumentList = { path }, UseShellExecute = false });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Could not open file manager:[/] {Markup.Escape(ex.Message)}");
        }
    }
}

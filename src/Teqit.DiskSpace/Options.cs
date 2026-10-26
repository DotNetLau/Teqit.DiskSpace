namespace Teqit.DiskSpace;

public sealed class Options
{
    public string Path { get; set; } = DefaultRoot();
    public int Top { get; set; } = 30;
    public bool Interactive { get; set; } = true;
    public bool CrossMounts { get; set; }
    public bool NoCache { get; set; }
    public List<string> Excludes { get; } = new();
    public bool Help { get; set; }
    public bool ShowVersion { get; set; }

    public static string DefaultRoot() =>
        OperatingSystem.IsWindows()
            ? System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\"
            : "/";

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help" or "/?": o.Help = true; break;
                case "--version": o.ShowVersion = true; break;
                case "-p" or "--path": o.Path = Next(args, ref i); break;
                case "-n" or "--top": o.Top = int.Parse(Next(args, ref i)); break;
                case "--exclude": o.Excludes.Add(System.IO.Path.GetFullPath(Next(args, ref i))); break;
                case "--no-interactive": o.Interactive = false; break;
                case "--cross-mounts": o.CrossMounts = true; break;
                case "--no-cache": o.NoCache = true; break;
                default:
                    if (!args[i].StartsWith('-') && i == 0) o.Path = args[i];
                    else throw new ArgumentException($"Unknown argument '{args[i]}'. Use --help.");
                    break;
            }
        }
        if (o.Top < 1) throw new ArgumentException("--top must be at least 1.");
        return o;
    }

    private static string Next(string[] args, ref int i) =>
        i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}.");

    public const string HelpText = """
        diskspace - find the biggest folders on your disk

        Usage: diskspace [path] [options]

          -p, --path <dir>     Folder to scan (default: system drive root, or / on Linux)
          -n, --top <count>    Size of the drilled-down list (default: 30)
              --exclude <dir>  Skip a folder (repeatable)
              --cross-mounts   Linux: also descend into other mounted filesystems
              --no-cache       Do not show/save the snapshot of the previous scan
              --no-interactive Print the report and exit (no drill-down browser)
              --version        Show the version
          -h, --help           Show this help
        """;
}

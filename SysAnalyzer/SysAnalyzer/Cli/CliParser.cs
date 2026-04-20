namespace SysAnalyzer.Cli;

public class CliOptions
{
    public string Profile { get; set; } = "gaming";
    public string? Label { get; set; }
    public string? Process { get; set; }
    public string? ConfigPath { get; set; }
    public string? OutputDir { get; set; }
    public int? Interval { get; set; }
    public bool Elevate { get; set; }
    public bool NoPresentmon { get; set; }
    public bool NoEtw { get; set; }
    public bool NoLive { get; set; }
    public string? Compare { get; set; }
    public bool Csv { get; set; }
    public bool Etl { get; set; }
    public bool NoElevate { get; set; }
    public int? Duration { get; set; }
    public bool Version { get; set; }
    public bool Help { get; set; }
}

public static class CliParser
{
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        int i = 0;

        while (i < args.Length)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--profile":
                    options.Profile = NextArg(args, ref i, "--profile");
                    break;
                case "--label":
                    options.Label = NextArg(args, ref i, "--label");
                    break;
                case "--process":
                    options.Process = NextArg(args, ref i, "--process");
                    break;
                case "--config":
                    options.ConfigPath = NextArg(args, ref i, "--config");
                    break;
                case "--output":
                    options.OutputDir = NextArg(args, ref i, "--output");
                    break;
                case "--interval":
                    options.Interval = int.Parse(NextArg(args, ref i, "--interval"));
                    break;
                case "--elevate":
                    options.Elevate = true;
                    break;
                case "--no-presentmon":
                    options.NoPresentmon = true;
                    break;
                case "--no-etw":
                    options.NoEtw = true;
                    break;
                case "--no-live":
                    options.NoLive = true;
                    break;
                case "--compare":
                    options.Compare = NextArg(args, ref i, "--compare");
                    break;
                case "--csv":
                    options.Csv = true;
                    break;
                case "--etl":
                    options.Etl = true;
                    break;
                case "--no-elevate":
                    options.NoElevate = true;
                    break;
                case "--duration":
                    options.Duration = int.Parse(NextArg(args, ref i, "--duration"));
                    break;
                case "--version":
                    options.Version = true;
                    break;
                case "--help":
                case "-h":
                    options.Help = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
            i++;
        }

        return options;
    }

    private static string NextArg(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"{flag} requires a value");
        return args[++i];
    }

    public static string GetHelpText()
    {
        return """
            SysAnalyzer — Windows 11 Bottleneck Analysis Tool

            USAGE:
              SysAnalyzer [options]

            OPTIONS:
              --profile <name>     Scoring profile (gaming, compiling, general_interactive)
              --label <text>       Label for this capture run
              --process <name>     Target process to monitor
              --config <path>      Path to config.yaml
              --output <dir>       Output directory for reports
              --interval <ms>      Poll interval in milliseconds
              --duration <sec>     Auto-stop after N seconds
              --compare <path>     Compare with a previous baseline JSON
              --elevate            Re-launch with admin privileges (default: auto)
              --no-elevate         Skip admin elevation (Tier 1 only)
              --no-presentmon      Disable PresentMon integration
              --no-etw             Disable ETW sessions
              --no-live            Disable live console display
              --csv                Enable CSV time-series export
              --etl                Enable ETL trace export
              --version            Print version and exit
              --help, -h           Show this help message

            EXIT CODES:
              0  Success
              1  Configuration error
              2  No providers available
              3  Capture error
            """;
    }
}

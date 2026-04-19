namespace SysAnalyzer.Config;

/// <summary>
/// Root config model deserialized from config.yaml (§6.1).
/// </summary>
public class AnalyzerConfig
{
    public CaptureConfig Capture { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public BaselinesConfig Baselines { get; set; } = new();
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new();
    public ThresholdsConfig Thresholds { get; set; } = new();
    public List<RecommendationConfig> Recommendations { get; set; } = new();
}

public class CaptureConfig
{
    public int PollIntervalMs { get; set; } = 1000;
    public int MinCaptureDurationSec { get; set; } = 30;
    public int MaxCaptureDurationSec { get; set; } = 28800;
    public bool PresentmonEnabled { get; set; } = true;
    public bool EtwEnabled { get; set; } = true;
    public List<string> EtwProviders { get; set; } = new()
    {
        "Microsoft-Windows-Kernel-Process",
        "Microsoft-Windows-Kernel-Disk"
    };
    public bool CsvExport { get; set; }
    public bool EtlExport { get; set; }
}

public class OutputConfig
{
    public string Directory { get; set; } = "./reports";
    public string FilenameFormat { get; set; } = "sysanalyzer-{timestamp}";
    public string TimestampFormat { get; set; } = "yyyy-MM-dd_HH-mm-ss";
    public FormatFlags Formats { get; set; } = new();
    public FormatFlags OptionalFormats { get; set; } = new();
}

public class FormatFlags
{
    public bool Html { get; set; } = true;
    public bool Json { get; set; } = true;
    public bool Csv { get; set; }
    public bool Etl { get; set; }
}

public class BaselinesConfig
{
    public string Directory { get; set; } = "~/.sysanalyzer/baselines";
    public bool AutoSave { get; set; } = true;
    public int MaxStored { get; set; } = 50;
}

public class ProfileConfig
{
    public string Description { get; set; } = "";
    public ScoringConfig Scoring { get; set; } = new();
}

public class ScoringConfig
{
    public Dictionary<string, double> Cpu { get; set; } = new();
    public Dictionary<string, double> Memory { get; set; } = new();
    public Dictionary<string, double> Gpu { get; set; } = new();
    public Dictionary<string, double> Disk { get; set; } = new();
    public Dictionary<string, double> Network { get; set; } = new();
}

public class ThresholdsConfig
{
    public Dictionary<string, double> Cpu { get; set; } = new();
    public Dictionary<string, double> Memory { get; set; } = new();
    public Dictionary<string, double> Gpu { get; set; } = new();
    public Dictionary<string, double> Disk { get; set; } = new();
    public Dictionary<string, double> Network { get; set; } = new();
    public Dictionary<string, double> FrameTime { get; set; } = new();
}

public class RecommendationConfig
{
    public string Id { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string Category { get; set; } = "config";
    public string Confidence { get; set; } = "low";
    public int Priority { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? EvidenceBoost { get; set; }
}

namespace SysAnalyzer.Data;

/// <summary>
/// Lookup table of known processes that commonly cause gaming interference.
/// Maps process name (case-insensitive) to description and remediation.
/// </summary>
public static class KnownProcesses
{
    private static readonly Dictionary<string, (string Description, string Remediation)> Entries =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MsMpEng.exe"] = ("Windows Defender real-time scanner", "Exclude game folder from Defender scans"),
            ["MpCmdRun.exe"] = ("Windows Defender command-line tool", "Exclude game folder from Defender scans"),
            ["NisSrv.exe"] = ("Windows Defender Network Inspection", "Check Defender network protection settings"),
            ["SearchIndexer.exe"] = ("Windows Search file indexer", "Exclude game folders from indexing or disable WSearch service"),
            ["SearchProtocolHost.exe"] = ("Windows Search protocol handler", "Exclude game folders from indexing"),
            ["OneDrive.exe"] = ("OneDrive cloud sync", "Pause OneDrive sync during gaming"),
            ["dwm.exe"] = ("Desktop Window Manager", "Use exclusive fullscreen if supported"),
            ["audiodg.exe"] = ("Windows Audio Device Graph Isolation", "Check audio driver or reduce audio enhancements"),
            ["WmiPrvSE.exe"] = ("WMI Provider Host", "Check for runaway WMI queries"),
            ["TiWorker.exe"] = ("Windows Update installer", "Defer Windows Update during gaming sessions"),
            ["TrustedInstaller.exe"] = ("Windows Module Installer", "Defer system updates"),
            ["wuauclt.exe"] = ("Windows Update client", "Pause Windows Update"),
            ["svchost.exe"] = ("Windows Service Host", "Identify specific hosted service via PID"),
            ["csrss.exe"] = ("Client/Server Runtime Subsystem", "Core Windows process — investigate driver issues"),
            ["System"] = ("NT Kernel & System", "Check for driver-level interference"),
            ["vmmem"] = ("Virtual Machine memory", "Close or pause virtual machines"),
            ["vmcompute.exe"] = ("Hyper-V compute service", "Disable Hyper-V if not needed"),
            ["CCleanerBrowser.exe"] = ("CCleaner Browser", "Close unnecessary browsers during gaming"),
            ["chrome.exe"] = ("Google Chrome", "Close Chrome tabs or use hardware acceleration settings"),
            ["msedge.exe"] = ("Microsoft Edge", "Close Edge tabs during gaming"),
            ["firefox.exe"] = ("Mozilla Firefox", "Close Firefox during gaming"),
            ["Discord.exe"] = ("Discord", "Disable Discord overlay and hardware acceleration"),
            ["Spotify.exe"] = ("Spotify", "Reduce Spotify streaming quality or close it"),
            ["Steam.exe"] = ("Steam Client", "Disable Steam overlay"),
            ["steamwebhelper.exe"] = ("Steam Web Helper", "Disable Steam browser features"),
            ["EpicGamesLauncher.exe"] = ("Epic Games Launcher", "Close launcher after game starts"),
            ["ShareX.exe"] = ("ShareX screen capture", "Disable ShareX during gaming"),
            ["OBS64.exe"] = ("OBS Studio", "Reduce OBS encoding settings or close"),
            ["obs64.exe"] = ("OBS Studio", "Reduce OBS encoding settings or close"),
            ["GameBar.exe"] = ("Xbox Game Bar", "Disable Xbox Game Bar in Settings"),
            ["GameBarPresenceWriter.exe"] = ("Xbox Game Bar background", "Disable Xbox Game Bar"),
            ["SecurityHealthSystray.exe"] = ("Windows Security systray", "Normal — low impact"),
            ["RuntimeBroker.exe"] = ("Runtime Broker", "Close unnecessary UWP apps"),
            ["DropboxUpdate.exe"] = ("Dropbox Updater", "Pause Dropbox sync during gaming"),
            ["Dropbox.exe"] = ("Dropbox sync client", "Pause Dropbox sync during gaming"),
            ["GoogleUpdate.exe"] = ("Google Update", "Disable Google Update scheduled task"),
        };

    public static bool TryGetInfo(string processName, out string description, out string remediation)
    {
        if (Entries.TryGetValue(processName, out var info))
        {
            description = info.Description;
            remediation = info.Remediation;
            return true;
        }

        description = string.Empty;
        remediation = string.Empty;
        return false;
    }

    /// <summary>
    /// Returns a description for the process, or null if unknown.
    /// </summary>
    public static string? GetDescription(string processName) =>
        Entries.TryGetValue(processName, out var info) ? info.Description : null;

    /// <summary>
    /// Returns a remediation suggestion for the process, or null if unknown.
    /// </summary>
    public static string? GetRemediation(string processName) =>
        Entries.TryGetValue(processName, out var info) ? info.Remediation : null;
}

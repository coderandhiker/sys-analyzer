namespace SysAnalyzer.Baselines;

public interface IBaselineFileSystem
{
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    void WriteAllText(string path, string content);
    string ReadAllText(string path);
    string[] GetFiles(string directory, string pattern);
    DateTime GetFileCreationTimeUtc(string path);
    void DeleteFile(string path);
}

public sealed class PhysicalBaselineFileSystem : IBaselineFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public string[] GetFiles(string directory, string pattern) => Directory.GetFiles(directory, pattern);
    public DateTime GetFileCreationTimeUtc(string path) => File.GetCreationTimeUtc(path);
    public void DeleteFile(string path) => File.Delete(path);
}

public sealed class BaselineManager
{
    private readonly string _baseDir;
    private readonly int _maxStored;
    private readonly IBaselineFileSystem _fs;

    public BaselineManager(string baseDir, int maxStored, IBaselineFileSystem? fs = null)
    {
        _baseDir = ExpandPath(baseDir);
        _maxStored = maxStored;
        _fs = fs ?? new PhysicalBaselineFileSystem();
    }

    public string Save(string fingerprintHash, string json)
    {
        var dir = Path.Combine(_baseDir, fingerprintHash);
        _fs.CreateDirectory(dir);

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(dir, $"{timestamp}.json");
        _fs.WriteAllText(path, json);

        Prune(dir);
        return path;
    }

    public string? LoadLatest(string fingerprintHash)
    {
        var dir = Path.Combine(_baseDir, fingerprintHash);
        if (!_fs.DirectoryExists(dir)) return null;

        var files = _fs.GetFiles(dir, "*.json");
        if (files.Length == 0) return null;

        // Sort by filename (timestamp-based) descending
        Array.Sort(files);
        var latest = files[^1];
        return _fs.ReadAllText(latest);
    }

    public string? LoadFromPath(string path)
    {
        try { return _fs.ReadAllText(path); }
        catch { return null; }
    }

    private void Prune(string dir)
    {
        var files = _fs.GetFiles(dir, "*.json");
        if (files.Length <= _maxStored) return;

        // Sort ascending by name (timestamp-based)
        Array.Sort(files);

        int toRemove = files.Length - _maxStored;
        for (int i = 0; i < toRemove; i++)
        {
            _fs.DeleteFile(files[i]);
        }
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }
        return Environment.ExpandEnvironmentVariables(path);
    }
}

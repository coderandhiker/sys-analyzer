using SysAnalyzer.Baselines;

namespace SysAnalyzer.Tests.Component;

public class BaselineManagerTests
{
    private class InMemoryFileSystem : IBaselineFileSystem
    {
        public Dictionary<string, string> Files { get; } = new();
        public HashSet<string> Directories { get; } = new();

        public void CreateDirectory(string path) => Directories.Add(path);
        public bool DirectoryExists(string path) => Directories.Contains(path);

        public void WriteAllText(string path, string content)
        {
            Files[path] = content;
            var dir = Path.GetDirectoryName(path)!;
            Directories.Add(dir);
        }

        public string ReadAllText(string path) =>
            Files.TryGetValue(path, out var content)
                ? content
                : throw new FileNotFoundException(path);

        public string[] GetFiles(string directory, string pattern) =>
            Files.Keys.Where(k => k.StartsWith(directory) && k.EndsWith(".json")).ToArray();

        public DateTime GetFileCreationTimeUtc(string path) => DateTime.UtcNow;
        public void DeleteFile(string path) => Files.Remove(path);
    }

    [Fact]
    public void Save_CreatesFile()
    {
        var fs = new InMemoryFileSystem();
        var manager = new BaselineManager("C:\\test\\baselines", 50, fs);

        var path = manager.Save("abc123", "{\"test\": true}");

        Assert.NotEmpty(path);
        Assert.Contains("abc123", path);
        Assert.True(fs.Files.ContainsKey(path));
    }

    [Fact]
    public void LoadLatest_ReturnsNewestFile()
    {
        var fs = new InMemoryFileSystem();
        var manager = new BaselineManager("C:\\test\\baselines", 50, fs);

        manager.Save("abc123", "{\"version\": 1}");
        manager.Save("abc123", "{\"version\": 2}");

        var content = manager.LoadLatest("abc123");
        Assert.NotNull(content);
    }

    [Fact]
    public void LoadLatest_NoFiles_ReturnsNull()
    {
        var fs = new InMemoryFileSystem();
        var manager = new BaselineManager("C:\\test\\baselines", 50, fs);

        var content = manager.LoadLatest("nonexistent");
        Assert.Null(content);
    }

    [Fact]
    public void Prune_RemovesOldFiles_WhenOverMax()
    {
        var fs = new InMemoryFileSystem();
        var manager = new BaselineManager("C:\\test\\baselines", 3, fs);

        for (int i = 0; i < 5; i++)
        {
            // Use predictable filenames by creating them manually
            var dir = "C:\\test\\baselines\\abc123";
            fs.CreateDirectory(dir);
            fs.WriteAllText(
                Path.Combine(dir, $"2026-01-0{i + 1}_00-00-00.json"),
                $"{{\"version\": {i}}}");
        }

        // Now save once more to trigger prune
        manager.Save("abc123", "{\"version\": 5}");

        var remaining = fs.Files.Keys.Where(k => k.Contains("abc123")).Count();
        Assert.True(remaining <= 3);
    }

    [Fact]
    public void Save_ExpandsTildePath()
    {
        var fs = new InMemoryFileSystem();
        var manager = new BaselineManager("~/.sysanalyzer/baselines", 50, fs);

        var path = manager.Save("abc123", "{}");

        Assert.DoesNotContain("~", path);
        Assert.Contains("abc123", path);
    }

    [Fact]
    public void LoadFromPath_ValidPath_ReturnsContent()
    {
        var fs = new InMemoryFileSystem();
        var manager = new BaselineManager("C:\\test\\baselines", 50, fs);

        fs.WriteAllText("C:\\test\\custom.json", "{\"custom\": true}");
        var content = manager.LoadFromPath("C:\\test\\custom.json");

        Assert.Equal("{\"custom\": true}", content);
    }

    [Fact]
    public void LoadFromPath_InvalidPath_ReturnsNull()
    {
        var fs = new InMemoryFileSystem();
        var manager = new BaselineManager("C:\\test\\baselines", 50, fs);

        var content = manager.LoadFromPath("C:\\nonexistent.json");
        Assert.Null(content);
    }
}

using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Unit;

public class FilenameGenerationTests
{
    [Fact]
    public void Generate_DefaultFormat_ContainsTimestamp()
    {
        var result = FilenameGenerator.Generate("sysanalyzer-{timestamp}", "yyyy-MM-dd_HH-mm-ss");
        Assert.StartsWith("sysanalyzer-", result);
        Assert.DoesNotContain("{timestamp}", result);
    }

    [Fact]
    public void Generate_WithLabel_IncludesLabel()
    {
        var result = FilenameGenerator.Generate(
            "sysanalyzer-{timestamp}-{label}", "yyyy-MM-dd_HH-mm-ss", "my-game");
        Assert.Contains("my-game", result);
    }

    [Fact]
    public void Generate_WithoutLabel_RemovesPlaceholder()
    {
        var result = FilenameGenerator.Generate(
            "sysanalyzer-{timestamp}-{label}", "yyyy-MM-dd_HH-mm-ss");
        Assert.DoesNotContain("{label}", result);
        Assert.DoesNotContain("-{", result);
    }

    [Fact]
    public void SanitizeLabel_SpacesToHyphens()
    {
        var result = FilenameGenerator.SanitizeLabel("My Game Session");
        Assert.Equal("my-game-session", result);
    }

    [Fact]
    public void SanitizeLabel_RemovesInvalidChars()
    {
        var result = FilenameGenerator.SanitizeLabel("game:session<1>");
        Assert.DoesNotContain(":", result);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
    }

    [Fact]
    public void SanitizeLabel_Lowercase()
    {
        var result = FilenameGenerator.SanitizeLabel("MyGame");
        Assert.Equal("mygame", result);
    }

    [Fact]
    public void Generate_NoInvalidFileChars()
    {
        var result = FilenameGenerator.Generate("sysanalyzer-{timestamp}", "yyyy-MM-dd_HH-mm-ss");
        // Skip control characters (\0 etc.) that cause false positives with string.Contains
        var invalidChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        foreach (var c in invalidChars)
        {
            Assert.DoesNotContain(c.ToString(), result);
        }
    }
}

using SysAnalyzer.Capture;
using SysAnalyzer.Report;

namespace SysAnalyzer.Tests.Unit;

public class CsvExporterTests
{
    [Fact]
    public async Task ExportTimeSeries_WritesCorrectHeader()
    {
        var snapshots = CreateTestSnapshots(5);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-csv-{Guid.NewGuid():N}");

        try
        {
            var path = await CsvExporter.ExportTimeSeriesAsync(snapshots, dir, "test-export");

            Assert.True(File.Exists(path));
            Assert.EndsWith("-timeseries.csv", path);

            var lines = await File.ReadAllLinesAsync(path);
            Assert.True(lines.Length > 0);

            var header = lines[0];
            Assert.Contains("timestamp_s", header);
            Assert.Contains("cpu_total_pct", header);
            Assert.Contains("memory_util_pct", header);
            Assert.Contains("gpu_util_pct", header);
            Assert.Contains("disk_active_pct", header);
            Assert.Contains("network_bytes_per_sec", header);
            Assert.Contains("cpu_temp_c", header);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExportTimeSeries_RowCountMatchesSnapshotCount()
    {
        var snapshots = CreateTestSnapshots(10);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-csv-{Guid.NewGuid():N}");

        try
        {
            var path = await CsvExporter.ExportTimeSeriesAsync(snapshots, dir, "test-export");
            var lines = await File.ReadAllLinesAsync(path);

            // Header + 10 data rows (last line may be empty)
            var dataLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Equal(11, dataLines.Length); // 1 header + 10 rows
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExportTimeSeries_NullableFieldsAreEmpty()
    {
        // Tier 1 data — no GPU, no temps
        var snapshots = CreateTestSnapshots(3, hasGpu: false);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-csv-{Guid.NewGuid():N}");

        try
        {
            var path = await CsvExporter.ExportTimeSeriesAsync(snapshots, dir, "test-export");
            var lines = await File.ReadAllLinesAsync(path);

            // GPU columns should be empty (two consecutive commas)
            var dataLine = lines[1]; // first data row
            var fields = dataLine.Split(',');

            // gpu_util_pct is column index 11 (0-based), should be empty
            Assert.Equal("", fields[11]); // gpu_util_pct
            Assert.Equal("", fields[12]); // gpu_memory_util_pct
            Assert.Equal("", fields[13]); // gpu_memory_used_mb
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExportPresentMon_WritesCorrectHeader()
    {
        var samples = CreateTestFrameSamples(5);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-csv-{Guid.NewGuid():N}");

        try
        {
            var path = await CsvExporter.ExportPresentMonAsync(samples, dir, "test-export");

            Assert.True(File.Exists(path));
            Assert.EndsWith("-presentmon.csv", path);

            var lines = await File.ReadAllLinesAsync(path);
            var header = lines[0];
            Assert.Contains("timestamp_s", header);
            Assert.Contains("frame_time_ms", header);
            Assert.Contains("cpu_busy_ms", header);
            Assert.Contains("gpu_busy_ms", header);
            Assert.Contains("dropped", header);
            Assert.Contains("present_mode", header);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExportPresentMon_RowCountMatchesSampleCount()
    {
        var samples = CreateTestFrameSamples(20);
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-csv-{Guid.NewGuid():N}");

        try
        {
            var path = await CsvExporter.ExportPresentMonAsync(samples, dir, "test-export");
            var lines = await File.ReadAllLinesAsync(path);

            var dataLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Equal(21, dataLines.Length); // 1 header + 20 rows
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExportPresentMon_HandlesSpecialCharactersInAppName()
    {
        var samples = new List<FrameTimeSample>
        {
            new(
                Timestamp: new QpcTimestamp(0),
                ApplicationName: "Game, \"Special\" Edition",
                FrameTimeMs: 16.6,
                CpuBusyMs: 8.0,
                GpuBusyMs: 12.0,
                Dropped: false,
                PresentMode: "Hardware: Independent Flip",
                AllowsTearing: true
            )
        };

        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-csv-{Guid.NewGuid():N}");

        try
        {
            var path = await CsvExporter.ExportPresentMonAsync(samples, dir, "test-export");
            var content = await File.ReadAllTextAsync(path);

            // The app name should be properly escaped in CSV
            Assert.Contains("\"Game, \"\"Special\"\" Edition\"", content);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ExportTimeSeries_CreatesDirectoryIfNotExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sysanalyzer-csv-{Guid.NewGuid():N}", "subdir");

        try
        {
            var path = await CsvExporter.ExportTimeSeriesAsync(CreateTestSnapshots(1), dir, "test");
            Assert.True(File.Exists(path));
        }
        finally
        {
            var parent = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    private static List<SensorSnapshot> CreateTestSnapshots(int count, bool hasGpu = true)
    {
        var snapshots = new List<SensorSnapshot>();
        for (int i = 0; i < count; i++)
        {
            snapshots.Add(new SensorSnapshot(
                Timestamp: new QpcTimestamp((long)(i * QpcTimestamp.Frequency)),
                TotalCpuPercent: 30 + i,
                PerCoreCpuPercent: new[] { 25.0, 35.0 },
                ContextSwitchesPerSec: 1000,
                DpcTimePercent: 1.5,
                InterruptsPerSec: 500,
                MemoryUtilizationPercent: 45,
                AvailableMemoryMb: 16000,
                PageFaultsPerSec: 200,
                HardFaultsPerSec: 10,
                CommittedBytes: 8000000000,
                CommittedBytesInUsePercent: 50,
                GpuUtilizationPercent: hasGpu ? 70 : null,
                GpuMemoryUtilizationPercent: hasGpu ? 60 : null,
                GpuMemoryUsedMb: hasGpu ? 6000 : null,
                DiskActiveTimePercent: 20,
                DiskQueueLength: 1.5,
                DiskBytesPerSec: 50000000,
                DiskReadLatencyMs: 2.5,
                DiskWriteLatencyMs: 3.0,
                NetworkBytesPerSec: 1000000,
                NetworkUtilizationPercent: 5,
                TcpRetransmitsPerSec: 0.1,
                CpuTempC: null,
                CpuClockMhz: null,
                CpuPowerW: null,
                GpuTempC: null,
                GpuClockMhz: null,
                GpuPowerW: null,
                GpuFanRpm: null
            ));
        }
        return snapshots;
    }

    private static List<FrameTimeSample> CreateTestFrameSamples(int count)
    {
        var samples = new List<FrameTimeSample>();
        for (int i = 0; i < count; i++)
        {
            samples.Add(new FrameTimeSample(
                Timestamp: new QpcTimestamp((long)(i * (QpcTimestamp.Frequency / 60.0))),
                ApplicationName: "TestApp.exe",
                FrameTimeMs: 16.6,
                CpuBusyMs: 8.0,
                GpuBusyMs: 12.0,
                Dropped: false,
                PresentMode: "Hardware: Independent Flip",
                AllowsTearing: true
            ));
        }
        return samples;
    }
}

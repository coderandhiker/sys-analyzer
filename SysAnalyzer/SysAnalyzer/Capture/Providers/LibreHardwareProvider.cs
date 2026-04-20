using System.Diagnostics;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;

namespace SysAnalyzer.Capture.Providers;

/// <summary>
/// Tier 2 provider using LibreHardwareMonitor for temperature, clock, power, and fan sensors.
/// Requires elevation (admin). Does NOT attempt to load the LHM driver if not elevated.
/// </summary>
public sealed class LibreHardwareProvider : IPolledProvider
{
    private Computer? _computer;
    private readonly UpdateVisitor _updateVisitor = new();
    private ProviderHealth _health = new(ProviderStatus.Unavailable, null, 0, 10, 0);

    // Cached sensor references for fast polling
    private ISensor? _cpuPackageTemp;
    private ISensor? _cpuPackagePower;
    private ISensor? _gpuTemp;
    private ISensor? _gpuCoreClock;
    private ISensor? _gpuPower;
    private ISensor? _gpuFanRpm;
    private ISensor? _gpuLoad;
    private ISensor? _gpuMemoryUsed;
    private ISensor? _gpuMemoryTotal;
    private ISensor?[] _cpuCoreClocks = [];

    public string Name => "LibreHardwareMonitor";
    public ProviderTier RequiredTier => ProviderTier.Tier2;
    public ProviderHealth Health => _health;

    public Task<ProviderHealth> InitAsync()
    {
        if (!IsElevated())
        {
            _health = new ProviderHealth(
                ProviderStatus.Unavailable,
                "Not elevated — Tier 2 sensors require admin. Use --elevate.",
                0, 10, 0);
            return Task.FromResult(_health);
        }

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsStorageEnabled = true,
                IsMotherboardEnabled = true
            };

            _computer.Open();

            int sensorCount = 0;
            foreach (var hw in _computer.Hardware)
            {
                sensorCount += hw.Sensors.Length;
                foreach (var sub in hw.SubHardware)
                    sensorCount += sub.Sensors.Length;
            }

            if (sensorCount == 0)
            {
                _health = new ProviderHealth(
                    ProviderStatus.Failed,
                    "No sensors found after driver load.",
                    0, 10, 0);
                try { _computer.Close(); } catch { }
                _computer = null;
                return Task.FromResult(_health);
            }

            // Cache sensor references
            CacheSensorReferences();

            int metricsAvailable = CountAvailableMetrics();
            _health = new ProviderHealth(
                ProviderStatus.Active,
                null,
                metricsAvailable, 10, 0);

            Console.WriteLine($"  LibreHardwareMonitor: {sensorCount} sensors active");
        }
        catch (Exception ex)
        {
            string reason = ex.Message;
            if (reason.Contains("memory integrity", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("HVCI", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("hypervisor", StringComparison.OrdinalIgnoreCase))
            {
                reason = "HVCI (Memory Integrity) may block the sensor driver. " + reason;
            }
            else if (reason.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                     reason.Contains("defender", StringComparison.OrdinalIgnoreCase) ||
                     reason.Contains("blocked", StringComparison.OrdinalIgnoreCase))
            {
                reason = "LHM driver blocked: " + reason;
            }

            _health = new ProviderHealth(
                ProviderStatus.Failed,
                reason,
                0, 10, 0);

            if (_computer != null)
            {
                try { _computer.Close(); } catch { }
                _computer = null;
            }
        }

        return Task.FromResult(_health);
    }

    public MetricBatch Poll(long qpcTimestamp)
    {
        if (_computer == null || _health.Status != ProviderStatus.Active)
            return MetricBatch.Empty;

        var batch = MetricBatch.Create();

        try
        {
            // Refresh all sensor readings
            _computer.Accept(_updateVisitor);

            // CPU temperature
            if (_cpuPackageTemp?.Value is float cpuTemp)
                batch.CpuTempC = cpuTemp;

            // CPU clock (average of core clocks)
            if (_cpuCoreClocks.Length > 0)
            {
                double totalClock = 0;
                int clockCount = 0;
                foreach (var sensor in _cpuCoreClocks)
                {
                    if (sensor?.Value is float clock)
                    {
                        totalClock += clock;
                        clockCount++;
                    }
                }
                if (clockCount > 0)
                    batch.CpuClockMhz = totalClock / clockCount;
            }

            // CPU power
            if (_cpuPackagePower?.Value is float cpuPower)
                batch.CpuPowerW = cpuPower;

            // GPU temperature
            if (_gpuTemp?.Value is float gpuTemp)
                batch.GpuTempC = gpuTemp;

            // GPU core clock
            if (_gpuCoreClock?.Value is float gpuClock)
                batch.GpuClockMhz = gpuClock;

            // GPU power
            if (_gpuPower?.Value is float gpuPower)
                batch.GpuPowerW = gpuPower;

            // GPU fan RPM
            if (_gpuFanRpm?.Value is float gpuFan)
                batch.GpuFanRpm = gpuFan;

            // GPU load (utilization %)
            if (_gpuLoad?.Value is float gpuLoad)
                batch.GpuUtilizationPercent = gpuLoad;

            // GPU memory
            if (_gpuMemoryUsed?.Value is float gpuMemUsed)
            {
                batch.GpuMemoryUsedMb = gpuMemUsed * 1024.0; // LHM reports SmallData in GB
                if (_gpuMemoryTotal?.Value is float gpuMemTotal && gpuMemTotal > 0)
                    batch.GpuMemoryUtilizationPercent = (gpuMemUsed / gpuMemTotal) * 100.0;
            }
        }
        catch
        {
            // Sensor read failure — return what we have, don't crash
        }

        return batch;
    }

    private void CacheSensorReferences()
    {
        if (_computer == null) return;

        var coreClocks = new List<ISensor>();

        foreach (var hw in _computer.Hardware)
        {
            CacheSensorsFromHardware(hw, coreClocks);
            foreach (var sub in hw.SubHardware)
                CacheSensorsFromHardware(sub, coreClocks);
        }

        _cpuCoreClocks = coreClocks.ToArray();
    }

    private void CacheSensorsFromHardware(IHardware hw, List<ISensor> coreClocks)
    {
        foreach (var sensor in hw.Sensors)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    if (sensor.SensorType == SensorType.Temperature &&
                        sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                        _cpuPackageTemp ??= sensor;
                    else if (sensor.SensorType == SensorType.Temperature &&
                             _cpuPackageTemp == null &&
                             sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        _cpuPackageTemp = sensor; // fallback to first core temp
                    else if (sensor.SensorType == SensorType.Clock &&
                             sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) &&
                             !sensor.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase))
                        coreClocks.Add(sensor);
                    else if (sensor.SensorType == SensorType.Power &&
                             sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                        _cpuPackagePower ??= sensor;
                    else if (sensor.SensorType == SensorType.Power &&
                             _cpuPackagePower == null &&
                             !sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        _cpuPackagePower = sensor;
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    if (sensor.SensorType == SensorType.Temperature &&
                        sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        _gpuTemp ??= sensor;
                    else if (sensor.SensorType == SensorType.Temperature && _gpuTemp == null)
                        _gpuTemp = sensor;
                    else if (sensor.SensorType == SensorType.Clock &&
                             sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        _gpuCoreClock ??= sensor;
                    else if (sensor.SensorType == SensorType.Power)
                        _gpuPower ??= sensor;
                    else if (sensor.SensorType == SensorType.Fan)
                        _gpuFanRpm ??= sensor;
                    else if (sensor.SensorType == SensorType.Load &&
                             sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        _gpuLoad ??= sensor;
                    else if (sensor.SensorType == SensorType.Load &&
                             _gpuLoad == null &&
                             sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                        _gpuLoad = sensor;
                    else if (sensor.SensorType == SensorType.SmallData &&
                             sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase))
                        _gpuMemoryUsed ??= sensor;
                    else if (sensor.SensorType == SensorType.SmallData &&
                             sensor.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase))
                        _gpuMemoryTotal ??= sensor;
                    break;
            }
        }
    }

    private int CountAvailableMetrics()
    {
        int count = 0;
        if (_cpuPackageTemp != null) count++;
        if (_cpuCoreClocks.Length > 0) count++;
        if (_cpuPackagePower != null) count++;
        if (_gpuTemp != null) count++;
        if (_gpuCoreClock != null) count++;
        if (_gpuPower != null) count++;
        if (_gpuFanRpm != null) count++;
        if (_gpuLoad != null) count++;
        if (_gpuMemoryUsed != null) count++;
        if (_gpuMemoryTotal != null) count++;
        return count;
    }

    internal static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void Dispose()
    {
        if (_computer != null)
        {
            try { _computer.Close(); } catch { }
            _computer = null;
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}

using GamePerfOverlay.Models;
using Xunit;

namespace GamePerfOverlay.Tests;

public class HardwareMetricsTests
{
    [Fact]
    public void FormatsMetricsCorrectly_WhenAllValuesProvided()
    {
        var metrics = new HardwareMetrics
        {
            Fps = 144,
            CpuUsage = 32,
            CpuTemp = 62,
            CpuClockGhz = 3.65f,
            GpuUsage = 68,
            GpuTemp = 65,
            GpuClockMhz = 1250,
            VramUsagePercent = 42,
            VramUsedGb = 3.4f,
            RamUsagePercent = 65,
            RamUsedGb = 10.4f
        };

        Assert.Equal("144 FPS", metrics.FpsDisplay);
        Assert.Equal("32%  62°C  3.65 GHz", metrics.CpuDisplay);
        Assert.Equal("68%  65°C  1250 MHz", metrics.GpuDisplay);
        Assert.Equal("42%  3.4 GB", metrics.VramDisplay);
        Assert.Equal("65%  10.4 GB", metrics.RamDisplay);
    }

    [Fact]
    public void FormatsFallback_WhenFpsOrTempUnavailable()
    {
        var metrics = new HardwareMetrics
        {
            Fps = null,
            CpuTemp = null,
            GpuTemp = null
        };

        Assert.Equal("-- FPS", metrics.FpsDisplay);
        Assert.Contains("--°C", metrics.CpuDisplay);
        Assert.Contains("--°C", metrics.GpuDisplay);
    }
}

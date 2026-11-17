using System;
using GamePerfOverlay.Services;
using Xunit;

namespace GamePerfOverlay.Tests;

public class SensorServiceTests
{
    [Fact]
    public void SensorService_InitializeAndSnapshot_DoesNotThrow()
    {
        using var service = new SensorService();
        service.Initialize();

        var metrics = service.SnapshotMetrics(60);

        Assert.NotNull(metrics);
        Assert.Equal("60 FPS", metrics.FpsDisplay);
        Assert.InRange(metrics.CpuUsage, 0, 100);
        Assert.InRange(metrics.RamUsagePercent, 0, 100);
    }

    [Fact]
    public void SensorService_HandlesNullFpsGracefully()
    {
        using var service = new SensorService();
        service.Initialize();

        var metrics = service.SnapshotMetrics(null);

        Assert.NotNull(metrics);
        Assert.Equal("-- FPS", metrics.FpsDisplay);
    }

    [Fact]
    public void SensorService_CpuClockGhz_IsGreaterThanZero()
    {
        using var service = new SensorService();
        service.Initialize();

        var metrics = service.SnapshotMetrics(null);

        Assert.NotNull(metrics);
        Assert.True(metrics.CpuClockGhz > 0, $"Expected CpuClockGhz > 0, but got {metrics.CpuClockGhz}");
    }
}

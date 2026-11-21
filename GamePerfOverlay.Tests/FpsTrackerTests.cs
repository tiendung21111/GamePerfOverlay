using System;
using System.Threading;
using GamePerfOverlay.Services;
using Xunit;

namespace GamePerfOverlay.Tests;

public class FpsTrackerTests
{
    [Fact]
    public void FpsCalculator_CalculatesCorrectFramerate()
    {
        var calculator = new FpsCalculator();
        var now = DateTime.UtcNow;

        // Record 60 frames evenly spaced over 1 second
        for (int i = 0; i < 60; i++)
        {
            calculator.RecordFrame(now.AddMilliseconds(-1000 + (i * (1000 / 60))));
        }

        var fps = calculator.GetFps(now);
        Assert.NotNull(fps);
        Assert.InRange(fps.Value, 58, 62);
    }

    [Fact]
    public void FpsCalculator_DiscardsDuplicateFramesWithinHalfMillisecond()
    {
        var calculator = new FpsCalculator();
        var now = DateTime.UtcNow;

        // Record 60 frames, but with duplicate events 0.1ms apart (e.g. MPO Event 55 + Event 42)
        for (int i = 0; i < 60; i++)
        {
            var frameTime = now.AddMilliseconds(-1000 + (i * (1000.0 / 60.0)));
            calculator.RecordFrame(frameTime);
            calculator.RecordFrame(frameTime.AddMilliseconds(0.1)); // Duplicate event within 0.5ms
        }

        var fps = calculator.GetFps(now);
        Assert.NotNull(fps);
        // Must NOT double-count to 120 FPS
        Assert.InRange(fps.Value, 58, 62);
    }

    [Fact]
    public void FpsCalculator_ReturnsNull_WhenNoRecentFrames()
    {
        var calculator = new FpsCalculator();
        var now = DateTime.UtcNow;

        // Last frame was 2.5 seconds ago (idle / non-3D)
        calculator.RecordFrame(now.AddSeconds(-2.5));

        var fps = calculator.GetFps(now);
        Assert.Null(fps);
    }

    [Fact]
    public void FpsCalculator_MaintainsFpsBetweenEtwFlushes_DoesNotFlickerToNull()
    {
        var calculator = new FpsCalculator();
        var baseTime = DateTime.UtcNow;

        // Simulate an ETW batch arriving with 60 frames over 1 second
        for (int i = 0; i < 60; i++)
        {
            calculator.RecordFrame(baseTime.AddMilliseconds(-1000 + (i * (1000.0 / 60.0))));
        }

        // At 500ms after batch arrival (in the middle of ETW buffering interval)
        var fpsAt500ms = calculator.GetFps(baseTime.AddMilliseconds(500));
        Assert.NotNull(fpsAt500ms);
        Assert.InRange(fpsAt500ms.Value, 58, 62);

        // At 1500ms after batch arrival (where old code prematurely flipped to null)
        var fpsAt1500ms = calculator.GetFps(baseTime.AddMilliseconds(1500));
        Assert.NotNull(fpsAt1500ms);
        Assert.InRange(fpsAt1500ms.Value, 58, 62);

        // At 2500ms after batch arrival (game closed / truly idle) -> flips to null
        var fpsAt2500ms = calculator.GetFps(baseTime.AddMilliseconds(2500));
        Assert.Null(fpsAt2500ms);
    }

    [Fact]
    public void FpsTracker_CanStartAndStop_WithoutThrowing()
    {
        using var tracker = new FpsTracker();
        tracker.Start();
        var fps = tracker.GetCurrentFps();
        tracker.Stop();

        // Either null or a positive integer
        Assert.True(fps == null || fps >= 0);
    }

    [Fact]
    public async Task FpsTracker_SessionEnablesDxgiAndD3D9Providers_Successfully()
    {
        using var session = new Microsoft.Diagnostics.Tracing.Session.TraceEventSession("TestEtwSession", Microsoft.Diagnostics.Tracing.Session.TraceEventSessionOptions.Create);
        session.EnableProvider(new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9"), Microsoft.Diagnostics.Tracing.TraceEventLevel.Informational, 0);
        session.EnableProvider(new Guid("783ACA0A-790E-4D7F-8451-AA8505ECF64C"), Microsoft.Diagnostics.Tracing.TraceEventLevel.Informational, 0);

        var processTask = Task.Run(() => session.Source.Process());
        Assert.True(session.IsActive);
        await Task.Delay(500);
        session.Stop();
        await processTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(session.IsActive);
    }
}

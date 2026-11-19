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

        bool receivedAnyEvent = false;
        session.Source.Dynamic.All += data =>
        {
            if ((int)data.ID == 42 || data.EventName.Contains("Present", StringComparison.OrdinalIgnoreCase))
            {
                receivedAnyEvent = true;
            }
        };

        var processTask = Task.Run(() => session.Source.Process());
        await Task.Delay(1500);
        session.Stop();
        await processTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(session.IsActive);
        Assert.True(receivedAnyEvent);
    }
}

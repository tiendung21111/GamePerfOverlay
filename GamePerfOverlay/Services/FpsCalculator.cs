using System;
using System.Collections.Generic;

namespace GamePerfOverlay.Services;

public class FpsCalculator
{
    private readonly Queue<DateTime> _frameTimes = new();
    private readonly object _lock = new();
    private DateTime _lastFrameTime = DateTime.MinValue;

    public void RecordFrame(DateTime timestamp)
    {
        lock (_lock)
        {
            // De-duplicate multiplane overlay (MPO) or blit events occurring < 0.5ms apart
            if (_lastFrameTime != DateTime.MinValue && (timestamp - _lastFrameTime).TotalMilliseconds < 0.5)
            {
                return;
            }

            _frameTimes.Enqueue(timestamp);
            if (timestamp > _lastFrameTime)
            {
                _lastFrameTime = timestamp;
            }

            // Prune timestamps older than 2.0 seconds
            var cutoff = timestamp.AddMilliseconds(-2000);
            while (_frameTimes.Count > 0 && _frameTimes.Peek() < cutoff)
            {
                _frameTimes.Dequeue();
            }
        }
    }

    public int? GetFps(DateTime? currentTime = null)
    {
        var now = currentTime ?? DateTime.UtcNow;

        lock (_lock)
        {
            // If no frame rendered in last 1.2s, treat as idle/non-3D
            if (_lastFrameTime == DateTime.MinValue || (now - _lastFrameTime).TotalMilliseconds > 1200)
            {
                return null;
            }

            // Measure over the 1000ms window ending at the latest frame timestamp
            // This decouples framerate calculation from ETW batch buffer flush delay
            var windowStart = _lastFrameTime.AddMilliseconds(-1000);
            DateTime oldestInWindow = DateTime.MaxValue;
            DateTime newestInWindow = DateTime.MinValue;
            int count = 0;

            foreach (var time in _frameTimes)
            {
                if (time >= windowStart && time <= _lastFrameTime)
                {
                    count++;
                    if (time < oldestInWindow) oldestInWindow = time;
                    if (time > newestInWindow) newestInWindow = time;
                }
            }

            if (count < 2)
            {
                return count > 0 ? count : null;
            }

            double durationSeconds = (newestInWindow - oldestInWindow).TotalSeconds;
            if (durationSeconds > 0.05)
            {
                // Exact framerate formula: (count - 1) / dt
                double fps = (count - 1) / durationSeconds;
                return (int)Math.Round(fps);
            }

            return count;
        }
    }
}

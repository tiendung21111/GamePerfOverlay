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
            _frameTimes.Enqueue(timestamp);
            if (timestamp > _lastFrameTime)
            {
                _lastFrameTime = timestamp;
            }

            // Prune timestamps older than 1.5 seconds
            var cutoff = timestamp.AddMilliseconds(-1500);
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

            var windowStart = now.AddMilliseconds(-1000);
            int count = 0;

            foreach (var time in _frameTimes)
            {
                if (time >= windowStart && time <= now)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

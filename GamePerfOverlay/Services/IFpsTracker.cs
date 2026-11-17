using System;

namespace GamePerfOverlay.Services;

public interface IFpsTracker : IDisposable
{
    void Start();
    void Stop();
    int? GetCurrentFps();
}

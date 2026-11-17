using System;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.Services;

public interface ISensorService : IDisposable
{
    void Initialize();
    HardwareMetrics SnapshotMetrics(int? currentFps);
}

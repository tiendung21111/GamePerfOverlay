using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.Services.Scanners;

public interface IGameScanner
{
    GamePlatform Platform { get; }
    Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken ct = default);
}

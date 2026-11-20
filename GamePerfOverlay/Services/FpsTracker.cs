using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GamePerfOverlay.Interop;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace GamePerfOverlay.Services;

public class FpsTracker : IFpsTracker
{
    private static readonly Guid DxgiProviderGuid = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3D9ProviderGuid = new("783ACA0A-790E-4D7F-8451-AA8505ECF64C");

    private readonly FpsCalculator _calculator = new();
    private TraceEventSession? _session;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private const string SessionName = "GamePerfOverlayFpsSession";

    public void Start()
    {
        if (_session != null || _disposed) return;

        try
        {
            // Clean up any stale session with the same name
            try
            {
                using var existingSession = TraceEventSession.GetActiveSession(SessionName);
                existingSession?.Stop(true);
            }
            catch
            {
            }

            _cts = new CancellationTokenSource();
            _session = new TraceEventSession(SessionName, TraceEventSessionOptions.Create)
            {
                StopOnDispose = true
            };

            // Enable DXGI and D3D9 Present providers (matchAllKeywords=0 for all presentation events)
            _session.EnableProvider(DxgiProviderGuid, TraceEventLevel.Informational, 0);
            _session.EnableProvider(D3D9ProviderGuid, TraceEventLevel.Informational, 0);

            _session.Source.Dynamic.All += OnDynamicEvent;

            _processingTask = Task.Run(() =>
            {
                try
                {
                    _session.Source.Process();
                }
                catch
                {
                }
            }, _cts.Token);
        }
        catch
        {
            // In non-elevated or restricted environments, ETW session creation can throw.
            // Catch and gracefully return null FPS instead of crashing.
            _session = null;
        }
    }

    private uint _cachedPid;
    private bool _cachedPidExcluded;
    private DateTime _lastPidCheck = DateTime.MinValue;

    private static readonly HashSet<string> ExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "dwm", "taskmgr", "searchhost", "shellexperiencehost", "gameperfoverlay", "applicationframehost"
    };

    private void OnDynamicEvent(TraceEvent data)
    {
        // Present Start events: DXGI (ID=42), D3D9 (ID=1), or DXGI MultiplaneOverlay (ID=55)
        int eventId = (int)data.ID;
        bool isPresentEvent = eventId == 42 || eventId == 1 || eventId == 55 ||
            (data.Opcode == TraceEventOpcode.Start && data.EventName.Contains("Present", StringComparison.OrdinalIgnoreCase));

        if (!isPresentEvent)
        {
            return;
        }

        // Cache foreground window PID periodically (every 100ms) to avoid high user32 context-switch overhead
        var now = DateTime.UtcNow;
        if ((now - _lastPidCheck).TotalMilliseconds > 100)
        {
            _lastPidCheck = now;
            var fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(fgHwnd, out _cachedPid);
                _cachedPidExcluded = _cachedPid <= 4 || ExcludedProcesses.Contains(GetProcessNameSafe(_cachedPid));
            }
            else
            {
                _cachedPid = 0;
                _cachedPidExcluded = true;
            }
        }

        if (_cachedPid > 4 && data.ProcessID == _cachedPid && !_cachedPidExcluded)
        {
            // Use high-resolution hardware timestamp from ETW kernel event (ToUniversalTime)
            // instead of managed DateTime.UtcNow to eliminate batch-flush latency jitter
            _calculator.RecordFrame(data.TimeStamp.ToUniversalTime());
        }
    }

    private static string GetProcessNameSafe(uint pid)
    {
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            // Protected anti-cheat games (e.g. Valorant Vanguard, LoL) throw access denied,
            // so they are definitely not excluded OS shell apps.
            return string.Empty;
        }
    }

    public int? GetCurrentFps()
    {
        return _calculator.GetFps();
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _session?.Stop();
            _session?.Dispose();
            _session = null;
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _cts?.Dispose();
    }
}

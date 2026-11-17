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
    private static readonly Guid DxgiProviderGuid = new("CA11C036-0102-4A2D-A6AD-666D24135B4A");
    private static readonly Guid D3D9ProviderGuid = new("783ACA0A-790E-4D7F-8451-AA85051C723B");

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

            // Enable DXGI and D3D9 Present providers
            _session.EnableProvider(DxgiProviderGuid, TraceEventLevel.Informational, 0x1);
            _session.EnableProvider(D3D9ProviderGuid, TraceEventLevel.Informational, 0x1);

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
        // Only count Start events to prevent double-counting Start vs Stop
        int eventId = (int)data.ID;
        bool isStartEvent = data.EventName.Contains("Start", StringComparison.OrdinalIgnoreCase) ||
                            eventId == 42 || eventId == 1 || eventId == 372;

        if (isStartEvent && data.EventName.Contains("Present", StringComparison.OrdinalIgnoreCase))
        {
            var fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(fgHwnd, out uint fgPid);
                if (data.ProcessID == fgPid && fgPid > 4)
                {
                    if (IsProcessExcluded(fgPid))
                    {
                        return;
                    }

                    _calculator.RecordFrame(DateTime.UtcNow);
                }
            }
        }
    }

    private bool IsProcessExcluded(uint pid)
    {
        var now = DateTime.UtcNow;
        if (pid == _cachedPid && (now - _lastPidCheck).TotalSeconds < 2.0)
        {
            return _cachedPidExcluded;
        }

        _cachedPid = pid;
        _lastPidCheck = now;
        _cachedPidExcluded = false;

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            _cachedPidExcluded = ExcludedProcesses.Contains(proc.ProcessName);
        }
        catch
        {
            _cachedPidExcluded = true;
        }

        return _cachedPidExcluded;
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

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;
using GamePerfOverlay.ViewModels;
using Forms = System.Windows.Forms;

namespace GamePerfOverlay;

public partial class App : System.Windows.Application
{
    private SensorService? _sensorService;
    private FpsTracker? _fpsTracker;
    private HotkeyManager? _hotkeyManager;
    private OverlayViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private Forms.NotifyIcon? _notifyIcon;
    private CancellationTokenSource? _cts;
    private Task? _updateTask;

    private volatile bool _isOverlayVisible = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _cts = new CancellationTokenSource();
        _viewModel = new OverlayViewModel();
        _mainWindow = new MainWindow(_viewModel);

        // 1. Initialize Sensor Service
        _sensorService = new SensorService();
        _sensorService.Initialize();

        // 2. Initialize FPS Tracker
        _fpsTracker = new FpsTracker();
        _fpsTracker.Start();

        // 3. Initialize Global Hotkey (Ctrl + D + M)
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.ToggleRequested += ToggleOverlay;
        _hotkeyManager.Register();

        // 4. Check Startup Mode
        bool startMinimized = false;
        foreach (var arg in e.Args)
        {
            if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase))
            {
                startMinimized = true;
                break;
            }
        }

        // 5. Initialize System Tray Icon
        InitializeTrayIcon();

        // 6. Show Overlay Window (only if not starting minimized at Windows boot)
        if (!startMinimized)
        {
            _mainWindow.Show();
            _isOverlayVisible = true;
        }
        else
        {
            _isOverlayVisible = false;
        }

        // 6. Start high-frequency background polling loop (100ms)
        _updateTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            int tickCounter = 0;
            HardwareMetrics? cachedMetrics = null;

            while (!_cts.Token.IsCancellationRequested && await timer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    if (_isOverlayVisible && _sensorService != null && _fpsTracker != null)
                    {
                        var fps = _fpsTracker.GetCurrentFps();

                        // Query hardware sensors every 3 ticks (300ms) to keep CPU overhead < 0.1%
                        // while updating FPS every 100ms for instantaneous reaction.
                        if (tickCounter % 3 == 0 || cachedMetrics == null)
                        {
                            cachedMetrics = _sensorService.SnapshotMetrics(fps);
                        }
                        else if (cachedMetrics != null)
                        {
                            cachedMetrics.Fps = fps;
                        }

                        tickCounter++;

                        if (cachedMetrics != null)
                        {
                            var metricsToPush = cachedMetrics;
                            _ = _mainWindow.Dispatcher.InvokeAsync(
                                () => _viewModel?.Update(metricsToPush),
                                DispatcherPriority.Render);
                        }
                    }
                }
                catch
                {
                }
            }
        }, _cts.Token);
    }

    private void ToggleOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            if (_mainWindow == null) return;

            if (_mainWindow.Visibility == Visibility.Visible)
            {
                _mainWindow.Visibility = Visibility.Collapsed;
                _isOverlayVisible = false;
            }
            else
            {
                _mainWindow.Visibility = Visibility.Visible;
                _isOverlayVisible = true;
            }
        });
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Game Performance Overlay (Ctrl + D + M)",
            Visible = true
        };

        var contextMenu = new Forms.ContextMenuStrip();

        var toggleItem = new Forms.ToolStripMenuItem("Bật/Tắt Overlay (Ctrl + D + M)");
        toggleItem.Click += (s, e) => ToggleOverlay();
        contextMenu.Items.Add(toggleItem);

        var autoStartItem = new Forms.ToolStripMenuItem("Khởi động cùng Windows (chạy ẩn)")
        {
            CheckOnClick = true,
            Checked = AutoStartManager.IsAutoStartEnabled()
        };
        autoStartItem.Click += (s, e) =>
        {
            bool newState = autoStartItem.Checked;
            bool success = AutoStartManager.SetAutoStart(newState);
            if (!success)
            {
                autoStartItem.Checked = !newState;
                _notifyIcon?.ShowBalloonTip(2000, "Game Performance Overlay", "Không thể cập nhật Task Scheduler. Hãy đảm bảo chạy app dưới quyền Admin.", Forms.ToolTipIcon.Warning);
            }
            else
            {
                var msg = newState ? "Đã bật tự động khởi động cùng Windows (chạy ẩn)." : "Đã tắt tự động khởi động cùng Windows.";
                _notifyIcon?.ShowBalloonTip(2000, "Game Performance Overlay", msg, Forms.ToolTipIcon.Info);
            }
        };
        contextMenu.Items.Add(autoStartItem);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem("Thoát");
        exitItem.Click += (s, e) => Shutdown();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ToggleOverlay();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _cts?.Cancel();
            try { _updateTask?.Wait(500); } catch { }

            _hotkeyManager?.Dispose();
            _fpsTracker?.Dispose();
            _sensorService?.Dispose();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
        catch
        {
        }

        base.OnExit(e);
    }
}

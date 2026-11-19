using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GamePerfOverlay.Interop;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;
using GamePerfOverlay.ViewModels;
using GamePerfOverlay.Views;
using Forms = System.Windows.Forms;

namespace GamePerfOverlay;

public partial class App : System.Windows.Application
{
    private SensorService? _sensorService;
    private FpsTracker? _fpsTracker;
    private HotkeyManager? _hotkeyManager;
    private OverlayViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private GameLibraryManager? _libraryManager;
    private GameLauncherService? _launcherService;
    private LauncherViewModel? _launcherViewModel;
    private LauncherWindow? _launcherWindow;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _toggleMenuItem;
    private AppSettings _settings = new();
    private CancellationTokenSource? _cts;
    private Task? _updateTask;

    private volatile bool _isOverlayVisible = true;
    private static Mutex? _singleInstanceMutex;
    public const string SingleInstanceMutexName = @"Local\GamePerfOverlay_SingleInstance_Mutex_39F1";

    protected override void OnStartup(StartupEventArgs e)
    {
        var mutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();

            // Another instance is already running -> bring existing window to foreground and exit
            var hwnd = NativeMethods.FindWindow(null, LauncherWindow.WindowTitle);
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hwnd);
            }
            Shutdown();
            return;
        }

        _singleInstanceMutex = mutex;

        base.OnStartup(e);

        _settings = SettingsService.Load();
        _cts = new CancellationTokenSource();
        _viewModel = new OverlayViewModel();
        _mainWindow = new MainWindow(_viewModel);
        _mainWindow.SetPosition(_settings.OverlayLeft, _settings.OverlayTop);

        // 1. Initialize Sensor Service
        _sensorService = new SensorService();
        _sensorService.Initialize();

        // 2. Initialize FPS Tracker
        _fpsTracker = new FpsTracker();
        _fpsTracker.Start();

        // 3. Initialize Global Hotkey with loaded settings
        _hotkeyManager = new HotkeyManager(_settings.HotkeyCodes);
        _hotkeyManager.ToggleRequested += ToggleOverlay;
        _hotkeyManager.Register();

        // 4. Initialize Game Library & Launcher Services
        _libraryManager = new GameLibraryManager();
        _launcherService = new GameLauncherService(_libraryManager);
        _launcherViewModel = new LauncherViewModel(_libraryManager, _launcherService);
        _launcherWindow = new LauncherWindow(_launcherViewModel);

        // 5. Check Startup Mode
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

        // 7. Show Overlay Window & Launcher Window (only if not starting minimized at Windows boot)
        if (!startMinimized)
        {
            _mainWindow.Show();
            _isOverlayVisible = true;
            _launcherWindow.Show();
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
        Icon trayIcon = SystemIcons.Application;
        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/app.ico");
            var streamInfo = GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                trayIcon = new Icon(stream);
            }
        }
        catch
        {
        }

        string initialTooltip = $"Game Performance Overlay ({_settings.HotkeyDisplay})";
        if (initialTooltip.Length > 63) initialTooltip = initialTooltip.Substring(0, 63);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = initialTooltip,
            Visible = true
        };

        var contextMenu = new Forms.ContextMenuStrip();

        var openLauncherItem = new Forms.ToolStripMenuItem("Mở Game Launcher");
        openLauncherItem.Font = new System.Drawing.Font(openLauncherItem.Font, System.Drawing.FontStyle.Bold);
        openLauncherItem.Click += (s, e) => OpenGameLauncher();
        contextMenu.Items.Add(openLauncherItem);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());

        _toggleMenuItem = new Forms.ToolStripMenuItem($"Bật/Tắt Overlay ({_settings.HotkeyDisplay})");
        _toggleMenuItem.Click += (s, e) => ToggleOverlay();
        contextMenu.Items.Add(_toggleMenuItem);

        var changeHotkeyItem = new Forms.ToolStripMenuItem("Đổi phím tắt...");
        changeHotkeyItem.Click += (s, e) => OpenHotkeySettings();
        contextMenu.Items.Add(changeHotkeyItem);

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
        _notifyIcon.DoubleClick += (s, e) => OpenGameLauncher();
    }

    private void OpenGameLauncher()
    {
        Dispatcher.Invoke(() =>
        {
            if (_launcherWindow == null)
            {
                if (_libraryManager == null) _libraryManager = new GameLibraryManager();
                if (_launcherService == null) _launcherService = new GameLauncherService(_libraryManager);
                _launcherViewModel = new LauncherViewModel(_libraryManager, _launcherService);
                _launcherWindow = new LauncherWindow(_launcherViewModel);
            }

            _launcherWindow.Show();
            _launcherWindow.WindowState = WindowState.Normal;
            _launcherWindow.Activate();
        });
    }

    private void OpenHotkeySettings()
    {
        Dispatcher.Invoke(() =>
        {
            var window = new GamePerfOverlay.Views.HotkeySettingsWindow(_settings, newSettings =>
            {
                _settings = newSettings;
                SettingsService.Save(_settings);
                _hotkeyManager?.UpdateHotkey(_settings.HotkeyCodes);

                if (_toggleMenuItem != null)
                {
                    _toggleMenuItem.Text = $"Bật/Tắt Overlay ({_settings.HotkeyDisplay})";
                }

                if (_notifyIcon != null)
                {
                    string fullText = $"Game Performance Overlay ({_settings.HotkeyDisplay})";
                    _notifyIcon.Text = fullText.Length > 63 ? fullText.Substring(0, 63) : fullText;
                    _notifyIcon.ShowBalloonTip(2000, "Game Performance Overlay", $"Đã đổi phím tắt thành: {_settings.HotkeyDisplay}", Forms.ToolTipIcon.Info);
                }
            });
            window.ShowDialog();
        });
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

            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                }
                finally
                {
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
        }
        catch
        {
        }

        base.OnExit(e);
    }
}

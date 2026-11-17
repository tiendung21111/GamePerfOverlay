# Game Performance Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a lightweight, 100% transparent, non-interactive Windows 11 Game Performance Overlay in C# .NET 10 WPF that displays FPS, CPU/GPU temperatures, clocks, VRAM, and RAM, toggled via `Ctrl + D + M`.

**Architecture:** A standalone WPF desktop application running a low-priority background timer (1 Hz) that samples hardware metrics via `LibreHardwareMonitorLib` and active DXGI game framerates via Event Tracing for Windows (ETW). The UI is a borderless, permanent click-through overlay (`WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`) positioned at the top-left of the display with Xbox neon-green text. A low-level keyboard hook captures the global hotkey `Ctrl + D + M` to show and hide the overlay without stealing game focus.

**Tech Stack:** C# .NET 10, WPF, `LibreHardwareMonitorLib`, `Microsoft.Diagnostics.Tracing.TraceEvent`, Win32 API interop (`user32.dll`).

**Spec:** `docs/superpowers/specs/2026-11-17-game-perf-overlay-design.md`

## Global Constraints
- Target OS: Windows 10 & 11 (64-bit).
- Text color: `#10ff50` (Xbox neon green) for all labels, values, and units. Font: `Consolas`, monospace with dark drop-shadow.
- Fixed coordinates: `Left = 20`, `Top = 20`.
- No interactive mouse processing: Window styles must include `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`.
- Hotkey: `Ctrl + D + M` pressed simultaneously toggles visibility.
- 5 lines of metrics: FPS, CPU, GPU, VRAM, RAM. No graphs or headers.
- Safe for anti-cheat: Zero DLL injection and zero API hooking in game processes.

## Review Focus
1. Non-admin execution: If launched without Administrator elevation, sensor access cannot open the ring-0 driver; CPU/GPU temperatures must display `--°C` instead of crashing.
2. Integrated GPU without dedicated VRAM (e.g. Intel Iris Xe / AMD Vega): VRAM metric must dynamically query WDDM shared video memory rather than showing 0 MB or 128 MB stub values.
3. No active 3D game: FPS line must display `-- FPS` instead of displaying desktop window manager (DWM) or browser framerates.
4. Hotkey chord release: Releasing any of the three keys (`Ctrl`, `D`, or `M`) must reset the trigger state so rapid re-triggering requires a distinct keypress.
5. High-DPI scaling: Overlay text must render crisp and stay at 20px physical margin regardless of Windows display scaling (100%, 125%, 150%).

---

### Task 1: Project Scaffolding & Domain Models

**Files:**
- Create: `GamePerfOverlay/GamePerfOverlay.csproj`
- Create: `GamePerfOverlay/Models/HardwareMetrics.cs`
- Create: `GamePerfOverlay/Services/ISensorService.cs`
- Create: `GamePerfOverlay.Tests/GamePerfOverlay.Tests.csproj`
- Create: `GamePerfOverlay.Tests/HardwareMetricsTests.cs`

**Interfaces:**
- Produces: `HardwareMetrics` class with formatted display properties:
  - `string FpsDisplay { get; }` (e.g., `144 FPS` or `-- FPS`)
  - `string CpuDisplay { get; }` (e.g., `32%  62°C  3.65 GHz`)
  - `string GpuDisplay { get; }` (e.g., `68%  65°C  1250 MHz`)
  - `string VramDisplay { get; }` (e.g., `42%  3.4 GB`)
  - `string RamDisplay { get; }` (e.g., `65%  10.4 GB`)

- [ ] **Step 1: Create test project and write failing unit tests for `HardwareMetrics`**

Create `GamePerfOverlay.Tests/HardwareMetricsTests.cs`:
```csharp
[Fact]
public void FormatsMetricsCorrectly_WhenAllValuesProvided()
{
    var metrics = new HardwareMetrics
    {
        Fps = 144,
        CpuUsage = 32,
        CpuTemp = 62,
        CpuClockGhz = 3.65f,
        GpuUsage = 68,
        GpuTemp = 65,
        GpuClockMhz = 1250,
        VramUsagePercent = 42,
        VramUsedGb = 3.4f,
        RamUsagePercent = 65,
        RamUsedGb = 10.4f
    };

    Assert.Equal("144 FPS", metrics.FpsDisplay);
    Assert.Equal("32%  62°C  3.65 GHz", metrics.CpuDisplay);
    Assert.Equal("68%  65°C  1250 MHz", metrics.GpuDisplay);
    Assert.Equal("42%  3.4 GB", metrics.VramDisplay);
    Assert.Equal("65%  10.4 GB", metrics.RamDisplay);
}

[Fact]
public void FormatsFallback_WhenFpsOrTempUnavailable()
{
    var metrics = new HardwareMetrics
    {
        Fps = null,
        CpuTemp = null,
        GpuTemp = null
    };

    Assert.Equal("-- FPS", metrics.FpsDisplay);
    Assert.Contains("--°C", metrics.CpuDisplay);
    Assert.Contains("--°C", metrics.GpuDisplay);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test`
Expected: FAIL with missing project or type definition.

- [ ] **Step 3: Create `GamePerfOverlay.csproj` and implement `HardwareMetrics.cs`**

Create `GamePerfOverlay/GamePerfOverlay.csproj` targeting `net10.0-windows` with `<UseWPF>true</UseWPF>` and NuGet package references:
- `LibreHardwareMonitorLib` (v0.9.3)
- `Microsoft.Diagnostics.Tracing.TraceEvent` (v3.1.8)

Implement `GamePerfOverlay/Models/HardwareMetrics.cs` with the required fields and formatting getters.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GamePerfOverlay/ GamePerfOverlay.Tests/
git commit -m "feat: scaffold wpf project and metrics model"
```

---

### Task 2: Universal Sensor Monitoring Service (`SensorService.cs`)

**Files:**
- Create: `GamePerfOverlay/Services/SensorService.cs`
- Modify: `GamePerfOverlay/Services/ISensorService.cs`
- Test: `GamePerfOverlay.Tests/SensorServiceTests.cs`

**Interfaces:**
- Consumes: `LibreHardwareMonitorLib.Hardware.Computer`
- Produces: `ISensorService` with:
  ```csharp
  public interface ISensorService : IDisposable
  {
      void Initialize();
      HardwareMetrics SnapshotMetrics(int? currentFps);
  }
  ```

- [ ] **Step 1: Write unit tests for `SensorService` fallback and formatting**

Test in `GamePerfOverlay.Tests/SensorServiceTests.cs`:
Verify `SnapshotMetrics` produces valid `HardwareMetrics` instances without throwing `NullReferenceException` even when sensors are absent or simulated.

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test --filter FullyQualifiedName~SensorServiceTests`
Expected: FAIL.

- [ ] **Step 3: Implement `SensorService.cs`**

Configure `Computer` with:
`IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true, IsMotherboardEnabled = false, IsStorageEnabled = false, IsNetworkEnabled = false`.
In `SnapshotMetrics`:
- Iterate `computer.Hardware`:
  - For CPU: Read `SensorType.Load` (total/package), `SensorType.Temperature` (Package/Core Max), `SensorType.Clock` (Average/Core #1 in GHz).
  - For GPU: Support NVIDIA, AMD, and Intel GPUs. Read `SensorType.Load` (Core), `SensorType.Temperature` (Core or Fallback to CPU package if null on iGPU), `SensorType.Clock` (Core MHz), and VRAM (Memory Used / Total).
  - For RAM: Read memory load and memory used.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GamePerfOverlay/Services/ GamePerfOverlay.Tests/
git commit -m "feat: implement hardware sensor service"
```

---

### Task 3: Zero-Hook Event Tracing (ETW) FPS Tracker (`FpsTracker.cs`)

**Files:**
- Create: `GamePerfOverlay/Services/IFpsTracker.cs`
- Create: `GamePerfOverlay/Services/FpsTracker.cs`
- Test: `GamePerfOverlay.Tests/FpsTrackerTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public interface IFpsTracker : IDisposable
  {
      void Start();
      void Stop();
      int? GetCurrentFps();
  }
  ```

- [ ] **Step 1: Write test for FPS calculation logic**

Create `GamePerfOverlay.Tests/FpsTrackerTests.cs`:
Test rolling frame count timestamp calculator to ensure accurate FPS values (e.g., 60 frames in 1.0s yields 60 FPS, 0 frames in 1.5s yields null / `--`).

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test --filter FullyQualifiedName~FpsTrackerTests`
Expected: FAIL.

- [ ] **Step 3: Implement `FpsTracker.cs` using DXGI Present ETW / Performance Counters**

Use `TraceEventSession` listening to `Microsoft-Windows-DXGI` (`{CA11C036-0102-4A2D-A6AD-666D24135B4A}`) or standard DXGI Present events:
- On each Present event from the foreground process: Record timestamp in circular buffer.
- `GetCurrentFps()` computes frames rendered in the preceding 1,000ms window.
- If foreground window has no present calls for > 1.2s or belongs to excluded system apps (`dwm.exe`, `explorer.exe`), return `null` (`--`).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GamePerfOverlay/Services/ GamePerfOverlay.Tests/
git commit -m "feat: implement etw fps tracker"
```

---

### Task 4: Low-Level Global Hotkey Listener (`HotkeyManager.cs`)

**Files:**
- Create: `GamePerfOverlay/Interop/NativeMethods.cs`
- Create: `GamePerfOverlay/Services/HotkeyManager.cs`
- Test: `GamePerfOverlay.Tests/HotkeyManagerTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public class HotkeyManager : IDisposable
  {
      public event Action? ToggleRequested;
      public void Register();
      public void Unregister();
  }
  ```

- [ ] **Step 1: Write unit tests for chord state tracking**

Create `GamePerfOverlay.Tests/HotkeyManagerTests.cs`:
Simulate key states:
- Ctrl down, D down, M down -> Triggers event.
- Releasing any key -> Event does not re-trigger until released and re-pressed.

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test --filter FullyQualifiedName~HotkeyManagerTests`
Expected: FAIL.

- [ ] **Step 3: Implement `HotkeyManager.cs` and Win32 interop**

In `NativeMethods.cs`:
- Declare `SetWindowsHookEx(WH_KEYBOARD_LL, ...)`, `UnhookWindowsHookEx`, `CallNextHookEx`, `GetKeyState`.
In `HotkeyManager.cs`:
- Maintain `bool isCtrlPressed, isDPressed, isMPressed`.
- When all 3 keys are down simultaneously and trigger flag is false: raise `ToggleRequested` and set flag = true.
- When any key releases: reset flag = false.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add GamePerfOverlay/Interop/ GamePerfOverlay/Services/ GamePerfOverlay.Tests/
git commit -m "feat: implement global hotkey manager"
```

---

### Task 5: 100% Transparent Non-Interactive Overlay Window (`MainWindow.xaml`)

**Files:**
- Create: `GamePerfOverlay/Views/MainWindow.xaml`
- Create: `GamePerfOverlay/Views/MainWindow.xaml.cs`
- Create: `GamePerfOverlay/ViewModels/OverlayViewModel.cs`

**Interfaces:**
- Consumes: `HardwareMetrics`, `ISensorService`, `IFpsTracker`
- Produces: Borderless, click-through, always-on-top HUD window.

- [ ] **Step 1: Create `OverlayViewModel.cs` with INotifyPropertyChanged**

Expose properties:
`FpsText`, `CpuText`, `GpuText`, `VramText`, `RamText`.
Implement update method that copies from `HardwareMetrics`.

- [ ] **Step 2: Design `MainWindow.xaml`**

```xml
<Window x:Class="GamePerfOverlay.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="GamePerfOverlay"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        Left="20" Top="20"
        Width="350" Height="150"
        FontFamily="Consolas"
        Focusable="False">
    <Window.Effect>
        <DropShadowEffect Color="Black" BlurRadius="4" ShadowDepth="1.5" Direction="315" Opacity="0.95"/>
    </Window.Effect>
    <StackPanel Margin="0" TextElement.Foreground="#10ff50" TextElement.FontWeight="Bold" TextElement.FontSize="14">
        <Grid Margin="0,1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="60"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="FPS" Grid.Column="0"/>
            <TextBlock Text="{Binding FpsText}" Grid.Column="1" HorizontalAlignment="Right"/>
        </Grid>
        <Grid Margin="0,1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="60"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="CPU" Grid.Column="0"/>
            <TextBlock Text="{Binding CpuText}" Grid.Column="1" HorizontalAlignment="Right"/>
        </Grid>
        <Grid Margin="0,1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="60"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="GPU" Grid.Column="0"/>
            <TextBlock Text="{Binding GpuText}" Grid.Column="1" HorizontalAlignment="Right"/>
        </Grid>
        <Grid Margin="0,1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="60"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="VRAM" Grid.Column="0"/>
            <TextBlock Text="{Binding VramText}" Grid.Column="1" HorizontalAlignment="Right"/>
        </Grid>
        <Grid Margin="0,1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="60"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Text="RAM" Grid.Column="0"/>
            <TextBlock Text="{Binding RamText}" Grid.Column="1" HorizontalAlignment="Right"/>
        </Grid>
    </StackPanel>
</Window>
```

- [ ] **Step 3: Implement Win32 Extended Window Styles in `MainWindow.xaml.cs`**

In `OnSourceInitialized`:
```csharp
var hwnd = new WindowInteropHelper(this).Handle;
int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
    extendedStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
```
Ensure window is 100% transparent to clicks and ignores focus.

- [ ] **Step 4: Build project to verify compilation**

Run: `dotnet build GamePerfOverlay/GamePerfOverlay.csproj`
Expected: 0 Errors.

- [ ] **Step 5: Commit**

```bash
git add GamePerfOverlay/Views/ GamePerfOverlay/ViewModels/
git commit -m "feat: create transparent click-through wpf overlay"
```

---

### Task 6: Application Orchestration, System Tray & Portable Deployment

**Files:**
- Create: `GamePerfOverlay/App.xaml`
- Create: `GamePerfOverlay/App.xaml.cs`
- Create: `GamePerfOverlay/app.manifest` (for `requireAdministrator` access to temperature sensors)
- Modify: `GamePerfOverlay/GamePerfOverlay.csproj`

**Interfaces:**
- Orchestrates: `SensorService`, `FpsTracker`, `HotkeyManager`, `MainWindow`.
- Provides: System Tray Icon (`System.Windows.Forms.NotifyIcon`) with Context Menu:
  - "Bật/Tắt Overlay (Ctrl + D + M)"
  - "Khởi động cùng Windows" (Tùy chọn)
  - "Thoát"

- [ ] **Step 1: Create application manifest `app.manifest`**

Set `<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />` so `LibreHardwareMonitorLib` can unpack its ring-0 driver to read CPU & GPU temperature diodes.

- [ ] **Step 2: Implement `App.xaml.cs` lifecycle and tray icon**

Initialize:
1. `SensorService.Initialize()`
2. `FpsTracker.Start()`
3. `HotkeyManager.Register()`
4. Start 1,000ms `DispatcherTimer` (or background thread with `Dispatcher.Invoke`) updating `OverlayViewModel`.
5. Attach `HotkeyManager.ToggleRequested += () => MainWindow.Visibility = MainWindow.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;`
6. Create `NotifyIcon` with "Thoát" menu item.

- [ ] **Step 3: Build and run all tests**

Run: `dotnet test`
Expected: PASS all tests.

- [ ] **Step 4: Build Release & publish single-file portable executable**

Run: `dotnet publish GamePerfOverlay/GamePerfOverlay.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`
Verify that `GamePerfOverlay.exe` is successfully generated and runs.

- [ ] **Step 5: Commit**

```bash
git add GamePerfOverlay/
git commit -m "feat: wire app lifecycle, tray icon and publish profile"
```

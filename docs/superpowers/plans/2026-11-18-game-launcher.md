# Game Launcher & Game Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform GamePerfOverlay into an All-in-One Game Launcher to scan, manage, and launch games from Steam, Riot Games, Epic Games, and custom executables with one click, while maintaining the lightweight zero-hook performance overlay.

**Architecture:** A modular MVVM WPF architecture where `GameLibraryManager` orchestrates independent scanners (`SteamScanner`, `RiotScanner`, `EpicScanner`), persists the game catalog to `%APPDATA%\GamePerfOverlay\games.json`, and hands off launching to `GameLauncherService`. `LauncherWindow` provides a dark neon-green gaming dashboard that cleanly minimizes to the system tray on game launch, leaving the existing `MainWindow` transparent HUD to monitor the running game.

**Tech Stack:** C# .NET 10, WPF, System.Text.Json, xUnit, Moq/NSubstitute.

**Spec:** `docs/superpowers/specs/2026-11-18-game-launcher-design.md`

## Global Constraints
- Target OS: Windows 10/11 64-bit.
- Visual style: Dark background (`#0b0e14`, `#131924`), Xbox neon green accents (`#10ff50`), Consolas/Segoe UI typography.
- Performance: Launcher idle RAM footprint < 50 MB; UI minimized to tray during gaming.
- Safe Git rule: Commit messages in English, <= 5 words; no force push.
- Zero Hook / Anti-Cheat: Passive ETW monitoring only, no DLL injection or process hooks.

## Review Focus
- Corrupted or locked `libraryfolders.vdf` or `games.json`: Parser catches JSON/VDF syntax errors and returns empty/cached list rather than crashing.
- Missing Steam / Riot / Epic installations: Scanner handles missing Registry keys and non-existent directories gracefully.
- Invalid or deleted custom `.exe` path: `GameLauncherService` handles `Win32Exception` / `FileNotFoundException` with a user-friendly error prompt.
- Game launch URI protocol rejection: Shell execution failure displays clear notification and does not hang the UI thread.
- Duplicate game scan detection: Rescanning merges updates by `Id` without duplicating game entries or wiping custom entries.

---

### Task 1: Core Models & JSON Storage (`GameEntry`, `GamePlatform`, `GameLibraryStorage`)

**Files:**
- Create: `GamePerfOverlay/Models/GamePlatform.cs`
- Create: `GamePerfOverlay/Models/GameEntry.cs`
- Create: `GamePerfOverlay/Services/GameLibraryStorage.cs`
- Test: `GamePerfOverlay.Tests/GameLibraryStorageTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum GamePlatform { Steam, Riot, Epic, Standalone }
  public class GameEntry {
      public string Id { get; set; }
      public string Title { get; set; }
      public GamePlatform Platform { get; set; }
      public string LaunchUri { get; set; }
      public string? LaunchArguments { get; set; }
      public string? InstallPath { get; set; }
      public string? ExecutablePath { get; set; }
      public string? CoverImageUrl { get; set; }
      public DateTime? LastPlayed { get; set; }
      public int PlayCount { get; set; }
  }
  public interface IGameLibraryStorage {
      List<GameEntry> Load();
      void Save(IEnumerable<GameEntry> games);
  }
  ```

- [ ] **Step 1: Write failing storage tests**
  Write tests in `GamePerfOverlay.Tests/GameLibraryStorageTests.cs` covering saving a list of `GameEntry` items to JSON and loading them back with identical fields, handling empty or non-existent files safely.

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~GameLibraryStorageTests`
  Expected: FAIL (types not found).

- [ ] **Step 3: Implement Models & Storage**
  Implement `GamePlatform.cs`, `GameEntry.cs`, and `GameLibraryStorage.cs` reading/writing to `%APPDATA%\GamePerfOverlay\games.json` using `System.Text.Json` (formatted, camelCase).

- [ ] **Step 4: Run test to verify pass**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~GameLibraryStorageTests`
  Expected: PASS.

- [ ] **Step 5: Commit**
  ```bash
  git add GamePerfOverlay/Models/ GamePerfOverlay/Services/GameLibraryStorage.cs GamePerfOverlay.Tests/GameLibraryStorageTests.cs
  git commit -m "Add game models and storage"
  ```

---

### Task 2: Steam Scanner (`SteamScanner` & Steam VDF Parser)

**Files:**
- Create: `GamePerfOverlay/Services/Scanners/IGameScanner.cs`
- Create: `GamePerfOverlay/Services/Scanners/SteamScanner.cs`
- Test: `GamePerfOverlay.Tests/SteamScannerTests.cs`

**Interfaces:**
- Consumes: `GameEntry`, `GamePlatform`
- Produces:
  ```csharp
  public interface IGameScanner {
      GamePlatform Platform { get; }
      Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken ct = default);
  }
  public class SteamScanner : IGameScanner { ... }
  ```

- [ ] **Step 1: Write failing Steam scanner tests**
  Create `GamePerfOverlay.Tests/SteamScannerTests.cs` with mocked VDF string (`libraryfolders.vdf` containing multiple paths) and sample `appmanifest_730.acf`. Verify that CS2 (`AppID 730`) is parsed into a `GameEntry` with `LaunchUri = "steam://run/730"`, `Platform = GamePlatform.Steam`, and `CoverImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/730/header.jpg"`.

- [ ] **Step 2: Run test to verify failure**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~SteamScannerTests`
  Expected: FAIL.

- [ ] **Step 3: Implement `SteamScanner`**
  Implement `SteamScanner.cs`: Read registry `HKCU\Software\Valve\Steam\SteamPath`, parse `steamapps/libraryfolders.vdf` for all library roots, parse all `appmanifest_*.acf` files, and construct `GameEntry` records.

- [ ] **Step 4: Run test to verify pass**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~SteamScannerTests`
  Expected: PASS.

- [ ] **Step 5: Commit**
  ```bash
  git add GamePerfOverlay/Services/Scanners/ GamePerfOverlay.Tests/SteamScannerTests.cs
  git commit -m "Add Steam library scanner"
  ```

---

### Task 3: Riot Games & Epic Games Scanners (`RiotScanner`, `EpicScanner`)

**Files:**
- Create: `GamePerfOverlay/Services/Scanners/RiotScanner.cs`
- Create: `GamePerfOverlay/Services/Scanners/EpicScanner.cs`
- Test: `GamePerfOverlay.Tests/RiotScannerTests.cs`
- Test: `GamePerfOverlay.Tests/EpicScannerTests.cs`

**Interfaces:**
- Consumes: `IGameScanner`, `GameEntry`, `GamePlatform`
- Produces:
  ```csharp
  public class RiotScanner : IGameScanner { ... }
  public class EpicScanner : IGameScanner { ... }
  ```

- [ ] **Step 1: Write failing tests for Riot and Epic scanners**
  Create `RiotScannerTests.cs` testing detection from mock Riot installs JSON for LoL and Valorant.
  Create `EpicScannerTests.cs` testing parsing mock `.item` JSON manifest for `DisplayName`, `AppName`, and `InstallLocation`.

- [ ] **Step 2: Run tests to verify failure**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~ScannerTests`
  Expected: FAIL.

- [ ] **Step 3: Implement `RiotScanner` and `EpicScanner`**
  - `RiotScanner`: Detects `RiotClientServices.exe` path, checks products (`league_of_legends`, `valorant`), and sets launch arguments `--launch-product={product} --launch-patchline=live`.
  - `EpicScanner`: Reads `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item`, parsing `DisplayName`, `AppName`, and constructing URI `com.epicgames.launcher://apps/{AppName}?action=launch&silent=true`.

- [ ] **Step 4: Run tests to verify pass**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~ScannerTests`
  Expected: PASS.

- [ ] **Step 5: Commit**
  ```bash
  git add GamePerfOverlay/Services/Scanners/ GamePerfOverlay.Tests/RiotScannerTests.cs GamePerfOverlay.Tests/EpicScannerTests.cs
  git commit -m "Add Riot and Epic scanners"
  ```

---

### Task 4: Library Manager & Launcher Service (`GameLibraryManager`, `GameLauncherService`)

**Files:**
- Create: `GamePerfOverlay/Services/GameLibraryManager.cs`
- Create: `GamePerfOverlay/Services/GameLauncherService.cs`
- Test: `GamePerfOverlay.Tests/GameLibraryManagerTests.cs`
- Test: `GamePerfOverlay.Tests/GameLauncherServiceTests.cs`

**Interfaces:**
- Consumes: `IGameScanner`, `IGameLibraryStorage`, `GameEntry`
- Produces:
  ```csharp
  public class GameLibraryManager {
      public ObservableCollection<GameEntry> Games { get; }
      public Task ScanAllAsync(IProgress<string>? progress = null);
      public void AddCustomGame(string exePath, string title);
      public void RemoveGame(string id);
  }
  public class GameLauncherService {
      public bool Launch(GameEntry game);
  }
  ```

- [ ] **Step 1: Write failing tests**
  Test `GameLibraryManager`: Deduplication during rescan, adding custom game, filtering by search keyword and platform.
  Test `GameLauncherService`: Verification of `ProcessStartInfo` configuration for protocol URIs vs. direct executables, and immediate increment of `PlayCount` and update of `LastPlayed`.

- [ ] **Step 2: Run tests to verify failure**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~GameLibrary`
  Expected: FAIL.

- [ ] **Step 3: Implement Manager & Launcher Service**
  Implement `GameLibraryManager.cs` with thread-safe collections and async scanner fan-out.
  Implement `GameLauncherService.cs` with `Process.Start(new ProcessStartInfo { FileName = ..., UseShellExecute = true })`.

- [ ] **Step 4: Run tests to verify pass**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~GameLibrary`
  Expected: PASS.

- [ ] **Step 5: Commit**
  ```bash
  git add GamePerfOverlay/Services/GameLibraryManager.cs GamePerfOverlay/Services/GameLauncherService.cs GamePerfOverlay.Tests/
  git commit -m "Add library manager and launcher"
  ```

---

### Task 5: Launcher UI & ViewModel (`LauncherViewModel`, `LauncherWindow`)

**Files:**
- Create: `GamePerfOverlay/ViewModels/LauncherViewModel.cs`
- Create: `GamePerfOverlay/Views/LauncherWindow.xaml`
- Create: `GamePerfOverlay/Views/LauncherWindow.xaml.cs`
- Test: `GamePerfOverlay.Tests/LauncherViewModelTests.cs`

**Interfaces:**
- Consumes: `GameLibraryManager`, `GameLauncherService`
- Produces: Complete WPF Dark-themed window with game grid, search, scan button, and "CHƠI NGAY" actions.

- [ ] **Step 1: Write failing ViewModel tests**
  Test `LauncherViewModel` search filtering, platform category switching, and triggering `PlayCommand` invokes `GameLauncherService`.

- [ ] **Step 2: Run tests to verify failure**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~LauncherViewModelTests`
  Expected: FAIL.

- [ ] **Step 3: Implement `LauncherViewModel` and `LauncherWindow`**
  Implement `LauncherViewModel.cs` using MVVM commands.
  Design `LauncherWindow.xaml` matching the approved mockup:
  - Header: Logo, Search input, "Quét tìm Game", "Thêm .exe", Overlay status.
  - Sidebar: Platform filter buttons with badge counts.
  - Body: Scrollable game card poster grid with hover glow, platform icon, title, and "CHƠI NGAY" button (`#10ff50`).
  - Wire closing event: override `OnClosing` to hide/minimize to tray (`Visibility = Visibility.Collapsed`).

- [ ] **Step 4: Run tests to verify pass**
  Run: `dotnet test GamePerfOverlay.slnx --filter FullyQualifiedName~LauncherViewModelTests`
  Expected: PASS.

- [ ] **Step 5: Commit**
  ```bash
  git add GamePerfOverlay/ViewModels/LauncherViewModel.cs GamePerfOverlay/Views/LauncherWindow.* GamePerfOverlay.Tests/LauncherViewModelTests.cs
  git commit -m "Add launcher window and viewmodel"
  ```

---

### Task 6: Tray Menu & Application Lifecycle Integration

**Files:**
- Modify: `GamePerfOverlay/App.xaml.cs`

**Interfaces:**
- Consumes: `LauncherWindow`, `MainWindow` (overlay HUD), `Forms.NotifyIcon`
- Produces: Integrated tray management:
  - Tray menu item: "Mở Game Launcher"
  - Double-click tray icon: Opens/shows `LauncherWindow`
  - When game launches from Launcher: Minimizes `LauncherWindow` to tray, leaves `MainWindow` overlay active.

- [ ] **Step 1: Update `App.xaml.cs`**
  Add `LauncherWindow` instantiation.
  Add "Mở Game Launcher" to `NotifyIcon` context menu.
  Hook `LauncherWindow` minimize callback on game launch.
  Wire double-click on tray icon to toggle `LauncherWindow` visibility.

- [ ] **Step 2: Build and verify 0 errors**
  Run: `dotnet build GamePerfOverlay.slnx`
  Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Run all unit tests**
  Run: `dotnet test GamePerfOverlay.slnx`
  Expected: All tests pass.

- [ ] **Step 4: Commit**
  ```bash
  git add GamePerfOverlay/App.xaml.cs
  git commit -m "Integrate launcher into tray menu"
  ```

---

### Task 7: End-to-End Build, Verification & Documentation

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update documentation in `README.md`**
  Document the new Game Launcher feature, auto-scan for Steam/Riot/Epic, and how it coexists with the performance overlay.

- [ ] **Step 2: Full Solution Build & Unit Test Verification**
  Run: `dotnet test GamePerfOverlay.slnx`
  Expected: All unit tests pass.

- [ ] **Step 3: Compile Single-File Release**
  Run: `dotnet publish GamePerfOverlay/GamePerfOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o Release/`
  Update `GamePerfOverlay-v1.0.0-win-x64.zip`.

- [ ] **Step 4: Commit**
  ```bash
  git add README.md
  git commit -m "Document game launcher feature"
  ```

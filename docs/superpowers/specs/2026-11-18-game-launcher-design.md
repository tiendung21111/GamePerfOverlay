# Game Launcher & Game Hub - Design Document

## 1. Intent & Context
The application started as a lightweight, zero-hook, transparent hardware performance overlay (`GamePerfOverlay`) for Windows 10/11. The user now wants to expand its capabilities into an **All-in-One Game Launcher & Game Hub** to manage and launch all existing games across different platforms (Steam, Riot Client, Epic Games Launcher, and standalone `.exe` games) from a single centralized interface, while retaining the lightweight neon-green performance overlay during gameplay.

---

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
- **Unified Game Library (Thư viện trò chơi tập trung):**
  - Consolidate games from multiple client platforms into a single clean dashboard:
    1. **Steam:** Counter-Strike 2, Dota 2, Black Myth: Wukong, etc.
    2. **Riot Games:** League of Legends (LoL), VALORANT.
    3. **Epic Games Launcher:** Grand Theft Auto V, etc.
    4. **Standalone / Custom Executables:** Portable games, emulator games, or non-launcher installations added manually via an `.exe` file picker.
- **Automatic Game Scanner ("Quét tìm Game"):**
  - **Steam Scanner:** Discovers Steam installation from Registry (`HKCU\Software\Valve\Steam`), parses `steamapps\libraryfolders.vdf` to find all library drives (`C:`, `D:`, `E:`), and reads `appmanifest_<id>.acf` files to extract game titles, installation paths, and AppIDs.
  - **Riot Games Scanner:** Discovers Riot Client installations from `%PROGRAMDATA%\Riot Games\RiotClientInstalls.json` and registry keys, detecting installed Riot products (LoL, Valorant).
  - **Epic Games Scanner:** Reads `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item` JSON manifests to retrieve `DisplayName`, `InstallLocation`, and `AppName`.
  - Background async scanning with progress feedback (banner / spinner).
- **One-Click Game Launch ("CHƠI NGAY"):**
  - Steam games: Launched via native URI protocol `steam://run/<AppID>` (safe, triggers Steam DRM and anti-cheat properly).
  - Riot games: Launched via `RiotClientServices.exe --launch-product=<product> --launch-patchline=live` (e.g., `league_of_legends`, `valorant`).
  - Epic games: Launched via `com.epicgames.launcher://apps/<AppName>?action=launch&silent=true`.
  - Standalone games: Launched via standard `Process.Start` with working directory set to executable directory.
- **Seamless Performance HUD Integration:**
  - Launching a game from the Launcher automatically minimizes the Launcher window to the System Tray to conserve CPU and GPU resources.
  - The existing zero-hook ETW framerate tracker and hardware sensor overlay (`#10ff50` Consolas, top-left corner `Left=6 Top=6`, `Ctrl + D + M`) automatically overlays over the running game once 3D rendering starts.
- **Modern WPF Gaming Dashboard UI:**
  - Dark-themed aesthetic matching the Xbox neon green branding (`#10ff50`, dark background `#0b0e14`, card `#131924`).
  - Poster Card Grid view with cover art / thumbnails and platform badges (Steam / Riot / Epic).
  - Instant search filtering by title.
  - Platform filter buttons (All, Steam, Riot Games, Epic Games, Custom).
  - Manual game addition modal dialog ("Thêm .exe thủ công").
- **Persistence:**
  - Game catalog, custom additions, and play history saved to `%APPDATA%\GamePerfOverlay\games.json`.
- **System Tray Integration:**
  - App tray menu gains a new entry: **"Mở Game Launcher"** (or double-clicking the tray icon when the overlay is active).

### Non-Goals
- No digital store, in-app purchases, game purchasing, or DRM cracking.
- No social networks, friend lists, or chat messaging.
- No cloud save synchronization.

### Anti-Goals
- **No Heavy Launcher Bloat:** Unlike Chromium/Electron-based launchers (which consume 300–600 MB of RAM), this native C# WPF launcher must consume < 50 MB RAM when idle, and immediately release UI rendering resources when minimized during gaming.
- **Zero Disruption to Standalone Overlay:** The performance overlay (`Ctrl + D + M`, tray icon, zero-hook ETW, hardware monitoring) must remain 100% usable independently, even if the user chooses not to open the Launcher dashboard.

---

## 3. Architecture & Component Boundaries

```
[System Tray (App.xaml.cs)]
        │
        ├──► [Overlay HUD (MainWindow.xaml)] ──► FpsTracker (ETW) & SensorService (Hardware)
        │
        └──► [Launcher Window (LauncherWindow.xaml)]
                    │
                    ├──► LauncherViewModel
                    │         │
                    │         └──► GameLibraryManager
                    │                   │
                    │                   ├──► SteamScanner
                    │                   ├──► RiotScanner
                    │                   ├──► EpicScanner
                    │                   └──► games.json Storage
                    │
                    └──► GameLauncherService ──► Process.Start / Shell URI Protocol
```

### Module Responsibilities:
1. `GameEntry.cs`: Data model for a game:
   - `Id` (GUID or platform-prefixed ID like `steam_730`)
   - `Title` (string)
   - `Platform` (Enum: `Steam`, `Riot`, `Epic`, `Standalone`)
   - `LaunchUri` (string, e.g. `steam://run/730` or executable path)
   - `LaunchArguments` (string)
   - `InstallPath` (string)
   - `ExecutablePath` (string)
   - `CoverImageUrl` (string or local cache path)
   - `LastPlayed` (DateTime?)
   - `PlayCount` (int)

2. `IGameScanner.cs` & Implementations:
   - `SteamScanner`: Scans Registry `HKCU\Software\Valve\Steam\SteamPath` -> reads `config/libraryfolders.vdf` -> reads all `steamapps/appmanifest_*.acf` across library folders -> outputs Steam `GameEntry` list.
   - `RiotScanner`: Checks `%PROGRAMDATA%\Riot Games\RiotClientInstalls.json` -> detects `rc_default` or `rc_live` -> checks installed products (League of Legends: `%PROGRAMDATA%\Riot Games\Metadata\league_of_legends.live\league_of_legends.live.product_settings.yaml` or registry).
   - `EpicScanner`: Scans `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (JSON format) -> extracts `DisplayName`, `InstallLocation`, `LaunchExecutable`, `AppName`.

3. `GameLibraryManager.cs`:
   - Orchestrates multi-threaded scanning across all scanners.
   - Merges newly discovered games with existing custom entries without overwriting user customization.
   - Serializes/deserializes to `%APPDATA%\GamePerfOverlay\games.json`.
   - Exposes observable collections for search and platform filtering.

4. `GameLauncherService.cs`:
   - Executes launch with appropriate protocol or process arguments.
   - Triggers UI callback to minimize/hide LauncherWindow to tray.
   - Signals `FpsTracker` to actively monitor new process rendering.

5. `LauncherWindow.xaml` / `LauncherViewModel.cs`:
   - Top navigation bar: App title, Search bar, "Quét tìm Game" button, "Thêm .exe" button, Overlay toggle indicator.
   - Left sidebar: Platform filter buttons (All, Steam, Riot, Epic, Standalone) with counts.
   - Center grid: Responsive wrap panel of gaming cards with cover image, platform badge, title, and "CHƠI NGAY" button.

---

## 4. Data Storage Contract (`games.json`)

Location: `%APPDATA%\GamePerfOverlay\games.json`
```json
[
  {
    "id": "steam_730",
    "title": "Counter-Strike 2",
    "platform": "Steam",
    "launchUri": "steam://run/730",
    "installPath": "D:\\SteamLibrary\\steamapps\\common\\Counter-Strike Global Offensive",
    "executablePath": "D:\\SteamLibrary\\steamapps\\common\\Counter-Strike Global Offensive\\game\\bin\\win64\\cs2.exe",
    "coverImageUrl": "https://cdn.cloudflare.steamstatic.com/steam/apps/730/header.jpg",
    "lastPlayed": "2026-11-18T10:00:00Z"
  },
  {
    "id": "riot_lol",
    "title": "League of Legends",
    "platform": "Riot",
    "launchUri": "RiotClientServices.exe",
    "launchArguments": "--launch-product=league_of_legends --launch-patchline=live",
    "installPath": "C:\\Riot Games\\League of Legends",
    "coverImageUrl": "pack://application:,,,/Assets/covers/lol.jpg",
    "lastPlayed": "2026-11-17T20:30:00Z"
  }
]
```

---

## 5. Architectural & Implementation Decisions (Builder Review Settled Calls)

1. **Overlay Automation & ETW Safety:**
   - *Decision:* `FpsTracker` remains completely passive and foreground-driven via ETW. It continues to report `-- FPS` on desktop and real-time framerate during 3D rendering.
   - When "CHƠI NGAY" is clicked, `GameLauncherService` minimizes `LauncherWindow` to the System Tray to free all UI rendering resources. The transparent overlay HUD remains visible at `Left=6 Top=6` (or toggled with `Ctrl + D + M`). No process tree injection or WMI process hooks are used, guaranteeing 100% anti-cheat safety with Vanguard, BattlEye, and EasyAntiCheat.

2. **Cover Art & Thumbnail Strategy:**
   - *Decision:* 100% Offline & Zero Configuration.
     - **Steam:** Fetches cover art directly via the public Steam CDN (`https://cdn.cloudflare.steamstatic.com/steam/apps/{id}/header.jpg`).
     - **Riot Games:** Bundles static high-resolution artwork for League of Legends, VALORANT, Teamfight Tactics.
     - **Epic Games & Standalone:** Extracts the native high-res application icon directly from the `.exe` file via `Shell32` Win32 API (`SHGetFileInfo` / `ExtractAssociatedIcon`), overlaid on an elegant dark game card. No external third-party API keys or accounts required.

3. **`ExecutablePath` Resolution:**
   - *Decision:* `ExecutablePath` is optional/nullable for Steam and Epic games (since launching relies on safe protocol handlers `steam://run/<id>`), and strictly mandatory only for Standalone custom games. This keeps disk scanning instantaneous (< 1 second) across multi-terabyte drives.

4. **Launch Tracking & Window Lifecycle (User Decision - Tray-Managed):**
   - *Decision:* Fire-and-forget update. Clicking "CHƠI NGAY" updates `LastPlayed = DateTime.UtcNow` and increments `PlayCount` in `games.json`, then minimizes `LauncherWindow` to the System Tray.
   - When a game exits, the Launcher remains quietly in the System Tray without intrusive pop-ups.
   - The user reopens the Launcher window anytime on demand by **double-clicking the system tray icon** or choosing **"Mở Game Launcher"** from the tray menu.
   - Closing `LauncherWindow` via the top-right `X` button cleanly hides/minimizes it to the tray rather than terminating the background overlay process.

---

## 6. Security & Stability Considerations
- **Non-blocking Scanners:** File I/O and Registry queries run entirely in background tasks (`Task.Run`), never freezing the WPF UI thread.
- **Fail-safe Error Handling:** If a drive is disconnected, or a manifest file is corrupt, the scanner skips that entry gracefully without throwing an unhandled exception.
- **Safe Process Invocation:** `UseShellExecute = true` when launching protocols (`steam://`, `com.epicgames.launcher://`) ensuring proper elevation and Windows URL handler routing.

---

## 7. Testing Strategy
1. **Steam Parser Tests (`SteamScannerTests.cs`):** Test parsing sample `libraryfolders.vdf` and `appmanifest_730.acf` mocks.
2. **Epic Manifest Parser Tests (`EpicScannerTests.cs`):** Test parsing mock Epic `.item` manifest JSON.
3. **Library Manager Tests (`GameLibraryManagerTests.cs`):** Test persistence to JSON, searching, and filtering by platform.
4. **Integration Verification:** Run test suite via `dotnet test GamePerfOverlay.slnx` with 0 failures.

# Game Performance Overlay (Win 11) - Design Document

## 1. Intent & Context
The user plays games on Windows 10/11 and previously relied on the built-in Xbox Game Bar (`Win + G`) Performance widget. However, Xbox Game Bar does not report hardware temperatures on many systems. The user wants a custom, lightweight, ultra-simple performance overlay that runs on multiple types of Windows PCs (desktops and laptops across different hardware vendors) without impacting gaming performance.

---

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
- **Universal Multi-Hardware Compatibility:**
  - Support Intel and AMD CPUs (temperature, clock speed, load).
  - Support NVIDIA GeForce, AMD Radeon, and Intel Arc / Iris Xe GPUs (temperature, clock, core load, and VRAM).
  - Smart VRAM detection: Auto-detects dedicated VRAM on discrete graphics cards (e.g. RTX/Radeon), or shared dynamic video memory on integrated GPUs.
- **Real-time Monitoring of 5 Key Metrics:**
  1. `FPS`: Real-time framerate of the active game (shows `--` when no 3D game is detected).
  2. `CPU`: Load percentage (`%`), core temperature (`°C`), and current clock speed (`GHz`).
  3. `GPU`: Core load percentage (`%`), temperature (`°C`), and clock frequency (`MHz`).
  4. `VRAM`: Allocation percentage (`%`) and allocated memory (`GB`).
  5. `RAM`: System memory usage (`%`) and used memory (`GB`).
- **Visual Design & Layout:**
  - 100% transparent background (no window frame, no titlebar, no borders, no opaque container).
  - All text rendered in uniform Xbox neon green (`#10ff50`) using a clean monospaced font (`Consolas`).
  - Dark text-shadow behind all glyphs to ensure high legibility over both dark and bright in-game scenes (e.g., snow/sky).
  - No header row, no branding ("PERF HUD" removed), no graphs or history charts.
  - Exactly 5 lines of metrics stacked vertically in order: FPS, CPU, GPU, VRAM, RAM.
- **Window Management & Zero Gaming Distraction (Pure Non-Interactive HUD):**
  - **Fixed Screen Position:** Permanently positioned at the Top-Left of the main screen with a comfortable margin (20px from the top edge, 20px from the left edge) so it never hugs the bezel or causes reading difficulty.
  - **Permanent Click-Through:** The window is permanently transparent to mouse input (`WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`). It never steals focus, never accepts clicks, and never interrupts mouse gameplay under any circumstances.
  - **Zero Mouse Interaction:** The user only interacts via the global hotkey `Ctrl + D + M` to toggle visibility (show/hide). No dragging or mouse UI needed.
  - **Always-on-Top:** `TopMost = true` so it overlays seamlessly over borderless-windowed and windowed games when visible.
- **Portability:**
  - Self-contained, portable build capable of running on any Windows 10/11 64-bit PC without complex setup.

### Non-Goals
- No graph plotting, sparklines, or historical trend charts.
- No hardware overclocking, fan speed control, or voltage tweaking.
- No in-game recording, screenshot capture, or benchmark logging to CSV.
- No fullscreen exclusive Direct3D hook injection.

### Anti-Goals
- **Anti-Cheat Safe (Zero Hooking):** Do not inject DLLs or hook game graphics APIs (e.g., Detours, MinHook, DirectX present hooking). The app must use out-of-process Windows Event Tracing (ETW) / OS counters so it never triggers anti-cheat bans (EasyAntiCheat, BattlEye, Vanguard).
- **Zero Game Performance Impact (Ultra-Lightweight):**
  - Background polling runs on a low-priority background thread at 1 Hz (1,000 ms).
  - Minimal UI redraw with negligible memory footprint (< 30 MB) and < 0.1% CPU consumption.
  - No mouse event capture overhead during normal gameplay.

---

## 3. Platform & Target Hardware Compatibility

- **Target OS:** Windows 10 & Windows 11 (64-bit).
- **Supported CPU Architectures:**
  - Intel Core (Desktop & Laptop, All Generations).
  - AMD Ryzen / Athlon (Desktop & Laptop, AM4/AM5/Mobile).
- **Supported GPU Vendors:**
  - NVIDIA GeForce (RTX / GTX series via NVML / driver counters).
  - AMD Radeon (RX series via ADLX / ADL).
  - Intel Arc & Intel Integrated Graphics (Iris Xe / UHD Graphics).
- **Installed Build Runtimes on Developer PC:** .NET SDK 10.0, Python 3.12.10.

---

## 4. Architectural & Implementation Decisions

### 4.1 Technology Stack Selection (Builder Call)
- **Framework:** **C# .NET 10 with Windows Presentation Foundation (WPF)**.
  - *Rationale:* WPF natively supports per-pixel alpha transparency (`AllowsTransparency="True" WindowStyle="None" Background="Transparent"`), hardware-accelerated text rendering with drop-shadow effects, and negligible RAM footprint (< 30MB).
  - *Zero Impact Architecture:* Runs with negligible CPU load (< 0.1%). Polling runs strictly on a background `Task` at 1,000 ms interval, updating the UI via light data binding.
- **Sensor Data Backend:** `LibreHardwareMonitorLib` (via NuGet).
  - Broad universal hardware support for AMD & Intel CPUs and NVIDIA, AMD, and Intel GPUs.
  - Automatically identifies available hardware at runtime; dynamically binds to whichever discrete GPU or iGPU is active.
  - *Admin Privilege Requirement:* Hardware temperature registers on x86/x64 platforms require kernel driver access (WinRing0 / LibreHardwareMonitor driver). The app includes an admin manifest (`requireAdministrator`) or prompts elevation so sensors work on any PC.
- **FPS Acquisition:** Event Tracing for Windows (`TraceEvent` / DXGI ETW provider) or Windows Graphics Performance Counters. Tracks `Present` events from the active foreground window without hooking.
- **Global Hotkey Implementation:**
  - Low-level keyboard hook (`SetWindowsHookEx(WH_KEYBOARD_LL)`) to reliably detect the 3-key chord `Ctrl + D + M` simultaneously across any Windows system.
- **Permanent Click-Through Architecture:**
  - Window extended styles: `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`.
  - The window is invisible to Windows hit-testing. All mouse messages pass directly to underlying windows/games without any processing.
  - Position is fixed at Top-Left (`Left = 20`, `Top = 20`), ensuring a clean margin from the display bezel. No dragging code or mouse event listeners exist in the process, maximizing execution simplicity and lightness.

---

## 5. User-Approved Design Decisions (Recorded from Conversation)

1. **Color Scheme:** All text and values must be Xbox green (`#10ff50`). No white or grey secondary text.
2. **Titlebar / Headers:** Completely removed. No "PERF HUD" text or close buttons.
3. **No Charts:** Graphs completely eliminated. Only category names and quantitative values.
4. **Background:** 100% transparent.
5. **Hotkey:** Explicitly set to `Ctrl + D + M`.
6. **Metrics Order:**
   - Line 1: `FPS   <val> FPS` (or `--`)
   - Line 2: `CPU   <val>%  <temp>°C  <clock> GHz`
   - Line 3: `GPU   <val>%  <temp>°C  <clock> MHz`
   - Line 4: `VRAM  <val>%  <used> GB`
   - Line 5: `RAM   <val>%  <used> GB`

---

## 6. What's Left to the Builder

- **Polling Rate:** Default set to 1,000 ms (1 Hz) to balance instant responsiveness with virtually 0% CPU consumption.
- **Universal Hardware Sensor Fallback:**
  - For Dedicated GPUs (NVIDIA/AMD): Read dedicated VRAM and GPU core temperature directly.
  - For Integrated GPUs (Intel/AMD APU): Read WDDM shared video memory (`DXGI_MEMORY_SEGMENT_GROUP_NON_LOCAL`). If the APU shares a single package diode without an isolated GPU sensor, mirror the unified CPU/Package temperature.
- **FPS 3D Game Filtering:** Filter out desktop window manager processes (`dwm.exe`, `explorer.exe`) and web browsers, reporting real FPS only when a 3D swapchain is active in the foreground, and `--` otherwise.
- **Fallback Handling:** If temperature sensors are initializing or running without elevated rights, display `--°C` instead of crashing.
- **Configuration Persistence:** Save `{ "Left": double, "Top": double }` to `%LOCALAPPDATA%\GamePerfOverlay\config.json`.
- **System Tray Icon:** Add a subtle system tray icon (notify icon) with a right-click menu ("Show/Hide", "Run as Admin", "Exit") so the user can easily exit or manage the app when it is hidden.
- **Portable Deployment:** Configure publish profile for single-file self-contained win-x64 binary so the user can copy the `.exe` to any PC and run it directly without installing .NET SDK.

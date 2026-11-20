using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using GamePerfOverlay.Interop;

namespace GamePerfOverlay.Services;

public class HotkeyManager : IDisposable
{
    private const int HOTKEY_ID = 0x484B; // "HK"

    private readonly HotkeyChordDetector _detector;
    private int[] _targetKeys = [];
    private HwndSource? _hwndSource;
    private bool _isRegisteredHotKey;

    private NativeMethods.LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public event Action? ToggleRequested;

    public bool IsKernelHotKeyActive => _isRegisteredHotKey;
    public bool IsHookActive => _hookId != IntPtr.Zero;

    public HotkeyManager(IEnumerable<int>? initialKeys = null)
    {
        var keys = initialKeys?.ToArray() ?? [HotkeyHelper.VK_MENU, 0x51]; // Default: Alt + Q
        _targetKeys = keys;
        _detector = new HotkeyChordDetector(keys);
    }

    public void UpdateHotkey(IEnumerable<int> keys)
    {
        _targetKeys = keys.ToArray();
        _detector.SetTargetKeys(_targetKeys);
        Register();
    }

    public void Register()
    {
        if (_disposed) return;

        // 1. Try native Windows RegisterHotKey first (immune to timeouts, thread-pool issues, and anti-cheat)
        if (TryParseHotKey(_targetKeys, out uint modifiers, out uint vk))
        {
            EnsureHwndSource();

            if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
            {
                UnregisterHotKeyOnly();

                if (NativeMethods.RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, modifiers, vk))
                {
                    _isRegisteredHotKey = true;
                    UnregisterHook();
                    return;
                }
            }
        }

        // 2. Fallback to low-level keyboard hook if RegisterHotKey was not applicable or failed (e.g. conflict)
        UnregisterHotKeyOnly();
        RegisterHook();
    }

    private void EnsureHwndSource()
    {
        if (_hwndSource != null) return;

        try
        {
            var parameters = new HwndSourceParameters("GamePerfOverlay_HotkeySink")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(HwndHook);
        }
        catch
        {
            _hwndSource = null;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleRequested?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void UnregisterHotKeyOnly()
    {
        if (_isRegisteredHotKey && _hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
        {
            try
            {
                NativeMethods.UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
            }
            catch
            {
            }
            _isRegisteredHotKey = false;
        }
    }

    private void RegisterHook()
    {
        if (_hookId != IntPtr.Zero || _disposed) return;

        _proc = HookCallback;
        var moduleHandle = NativeMethods.GetModuleHandle(null);

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            moduleHandle,
            0);
    }

    private void UnregisterHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private static bool IsPhysicalKeyDown(int vkCode)
    {
        return (NativeMethods.GetAsyncKeyState(vkCode) & 0x8000) != 0;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            bool isDown = (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN);
            bool isUp = (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP);

            if (isDown || isUp)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (_detector.ProcessKeyEvent(vkCode, isDown, IsPhysicalKeyDown))
                {
                    ToggleRequested?.Invoke();
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public static bool TryParseHotKey(IEnumerable<int> keys, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var normalized = keys.Select(HotkeyHelper.NormalizeVkCode).Distinct().ToList();
        if (normalized.Count == 0) return false;

        var nonModifiers = new List<int>();

        foreach (var k in normalized)
        {
            switch (k)
            {
                case HotkeyHelper.VK_CONTROL:
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case HotkeyHelper.VK_MENU:
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case HotkeyHelper.VK_SHIFT:
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case HotkeyHelper.VK_LWIN:
                case HotkeyHelper.VK_RWIN:
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    nonModifiers.Add(k);
                    break;
            }
        }

        if (nonModifiers.Count == 1)
        {
            vk = (uint)nonModifiers[0];
            modifiers |= NativeMethods.MOD_NOREPEAT;
            return true;
        }

        return false;
    }

    public void Unregister()
    {
        UnregisterHotKeyOnly();
        UnregisterHook();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unregister();

        if (_hwndSource != null)
        {
            try
            {
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource.Dispose();
            }
            catch
            {
            }
            _hwndSource = null;
        }
    }
}

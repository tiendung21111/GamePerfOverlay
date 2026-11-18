using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GamePerfOverlay.Interop;

namespace GamePerfOverlay.Services;

public class HotkeyManager : IDisposable
{
    private readonly HotkeyChordDetector _detector;
    private NativeMethods.LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public event Action? ToggleRequested;

    public HotkeyManager(IEnumerable<int>? initialKeys = null)
    {
        _detector = new HotkeyChordDetector(initialKeys);
    }

    public void UpdateHotkey(IEnumerable<int> keys)
    {
        _detector.SetTargetKeys(keys);
    }

    public void Register()
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
                if (_detector.ProcessKeyEvent(vkCode, isDown))
                {
                    ToggleRequested?.Invoke();
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Unregister()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unregister();
    }
}

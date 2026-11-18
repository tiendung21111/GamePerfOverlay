using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePerfOverlay.Services;

public static class HotkeyHelper
{
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12; // Alt
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;

    public static int NormalizeVkCode(int vkCode)
    {
        return vkCode switch
        {
            VK_LCONTROL or VK_RCONTROL => VK_CONTROL,
            VK_LSHIFT or VK_RSHIFT => VK_SHIFT,
            VK_LMENU or VK_RMENU => VK_MENU,
            VK_LWIN or VK_RWIN => VK_LWIN,
            _ => vkCode
        };
    }

    public static bool IsModifier(int vkCode)
    {
        int normalized = NormalizeVkCode(vkCode);
        return normalized is VK_CONTROL or VK_SHIFT or VK_MENU or VK_LWIN;
    }

    public static string GetKeyName(int vkCode)
    {
        int normalized = NormalizeVkCode(vkCode);

        return normalized switch
        {
            VK_CONTROL => "Ctrl",
            VK_SHIFT => "Shift",
            VK_MENU => "Alt",
            VK_LWIN => "Win",
            0x20 => "Space",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x08 => "Backspace",
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0xC0 => "~",
            >= 0x70 and <= 0x87 => $"F{normalized - 0x70 + 1}",
            >= 0x30 and <= 0x39 => ((char)('0' + (normalized - 0x30))).ToString(),
            >= 0x41 and <= 0x5A => ((char)('A' + (normalized - 0x41))).ToString(),
            _ => ((System.Windows.Forms.Keys)normalized).ToString()
        };
    }

    public static string FormatHotkey(IEnumerable<int> vkCodes)
    {
        var distinctCodes = vkCodes.Select(NormalizeVkCode).Distinct().ToList();

        // Sort by conventional priority: Ctrl, Alt, Shift, Win, then others
        distinctCodes.Sort((a, b) =>
        {
            int prioA = GetKeyPriority(a);
            int prioB = GetKeyPriority(b);
            if (prioA != prioB) return prioA.CompareTo(prioB);
            return a.CompareTo(b);
        });

        return string.Join(" + ", distinctCodes.Select(GetKeyName));
    }

    private static int GetKeyPriority(int vkCode)
    {
        return vkCode switch
        {
            VK_CONTROL => 1,
            VK_MENU => 2,
            VK_SHIFT => 3,
            VK_LWIN => 4,
            _ => 10
        };
    }
}

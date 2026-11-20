using GamePerfOverlay.Services;
using Xunit;

namespace GamePerfOverlay.Tests;

public class HotkeyManagerTests
{
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_D = 0x44;
    private const int VK_M = 0x4D;
    private const int VK_A = 0x41;
    private const int VK_O = 0x4F;
    private const int VK_Q = 0x51;

    [Fact]
    public void ChordDetector_DefaultIsAltQ()
    {
        var detector = new HotkeyChordDetector();

        // Default combo is Alt + Q
        Assert.False(detector.ProcessKeyEvent(VK_MENU, true));
        Assert.True(detector.ProcessKeyEvent(VK_Q, true));

        // Repeat should not trigger
        Assert.False(detector.ProcessKeyEvent(VK_Q, true));
    }

    [Fact]
    public void ChordDetector_TriggersWhenCtrlDMPressedSimultaneously()
    {
        var detector = new HotkeyChordDetector([VK_CONTROL, VK_D, VK_M]);

        // Press Ctrl
        Assert.False(detector.ProcessKeyEvent(VK_CONTROL, true));
        // Press D
        Assert.False(detector.ProcessKeyEvent(VK_D, true));
        // Press M -> all 3 down!
        Assert.True(detector.ProcessKeyEvent(VK_M, true));

        // Holding M down further (key repeat) should NOT re-trigger
        Assert.False(detector.ProcessKeyEvent(VK_M, true));

        // Release D
        Assert.False(detector.ProcessKeyEvent(VK_D, false));

        // Press D again -> triggers again!
        Assert.True(detector.ProcessKeyEvent(VK_D, true));
    }

    [Fact]
    public void ChordDetector_SelfHeals_WhenKeyUpDropped_UsingPhysicalChecker()
    {
        var detector = new HotkeyChordDetector([VK_MENU, VK_Q]); // Alt + Q

        // 1. Press Alt and Q -> triggers
        Assert.False(detector.ProcessKeyEvent(VK_MENU, true));
        Assert.True(detector.ProcessKeyEvent(VK_Q, true));

        // 2. Both keys are released in the real world, but Windows hook DROPPED KeyUp for VK_Q!
        detector.ProcessKeyEvent(VK_MENU, false);
        // Notice: VK_Q was NEVER sent a KeyUp!

        // 3. User attempts to toggle again later by pressing Alt.
        // Physical key checker returns true ONLY for keys currently physically down.
        // At this instant, only Alt is down, Q is physically released.
        bool isPhysicalDownAltOnly(int k) => k == VK_MENU;

        // ProcessKeyEvent should detect that VK_Q is physically released, clean it out, and reset _triggered!
        Assert.False(detector.ProcessKeyEvent(VK_MENU, true, isPhysicalDownAltOnly));

        // 4. User now presses Q. Both Alt and Q are down.
        bool isPhysicalDownBoth(int k) => k == VK_MENU || k == VK_Q;

        // MUST TRIGGER! Without self-healing, this would fail because _triggered was stuck from the missed KeyUp.
        Assert.True(detector.ProcessKeyEvent(VK_Q, true, isPhysicalDownBoth));
    }

    [Fact]
    public void ChordDetector_DoesNotTrigger_ForOtherKeys()
    {
        var detector = new HotkeyChordDetector();

        detector.ProcessKeyEvent(VK_MENU, true);
        detector.ProcessKeyEvent(VK_A, true);
        var triggered = detector.ProcessKeyEvent(VK_O, true);

        Assert.False(triggered);
    }

    [Fact]
    public void ChordDetector_CustomSingleKey_F12()
    {
        const int VK_F12 = 0x7B;
        var detector = new HotkeyChordDetector([VK_F12]);

        Assert.True(detector.ProcessKeyEvent(VK_F12, true));
        Assert.False(detector.ProcessKeyEvent(VK_F12, true)); // repeat
        Assert.False(detector.ProcessKeyEvent(VK_F12, false)); // release
        Assert.True(detector.ProcessKeyEvent(VK_F12, true)); // press again
    }

    [Fact]
    public void ChordDetector_CustomChord_CtrlShiftO()
    {
        var detector = new HotkeyChordDetector([HotkeyHelper.VK_CONTROL, HotkeyHelper.VK_SHIFT, VK_O]);

        Assert.False(detector.ProcessKeyEvent(HotkeyHelper.VK_LCONTROL, true));
        Assert.False(detector.ProcessKeyEvent(HotkeyHelper.VK_LSHIFT, true));
        Assert.True(detector.ProcessKeyEvent(VK_O, true));
    }

    [Fact]
    public void HotkeyHelper_FormatsCorrectly()
    {
        // Alt + Q
        string formatted0 = HotkeyHelper.FormatHotkey([0x51, 0x12]);
        Assert.Equal("Alt + Q", formatted0);

        // Ctrl + Shift + O
        string formatted1 = HotkeyHelper.FormatHotkey([0x4F, 0x10, 0x11]);
        Assert.Equal("Ctrl + Shift + O", formatted1);

        // F12
        string formatted2 = HotkeyHelper.FormatHotkey([0x7B]);
        Assert.Equal("F12", formatted2);

        // Ctrl + D + M
        string formatted3 = HotkeyHelper.FormatHotkey([0x4D, 0x11, 0x44]);
        Assert.Equal("Ctrl + D + M", formatted3);
    }

    [Fact]
    public void TryParseHotKey_ParsesAltQ_Successfully()
    {
        bool success = HotkeyManager.TryParseHotKey([0x12, 0x51], out uint modifiers, out uint vk);
        Assert.True(success);
        Assert.Equal(0x51u, vk);
        Assert.True((modifiers & GamePerfOverlay.Interop.NativeMethods.MOD_ALT) != 0);
        Assert.True((modifiers & GamePerfOverlay.Interop.NativeMethods.MOD_NOREPEAT) != 0);
    }

    [Fact]
    public void TryParseHotKey_ParsesCtrlShiftO_Successfully()
    {
        bool success = HotkeyManager.TryParseHotKey([0x11, 0x10, 0x4F], out uint modifiers, out uint vk);
        Assert.True(success);
        Assert.Equal(0x4Fu, vk);
        Assert.True((modifiers & GamePerfOverlay.Interop.NativeMethods.MOD_CONTROL) != 0);
        Assert.True((modifiers & GamePerfOverlay.Interop.NativeMethods.MOD_SHIFT) != 0);
    }

    [Fact]
    public void TryParseHotKey_ParsesSingleKeyF12_Successfully()
    {
        bool success = HotkeyManager.TryParseHotKey([0x7B], out uint modifiers, out uint vk);
        Assert.True(success);
        Assert.Equal(0x7Bu, vk);
        Assert.Equal(GamePerfOverlay.Interop.NativeMethods.MOD_NOREPEAT, modifiers);
    }

    [Fact]
    public void TryParseHotKey_ReturnsFalse_ForNonModifierChords()
    {
        // Ctrl + D + M (2 non-modifier keys D and M) -> must return false and fall back to hook
        bool success = HotkeyManager.TryParseHotKey([0x11, 0x44, 0x4D], out _, out _);
        Assert.False(success);
    }

    [Fact]
    public void SettingsService_SaveAndLoad_RoundtripsSuccessfully()
    {
        var settings = new Models.AppSettings
        {
            HotkeyCodes = [0x12, 0x51],
            HotkeyDisplay = "Alt + Q"
        };

        bool saved = SettingsService.Save(settings);
        Assert.True(saved);

        var loaded = SettingsService.Load();
        Assert.NotNull(loaded);
        Assert.Equal("Alt + Q", loaded.HotkeyDisplay);
        Assert.Equal([0x12, 0x51], loaded.HotkeyCodes);
    }
}

using GamePerfOverlay.Services;
using Xunit;

namespace GamePerfOverlay.Tests;

public class HotkeyManagerTests
{
    private const int VK_CONTROL = 0x11;
    private const int VK_D = 0x44;
    private const int VK_M = 0x4D;
    private const int VK_A = 0x41;

    [Fact]
    public void ChordDetector_TriggersWhenCtrlDMPressedSimultaneously()
    {
        var detector = new HotkeyChordDetector();

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
    public void ChordDetector_DoesNotTrigger_ForOtherKeys()
    {
        var detector = new HotkeyChordDetector();

        detector.ProcessKeyEvent(VK_CONTROL, true);
        detector.ProcessKeyEvent(VK_A, true);
        var triggered = detector.ProcessKeyEvent(VK_M, true);

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
        const int VK_O = 0x4F;
        var detector = new HotkeyChordDetector([HotkeyHelper.VK_CONTROL, HotkeyHelper.VK_SHIFT, VK_O]);

        Assert.False(detector.ProcessKeyEvent(HotkeyHelper.VK_LCONTROL, true));
        Assert.False(detector.ProcessKeyEvent(HotkeyHelper.VK_LSHIFT, true));
        Assert.True(detector.ProcessKeyEvent(VK_O, true));
    }

    [Fact]
    public void HotkeyHelper_FormatsCorrectly()
    {
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
    public void SettingsService_SaveAndLoad_RoundtripsSuccessfully()
    {
        var settings = new Models.AppSettings
        {
            HotkeyCodes = [0x11, 0x10, 0x4F],
            HotkeyDisplay = "Ctrl + Shift + O"
        };

        bool saved = SettingsService.Save(settings);
        Assert.True(saved);

        var loaded = SettingsService.Load();
        Assert.NotNull(loaded);
        Assert.Equal("Ctrl + Shift + O", loaded.HotkeyDisplay);
        Assert.Equal([0x11, 0x10, 0x4F], loaded.HotkeyCodes);
    }
}

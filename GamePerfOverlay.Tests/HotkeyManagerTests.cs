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
}

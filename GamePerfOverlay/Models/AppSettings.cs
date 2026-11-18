namespace GamePerfOverlay.Models;

public class AppSettings
{
    // Default: Ctrl (0x11) + D (0x44) + M (0x4D)
    public int[] HotkeyCodes { get; set; } = [0x11, 0x44, 0x4D];
    public string HotkeyDisplay { get; set; } = "Ctrl + D + M";
    public double OverlayLeft { get; set; } = 6;
    public double OverlayTop { get; set; } = 6;
}

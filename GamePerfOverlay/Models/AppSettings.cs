namespace GamePerfOverlay.Models;

public class AppSettings
{
    // Default: Alt (0x12) + Q (0x51)
    public int[] HotkeyCodes { get; set; } = [0x12, 0x51];
    public string HotkeyDisplay { get; set; } = "Alt + Q";
    public double OverlayLeft { get; set; } = 6;
    public double OverlayTop { get; set; } = 6;
}

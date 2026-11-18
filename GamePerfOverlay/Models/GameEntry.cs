using System;

namespace GamePerfOverlay.Models;

public class GameEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public GamePlatform Platform { get; set; } = GamePlatform.Standalone;
    public string LaunchUri { get; set; } = string.Empty;
    public string? LaunchArguments { get; set; }
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? CoverImageUrl { get; set; }
    public DateTime? LastPlayed { get; set; }
    public int PlayCount { get; set; }
}

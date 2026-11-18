using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.Services.Scanners;

public class EpicScanner : IGameScanner
{
    private readonly string? _overrideManifestsDir;

    public GamePlatform Platform => GamePlatform.Epic;

    public EpicScanner(string? overrideManifestsDir = null)
    {
        _overrideManifestsDir = overrideManifestsDir;
    }

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken ct = default)
    {
        return Task.Run(() => (IReadOnlyList<GameEntry>)ScanInternal(), ct);
    }

    private List<GameEntry> ScanInternal()
    {
        var games = new List<GameEntry>();
        string manifestsDir = _overrideManifestsDir ?? ResolveManifestsDir();

        if (string.IsNullOrEmpty(manifestsDir) || !Directory.Exists(manifestsDir))
        {
            return games;
        }

        try
        {
            var manifestFiles = Directory.GetFiles(manifestsDir, "*.item");
            foreach (var file in manifestFiles)
            {
                var entry = ParseItemFile(file);
                if (entry != null)
                {
                    games.Add(entry);
                }
            }
        }
        catch
        {
        }

        return games;
    }

    private static string ResolveManifestsDir()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
    }

    private static GameEntry? ParseItemFile(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("AppName", out var appNameProp) ||
                !root.TryGetProperty("DisplayName", out var displayNameProp))
            {
                return null;
            }

            string appName = appNameProp.GetString() ?? string.Empty;
            string displayName = displayNameProp.GetString() ?? string.Empty;

            if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(displayName))
            {
                return null;
            }

            // Skip Unreal Engine editors and developer tools
            if (appName.StartsWith("UE_", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? installLocation = root.TryGetProperty("InstallLocation", out var installProp)
                ? installProp.GetString()
                : null;

            string? launchExecutable = root.TryGetProperty("LaunchExecutable", out var exeProp)
                ? exeProp.GetString()
                : null;

            string? fullExePath = null;
            if (!string.IsNullOrEmpty(installLocation) && !string.IsNullOrEmpty(launchExecutable))
            {
                fullExePath = Path.Combine(installLocation, launchExecutable);
            }

            return new GameEntry
            {
                Id = $"epic_{appName}",
                Title = displayName,
                Platform = GamePlatform.Epic,
                LaunchUri = $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true",
                InstallPath = installLocation,
                ExecutablePath = fullExePath,
                PlayCount = 0
            };
        }
        catch
        {
            return null;
        }
    }
}

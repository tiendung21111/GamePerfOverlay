using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePerfOverlay.Models;
using Microsoft.Win32;

namespace GamePerfOverlay.Services.Scanners;

public class SteamScanner : IGameScanner
{
    private static readonly HashSet<string> ExcludedAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "228980",  // Steamworks Common Redistributables
        "1070560", // Steam Linux Runtime
        "1391110", // Steam Linux Runtime - Soldier
        "1628350", // Steam Linux Runtime - Sniper
        "250820",  // SteamVR
        "1493710", // Proton
        "896660",  // Proton
    };

    private readonly string? _customSteamPath;

    public GamePlatform Platform => GamePlatform.Steam;

    public SteamScanner(string? customSteamPath = null, string? nonExistentPath = null)
    {
        _customSteamPath = nonExistentPath ?? customSteamPath;
    }

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken ct = default)
    {
        return Task.Run(() => (IReadOnlyList<GameEntry>)ScanInternal(), ct);
    }

    private List<GameEntry> ScanInternal()
    {
        var games = new List<GameEntry>();
        string? steamPath = _customSteamPath ?? ResolveSteamPath();

        if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
        {
            return games;
        }

        var libraryFolders = ResolveLibraryFolders(steamPath);

        foreach (var libPath in libraryFolders)
        {
            try
            {
                string steamAppsDir = Path.Combine(libPath, "steamapps");
                if (!Directory.Exists(steamAppsDir))
                {
                    steamAppsDir = libPath; // if path was already steamapps
                }

                if (!Directory.Exists(steamAppsDir)) continue;

                var manifestFiles = Directory.GetFiles(steamAppsDir, "appmanifest_*.acf");
                foreach (var manifestFile in manifestFiles)
                {
                    var game = ParseManifest(manifestFile, libPath);
                    if (game != null && !ExcludedAppIds.Contains(game.Id.Replace("steam_", "")))
                    {
                        games.Add(game);
                    }
                }
            }
            catch
            {
                // Ignore inaccessible folders
            }
        }

        return games;
    }

    private static string? ResolveSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string steamPath && Directory.Exists(steamPath))
            {
                return steamPath;
            }

            using var key64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            if (key64?.GetValue("InstallPath") is string installPath && Directory.Exists(installPath))
            {
                return installPath;
            }

            string defaultPath = @"C:\Program Files (x86)\Steam";
            if (Directory.Exists(defaultPath))
            {
                return defaultPath;
            }
        }
        catch
        {
        }

        return null;
    }

    private static HashSet<string> ResolveLibraryFolders(string steamPath)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };

        string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
        {
            vdfPath = Path.Combine(steamPath, "config", "libraryfolders.vdf");
        }

        if (!File.Exists(vdfPath))
        {
            return folders;
        }

        try
        {
            string content = File.ReadAllText(vdfPath);
            // Matches "path"		"D:\\SteamLibrary"
            var matches = Regex.Matches(content, @"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    string rawPath = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(rawPath))
                    {
                        folders.Add(rawPath);
                    }
                }
            }
        }
        catch
        {
        }

        return folders;
    }

    private static GameEntry? ParseManifest(string manifestPath, string libraryRoot)
    {
        try
        {
            string content = File.ReadAllText(manifestPath);

            string? appId = ExtractValue(content, "appid");
            string? name = ExtractValue(content, "name");
            string? installDir = ExtractValue(content, "installdir");

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(name))
            {
                return null;
            }

            string fullInstallPath = Path.Combine(libraryRoot, "steamapps", "common", installDir ?? name);

            return new GameEntry
            {
                Id = $"steam_{appId}",
                Title = name,
                Platform = GamePlatform.Steam,
                LaunchUri = $"steam://run/{appId}",
                InstallPath = fullInstallPath,
                CoverImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                PlayCount = 0
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractValue(string content, string key)
    {
        var match = Regex.Match(content, $@"""{key}""\s+""([^""]+)""", RegexOptions.IgnoreCase);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
    }
}

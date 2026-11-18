using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.Services.Scanners;

public class RiotScanner : IGameScanner
{
    private readonly string? _overrideRiotClientPath;
    private readonly string? _overrideProgramData;
    private readonly string[]? _overrideGameSearchPaths;

    public GamePlatform Platform => GamePlatform.Riot;

    public RiotScanner(
        string? overrideRiotClientPath = null,
        string? overrideProgramData = null,
        string[]? overrideGameSearchPaths = null)
    {
        _overrideRiotClientPath = overrideRiotClientPath;
        _overrideProgramData = overrideProgramData;
        _overrideGameSearchPaths = overrideGameSearchPaths;
    }

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken ct = default)
    {
        return Task.Run(() => (IReadOnlyList<GameEntry>)ScanInternal(), ct);
    }

    private List<GameEntry> ScanInternal()
    {
        var games = new List<GameEntry>();
        string? riotClientExe = _overrideRiotClientPath ?? ResolveRiotClientPath();

        if (string.IsNullOrEmpty(riotClientExe) || !File.Exists(riotClientExe))
        {
            return games;
        }

        var searchDirs = _overrideGameSearchPaths ?? GetDefaultRiotGameSearchDirs();

        // 1. Check League of Legends
        var lolEntry = FindLeagueOfLegends(riotClientExe, searchDirs);
        if (lolEntry != null)
        {
            games.Add(lolEntry);
        }

        // 2. Check VALORANT
        var valEntry = FindValorant(riotClientExe, searchDirs);
        if (valEntry != null)
        {
            games.Add(valEntry);
        }

        return games;
    }

    private string? ResolveRiotClientPath()
    {
        try
        {
            string programData = _overrideProgramData ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string installsJson = Path.Combine(programData, "Riot Games", "RiotClientInstalls.json");

            if (File.Exists(installsJson))
            {
                string json = File.ReadAllText(installsJson);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("rc_default", out var rcDefault) && !string.IsNullOrEmpty(rcDefault.GetString()))
                {
                    string path = rcDefault.GetString()!;
                    if (File.Exists(path)) return path;
                }

                if (root.TryGetProperty("rc_live", out var rcLive) && !string.IsNullOrEmpty(rcLive.GetString()))
                {
                    string path = rcLive.GetString()!;
                    if (File.Exists(path)) return path;
                }
            }

            string defaultPath = @"C:\Riot Games\Riot Client\RiotClientServices.exe";
            if (File.Exists(defaultPath)) return defaultPath;
        }
        catch
        {
        }

        return null;
    }

    private static string[] GetDefaultRiotGameSearchDirs()
    {
        var dirs = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    dirs.Add(Path.Combine(drive.RootDirectory.FullName, "Riot Games"));
                }
            }
        }
        catch
        {
            dirs.Add(@"C:\Riot Games");
        }
        return dirs.ToArray();
    }

    private static GameEntry? FindLeagueOfLegends(string riotClientExe, IEnumerable<string> searchDirs)
    {
        foreach (var baseDir in searchDirs)
        {
            try
            {
                string lolDir = Path.Combine(baseDir, "League of Legends");
                string lolExe = Path.Combine(lolDir, "LeagueClient.exe");
                if (File.Exists(lolExe))
                {
                    return new GameEntry
                    {
                        Id = "riot_league_of_legends",
                        Title = "League of Legends",
                        Platform = GamePlatform.Riot,
                        LaunchUri = riotClientExe,
                        LaunchArguments = "--launch-product=league_of_legends --launch-patchline=live",
                        InstallPath = lolDir,
                        ExecutablePath = lolExe,
                        CoverImageUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=600&q=80",
                        PlayCount = 0
                    };
                }
            }
            catch
            {
            }
        }
        return null;
    }

    private static GameEntry? FindValorant(string riotClientExe, IEnumerable<string> searchDirs)
    {
        foreach (var baseDir in searchDirs)
        {
            try
            {
                string valLiveDir = Path.Combine(baseDir, "VALORANT", "live");
                string valExe = Path.Combine(valLiveDir, "VALORANT.exe");
                if (File.Exists(valExe))
                {
                    return new GameEntry
                    {
                        Id = "riot_valorant",
                        Title = "VALORANT",
                        Platform = GamePlatform.Riot,
                        LaunchUri = riotClientExe,
                        LaunchArguments = "--launch-product=valorant --launch-patchline=live",
                        InstallPath = Path.Combine(baseDir, "VALORANT"),
                        ExecutablePath = valExe,
                        CoverImageUrl = "https://images.unsplash.com/photo-1538481199705-c710c4e965fc?auto=format&fit=crop&w=600&q=80",
                        PlayCount = 0
                    };
                }
            }
            catch
            {
            }
        }
        return null;
    }
}

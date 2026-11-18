using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services.Scanners;

namespace GamePerfOverlay.Services;

public class GameLibraryManager
{
    private readonly IGameLibraryStorage _storage;
    private readonly IReadOnlyList<IGameScanner> _scanners;

    public ObservableCollection<GameEntry> Games { get; } = [];

    public GameLibraryManager(
        IGameLibraryStorage? storage = null,
        IReadOnlyList<IGameScanner>? scanners = null)
    {
        _storage = storage ?? new GameLibraryStorage();
        _scanners = scanners ??
        [
            new SteamScanner(),
            new RiotScanner(),
            new EpicScanner()
        ];
        Load();
    }

    public void Load()
    {
        Games.Clear();
        var loaded = _storage.Load();
        foreach (var game in loaded)
        {
            Games.Add(game);
        }
    }

    public void Save()
    {
        _storage.Save(Games);
    }

    public async Task ScanAllAsync(IProgress<string>? progress = null)
    {
        progress?.Report("Đang quét thư viện game...");

        var tasks = _scanners.Select(s => s.ScanAsync()).ToArray();
        var results = await Task.WhenAll(tasks);

        var existingMap = Games.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var scannerResults in results)
        {
            foreach (var scanned in scannerResults)
            {
                if (existingMap.TryGetValue(scanned.Id, out var existing))
                {
                    existing.Title = scanned.Title;
                    existing.LaunchUri = scanned.LaunchUri;
                    existing.LaunchArguments = scanned.LaunchArguments;
                    existing.InstallPath = scanned.InstallPath;
                    existing.ExecutablePath = scanned.ExecutablePath;
                    if (!string.IsNullOrEmpty(scanned.CoverImageUrl))
                    {
                        existing.CoverImageUrl = scanned.CoverImageUrl;
                    }
                }
                else
                {
                    Games.Add(scanned);
                    existingMap[scanned.Id] = scanned;
                }
            }
        }

        Save();
        progress?.Report($"Hoàn tất quét. Đã tìm thấy {Games.Count} tựa game.");
    }

    public GameEntry AddCustomGame(string exePath, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(exePath);
        }

        string id = "custom_" + Guid.NewGuid().ToString("N")[..8];
        var entry = new GameEntry
        {
            Id = id,
            Title = title,
            Platform = GamePlatform.Standalone,
            LaunchUri = exePath,
            ExecutablePath = exePath,
            InstallPath = Path.GetDirectoryName(exePath),
            PlayCount = 0
        };

        Games.Add(entry);
        Save();
        return entry;
    }

    public bool RemoveGame(string id)
    {
        var existing = Games.FirstOrDefault(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            Games.Remove(existing);
            Save();
            return true;
        }
        return false;
    }

    public List<GameEntry> GetFilteredGames(string? query, GamePlatform? platform)
    {
        IEnumerable<GameEntry> result = Games;

        if (platform.HasValue)
        {
            result = result.Where(g => g.Platform == platform.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            string q = query.Trim();
            result = result.Where(g => g.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return result.ToList();
    }
}

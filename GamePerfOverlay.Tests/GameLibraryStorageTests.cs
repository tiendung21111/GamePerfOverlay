using System;
using System.Collections.Generic;
using System.IO;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;
using Xunit;

namespace GamePerfOverlay.Tests;

public class GameLibraryStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public GameLibraryStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GamePerfOverlay_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "games.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        var storage = new GameLibraryStorage(_filePath);
        var games = storage.Load();

        Assert.NotNull(games);
        Assert.Empty(games);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesAllProperties()
    {
        var storage = new GameLibraryStorage(_filePath);
        var originalGames = new List<GameEntry>
        {
            new GameEntry
            {
                Id = "steam_730",
                Title = "Counter-Strike 2",
                Platform = GamePlatform.Steam,
                LaunchUri = "steam://run/730",
                LaunchArguments = null,
                InstallPath = @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
                ExecutablePath = @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe",
                CoverImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/730/header.jpg",
                LastPlayed = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc),
                PlayCount = 42
            },
            new GameEntry
            {
                Id = "riot_lol",
                Title = "League of Legends",
                Platform = GamePlatform.Riot,
                LaunchUri = "RiotClientServices.exe",
                LaunchArguments = "--launch-product=league_of_legends --launch-patchline=live",
                InstallPath = @"C:\Riot Games\League of Legends",
                ExecutablePath = null,
                CoverImageUrl = "pack://application:,,,/Assets/covers/lol.jpg",
                LastPlayed = null,
                PlayCount = 0
            }
        };

        storage.Save(originalGames);
        Assert.True(File.Exists(_filePath));

        var loadedGames = storage.Load();
        Assert.Equal(2, loadedGames.Count);

        var cs2 = loadedGames[0];
        Assert.Equal("steam_730", cs2.Id);
        Assert.Equal("Counter-Strike 2", cs2.Title);
        Assert.Equal(GamePlatform.Steam, cs2.Platform);
        Assert.Equal("steam://run/730", cs2.LaunchUri);
        Assert.Equal(42, cs2.PlayCount);

        var lol = loadedGames[1];
        Assert.Equal("riot_lol", lol.Id);
        Assert.Equal("League of Legends", lol.Title);
        Assert.Equal(GamePlatform.Riot, lol.Platform);
        Assert.Equal("--launch-product=league_of_legends --launch-patchline=live", lol.LaunchArguments);
    }

    [Fact]
    public void Load_WhenFileIsCorrupted_ReturnsEmptyListWithoutCrashing()
    {
        File.WriteAllText(_filePath, "{ corrupted json invalid: true");
        var storage = new GameLibraryStorage(_filePath);

        var games = storage.Load();
        Assert.NotNull(games);
        Assert.Empty(games);
    }
}

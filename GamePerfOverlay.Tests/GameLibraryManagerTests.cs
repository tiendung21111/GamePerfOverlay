using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;
using GamePerfOverlay.Services.Scanners;
using Xunit;

namespace GamePerfOverlay.Tests;

public class GameLibraryManagerTests
{
    private class MockStorage : IGameLibraryStorage
    {
        public List<GameEntry> Items = [];
        public List<GameEntry> Load() => new(Items);
        public void Save(IEnumerable<GameEntry> games) => Items = new(games);
    }

    private class MockScanner : IGameScanner
    {
        public GamePlatform Platform { get; set; }
        public List<GameEntry> GamesToReturn { get; set; } = [];

        public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<GameEntry>>(GamesToReturn);
        }
    }

    [Fact]
    public async Task ScanAllAsync_MergesScannersAndPreservesExistingPlayStats()
    {
        var storage = new MockStorage();
        storage.Items.Add(new GameEntry
        {
            Id = "steam_730",
            Title = "Counter-Strike 2",
            Platform = GamePlatform.Steam,
            PlayCount = 10,
            LastPlayed = new DateTime(2026, 11, 10)
        });

        var steamScanner = new MockScanner
        {
            Platform = GamePlatform.Steam,
            GamesToReturn =
            [
                new GameEntry
                {
                    Id = "steam_730",
                    Title = "Counter-Strike 2 Updated",
                    Platform = GamePlatform.Steam,
                    LaunchUri = "steam://run/730"
                },
                new GameEntry
                {
                    Id = "steam_570",
                    Title = "Dota 2",
                    Platform = GamePlatform.Steam,
                    LaunchUri = "steam://run/570"
                }
            ]
        };

        var manager = new GameLibraryManager(storage, [steamScanner]);
        await manager.ScanAllAsync();

        Assert.Equal(2, manager.Games.Count);

        var cs2 = manager.Games.First(g => g.Id == "steam_730");
        Assert.Equal(10, cs2.PlayCount); // Preserved
        Assert.NotNull(cs2.LastPlayed);  // Preserved
        Assert.Equal("steam://run/730", cs2.LaunchUri); // Updated

        var dota = manager.Games.First(g => g.Id == "steam_570");
        Assert.Equal(0, dota.PlayCount);
    }

    [Fact]
    public void AddCustomGame_AddsStandaloneEntryAndSaves()
    {
        var storage = new MockStorage();
        var manager = new GameLibraryManager(storage, []);

        var entry = manager.AddCustomGame(@"C:\Games\RetroGame\game.exe", "Retro Game");

        Assert.NotNull(entry);
        Assert.Equal("Retro Game", entry.Title);
        Assert.Equal(GamePlatform.Standalone, entry.Platform);
        Assert.Equal(@"C:\Games\RetroGame\game.exe", entry.LaunchUri);
        Assert.Contains(entry, manager.Games);
        Assert.Contains(entry, storage.Items);
    }

    [Fact]
    public void AddCustomGame_DuplicatePath_ReturnsExistingWithoutDuplicating()
    {
        var storage = new MockStorage();
        var manager = new GameLibraryManager(storage, []);

        var first = manager.AddCustomGame(@"C:\Games\RetroGame\game.exe", "Retro Game 1");
        var second = manager.AddCustomGame(@"C:\Games\RetroGame\game.exe", "Retro Game 2");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(manager.Games);
    }

    [Fact]
    public async Task ScanAllAsync_HandlesDuplicateIdsGracefully()
    {
        var storage = new MockStorage();
        storage.Items.Add(new GameEntry { Id = "dup_id", Title = "Existing Game" });

        var scanner = new MockScanner
        {
            GamesToReturn =
            [
                new GameEntry { Id = "dup_id", Title = "Updated Name" },
                new GameEntry { Id = "dup_id", Title = "Duplicate From Scanner" }
            ]
        };

        var manager = new GameLibraryManager(storage, [scanner]);
        // Should not throw ArgumentException
        await manager.ScanAllAsync();

        Assert.Single(manager.Games);
        Assert.Equal("Duplicate From Scanner", manager.Games[0].Title);
    }

    [Fact]
    public void GetFilteredGames_FiltersBySearchAndPlatform()
    {
        var storage = new MockStorage
        {
            Items =
            [
                new GameEntry { Id = "1", Title = "Counter-Strike 2", Platform = GamePlatform.Steam },
                new GameEntry { Id = "2", Title = "League of Legends", Platform = GamePlatform.Riot },
                new GameEntry { Id = "3", Title = "Cyberpunk 2077", Platform = GamePlatform.Steam }
            ]
        };

        var manager = new GameLibraryManager(storage, []);
        manager.Load();

        var steamGames = manager.GetFilteredGames(null, GamePlatform.Steam);
        Assert.Equal(2, steamGames.Count);

        var strikeSearch = manager.GetFilteredGames("strike", null);
        Assert.Single(strikeSearch);
        Assert.Equal("Counter-Strike 2", strikeSearch[0].Title);
    }
}

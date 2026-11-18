using System;
using System.Collections.Generic;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;
using Xunit;

namespace GamePerfOverlay.Tests;

public class GameLauncherServiceTests
{
    private class MockProcessStarter : IProcessStarter
    {
        public string? LastFileName;
        public string? LastArguments;
        public string? LastWorkingDirectory;
        public bool LastUseShellExecute;
        public bool ReturnValue = true;

        public bool Start(string fileName, string? arguments, string? workingDirectory, bool useShellExecute)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            LastWorkingDirectory = workingDirectory;
            LastUseShellExecute = useShellExecute;
            return ReturnValue;
        }
    }

    private class MockStorage : IGameLibraryStorage
    {
        public List<GameEntry> Items = [];
        public List<GameEntry> Load() => new(Items);
        public void Save(IEnumerable<GameEntry> games) => Items = new(games);
    }

    [Fact]
    public void Launch_UpdatesPlayCountAndLastPlayed_AndInvokesStarter()
    {
        var starter = new MockProcessStarter();
        var storage = new MockStorage();
        var manager = new GameLibraryManager(storage, []);

        var game = new GameEntry
        {
            Id = "steam_730",
            Title = "Counter-Strike 2",
            Platform = GamePlatform.Steam,
            LaunchUri = "steam://run/730",
            PlayCount = 5,
            LastPlayed = null
        };
        manager.Games.Add(game);
        storage.Save(manager.Games);

        var launcher = new GameLauncherService(manager, starter);

        bool success = launcher.Launch(game);

        Assert.True(success);
        Assert.Equal("steam://run/730", starter.LastFileName);
        Assert.True(starter.LastUseShellExecute);
        Assert.Equal(6, game.PlayCount);
        Assert.NotNull(game.LastPlayed);
        Assert.True(game.LastPlayed.Value > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Launch_WhenStarterReturnsFalse_DoesNotUpdatePlayCount()
    {
        var starter = new MockProcessStarter { ReturnValue = false };
        var storage = new MockStorage();
        var manager = new GameLibraryManager(storage, []);

        var game = new GameEntry
        {
            Id = "game_failed",
            Title = "Failed Game",
            LaunchUri = "invalid://uri",
            PlayCount = 2
        };
        manager.Games.Add(game);

        var launcher = new GameLauncherService(manager, starter);
        bool success = launcher.Launch(game);

        Assert.False(success);
        Assert.Equal(2, game.PlayCount);
        Assert.Null(game.LastPlayed);
    }
}

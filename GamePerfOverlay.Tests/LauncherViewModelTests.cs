using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;
using GamePerfOverlay.ViewModels;
using Xunit;

namespace GamePerfOverlay.Tests;

public class LauncherViewModelTests
{
    private class MockStorage : IGameLibraryStorage
    {
        public List<GameEntry> Items = [];
        public List<GameEntry> Load() => new(Items);
        public void Save(IEnumerable<GameEntry> games) => Items = new(games);
    }

    [Fact]
    public void Filtering_BySearchAndPlatform_UpdatesDisplayedGames()
    {
        var storage = new MockStorage
        {
            Items =
            [
                new GameEntry { Id = "1", Title = "Counter-Strike 2", Platform = GamePlatform.Steam },
                new GameEntry { Id = "2", Title = "League of Legends", Platform = GamePlatform.Riot },
                new GameEntry { Id = "3", Title = "Dota 2", Platform = GamePlatform.Steam }
            ]
        };

        var manager = new GameLibraryManager(storage, []);
        var vm = new LauncherViewModel(manager, new GameLauncherService(manager));

        Assert.Equal(3, vm.DisplayedGames.Count);

        // Filter by platform Steam
        vm.SelectedPlatform = GamePlatform.Steam;
        Assert.Equal(2, vm.DisplayedGames.Count);

        // Filter by search query "Strike"
        vm.SearchQuery = "Strike";
        Assert.Single(vm.DisplayedGames);
        Assert.Equal("Counter-Strike 2", vm.DisplayedGames[0].Title);

        // Clear filter
        vm.SelectedPlatform = null;
        vm.SearchQuery = string.Empty;
        Assert.Equal(3, vm.DisplayedGames.Count);
    }

    [Fact]
    public void AddSampleGames_PopulatesLibraryWhenEmpty()
    {
        var storage = new MockStorage();
        var manager = new GameLibraryManager(storage, []);
        var vm = new LauncherViewModel(manager, new GameLauncherService(manager));

        Assert.Empty(vm.DisplayedGames);

        vm.AddSampleGames();

        Assert.NotEmpty(vm.DisplayedGames);
        Assert.True(vm.DisplayedGames.Count >= 4);
        Assert.Contains(vm.DisplayedGames, g => g.Title == "League of Legends");
        Assert.Contains(vm.DisplayedGames, g => g.Title == "Counter-Strike 2");
    }
}

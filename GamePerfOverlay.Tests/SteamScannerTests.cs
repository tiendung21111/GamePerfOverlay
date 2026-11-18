using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services.Scanners;
using Xunit;

namespace GamePerfOverlay.Tests;

public class SteamScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _steamPath;

    public SteamScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SteamScanner_Tests_" + Guid.NewGuid().ToString("N"));
        _steamPath = Path.Combine(_tempDir, "Steam");
        Directory.CreateDirectory(_steamPath);
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
    public async Task ScanAsync_WhenSteamDirectoryDoesNotExist_ReturnsEmptyList()
    {
        var scanner = new SteamScanner(nonExistentPath: @"Z:\NonExistentSteamFolder");
        var games = await scanner.ScanAsync();

        Assert.NotNull(games);
        Assert.Empty(games);
    }

    [Fact]
    public async Task ScanAsync_WithValidLibraryFoldersAndManifests_DiscoversGames()
    {
        // 1. Create primary steamapps
        string primarySteamApps = Path.Combine(_steamPath, "steamapps");
        Directory.CreateDirectory(primarySteamApps);

        // 2. Create secondary library folder
        string secondaryLib = Path.Combine(_tempDir, "SteamLibraryD");
        string secondarySteamApps = Path.Combine(secondaryLib, "steamapps");
        Directory.CreateDirectory(secondarySteamApps);

        // 3. Write libraryfolders.vdf
        string vdfPath = Path.Combine(primarySteamApps, "libraryfolders.vdf");
        string vdfContent = $@"
""libraryfolders""
{{
    ""0""
    {{
        ""path""    ""{_steamPath.Replace("\\", "\\\\")}""
        ""apps""
        {{
            ""730""     ""12345678""
            ""228980""  ""987654""
        }}
    }}
    ""1""
    {{
        ""path""    ""{secondaryLib.Replace("\\", "\\\\")}""
        ""apps""
        {{
            ""570""     ""55555555""
        }}
    }}
}}";
        File.WriteAllText(vdfPath, vdfContent);

        // 4. Write appmanifest_730.acf (CS2) in primary
        string acf730 = Path.Combine(primarySteamApps, "appmanifest_730.acf");
        File.WriteAllText(acf730, @"
""AppState""
{
    ""appid""       ""730""
    ""name""        ""Counter-Strike 2""
    ""installdir""  ""Counter-Strike Global Offensive""
    ""StateFlags""  ""4""
}");

        // Write redistributable 228980 (should be skipped)
        string acf228980 = Path.Combine(primarySteamApps, "appmanifest_228980.acf");
        File.WriteAllText(acf228980, @"
""AppState""
{
    ""appid""       ""228980""
    ""name""        ""Steamworks Common Redistributables""
    ""installdir""  ""Steamworks Shared""
}");

        // 5. Write appmanifest_570.acf (Dota 2) in secondary library
        string acf570 = Path.Combine(secondarySteamApps, "appmanifest_570.acf");
        File.WriteAllText(acf570, @"
""AppState""
{
    ""appid""       ""570""
    ""name""        ""Dota 2""
    ""installdir""  ""dota 2 beta""
    ""StateFlags""  ""4""
}");

        // Act
        var scanner = new SteamScanner(customSteamPath: _steamPath);
        var games = await scanner.ScanAsync();

        // Assert
        Assert.NotNull(games);
        Assert.Equal(2, games.Count);

        var cs2 = games.FirstOrDefault(g => g.Id == "steam_730");
        Assert.NotNull(cs2);
        Assert.Equal("Counter-Strike 2", cs2.Title);
        Assert.Equal(GamePlatform.Steam, cs2.Platform);
        Assert.Equal("steam://run/730", cs2.LaunchUri);
        Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/730/header.jpg", cs2.CoverImageUrl);
        Assert.Equal(Path.Combine(primarySteamApps, "common", "Counter-Strike Global Offensive"), cs2.InstallPath);

        var dota2 = games.FirstOrDefault(g => g.Id == "steam_570");
        Assert.NotNull(dota2);
        Assert.Equal("Dota 2", dota2.Title);
        Assert.Equal("steam://run/570", dota2.LaunchUri);
        Assert.Equal(Path.Combine(secondarySteamApps, "common", "dota 2 beta"), dota2.InstallPath);
    }
}

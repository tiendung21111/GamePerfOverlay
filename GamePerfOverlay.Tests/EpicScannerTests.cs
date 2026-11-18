using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services.Scanners;
using Xunit;

namespace GamePerfOverlay.Tests;

public class EpicScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _manifestsDir;

    public EpicScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "EpicScanner_Tests_" + Guid.NewGuid().ToString("N"));
        _manifestsDir = Path.Combine(_tempDir, "Manifests");
        Directory.CreateDirectory(_manifestsDir);
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
    public async Task ScanAsync_WhenManifestsDirDoesNotExist_ReturnsEmptyList()
    {
        var scanner = new EpicScanner(overrideManifestsDir: @"Z:\NonExistent\Manifests");
        var games = await scanner.ScanAsync();

        Assert.NotNull(games);
        Assert.Empty(games);
    }

    [Fact]
    public async Task ScanAsync_WithValidManifests_ParsesGamesAndSkipsUnrealEngine()
    {
        // 1. Valid Game Manifest (GTA V)
        string gtaItem = Path.Combine(_manifestsDir, "GTAV.item");
        File.WriteAllText(gtaItem, @"{
            ""AppName"": ""9d2d0eb64d5c44529cece33fe2a49132"",
            ""DisplayName"": ""Grand Theft Auto V"",
            ""InstallLocation"": ""D:\\EpicGames\\GTAV"",
            ""LaunchExecutable"": ""PlayGTAV.exe""
        }");

        // 2. Unreal Engine Editor (Should be skipped)
        string ueItem = Path.Combine(_manifestsDir, "UE_5.3.item");
        File.WriteAllText(ueItem, @"{
            ""AppName"": ""UE_5.3"",
            ""DisplayName"": ""Unreal Engine 5.3"",
            ""InstallLocation"": ""C:\\Program Files\\Epic Games\\UE_5.3"",
            ""LaunchExecutable"": ""Engine/Binaries/Win64/UnrealEditor.exe""
        }");

        // 3. Another Valid Game (Hades)
        string hadesItem = Path.Combine(_manifestsDir, "Hades.item");
        File.WriteAllText(hadesItem, @"{
            ""AppName"": ""Minerva"",
            ""DisplayName"": ""Hades"",
            ""InstallLocation"": ""C:\\Games\\Hades"",
            ""LaunchExecutable"": ""Hades.exe""
        }");

        var scanner = new EpicScanner(overrideManifestsDir: _manifestsDir);
        var games = await scanner.ScanAsync();

        Assert.NotNull(games);
        Assert.Equal(2, games.Count);

        var gta = games.FirstOrDefault(g => g.Title == "Grand Theft Auto V");
        Assert.NotNull(gta);
        Assert.Equal("epic_9d2d0eb64d5c44529cece33fe2a49132", gta.Id);
        Assert.Equal(GamePlatform.Epic, gta.Platform);
        Assert.Equal("com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a49132?action=launch&silent=true", gta.LaunchUri);
        Assert.Equal(@"D:\EpicGames\GTAV\PlayGTAV.exe", gta.ExecutablePath);

        var hades = games.FirstOrDefault(g => g.Title == "Hades");
        Assert.NotNull(hades);
        Assert.Equal("epic_Minerva", hades.Id);
        Assert.Equal("com.epicgames.launcher://apps/Minerva?action=launch&silent=true", hades.LaunchUri);
    }
}

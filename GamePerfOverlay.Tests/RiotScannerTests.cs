using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services.Scanners;
using Xunit;

namespace GamePerfOverlay.Tests;

public class RiotScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _riotClientPath;
    private readonly string _programDataDir;

    public RiotScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RiotScanner_Tests_" + Guid.NewGuid().ToString("N"));
        _programDataDir = Path.Combine(_tempDir, "ProgramData", "Riot Games");
        Directory.CreateDirectory(_programDataDir);

        _riotClientPath = Path.Combine(_tempDir, "Riot Client", "RiotClientServices.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(_riotClientPath)!);
        File.WriteAllText(_riotClientPath, "fake exe");
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
    public async Task ScanAsync_WhenRiotClientNotInstalled_ReturnsEmpty()
    {
        var scanner = new RiotScanner(overrideRiotClientPath: @"Z:\NonExistent\RiotClient.exe", overrideProgramData: @"Z:\NonExistent");
        var games = await scanner.ScanAsync();

        Assert.NotNull(games);
        Assert.Empty(games);
    }

    [Fact]
    public async Task ScanAsync_WhenLeagueAndValorantDetected_ReturnsGameEntries()
    {
        // Setup RiotClientInstalls.json
        string jsonPath = Path.Combine(_programDataDir, "RiotClientInstalls.json");
        string json = $@"{{ ""rc_default"": ""{_riotClientPath.Replace("\\", "/")}"" }}";
        File.WriteAllText(jsonPath, json);

        // Setup mock LoL install
        string lolDir = Path.Combine(_tempDir, "Riot Games", "League of Legends");
        Directory.CreateDirectory(lolDir);
        File.WriteAllText(Path.Combine(lolDir, "LeagueClient.exe"), "fake lol");

        // Setup mock Valorant install
        string valDir = Path.Combine(_tempDir, "Riot Games", "VALORANT", "live");
        Directory.CreateDirectory(valDir);
        File.WriteAllText(Path.Combine(valDir, "VALORANT.exe"), "fake val");

        var scanner = new RiotScanner(
            overrideRiotClientPath: _riotClientPath,
            overrideProgramData: _programDataDir,
            overrideGameSearchPaths: [Path.Combine(_tempDir, "Riot Games")]
        );

        var games = await scanner.ScanAsync();

        Assert.NotNull(games);
        Assert.Equal(2, games.Count);

        var lol = games.FirstOrDefault(g => g.Id == "riot_league_of_legends");
        Assert.NotNull(lol);
        Assert.Equal("League of Legends", lol.Title);
        Assert.Equal(GamePlatform.Riot, lol.Platform);
        Assert.Equal(_riotClientPath, lol.LaunchUri);
        Assert.Contains("--launch-product=league_of_legends", lol.LaunchArguments);

        var val = games.FirstOrDefault(g => g.Id == "riot_valorant");
        Assert.NotNull(val);
        Assert.Equal("VALORANT", val.Title);
        Assert.Equal(GamePlatform.Riot, val.Platform);
        Assert.Contains("--launch-product=valorant", val.LaunchArguments);
    }
}

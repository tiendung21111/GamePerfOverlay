using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.Services;

public interface IGameLibraryStorage
{
    List<GameEntry> Load();
    void Save(IEnumerable<GameEntry> games);
}

public class GameLibraryStorage : IGameLibraryStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public GameLibraryStorage(string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "GamePerfOverlay");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "games.json");
        }
        else
        {
            _filePath = filePath;
        }
    }

    public List<GameEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var result = JsonSerializer.Deserialize<List<GameEntry>>(json, JsonOptions);
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<GameEntry> games)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(games, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
        }
    }
}

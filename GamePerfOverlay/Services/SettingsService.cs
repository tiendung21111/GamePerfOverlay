using System;
using System.IO;
using System.Text.Json;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.Services;

public static class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GamePerfOverlay");

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings?.HotkeyCodes != null && settings.HotkeyCodes.Length > 0)
                {
                    return settings;
                }
            }
        }
        catch
        {
        }

        return new AppSettings();
    }

    public static bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.Services;

public interface IProcessStarter
{
    bool Start(string fileName, string? arguments, string? workingDirectory, bool useShellExecute);
}

public class DefaultProcessStarter : IProcessStarter
{
    public bool Start(string fileName, string? arguments, string? workingDirectory, bool useShellExecute)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = useShellExecute
            };
            var process = Process.Start(psi);
            return process != null;
        }
        catch
        {
            return false;
        }
    }
}

public class GameLauncherService
{
    private readonly GameLibraryManager _manager;
    private readonly IProcessStarter _starter;

    public GameLauncherService(GameLibraryManager manager, IProcessStarter? starter = null)
    {
        _manager = manager;
        _starter = starter ?? new DefaultProcessStarter();
    }

    public bool Launch(GameEntry game)
    {
        if (string.IsNullOrWhiteSpace(game.LaunchUri))
        {
            return false;
        }

        string fileName = game.LaunchUri;
        string? arguments = game.LaunchArguments;
        string? workingDir = null;
        bool useShellExecute = true;

        if (File.Exists(fileName))
        {
            workingDir = Path.GetDirectoryName(fileName);
        }
        else if (!string.IsNullOrEmpty(game.InstallPath) && Directory.Exists(game.InstallPath))
        {
            workingDir = game.InstallPath;
        }

        bool started = _starter.Start(fileName, arguments, workingDir, useShellExecute);
        if (started)
        {
            game.LastPlayed = DateTime.UtcNow;
            game.PlayCount++;
            _manager.Save();
        }

        return started;
    }
}

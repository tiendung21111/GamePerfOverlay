using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;

namespace GamePerfOverlay.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public class LauncherViewModel : INotifyPropertyChanged
{
    private readonly GameLibraryManager _manager;
    private readonly GameLauncherService _launcher;

    private string _searchQuery = string.Empty;
    private GamePlatform? _selectedPlatform = null;
    private bool _isScanning;
    private string _statusMessage = string.Empty;

    public ObservableCollection<GameEntry> DisplayedGames { get; } = [];

    public event Action<GameEntry>? GameLaunched;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetField(ref _searchQuery, value))
            {
                RefreshDisplayedGames();
            }
        }
    }

    public GamePlatform? SelectedPlatform
    {
        get => _selectedPlatform;
        set
        {
            if (SetField(ref _selectedPlatform, value))
            {
                RefreshDisplayedGames();
                OnPropertyChanged(nameof(IsAllSelected));
                OnPropertyChanged(nameof(IsSteamSelected));
                OnPropertyChanged(nameof(IsRiotSelected));
                OnPropertyChanged(nameof(IsEpicSelected));
                OnPropertyChanged(nameof(IsStandaloneSelected));
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetField(ref _isScanning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public int TotalGameCount => _manager.Games.Count;
    public int SteamGameCount => _manager.Games.Count(g => g.Platform == GamePlatform.Steam);
    public int RiotGameCount => _manager.Games.Count(g => g.Platform == GamePlatform.Riot);
    public int EpicGameCount => _manager.Games.Count(g => g.Platform == GamePlatform.Epic);
    public int StandaloneGameCount => _manager.Games.Count(g => g.Platform == GamePlatform.Standalone);

    public bool IsAllSelected => SelectedPlatform == null;
    public bool IsSteamSelected => SelectedPlatform == GamePlatform.Steam;
    public bool IsRiotSelected => SelectedPlatform == GamePlatform.Riot;
    public bool IsEpicSelected => SelectedPlatform == GamePlatform.Epic;
    public bool IsStandaloneSelected => SelectedPlatform == GamePlatform.Standalone;

    public ICommand ScanCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand SelectPlatformCommand { get; }
    public ICommand RemoveGameCommand { get; }

    public LauncherViewModel(GameLibraryManager manager, GameLauncherService? launcher = null)
    {
        _manager = manager;
        _launcher = launcher ?? new GameLauncherService(_manager);

        ScanCommand = new RelayCommand(async () => await RunScanAsync(), () => !IsScanning);
        PlayCommand = new RelayCommand(obj =>
        {
            if (obj is GameEntry game)
            {
                PlayGame(game);
            }
        });
        SelectPlatformCommand = new RelayCommand(obj =>
        {
            if (obj == null || obj.ToString() == "All")
            {
                SelectedPlatform = null;
            }
            else if (Enum.TryParse<GamePlatform>(obj.ToString(), out var p))
            {
                SelectedPlatform = p;
            }
        });
        RemoveGameCommand = new RelayCommand(obj =>
        {
            if (obj is GameEntry game)
            {
                _manager.RemoveGame(game.Id);
                RefreshCounts();
                RefreshDisplayedGames();
            }
        });

        RefreshDisplayedGames();
    }

    public void RefreshDisplayedGames()
    {
        DisplayedGames.Clear();
        var list = _manager.GetFilteredGames(_searchQuery, _selectedPlatform);
        foreach (var item in list)
        {
            DisplayedGames.Add(item);
        }
    }

    public void RefreshCounts()
    {
        OnPropertyChanged(nameof(TotalGameCount));
        OnPropertyChanged(nameof(SteamGameCount));
        OnPropertyChanged(nameof(RiotGameCount));
        OnPropertyChanged(nameof(EpicGameCount));
        OnPropertyChanged(nameof(StandaloneGameCount));
    }

    public async Task RunScanAsync()
    {
        IsScanning = true;
        StatusMessage = "Đang quét các nền tảng game...";

        try
        {
            var progress = new Progress<string>(msg => StatusMessage = msg);
            await _manager.ScanAllAsync(progress);
            RefreshCounts();
            RefreshDisplayedGames();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi quét: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void PlayGame(GameEntry game)
    {
        bool launched = _launcher.Launch(game);
        if (launched)
        {
            StatusMessage = $"Đang khởi chạy {game.Title}...";
            GameLaunched?.Invoke(game);
        }
        else
        {
            StatusMessage = $"Không thể khởi chạy {game.Title}. Vui lòng kiểm tra lại đường dẫn cài đặt.";
        }
    }

    public void AddCustomGame(string exePath, string? title = null)
    {
        _manager.AddCustomGame(exePath, title);
        RefreshCounts();
        RefreshDisplayedGames();
        StatusMessage = $"Đã thêm tựa game mới!";
    }

    public void AddSampleGames()
    {
        var samples = new List<GameEntry>
        {
            new GameEntry
            {
                Id = "steam_730",
                Title = "Counter-Strike 2",
                Platform = GamePlatform.Steam,
                LaunchUri = "steam://run/730",
                CoverImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/730/header.jpg",
                InstallPath = @"SteamLibrary\steamapps\common\Counter-Strike Global Offensive"
            },
            new GameEntry
            {
                Id = "riot_lol",
                Title = "League of Legends",
                Platform = GamePlatform.Riot,
                LaunchUri = "RiotClientServices.exe",
                LaunchArguments = "--launch-product=league_of_legends --launch-patchline=live",
                CoverImageUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=600&q=80",
                InstallPath = @"Riot Games\League of Legends"
            },
            new GameEntry
            {
                Id = "riot_valorant",
                Title = "VALORANT",
                Platform = GamePlatform.Riot,
                LaunchUri = "RiotClientServices.exe",
                LaunchArguments = "--launch-product=valorant --launch-patchline=live",
                CoverImageUrl = "https://images.unsplash.com/photo-1538481199705-c710c4e965fc?auto=format&fit=crop&w=600&q=80",
                InstallPath = @"Riot Games\VALORANT"
            },
            new GameEntry
            {
                Id = "steam_2358720",
                Title = "Black Myth: Wukong",
                Platform = GamePlatform.Steam,
                LaunchUri = "steam://run/2358720",
                CoverImageUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/2358720/header.jpg",
                InstallPath = @"SteamLibrary\steamapps\common\BlackMythWukong"
            },
            new GameEntry
            {
                Id = "epic_gtav",
                Title = "Grand Theft Auto V",
                Platform = GamePlatform.Epic,
                LaunchUri = "com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a49132?action=launch&silent=true",
                CoverImageUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?auto=format&fit=crop&w=600&q=80",
                InstallPath = @"EpicGames\GTAV"
            }
        };

        foreach (var sample in samples)
        {
            if (!_manager.Games.Any(g => g.Id == sample.Id))
            {
                _manager.Games.Add(sample);
            }
        }

        _manager.Save();
        RefreshCounts();
        RefreshDisplayedGames();
        StatusMessage = "Đã nạp 5 tựa game mẫu vào thư viện!";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

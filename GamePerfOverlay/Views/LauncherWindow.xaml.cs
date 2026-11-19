using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using GamePerfOverlay.ViewModels;
using Microsoft.Win32;

namespace GamePerfOverlay.Views;

public partial class LauncherWindow : Window
{
    public const string WindowTitle = "Game Hub & Launcher";

    public LauncherViewModel ViewModel { get; }

    public LauncherWindow(LauncherViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        ViewModel.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(Models.GameEntry game)
    {
        Dispatcher.Invoke(() =>
        {
            // Auto-minimize/hide to tray when game starts so UI does not consume resources
            Hide();
        });
    }

    private void AddCustomGame_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn file chạy game (.exe)",
            Filter = "Game Executable (*.exe)|*.exe|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog() == true)
        {
            string exePath = ofd.FileName;
            string defaultTitle = Path.GetFileNameWithoutExtension(exePath);
            ViewModel.AddCustomGame(exePath, defaultTitle);
        }
    }

    private void AddSampleGames_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddSampleGames();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Minimize to tray instead of closing app
        e.Cancel = true;
        Hide();
    }
}

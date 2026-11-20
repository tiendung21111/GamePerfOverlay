using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using GamePerfOverlay.Models;
using GamePerfOverlay.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using KeyInterop = System.Windows.Input.KeyInterop;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace GamePerfOverlay.Views;

public partial class HotkeySettingsWindow : Window
{
    private readonly Action<AppSettings> _onSave;
    private readonly HashSet<int> _heldKeys = new();
    private int[] _capturedCodes;

    public HotkeySettingsWindow(AppSettings currentSettings, Action<AppSettings> onSave)
    {
        InitializeComponent();
        _onSave = onSave;

        _capturedCodes = currentSettings.HotkeyCodes != null && currentSettings.HotkeyCodes.Length > 0
            ? currentSettings.HotkeyCodes
            : [0x12, 0x51];

        HotkeyDisplayText.Text = !string.IsNullOrWhiteSpace(currentSettings.HotkeyDisplay)
            ? currentSettings.HotkeyDisplay
            : HotkeyHelper.FormatHotkey(_capturedCodes);

        Loaded += (s, e) => KeyCaptureBorder.Focus();
    }

    private void KeyCaptureBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        KeyCaptureBorder.Focus();
        KeyCaptureBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10ff50"));
        StatusHintText.Text = "Đang lắng nghe bàn phím: Hãy nhấn tổ hợp phím bạn muốn...";
        _heldKeys.Clear();
    }

    private void KeyCaptureBorder_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        int rawVk = e.Key == Key.System ? KeyInterop.VirtualKeyFromKey(e.SystemKey) : KeyInterop.VirtualKeyFromKey(e.Key);
        if (rawVk == 0 || e.Key == Key.None) return;

        int normalized = HotkeyHelper.NormalizeVkCode(rawVk);

        // Include any active WPF modifiers
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) _heldKeys.Add(HotkeyHelper.VK_CONTROL);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) _heldKeys.Add(HotkeyHelper.VK_MENU);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) _heldKeys.Add(HotkeyHelper.VK_SHIFT);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) _heldKeys.Add(HotkeyHelper.VK_LWIN);

        _heldKeys.Add(normalized);

        _capturedCodes = _heldKeys.ToArray();
        string formatted = HotkeyHelper.FormatHotkey(_capturedCodes);
        HotkeyDisplayText.Text = formatted;
        StatusHintText.Text = $"Đã ghi nhận: {formatted} (Nhấn 'Lưu phím tắt' để áp dụng)";

        e.Handled = true;
    }

    private void ResetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _capturedCodes = [0x12, 0x51];
        _heldKeys.Clear();
        HotkeyDisplayText.Text = "Alt + Q";
        StatusHintText.Text = "Đã đặt về mặc định: Alt + Q (Nhấn 'Lưu phím tắt' để áp dụng)";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_capturedCodes == null || _capturedCodes.Length == 0)
        {
            MessageBox.Show(this, "Vui lòng nhấn ít nhất một phím để gán làm phím tắt.", "Game Performance Overlay", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string formatted = HotkeyHelper.FormatHotkey(_capturedCodes);

        var newSettings = new AppSettings
        {
            HotkeyCodes = _capturedCodes,
            HotkeyDisplay = formatted
        };

        _onSave(newSettings);
        Close();
    }
}

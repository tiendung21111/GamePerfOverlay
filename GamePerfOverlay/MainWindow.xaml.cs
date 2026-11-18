using System;
using System.Windows;
using System.Windows.Interop;
using GamePerfOverlay.Interop;
using GamePerfOverlay.ViewModels;

namespace GamePerfOverlay;

public partial class MainWindow : Window
{
    public OverlayViewModel ViewModel { get; }

    public MainWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    public MainWindow() : this(new OverlayViewModel())
    {
    }

    public void SetPosition(double left, double top)
    {
        Left = left;
        Top = top;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            extendedStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
    }
}

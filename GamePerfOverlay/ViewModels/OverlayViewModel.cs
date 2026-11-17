using System.ComponentModel;
using System.Runtime.CompilerServices;
using GamePerfOverlay.Models;

namespace GamePerfOverlay.ViewModels;

public class OverlayViewModel : INotifyPropertyChanged
{
    private string _fpsText = "-- FPS";
    private string _cpuText = "0%  --°C  0.00 GHz";
    private string _gpuText = "0%  --°C  0 MHz";
    private string _vramText = "0%  0.0 GB";
    private string _ramText = "0%  0.0 GB";

    public string FpsText
    {
        get => _fpsText;
        private set => SetField(ref _fpsText, value);
    }

    public string CpuText
    {
        get => _cpuText;
        private set => SetField(ref _cpuText, value);
    }

    public string GpuText
    {
        get => _gpuText;
        private set => SetField(ref _gpuText, value);
    }

    public string VramText
    {
        get => _vramText;
        private set => SetField(ref _vramText, value);
    }

    public string RamText
    {
        get => _ramText;
        private set => SetField(ref _ramText, value);
    }

    public void Update(HardwareMetrics metrics)
    {
        FpsText = metrics.FpsDisplay;
        CpuText = metrics.CpuDisplay;
        GpuText = metrics.GpuDisplay;
        VramText = metrics.VramDisplay;
        RamText = metrics.RamDisplay;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

using System.Globalization;

namespace GamePerfOverlay.Models;

public class HardwareMetrics
{
    public int? Fps { get; set; }
    public int CpuUsage { get; set; }
    public int? CpuTemp { get; set; }
    public float CpuClockGhz { get; set; }

    public int GpuUsage { get; set; }
    public int? GpuTemp { get; set; }
    public int GpuClockMhz { get; set; }

    public int VramUsagePercent { get; set; }
    public float VramUsedGb { get; set; }

    public int RamUsagePercent { get; set; }
    public float RamUsedGb { get; set; }

    public string FpsDisplay => Fps.HasValue ? $"{Fps.Value} FPS" : "-- FPS";

    public string CpuDisplay
    {
        get
        {
            var tempStr = CpuTemp.HasValue ? $"{CpuTemp.Value}°C" : "--°C";
            return string.Format(CultureInfo.InvariantCulture, "{0}%  {1}  {2:0.00} GHz", CpuUsage, tempStr, CpuClockGhz);
        }
    }

    public string GpuDisplay
    {
        get
        {
            var tempStr = GpuTemp.HasValue ? $"{GpuTemp.Value}°C" : "--°C";
            return string.Format(CultureInfo.InvariantCulture, "{0}%  {1}  {2} MHz", GpuUsage, tempStr, GpuClockMhz);
        }
    }

    public string VramDisplay =>
        string.Format(CultureInfo.InvariantCulture, "{0}%  {1:0.0} GB", VramUsagePercent, VramUsedGb);

    public string RamDisplay =>
        string.Format(CultureInfo.InvariantCulture, "{0}%  {1:0.0} GB", RamUsagePercent, RamUsedGb);
}

using System;
using System.Collections.Generic;
using System.Linq;
using GamePerfOverlay.Models;
using LibreHardwareMonitor.Hardware;

namespace GamePerfOverlay.Services;

public class SensorService : ISensorService
{
    private Computer? _computer;
    private readonly List<IHardware> _cpus = new();
    private readonly List<IHardware> _gpus = new();
    private readonly List<IHardware> _memories = new();
    private bool _initialized;
    private bool _disposed;

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };

            _computer.Open();

            foreach (var hardware in _computer.Hardware)
            {
                CategorizeHardware(hardware);
            }

            _initialized = true;
        }
        catch
        {
            _initialized = false;
        }
    }

    private void CategorizeHardware(IHardware hardware)
    {
        switch (hardware.HardwareType)
        {
            case HardwareType.Cpu:
                _cpus.Add(hardware);
                break;
            case HardwareType.GpuNvidia:
            case HardwareType.GpuAmd:
            case HardwareType.GpuIntel:
                _gpus.Add(hardware);
                break;
            case HardwareType.Memory:
                _memories.Add(hardware);
                break;
        }

        foreach (var sub in hardware.SubHardware)
        {
            CategorizeHardware(sub);
        }
    }

    public HardwareMetrics SnapshotMetrics(int? currentFps)
    {
        var metrics = new HardwareMetrics
        {
            Fps = currentFps
        };

        if (!_initialized)
        {
            return metrics;
        }

        try
        {
            // 1. CPU
            int? cpuTemp = null;
            float cpuClockMhz = 0;
            float cpuLoad = 0;

            foreach (var cpu in _cpus)
            {
                cpu.Update();

                foreach (var sensor in cpu.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load &&
                        (sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                         sensor.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase)))
                    {
                        cpuLoad = sensor.Value ?? cpuLoad;
                    }
                    else if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                    {
                        if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase))
                        {
                            cpuTemp = (int)Math.Round(sensor.Value.Value);
                        }
                        else if (!cpuTemp.HasValue)
                        {
                            cpuTemp = (int)Math.Round(sensor.Value.Value);
                        }
                    }
                    else if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                    {
                        if (sensor.Name.Contains("#1", StringComparison.OrdinalIgnoreCase) || cpuClockMhz == 0)
                        {
                            cpuClockMhz = sensor.Value.Value;
                        }
                    }
                }
            }

            metrics.CpuUsage = (int)Math.Clamp(Math.Round(cpuLoad), 0, 100);
            metrics.CpuTemp = cpuTemp;
            metrics.CpuClockGhz = cpuClockMhz > 0 ? (float)Math.Round(cpuClockMhz / 1000.0f, 2) : 0;

            // 2. GPU
            int? gpuTemp = null;
            float gpuLoad = 0;
            float gpuClockMhz = 0;
            float dedicatedUsedMb = 0;
            float dedicatedTotalMb = 0;
            float sharedUsedMb = 0;
            float sharedTotalMb = 0;
            float vramUsagePercent = 0;

            var targetGpu = _gpus.FirstOrDefault(g => g.HardwareType == HardwareType.GpuNvidia || g.HardwareType == HardwareType.GpuAmd)
                            ?? _gpus.FirstOrDefault();

            if (targetGpu != null)
            {
                targetGpu.Update();

                foreach (var sensor in targetGpu.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load)
                    {
                        if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Contains("D3D 3D", StringComparison.OrdinalIgnoreCase))
                        {
                            gpuLoad = sensor.Value ?? gpuLoad;
                        }
                        else if (sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                        {
                            vramUsagePercent = sensor.Value ?? vramUsagePercent;
                        }
                    }
                    else if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                    {
                        if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || !gpuTemp.HasValue)
                        {
                            gpuTemp = (int)Math.Round(sensor.Value.Value);
                        }
                    }
                    else if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue)
                    {
                        if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || gpuClockMhz == 0)
                        {
                            gpuClockMhz = sensor.Value.Value;
                        }
                    }
                    else if (sensor.SensorType == SensorType.SmallData || sensor.SensorType == SensorType.Data)
                    {
                        if (sensor.Name.Contains("Dedicated Memory Used", StringComparison.OrdinalIgnoreCase) ||
                            sensor.Name.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase))
                        {
                            dedicatedUsedMb = sensor.Value ?? dedicatedUsedMb;
                        }
                        else if (sensor.Name.Contains("Dedicated Memory Total", StringComparison.OrdinalIgnoreCase) ||
                                 sensor.Name.Equals("GPU Memory Total", StringComparison.OrdinalIgnoreCase))
                        {
                            dedicatedTotalMb = sensor.Value ?? dedicatedTotalMb;
                        }
                        else if (sensor.Name.Contains("Shared Memory Used", StringComparison.OrdinalIgnoreCase))
                        {
                            sharedUsedMb = sensor.Value ?? sharedUsedMb;
                        }
                        else if (sensor.Name.Contains("Shared Memory Total", StringComparison.OrdinalIgnoreCase))
                        {
                            sharedTotalMb = sensor.Value ?? sharedTotalMb;
                        }
                    }
                }
            }

            // Fallback for GPU temperature on monolithic Intel package
            if (!gpuTemp.HasValue && cpuTemp.HasValue)
            {
                gpuTemp = cpuTemp;
            }

            metrics.GpuUsage = (int)Math.Clamp(Math.Round(gpuLoad), 0, 100);
            metrics.GpuTemp = gpuTemp;
            metrics.GpuClockMhz = (int)Math.Round(gpuClockMhz);

            // Prioritize dedicated VRAM if available, otherwise fall back to shared memory (iGPU)
            float effectiveUsedMb = dedicatedUsedMb > 0 ? dedicatedUsedMb : sharedUsedMb;
            float effectiveTotalMb = dedicatedTotalMb > 0 ? dedicatedTotalMb : sharedTotalMb;

            if (effectiveTotalMb > 0)
            {
                metrics.VramUsedGb = (float)Math.Round(effectiveUsedMb / 1024.0f, 1);
                metrics.VramUsagePercent = (int)Math.Clamp(Math.Round((effectiveUsedMb / effectiveTotalMb) * 100), 0, 100);
            }
            else if (effectiveUsedMb > 0)
            {
                metrics.VramUsedGb = (float)Math.Round(effectiveUsedMb / 1024.0f, 1);
                metrics.VramUsagePercent = (int)Math.Clamp(Math.Round(vramUsagePercent), 0, 100);
            }

            // 3. RAM (Strictly match physical memory, not virtual memory / pagefile)
            float ramLoad = 0;
            float ramUsedGb = 0;

            foreach (var mem in _memories)
            {
                mem.Update();

                foreach (var sensor in mem.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load &&
                        sensor.Name.Equals("Memory", StringComparison.OrdinalIgnoreCase) &&
                        sensor.Value.HasValue)
                    {
                        ramLoad = sensor.Value.Value;
                    }
                    else if (sensor.SensorType == SensorType.Data &&
                             sensor.Name.Equals("Memory Used", StringComparison.OrdinalIgnoreCase) &&
                             sensor.Value.HasValue)
                    {
                        ramUsedGb = sensor.Value.Value;
                    }
                }
            }

            metrics.RamUsagePercent = (int)Math.Clamp(Math.Round(ramLoad), 0, 100);
            metrics.RamUsedGb = (float)Math.Round(ramUsedGb, 1);
        }
        catch
        {
        }

        return metrics;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _computer?.Close();
        }
        catch
        {
        }
    }
}

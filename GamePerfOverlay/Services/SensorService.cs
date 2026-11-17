using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using GamePerfOverlay.Interop;
using GamePerfOverlay.Models;
using LibreHardwareMonitor.Hardware;

namespace GamePerfOverlay.Services;

public class SensorService : ISensorService
{
    private Computer? _computer;
    private readonly List<IHardware> _cpus = new();
    private readonly List<IHardware> _gpus = new();
    private readonly List<IHardware> _memories = new();
    private PerformanceCounter? _cpuPerfCounter;
    private float _cpuBaseMhz;
    private int _processorCount;
    private bool _initialized;
    private bool _disposed;

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            _processorCount = Environment.ProcessorCount;
            _cpuBaseMhz = QueryBaseCpuMhz();

            try
            {
                _cpuPerfCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
                _cpuPerfCounter.NextValue();
            }
            catch
            {
                _cpuPerfCounter = null;
            }

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

    private float QueryBaseCpuMhz()
    {
        try
        {
            int structSize = Marshal.SizeOf<NativeMethods.PROCESSOR_POWER_INFORMATION>();
            int bufferSize = structSize * _processorCount;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                int status = NativeMethods.CallNtPowerInformation(11, IntPtr.Zero, 0, buffer, (uint)bufferSize);
                if (status == 0)
                {
                    uint maxMhz = 0;
                    for (int i = 0; i < _processorCount; i++)
                    {
                        var info = Marshal.PtrToStructure<NativeMethods.PROCESSOR_POWER_INFORMATION>(IntPtr.Add(buffer, i * structSize));
                        if (info.MaxMhz > maxMhz)
                        {
                            maxMhz = info.MaxMhz;
                        }
                    }

                    if (maxMhz > 0)
                    {
                        return maxMhz;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
        }

        return 2000f;
    }

    private float GetFallbackCpuClockMhz()
    {
        if (_cpuPerfCounter != null && _cpuBaseMhz > 0)
        {
            try
            {
                float perfPct = _cpuPerfCounter.NextValue();
                if (perfPct > 0)
                {
                    return _cpuBaseMhz * (perfPct / 100.0f);
                }
            }
            catch
            {
            }
        }

        return QueryCurrentCpuMhzFromPowerInfo();
    }

    private float QueryCurrentCpuMhzFromPowerInfo()
    {
        try
        {
            int structSize = Marshal.SizeOf<NativeMethods.PROCESSOR_POWER_INFORMATION>();
            int bufferSize = structSize * _processorCount;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                int status = NativeMethods.CallNtPowerInformation(11, IntPtr.Zero, 0, buffer, (uint)bufferSize);
                if (status == 0)
                {
                    uint maxCurrentMhz = 0;
                    for (int i = 0; i < _processorCount; i++)
                    {
                        var info = Marshal.PtrToStructure<NativeMethods.PROCESSOR_POWER_INFORMATION>(IntPtr.Add(buffer, i * structSize));
                        if (info.CurrentMhz > maxCurrentMhz)
                        {
                            maxCurrentMhz = info.CurrentMhz;
                        }
                    }

                    if (maxCurrentMhz > 0)
                    {
                        return maxCurrentMhz;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
        }

        return _cpuBaseMhz > 0 ? _cpuBaseMhz : 0;
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
                    else if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue && sensor.Value.Value > 0)
                    {
                        if (sensor.Name.Contains("#1", StringComparison.OrdinalIgnoreCase) || cpuClockMhz == 0)
                        {
                            cpuClockMhz = sensor.Value.Value;
                        }
                    }
                }
            }

            // Fallback for CPU clock if LibreHardwareMonitor cannot read MSR registers (Windows 11 Driver Blocklist / Core Isolation)
            if (cpuClockMhz <= 0)
            {
                cpuClockMhz = GetFallbackCpuClockMhz();
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
                    else if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue && sensor.Value.Value > 0)
                    {
                        bool isCoreOrGraphics = sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                               sensor.Name.Contains("Graphics", StringComparison.OrdinalIgnoreCase);
                        bool isMemory = sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase);

                        if (isCoreOrGraphics || (gpuClockMhz == 0 && !isMemory))
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
            _cpuPerfCounter?.Dispose();
            _computer?.Close();
        }
        catch
        {
        }
    }
}

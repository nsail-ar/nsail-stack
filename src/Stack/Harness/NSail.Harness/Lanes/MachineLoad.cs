// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.InteropServices;
using NSail.Harness.Planning;

namespace NSail.Harness.Lanes;

// Samples the machine's CPU and memory every few seconds into LoadSamples, for the plans that
// read them. CPU is the share of time not idle between two samples, across all cores; memory is
// the share not available. Linux reads /proc, Windows its system counters; elsewhere nothing is
// sampled and a plan that asks for load never holds.
public sealed class MachineLoad(LoadSamples samples, ILogger<MachineLoad> log) : BackgroundService
{
    static readonly TimeSpan Every = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        (ulong Idle, ulong Total)? last = null;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (Times() is { } now)
                {
                    if (last is { } before && now.Total > before.Total)
                    {
                        var cpu = 100.0 * (1 - (double)(now.Idle - before.Idle) / (now.Total - before.Total));
                        samples.Add(DateTimeOffset.UtcNow, Math.Clamp(cpu, 0, 100), MemoryInUse() ?? 0);
                    }
                    last = now;
                }
            }
            catch (Exception e) when (e is IOException or FormatException or UnauthorizedAccessException)
            {
                log.LogWarning("machine load not sampled: {Error}", e.Message);
            }
            await Task.Delay(Every, stop).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    // Idle and total CPU time since boot, in the platform's own ticks.
    static (ulong Idle, ulong Total)? Times()
    {
        if (OperatingSystem.IsLinux())
        {
            // cpu  user nice system idle iowait irq softirq steal ...
            var fields = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ulong.Parse).ToArray();
            return (fields[3] + (fields.Length > 4 ? fields[4] : 0), fields.Take(8).Aggregate(0UL, (a, b) => a + b));
        }
        if (OperatingSystem.IsWindows() && GetSystemTimes(out var idle, out var kernel, out var user))
        {
            // Kernel time includes idle time.
            return (idle, kernel + user);
        }
        return null;
    }

    static double? MemoryInUse()
    {
        if (OperatingSystem.IsLinux())
        {
            var info = File.ReadLines("/proc/meminfo").Select(l => l.Split(':', 2)).ToDictionary(p => p[0], p => double.Parse(p[1].Trim().Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture));
            return info.TryGetValue("MemTotal", out var total) && info.TryGetValue("MemAvailable", out var available) && total > 0 ? 100 * (1 - available / total) : null;
        }
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            return GlobalMemoryStatusEx(ref status) ? status.MemoryLoad : null;
        }
        return null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);
}

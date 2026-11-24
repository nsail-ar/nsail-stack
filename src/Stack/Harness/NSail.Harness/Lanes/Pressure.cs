// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Harness.Lanes;

// The kernel's pressure stall information over the last minute (`avg60`, percent of time): memory
// `full` (every task stalled waiting for memory) and CPU `some` (at least one runnable task waiting).
// Either is null when the kernel does not report it.
public sealed record PressureReading(double? MemoryFull, double? CpuSome);

public interface IPressure
{
    // Null where the machine reports no pressure (Windows, a kernel without PSI): nothing is held then.
    PressureReading? Read();
}

// Reads /proc/pressure/{memory,cpu} on each call: a file read, no sampling.
public sealed class ProcPressure(string root = "/proc/pressure") : IPressure
{
    public PressureReading? Read()
    {
        try
        {
            var memory = Avg60(Path.Combine(root, "memory"), "full");
            var cpu = Avg60(Path.Combine(root, "cpu"), "some");
            return memory is null && cpu is null ? null : new PressureReading(memory, cpu);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    // A line reads `some avg10=0.00 avg60=0.00 avg300=0.00 total=0`.
    static double? Avg60(string file, string kind)
    {
        if (!File.Exists(file)) return null;
        foreach (var line in File.ReadLines(file))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || fields[0] != kind) continue;
            var avg = fields.FirstOrDefault(f => f.StartsWith("avg60=", StringComparison.Ordinal));
            return avg is null ? null : double.Parse(avg["avg60=".Length..], CultureInfo.InvariantCulture);
        }
        return null;
    }
}

// Where a pressure reading saturates the machine (HarnessOptions.Pressure): at or over either bound.
public sealed class PressureOptions
{
    public double MemoryFull { get; set; } = 10;
    public double CpuSome { get; set; } = 80;

    // Why the machine is saturated, with the reading and its bound; null when it is not or nothing was read.
    public string? Saturated(PressureReading? reading)
    {
        if (reading?.MemoryFull is { } memory && memory >= MemoryFull) return Said("memory full", memory, MemoryFull);
        if (reading?.CpuSome is { } cpu && cpu >= CpuSome) return Said("cpu some", cpu, CpuSome);
        return null;
    }

    static string Said(string what, double read, double bound)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{what} avg60 {read:0.0} ≥ {bound:0.##}");
    }
}

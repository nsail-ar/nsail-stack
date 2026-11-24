// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

// What the plans read, as the harness sees it now: the machine's CPU and memory in use (percent,
// averaged over the last minute and five; null until there are samples enough) and the agent's
// quota windows from its newest run; the kernel's pressure over the last minute, null where it reports none.
public sealed class MachineReadings
{
    public double? Cpu1 { get; init; }
    public double? Cpu5 { get; init; }
    public double? Memory5 { get; init; }
    public PressureView? Pressure { get; init; }
    public IReadOnlyList<QuotaReading> Quota { get; init; } = [];
}

// `/proc/pressure` at avg60: memory `full` and CPU `some`, percent of time stalled.
public sealed class PressureView
{
    public double? MemoryFull { get; init; }
    public double? CpuSome { get; init; }
}

public sealed class QuotaReading
{
    public required string Window { get; init; }
    public double Used { get; init; }
    public double Pace { get; init; }
    public DateTimeOffset ResetsAt { get; init; }
}

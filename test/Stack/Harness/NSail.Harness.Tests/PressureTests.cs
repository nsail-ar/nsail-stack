// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Lanes;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// While the kernel reports the machine saturated no new job starts, of any kind, and the plan says
// why; with no reading nothing is held.
public sealed class PressureTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("pressure").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    static readonly Plan Work = new(
    [
        new WorkItem(WorkKind.Gate, 1, 10, "gate", "no gate at this head"),
        new WorkItem(WorkKind.Dev, 2, null, "bug", "bug"),
        new WorkItem(WorkKind.Main, null, null, "red", "main moved"),
    ], [], ["an earlier note"]);

    static readonly Limits Room = new(Agents: 4, Builds: 3, Browsers: 1);

    [Fact]
    public void TheKernelsLinesAreReadAtTheirMinuteAverage()
    {
        File.WriteAllText(Path.Combine(_root, "memory"), "some avg10=40.00 avg60=35.10 avg300=20.00 total=1\nfull avg10=30.00 avg60=23.40 avg300=10.00 total=1\n");
        File.WriteAllText(Path.Combine(_root, "cpu"), "some avg10=90.00 avg60=81.00 avg300=70.00 total=1\nfull avg10=0.00 avg60=0.00 avg300=0.00 total=0\n");

        Assert.Equal(new PressureReading(23.4, 81), new ProcPressure(_root).Read());
    }

    [Fact]
    public void AMachineWithNoPressureFilesReadsNothingAndHoldsNothing()
    {
        var reading = new ProcPressure(Path.Combine(_root, "absent")).Read();

        Assert.Null(reading);
        var (plan, held) = HarnessLoop.Saturated(Work, reading, new PressureOptions());
        Assert.False(held);
        Assert.Same(Work, plan);
    }

    [Fact]
    public void OverEitherBoundNothingStartsAndThePlanSaysTheReading()
    {
        var o = new PressureOptions();

        var (memory, heldOnMemory) = HarnessLoop.Saturated(Work, new PressureReading(23.4, 5), o);
        var (cpu, heldOnCpu) = HarnessLoop.Saturated(Work, new PressureReading(0, 80), o);

        Assert.True(heldOnMemory);
        Assert.Equal(["an earlier note", "machine saturated (memory full avg60 23.4 ≥ 10) — no new job starts"], memory.Notes);
        Assert.True(heldOnCpu);
        Assert.Contains("machine saturated (cpu some avg60 80.0 ≥ 80) — no new job starts", cpu.Notes);
    }

    // The same plan, read again once the pressure fell: what fits starts.
    [Fact]
    public void BelowTheBoundsAgainTheJobsStart()
    {
        var o = new PressureOptions();
        var readings = new[] { new PressureReading(23.4, 85), new PressureReading(9.9, 79.9) };

        var starts = readings.Select(r => HarnessLoop.Saturated(Work, r, o)).Select(s => s.Held ? Array.Empty<WorkItem>() : Capacity.Pick(s.Plan.Items, new Dictionary<Resource, int>(), Room)).ToList();

        Assert.Empty(starts[0]);
        Assert.Equal(Work.Items, starts[1]);
    }

    [Fact]
    public void TheBoundsAreSettings()
    {
        var o = new PressureOptions { MemoryFull = 30, CpuSome = 95 };

        Assert.Null(o.Saturated(new PressureReading(23.4, 85)));
        Assert.Equal("memory full avg60 30.0 ≥ 30", o.Saturated(new PressureReading(30, 0)));
        Assert.Equal(10, new HarnessOptions().Pressure.MemoryFull);
        Assert.Equal(80, new HarnessOptions().Pressure.CpuSome);
    }
}

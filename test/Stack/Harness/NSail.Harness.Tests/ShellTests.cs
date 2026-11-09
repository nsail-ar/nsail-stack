// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class ShellTests
{
    [Fact]
    public async Task AChildThatNeverReadsStdinIsKilledByTheTimeout()
    {
        if (OperatingSystem.IsWindows()) return;
        var big = new string('x', 4 * 1024 * 1024);

        var started = DateTime.UtcNow;
        var result = await Shell.Run("sleep", ["60"], Path.GetTempPath(), timeout: TimeSpan.FromSeconds(1), stdin: big);

        Assert.True(result.TimedOut);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ATokenCancelsAStdinWriteTheChildNeverReads()
    {
        if (OperatingSystem.IsWindows()) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Shell.Run("sleep", ["60"], Path.GetTempPath(), stdin: new string('x', 4 * 1024 * 1024), ct: cts.Token));
    }

    [Fact]
    public async Task OnlyABoundedTailOfALongOutputComesBack()
    {
        if (OperatingSystem.IsWindows()) return;
        var log = new StringWriter();

        var result = await Shell.Run("seq", ["1", "20000"], Path.GetTempPath(), log: log, tailChars: 1000);

        Assert.True(result.Output.Length < 1200);
        Assert.EndsWith("20000" + Environment.NewLine, result.Output);
        Assert.Contains("1" + Environment.NewLine, log.ToString());
        Assert.Contains("19999", log.ToString());
    }

    [Fact]
    public async Task LinesTheCallerKeepSurviveTheCutOfTheHead()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = await Shell.Run("seq", ["1", "20000"], Path.GetTempPath(), tailChars: 1000, keep: l => l == "7" || l == "19999");

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("7", lines[0]);
        Assert.Contains("19999", lines);
        Assert.Equal("20000", lines[^1]);
    }

    [Fact]
    public void StreamSignalsAreRecognisedWhole()
    {
        Assert.True(NSail.Harness.Claude.StreamSignals.IsSignal("{\"type\":\"rate_limit_event\"}"));
        Assert.False(NSail.Harness.Claude.StreamSignals.IsSignal("plain hit your weekly limit"));
    }

    [Fact]
    public void GateFailureLinesAreKept()
    {
        Assert.True(Jobs.GateJob.IsFailureLine("  Failed Foo.Bar [2 ms]"));
        Assert.False(Jobs.GateJob.IsFailureLine("  Passed Foo.Bar [2 ms]"));
    }

    [Fact]
    public async Task AShortOutputComesBackWhole()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = await Shell.Run("sh", ["-c", "echo a; echo b >&2"], Path.GetTempPath());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("a", result.Output);
        Assert.Contains("b", result.Output);
    }

    [Fact]
    public async Task AStdinTheChildReadsIsDelivered()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = await Shell.Run("cat", [], Path.GetTempPath(), stdin: "hello");

        Assert.Contains("hello", result.Output);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSail.Harness.Agents;
using NSail.Harness.Claude;
using NSail.Harness.Lanes;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// What the harness keeps of its own work and for how long, what a seat's run loads, and what a
// worker sets up on the machine before it takes a job.
public sealed class HousekeepingTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("harness-keep").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    static JobRecord Job(string id, DateTimeOffset started)
    {
        return new JobRecord(id, "dev #5", 5, started, started.AddMinutes(1), "pushed", "x.log", "ok");
    }

    [Fact]
    public void TheHistoryKeepsAJobForItsKeepHoweverManyRan()
    {
        var history = new JobHistory(_root);
        var now = DateTimeOffset.UtcNow;
        history.Record(Job("old", now - JobHistory.Keep - TimeSpan.FromHours(1)));
        for (var n = 0; n < 600; n++) history.Record(Job($"job-{n}", now - JobHistory.Keep + TimeSpan.FromMinutes(n + 1)));

        history.Record(Job("new", now));

        var kept = new JobHistory(_root).Jobs();
        Assert.Equal(601, kept.Count);
        Assert.DoesNotContain(kept, j => j.Id == "old");
    }

    [Fact]
    public void TheFirstLineOfADayDropsTheLogsLastWrittenBeforeItsKeep()
    {
        var logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logs);
        var old = Path.Combine(logs, "1001000000-dev-5.log");
        var recent = Path.Combine(logs, "1109000000-dev-5.log");
        var other = Path.Combine(logs, "notes.txt");
        foreach (var file in new[] { old, recent, other }) File.WriteAllText(file, "x");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-15));
        File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddDays(-15));
        File.SetLastWriteTimeUtc(recent, DateTime.UtcNow.AddDays(-13));

        using var log = new FileLog(logs, TimeSpan.FromDays(14));
        log.CreateLogger("NSail.Harness.HarnessLoop").LogInformation("up");

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(other));
        Assert.Single(Directory.GetFiles(logs, "harness-*.log"));
    }

    [Fact]
    public void AFileLogWithNoKeepDropsNothing()
    {
        var logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logs);
        var old = Path.Combine(logs, "1001000000-dev-5.log");
        File.WriteAllText(old, "x");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-400));

        using var log = new FileLog(logs);
        log.CreateLogger("NSail.Harness.HarnessLoop").LogInformation("up");

        Assert.True(File.Exists(old));
    }

    [Fact]
    public void ASeatLoadsOnlyTheBuiltInToolsItsAccessAndItsWorkerName()
    {
        Assert.Equal("Bash,Read,Edit,MultiEdit,Write,Glob,Grep,TodoWrite,Skill", ClaudeAgent.Loaded(AgentAccess.Builder, null));
        Assert.Equal("Read,Glob,Grep,Write,Skill,Bash", ClaudeAgent.Loaded(AgentAccess.Reader, []));
        Assert.Equal("Read,Glob,Grep,Write,Skill", ClaudeAgent.Loaded(AgentAccess.Walker, ["mcp__playwright__*"]));
        Assert.Equal("Read,Glob,Grep,Write,Skill,Bash", ClaudeAgent.Loaded(AgentAccess.Walker, ["Bash(.github/qa-mail.sh:*)", "mcp__playwright__browser_click"]));
    }

    HarnessLoop Loop(params (string Name, CommandOptions? Setup)[] workers)
    {
        var options = new HarnessOptions { WorkRoot = Path.Combine(_root, "work"), Seats = { ["qa"] = new AgentOptions { Prompt = "walk" } } };
        foreach (var (name, setup) in workers) options.Workers[name] = new WorkerOptions { Type = "Test", Seat = "qa", Setup = setup };
        return new HarnessLoop(null!, null!, null!, null!, null!, null!, null!, null!, null!,
            Options.Create(options), null!, new HarnessMetrics(), new WatchState(null),
            new HarnessState(Path.Combine(_root, "state")), NullLogger<HarnessLoop>.Instance);
    }

    [Fact]
    public async Task ASetupThatFailsHoldsOnlyTheWorkersThatNameIt()
    {
        var works = new CommandOptions { Run = "dotnet", Args = ["--version"] };
        var fails = new CommandOptions { Run = "dotnet", Args = ["no-such-command-of-this-test"] };
        var missing = new CommandOptions { Run = "no-such-binary-of-this-test" };

        var held = await Loop(("a", works), ("b", fails), ("c", fails), ("d", missing), ("e", null)).SetUp();

        Assert.Equal(["b", "c", "d"], held.Order());
        Assert.Empty(await Loop(("a", works), ("e", null)).SetUp());
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSail.Harness.Lanes;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// What a restart keeps (the pause, take-new, the quota wall) and what a torn state file costs (nothing).
public sealed class StateFileTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("harness-state").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    HarnessLoop Loop(bool takeNew = true)
    {
        return new HarnessLoop(null!, null!, null!, null!, null!, null!, null!, null!, null!,
            Options.Create(new HarnessOptions { TakeNew = takeNew }), null!, new HarnessMetrics(), new WatchState(null),
            new HarnessState(_root), NullLogger<HarnessLoop>.Instance);
    }

    [Fact]
    public void A_restart_keeps_the_pause_take_new_off_and_the_quota_wall()
    {
        var first = Loop();
        first.Pause("by hand");
        first.TakeNew = false;
        var written = new HarnessState(_root);
        written.Set(written.Paused, written.PausedSince, DateTimeOffset.UtcNow.AddHours(2), written.TakeNew);

        var second = Loop();
        Assert.Equal("by hand", second.Paused);
        Assert.False(second.TakeNew);
        Assert.NotNull(second.AgentsOffUntil);

        second.Resume();
        second.TakeNew = true;
        var third = Loop();
        Assert.Null(third.Paused);
        Assert.True(third.TakeNew);
        Assert.Null(third.AgentsOffUntil);
    }

    [Fact]
    public void A_deploy_restart_pause_is_not_kept()
    {
        var first = new HarnessLoop(null!, null!, null!, null!, null!, null!, null!, null!, null!,
            Options.Create(new HarnessOptions()), new Lifetime(), new HarnessMetrics(), new WatchState(null),
            new HarnessState(_root), NullLogger<HarnessLoop>.Instance);
        first.Restart("a deploy");
        Assert.NotNull(first.Paused);
        Assert.Null(Loop().Paused);
        Environment.ExitCode = 0;
    }

    [Fact]
    public void A_hand_pause_survives_a_deploy_restart()
    {
        var first = new HarnessLoop(null!, null!, null!, null!, null!, null!, null!, null!, null!,
            Options.Create(new HarnessOptions()), new Lifetime(), new HarnessMetrics(), new WatchState(null),
            new HarnessState(_root), NullLogger<HarnessLoop>.Instance);
        first.Pause("by hand");
        first.Restart("a deploy");
        Environment.ExitCode = 0;
        Assert.Equal("by hand", Loop().Paused);
    }

    sealed class Lifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => default;
        public CancellationToken ApplicationStopping => default;
        public CancellationToken ApplicationStopped => default;

        public void StopApplication()
        {
        }
    }

    [Fact]
    public void A_wall_already_past_is_dropped()
    {
        new HarnessState(_root).Set(null, null, DateTimeOffset.UtcNow.AddMinutes(-1), null);
        Assert.Null(new HarnessState(_root).QuotaUntil);
    }

    [Fact]
    public void A_clean_start_has_nothing_set()
    {
        var loop = Loop();
        Assert.Null(loop.Paused);
        Assert.True(loop.TakeNew);
        Assert.Null(loop.AgentsOffUntil);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"Hours\": [")]
    public void A_zero_length_or_torn_file_is_moved_aside_and_the_state_starts_empty(string content)
    {
        foreach (var name in new[] { "jobs.json", "stats.json", "watch.json", "state.json", "ops.json" })
        {
            File.WriteAllText(Path.Combine(_root, name), content);
        }

        Assert.Empty(new JobHistory(_root).Jobs());
        Assert.Empty(new OpsLedger(_root, new JobHistory(_root)).Owed());
        _ = new JobStats(_root);
        Assert.Null(new WatchState(_root).ReadUntil("any"));
        Assert.Null(new HarnessState(_root).Paused);

        foreach (var name in new[] { "jobs.json", "stats.json", "watch.json", "state.json", "ops.json" })
        {
            Assert.False(File.Exists(Path.Combine(_root, name)));
            Assert.Single(Directory.GetFiles(_root, name + ".bad-*"));
        }
    }

    [Fact]
    public void A_write_leaves_no_temp_file_behind()
    {
        var watch = new WatchState(_root);
        watch.Read("w", DateTimeOffset.UtcNow);
        new HarnessState(_root).Set("x", null, null, null);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        Assert.NotNull(new WatchState(_root).ReadUntil("w"));
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;
using NSail.Harness.Watching;

namespace NSail.Harness.Tests;

// A lit andon that stood still says so on its red story, where a person looks: once, and not while
// the product's cut runs on the code host.
public sealed class AndonStillTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("andon-still").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task AStandingAndonIsSaidOnceOnItsStoryAndNotWhileItsCutRuns()
    {
        var o = new HarnessOptions { Machine = "box", Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        o.Workers["release-shop"] = new WorkerOptions
        {
            Type = "Release",
            Enabled = true,
            Product = "Shop",
            Workflow = "release-shop.yml",
            Paths = "shop.paths",
            When = new WhenOptions { Schedule = new ScheduleCondition { Start = "0 2 * * *" } },
        };
        var board = new MemoryBoard();
        board.Add(1, title: "Regression red on main (Shop)", stage: Stages.Merged);
        var repo = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => "acme/app",
            ["Login"] = _ => Task.FromResult("harness"),
        });
        var repos = new RepositorySet([(repo, o.Repositories[0])]);
        var leases = new BuildLeases(1);
        var jobs = new JobContext(board, repos, new Dictionary<string, Workspace>(), new NoAgent(), o, [], NullLoggerFactory.Instance, leases);
        var loop = new HarnessLoop(new SnapshotReader(board, repos, null!, o), board, null!, null!, new JobHistory(Path.Combine(_root, "history")),
            new JobStats(null), jobs, leases, null!, Options.Create(o), null!, new HarnessMetrics(), new WatchState(null), new HarnessState(Path.Combine(_root, "state")),
            NullLogger<HarnessLoop>.Instance);
        var plan = new Plan([], [], []) { Andon = [new Andon("Shop", 1, null, "the cut: the fix of #1 merged")] };
        var idle = new Fixture().Build();
        var cutting = idle with { Flight = Flight.Empty with { ReleaseWorkflows = new HashSet<string> { "release-shop.yml" } } };

        await loop.SayStill(plan, cutting, o, CancellationToken.None);
        Assert.Empty(board.Cards[1].Comments);

        await loop.SayStill(plan, idle, o, CancellationToken.None);
        await loop.SayStill(plan, idle, o, CancellationToken.None);

        var said = Assert.Single(board.Cards[1].Comments);
        Assert.Contains("it waits for the cut: the fix of #1 merged", said.Body);
        Assert.Contains("The line runs behind it", said.Body);
        Assert.EndsWith(Markers.AndonStill, said.Body);
    }
}

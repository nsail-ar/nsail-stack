// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A blocker merged in a pinned repository has landed for what waits on it only once the default
// branch's pin holds the merge.
public class PinnedBlockerTests
{
    static readonly DateTimeOffset MergedAt = DateTimeOffset.Parse("2026-11-10T10:00:00Z");

    sealed class Calls
    {
        public int Merges;
    }

    static (RepositorySet Repos, HarnessOptions Options) Repos(bool pinned, Calls? calls = null)
    {
        var lib = new RepositoryOptions { Name = "acme/lib", Label = "area:lib", Pin = new PinOptions { File = "pin.props", Pattern = "<V>([^<]+)</V>", Ref = "v{version}" } };
        var options = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }, lib] };
        var app = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => "acme/app",
            ["Changes"] = _ => Task.FromResult<IReadOnlyList<Merge>>(pinned ? [new Merge("bump", MergedAt.AddHours(1))] : []),
            ["FileAt"] = _ => Task.FromResult<string?>("<V>1.0.1</V>"),
        });
        var libRepo = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => "acme/lib",
            ["MergeOf"] = a =>
            {
                if (calls is not null) calls.Merges++;
                return Task.FromResult<Merge?>((int)a[0]! == 2 ? new Merge("fixsha", MergedAt) : null);
            },
            ["Holds"] = a => Task.FromResult((string)a[0]! == "v1.0.1"),
        });
        return (new RepositorySet([(app, options.Repositories[0]), (libRepo, lib)]), options);
    }

    static async Task<HashSet<int>> Awaiting(bool pinned, Fixture board, Calls? calls = null)
    {
        var (repos, options) = Repos(pinned, calls);
        return await new SnapshotReader(new MemoryBoard(), repos, null!, options).ReadAwaitingPin(board.Build().Issues);
    }

    [Fact]
    public async Task AMergedBlockerWaitsForItsPin()
    {
        var board = new Fixture().Story(1, "Ready", blockers: [2]).Story(2, "Merged", "area:lib");

        Assert.Equal([2], await Awaiting(pinned: false, board));
        Assert.Empty(await Awaiting(pinned: true, board));
    }

    [Fact]
    public async Task OnlyAMergedBlockerOfAPinnedRepositoryIsAsked()
    {
        var calls = new Calls();
        var board = new Fixture()
            .Story(1, "Ready", blockers: [2, 3, 4])
            .Story(2, "Released", "area:lib")
            .Story(3, "Merged", "area:shop")
            .Story(4, "In-Progress", "area:lib")
            .Story(5, "Merged", "area:lib");

        Assert.Empty(await Awaiting(pinned: false, board, calls));
        Assert.Equal(0, calls.Merges);
    }

    [Fact]
    public async Task ABlockerThePinHoldsIsNotAskedAgainWhileItStaysMerged()
    {
        var calls = new Calls();
        var (repos, options) = Repos(pinned: true, calls);
        var reader = new SnapshotReader(new MemoryBoard(), repos, null!, options);
        var issues = new Fixture().Story(1, "Ready", blockers: [2]).Story(2, "Merged", "area:lib").Build().Issues;

        await reader.ReadAwaitingPin(issues);
        await reader.ReadAwaitingPin(issues);

        Assert.Equal(1, calls.Merges);
    }

    [Fact]
    public void TheDependentIsNotBuiltAndItsHoldStaysWhileTheBlockerWaitsForItsPin()
    {
        var waiting = new Fixture().Story(1, "Ready", blockers: [2]).Story(2, "Merged").Story(3, "Ready", "blocked", blockers: [2]).AwaitingPin(2);
        var landed = new Fixture().Story(1, "Ready", blockers: [2]).Story(2, "Merged").Story(3, "Ready", "blocked", blockers: [2]);

        var held = SampleWorkers.Plan(waiting.Build());
        var free = SampleWorkers.Plan(landed.Build());

        Assert.DoesNotContain(held.Items, i => i.Story == 1);
        Assert.DoesNotContain(held.Chores, c => c.Story == 3);
        Assert.Contains(free.Items, i => i.Story == 1);
        Assert.Contains(free.Chores, c => c is { Kind: ChoreKind.Unlock, Story: 3, Value: "blocked" });
    }

    [Fact]
    public async Task ABlockerNamedInWordsHoldsTheStoryWhileItWaitsForItsPin()
    {
        var board = new MemoryBoard();
        board.Add(1, body: "blocked by #2");
        board.Add(2, stage: "Merged");

        var open = await WrittenBlockers.Link(board, 1, board.Cards[1].Body, "app", repos: Repos(pinned: false).Repos);

        Assert.Equal([2], open);
        Assert.Contains("blocked", board.Cards[1].Labels);
    }

    [Fact]
    public async Task ABlockerNamedInWordsThatThePinHoldsHoldsNothing()
    {
        var board = new MemoryBoard();
        board.Add(1, body: "blocked by #2");
        board.Add(2, stage: "Merged");

        Assert.Empty(await WrittenBlockers.Link(board, 1, board.Cards[1].Body, "app", repos: Repos(pinned: true).Repos));
        Assert.DoesNotContain("blocked", board.Cards[1].Labels);
    }
}

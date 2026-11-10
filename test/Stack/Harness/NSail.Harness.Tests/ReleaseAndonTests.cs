// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A product whose cut failed has an open red story; until it ships, the release takes the whole
// harness: only its fix, main's gate and its own cut are planned.
public class ReleaseAndonTests
{
    const string ShopRed = "Regression red on main (Shop)";

    static PlannerOptions Options(int wip = 0)
    {
        return new PlannerOptions(Gate: true) { WipLimit = wip };
    }

    // A red shop story, and around it every kind of work the harness could start.
    static Fixture Lit(string stage = "Ready")
    {
        return new Fixture().Story(1, stage, title: ShopRed)
            .Story(2, "Ready", "p1").Story(3, "Ready").Story(4, "Ready", "bug")
            .Story(5, "In-Progress").Pr(5, 50, gate: CiState.Red)
            .Story(6, "In-Progress").Pr(6, 60)
            .Story(7, "Released")
            .Story(8, "Analyze");
    }

    [Fact]
    public void AFailedCutMakesThePlanReleaseOnly()
    {
        var plan = SampleWorkers.Plan(Lit().Due("release-clinic.yml").Due("release-shop.yml").Build(), Options());

        var lit = Assert.Single(plan.Andon);
        Assert.Equal("Shop", lit.Product);
        Assert.Equal(1, lit.Story);
        Assert.NotEmpty(plan.Items);
        Assert.All(plan.Items, i => Assert.Equal(ReleaseAndon.Tier, i.Tier));
        Assert.Contains(plan.Items, i => i.Kind == WorkKind.Dev && i.Story == 1);
        Assert.Contains(plan.Items, i => i.Kind == WorkKind.Release && i.Product == "shop");
        Assert.DoesNotContain(plan.Items, i => i.Story is 2 or 3 or 4 or 5 or 6 or 7 or 8);
        Assert.DoesNotContain(plan.Items, i => i.Kind == WorkKind.Release && i.Product != "shop");
        Assert.DoesNotContain(plan.Items, i => i.Kind is WorkKind.Qa or WorkKind.Watch);
    }

    [Fact]
    public void TheRedStoryIsAnalyzedBuiltAndItsPrWorkedBeforeAnythingElse()
    {
        var analyze = SampleWorkers.Plan(Lit("Analyze").Build(), Options());
        var pr = SampleWorkers.Plan(Lit("In-Progress").Pr(1, 10, gate: CiState.Red).Build(), Options());
        var read = SampleWorkers.Plan(Lit("In-Progress").Pr(1, 10, gate: CiState.Green).Build(), Options());

        Assert.Contains(analyze.Items, i => i.Kind == WorkKind.Analyze && i.Story == 1);
        Assert.Contains(pr.Items, i => i.Kind == WorkKind.Dev && i.Pr == 10);
        Assert.Contains(read.Items, i => i.Kind == WorkKind.Review && i.Pr == 10);
        Assert.All(new[] { analyze, pr, read }.SelectMany(p => p.Items), i => Assert.Equal(1, i.Story));
    }

    [Fact]
    public void ARedMainsStoryIsReleaseWorkToo()
    {
        var plan = SampleWorkers.Plan(Lit().Story(9, "Ready", title: "CI red on main").Build(), Options());

        Assert.Contains(plan.Items, i => i.Story == 9 && i.Tier == ReleaseAndon.Tier);
    }

    [Fact]
    public void MainsGateAfterTheMergeIsReleaseWork()
    {
        var board = Lit("Merged").Build() with { MainSha = "newhead", MainGated = "oldhead" };

        var plan = SampleWorkers.Plan(board, Options());

        Assert.Contains(plan.Items, i => i.Kind == WorkKind.Main && i.Tier == ReleaseAndon.Tier);
    }

    [Fact]
    public void TheMergedFixIsCutOnItsReleaseBranch()
    {
        var plan = SampleWorkers.Plan(Lit("Merged").Release(1).Build(), Options());

        var cut = Assert.Single(plan.Items, i => i.Kind == WorkKind.Release);
        Assert.Equal(1, cut.Story);
        Assert.Equal(ReleaseAndon.Tier, cut.Tier);
    }

    [Fact]
    public void JobsAlreadyRunningAreLeftAlone()
    {
        var board = Lit().Flying(dev: [3], reviewPrs: [60], analyze: [8]).Build();

        var plan = SampleWorkers.Plan(board, Options());

        Assert.DoesNotContain(plan.Items, i => i.Story is 3 or 6 or 8);
        Assert.DoesNotContain(plan.Chores, c => c.Story is 3 or 6 or 8);
        Assert.Single(plan.Andon);
    }

    [Fact]
    public void ARunningJobOfTheReleaseIsNotPlannedTwice()
    {
        var plan = SampleWorkers.Plan(Lit().Flying(dev: [1]).Build(), Options());

        Assert.DoesNotContain(plan.Items, i => i.Story == 1);
    }

    [Fact]
    public void TheWipLimitDoesNotHoldReleaseWork()
    {
        var full = Lit().Story(10, "In-Progress").Pr(10, 100, gate: CiState.Pending).Story(11, "In-Progress").Pr(11, 110, gate: CiState.Pending);

        var plan = SampleWorkers.Plan(full.Build(), Options(wip: 2));

        Assert.Contains(plan.Items, i => i.Kind == WorkKind.Dev && i.Story == 1);
        Assert.DoesNotContain(plan.Notes, n => n.Contains("no new story starts", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutTheAndonTheSameLimitHoldsTheSameStory()
    {
        var full = new Fixture().Story(1).Story(10, "In-Progress").Pr(10, 100, gate: CiState.Pending).Story(11, "In-Progress").Pr(11, 110, gate: CiState.Pending);

        var plan = SampleWorkers.Plan(full.Build(), Options(wip: 2));

        Assert.Empty(plan.Andon);
        Assert.DoesNotContain(plan.Items, i => i.Story == 1);
    }

    [Fact]
    public void TheAndonLiftsWhenTheFixShips()
    {
        var merged = SampleWorkers.Plan(Lit("Merged").Published("shop", "shop/v1.3.0").Build(), Options());
        var shipped = SampleWorkers.Plan(Lit("Released").Build(), Options());

        Assert.Single(merged.Andon);
        Assert.Contains(merged.Items, i => i.Kind == WorkKind.Ship && i.Tag == "shop/v1.3.0" && i.Tier == ReleaseAndon.Tier);
        Assert.Empty(shipped.Andon);
        Assert.Contains(shipped.Items, i => i.Story is 2 or 3 or 4);
        Assert.DoesNotContain(shipped.Items, i => i.Tier == ReleaseAndon.Tier);
    }

    [Fact]
    public void AFixTheProductAlreadyPublishedHoldsNothingWhateverTheWalkDid()
    {
        var shipped = SampleWorkers.Plan(Lit("Merged").FixShipped(1).Build(), Options());

        Assert.Empty(shipped.Andon);
        Assert.Contains(shipped.Items, i => i.Story is 2 or 3 or 4);
    }

    [Fact]
    public void AClosedRedStoryIsNoLongerOnTheBoardAndHoldsNothing()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(2, "Ready", "p1").Build(), Options());

        Assert.Empty(plan.Andon);
        Assert.Contains(plan.Items, i => i.Story == 2);
    }

    // The reader's half: a red story in Merged is out once its product published a release after the merge.
    static async Task<(List<ReleaseCandidate> Candidates, HashSet<int> Shipped)> RedFixes(DateTimeOffset mergedAt, params Release[] releases)
    {
        var options = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        options.Workers["release-shop"] = new WorkerOptions { Type = "Release", Enabled = true, Product = "Shop", Workflow = "release-shop.yml" };
        var repo = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["MergeOf"] = _ => Task.FromResult<Merge?>(new Merge("fixsha", mergedAt)),
            ["RunsSince"] = _ => Task.FromResult(0),
        });
        var reader = new SnapshotReader(new MemoryBoard(), new RepositorySet([(repo, options.Repositories[0])]), null!, options);
        var issues = new Fixture().Story(1, "Merged", title: ShopRed).Build().Issues.ToList();
        return await reader.ReadRedFixes(issues, Flight.Empty, releases, CancellationToken.None);
    }

    // A fix merged in a pinned repository: the default repository has no merge of the story, and the
    // commit that moved the pin (an hour after the merge) is where its work reached the default branch.
    static async Task<List<ReleaseCandidate>> PinnedFix(DateTimeOffset mergedAt, bool pinned)
    {
        var lib = new RepositoryOptions { Name = "acme/lib", Label = "area:lib", Pin = new PinOptions { File = "pin.props", Pattern = "<V>([^<]+)</V>", Ref = "v{version}" } };
        var options = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }, lib] };
        options.Workers["release-shop"] = new WorkerOptions { Type = "Release", Enabled = true, Product = "Shop", Workflow = "release-shop.yml" };
        var app = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => "acme/app",
            ["MergeOf"] = _ => Task.FromResult<Merge?>(null),
            ["Changes"] = _ => Task.FromResult<IReadOnlyList<Merge>>(pinned ? [new Merge("bump", mergedAt.AddHours(1))] : []),
            ["FileAt"] = _ => Task.FromResult<string?>("<V>1.0.1</V>"),
            ["RunsSince"] = a => Task.FromResult((DateTimeOffset)a[1]! == mergedAt.AddHours(1) ? 0 : 9),
        });
        var libRepo = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => "acme/lib",
            ["MergeOf"] = _ => Task.FromResult<Merge?>(new Merge("fixsha", mergedAt)),
            ["Holds"] = a => Task.FromResult((string)a[0]! == "v1.0.1"),
        });
        var reader = new SnapshotReader(new MemoryBoard(), new RepositorySet([(app, options.Repositories[0]), (libRepo, lib)]), null!, options);
        var issues = new Fixture().Story(1, "Merged", title: ShopRed).Build().Issues.ToList();
        return (await reader.ReadRedFixes(issues, Flight.Empty, [], CancellationToken.None)).Candidates;
    }

    [Fact]
    public async Task AFixMergedInAPinnedRepositoryIsCutOnceItsPinLands()
    {
        var merged = DateTimeOffset.UtcNow.AddHours(-3);

        var waiting = await PinnedFix(merged, pinned: false);
        var landed = await PinnedFix(merged, pinned: true);

        Assert.Empty(waiting);
        Assert.Equal(new ReleaseCandidate(1, "release-shop.yml", "bump"), Assert.Single(landed));
    }

    [Fact]
    public async Task AReleaseOfTheProductAfterTheMergeShipsTheFix()
    {
        var merged = DateTimeOffset.UtcNow.AddHours(-2);

        var (candidates, shipped) = await RedFixes(merged, new Release("shop/v1.4.0", merged.AddHours(1)));

        Assert.Equal([1], shipped);
        Assert.Empty(candidates);
    }

    [Fact]
    public async Task AReleaseBeforeTheMergeOrOfAnotherProductDoesNot()
    {
        var merged = DateTimeOffset.UtcNow.AddHours(-2);

        var (candidates, shipped) = await RedFixes(merged, new Release("shop/v1.3.0", merged.AddHours(-1)), new Release("clinic/v2.0.0", merged.AddHours(1)));

        Assert.Empty(shipped);
        Assert.Equal([1], candidates.Select(c => c.Story));
    }

    [Fact]
    public void AStoryInQaOrClosedHoldsNothing()
    {
        Assert.Empty(SampleWorkers.Plan(Lit("QA").Build(), Options()).Andon);
        Assert.Empty(SampleWorkers.Plan(Lit("Closed").Build(), Options()).Andon);
    }

    [Fact]
    public void AnotherProductsShipIsNotReleaseWork()
    {
        var plan = SampleWorkers.Plan(Lit().Published("clinic", "clinic/v2.0.0").Build(), Options());

        Assert.DoesNotContain(plan.Items, i => i.Kind == WorkKind.Ship);
    }

    [Fact]
    public void TheRedStoryWaitingOnAHandSaysSo()
    {
        var plan = SampleWorkers.Plan(Lit().Build(), Options());
        var held = SampleWorkers.Plan(new Fixture().Story(1, "Ready", "ruling", title: ShopRed).Story(2, "Ready").Build(), Options());

        Assert.Contains("builder", Assert.Single(plan.Andon).Waits, StringComparison.Ordinal);
        Assert.Contains("`ruling`", Assert.Single(held.Andon).Waits, StringComparison.Ordinal);
        Assert.Empty(held.Items);
    }

    [Fact]
    public void ALitAndonWithNothingToStartAndNothingRunningStandsStillSinceItsFirstSuchPass()
    {
        var now = DateTimeOffset.UtcNow;
        var held = SampleWorkers.Plan(new Fixture().Story(1, "Ready", "ruling", title: ShopRed).Story(2, "Ready").Build(), Options());
        var working = SampleWorkers.Plan(Lit().Build(), Options());
        var unlit = SampleWorkers.Plan(new Fixture().Build(), Options());

        Assert.Equal(now, HarnessLoop.StillSince(null, held, nothingRuns: true, now));
        Assert.Equal(now.AddHours(-1), HarnessLoop.StillSince(now.AddHours(-1), held, nothingRuns: true, now));
        Assert.Null(HarnessLoop.StillSince(now.AddHours(-1), held, nothingRuns: false, now));
        Assert.Null(HarnessLoop.StillSince(now.AddHours(-1), working, nothingRuns: true, now));
        Assert.Null(HarnessLoop.StillSince(null, unlit, nothingRuns: true, now));
    }

    [Fact]
    public void TheAndonIsTheMostUrgentTurn()
    {
        Assert.True(Planner.Rank(ReleaseAndon.Tier) < Planner.Rank("bug"));
    }

    [Fact]
    public void TakeNewOffStillHoldsTheStoryTheAndonStarts()
    {
        var plan = SampleWorkers.Plan(Lit().Pr(1, 10, gate: CiState.Red).Build(), Options());
        var start = new WorkItem(WorkKind.Dev, 1, null, ReleaseAndon.Tier, "andon");
        var round = Assert.Single(plan.Items, i => i.Kind == WorkKind.Dev && i.Pr == 10);
        var cut = new WorkItem(WorkKind.Release, null, null, ReleaseAndon.Tier, "the day's cut", "release-shop.yml");

        Assert.True(HarnessLoop.StartsStory(start));
        Assert.False(HarnessLoop.StartsStory(round));
        Assert.False(HarnessLoop.StartsStory(cut));
    }
}

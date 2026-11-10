// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A merge in a pinned repository reaches the default branch with the pin that holds it: until then the
// default branch does not hold the story's work, and from then on that commit is the story's merge.
public class PinLandingTests
{
    static readonly DateTimeOffset At = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    static readonly Merge InLib = new("lib1", At);

    sealed class Reads
    {
        public int Changes { get; set; }
    }

    // The default repository with its own merge of the story (or none) and the commits that moved the
    // pin, each to a version; the pinned one with its merge and the versions whose tag holds it.
    static RepositorySet Repos(Merge? own, Merge? inLib, Reads reads, string[] holding, params (Merge Commit, string Version)[] bumps)
    {
        var app = new RepositoryOptions { Name = "acme/app" };
        var lib = new RepositoryOptions
        {
            Name = "acme/lib", Label = "area:lib",
            Pin = new PinOptions { File = "Directory.Build.props", Pattern = "<LibVersion>([^<]+)</LibVersion>", Ref = "v{version}" },
        };
        var appRepo = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => app.Name,
            ["MergeOf"] = _ => Task.FromResult(own),
            ["Changes"] = a =>
            {
                reads.Changes++;
                Assert.Equal("Directory.Build.props", a[0]);
                return Task.FromResult<IReadOnlyList<Merge>>(bumps.Select(b => b.Commit).Where(c => c.At >= (DateTimeOffset)a[1]!).ToList());
            },
            ["FileAt"] = a => Task.FromResult<string?>($"<Project><LibVersion>{bumps.Single(b => b.Commit.Sha == (string)a[1]!).Version}</LibVersion></Project>"),
        });
        var libRepo = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => lib.Name,
            ["MergeOf"] = _ => Task.FromResult(inLib),
            ["Holds"] = a => Task.FromResult(holding.Contains((string)a[0]!) && (string)a[1]! == inLib!.Sha),
        });
        return new RepositorySet([(appRepo, app), (libRepo, lib)]);
    }

    static (Merge Commit, string Version) Bump(string sha, int hoursAfter, string version)
    {
        return (new Merge(sha, At.AddHours(hoursAfter)), version);
    }

    [Fact]
    public async Task AMergeInAPinnedRepositoryLandsWithTheOldestPinThatHoldsIt()
    {
        var repos = Repos(null, InLib, new Reads(), ["v1.0.2", "v1.0.3"], Bump("b1", 1, "1.0.1"), Bump("b2", 2, "1.0.2"), Bump("b3", 3, "1.0.3"));

        Assert.Equal(new Merge("b2", At.AddHours(2)), await repos.LandingOf(7));
    }

    [Fact]
    public async Task UntilItsPinTheDefaultBranchDoesNotHoldTheStory()
    {
        var unpinned = Repos(null, InLib, new Reads(), []);
        var behind = Repos(new Merge("app1", At.AddHours(-1)), InLib, new Reads(), ["v1.0.2"], Bump("b1", 1, "1.0.1"));

        Assert.Null(await unpinned.LandingOf(7));
        Assert.Null(await behind.LandingOf(7));
    }

    [Fact]
    public async Task AStoryMergedOnlyInTheDefaultRepositoryLandsWithItsMerge()
    {
        var reads = new Reads();
        var own = new Merge("app1", At);
        var repos = Repos(own, null, reads, []);

        Assert.Equal(own, await repos.LandingOf(7));
        Assert.Equal(0, reads.Changes);
    }

    [Fact]
    public async Task OfAStorysTwoHalvesTheLaterOneIsItsLanding()
    {
        var after = new Merge("app1", At.AddHours(5));
        var before = new Merge("app0", At.AddHours(-5));

        Assert.Equal(after, await Repos(after, InLib, new Reads(), ["v1.0.1"], Bump("b1", 1, "1.0.1")).LandingOf(7));
        Assert.Equal("b1", (await Repos(before, InLib, new Reads(), ["v1.0.1"], Bump("b1", 1, "1.0.1")).LandingOf(7))?.Sha);
    }

    [Fact]
    public async Task AStoryWithNoMergeHasNoLanding()
    {
        Assert.Null(await Repos(null, null, new Reads(), []).LandingOf(7));
    }

    [Fact]
    public async Task APinOnceFoundIsNotLookedForAgain()
    {
        var reads = new Reads();
        var repos = Repos(null, InLib, reads, ["v1.0.1"], Bump("b1", 1, "1.0.1"));

        await repos.LandingOf(7);
        await repos.LandingOf(7);

        Assert.Equal(1, reads.Changes);
    }
}

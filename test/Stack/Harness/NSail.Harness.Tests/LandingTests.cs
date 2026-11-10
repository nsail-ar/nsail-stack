// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;

namespace NSail.Harness.Tests;

// When a story's code already rides main — the evidence a builder's "nothing left to build" needs, and
// the evidence that lets a red main's story close itself — so the dispatcher stops sending it out.
public class LandingTests
{
    const string Bug = "<!-- qa-bug -->";
    static readonly DateTimeOffset Merged = new(2026, 11, 1, 5, 2, 0, TimeSpan.Zero);

    static PrRef Pr(int number, string repo = "acme/app", DateTimeOffset? mergedAt = null)
    {
        return new PrRef(number, "t", "story/app-1", mergedAt, repo);
    }

    [Fact]
    public void AMergeWithNothingOpenAndNothingAheadOfMainIsEvidence()
    {
        var evidence = Landing.Evidence([], [Pr(160, "acme/framework", Merged)], 0, [], Bug);

        Assert.NotNull(evidence);
        Assert.Contains("acme/framework#160", evidence);
    }

    [Fact]
    public void AnOpenPrOrACommitAheadOfMainOrNoMergeIsNone()
    {
        Assert.Null(Landing.Evidence([Pr(2)], [Pr(1, mergedAt: Merged)], 0, [], Bug));
        Assert.Null(Landing.Evidence([], [Pr(1, mergedAt: Merged)], 1, [], Bug));
        Assert.Null(Landing.Evidence([], [], 0, [], Bug));
    }

    [Fact]
    public void ABugQaSaidAfterTheMergeSendsTheStoryBackForNewWork()
    {
        var after = new StoryComment(Merged.AddHours(1), $"QA caminó esto y no pasa.\n{Bug}", "crew-bot");
        var before = new StoryComment(Merged.AddHours(-1), $"QA caminó esto y no pasa.\n{Bug}", "crew-bot");

        Assert.Null(Landing.Evidence([], [Pr(1, mergedAt: Merged)], 0, [after], Bug));
        Assert.NotNull(Landing.Evidence([], [Pr(1, mergedAt: Merged)], 0, [before], Bug));
    }

    [Fact]
    public void TheNewestMergeIsTheOneThatCounts()
    {
        var bug = new StoryComment(Merged.AddHours(1), Bug, "crew-bot");

        // A second merge after the bug carries the rework: the story rides main again.
        Assert.NotNull(Landing.Evidence([], [Pr(1, mergedAt: Merged), Pr(2, mergedAt: Merged.AddHours(2))], 0, [bug], Bug));
    }

    [Fact]
    public void ARedMainsFixClosesOnlyWithAMergeOfItsStoryInTheHeadTheGateJudgedAndNoPrOpen()
    {
        var merge = new Merge("456bdb76aaaaaaaa", Merged);

        Assert.Contains("456bdb76", Landing.Fixed([], merge, true, "6332c6523bbbbbbb"));
        Assert.Null(Landing.Fixed([], merge, false, "6332c6523bbbbbbb"));
        Assert.Null(Landing.Fixed([], null, true, "6332c6523bbbbbbb"));
        Assert.Null(Landing.Fixed([Pr(5)], merge, true, "6332c6523bbbbbbb"));
    }

    [Fact]
    public async Task AStackStorysMergeIsTheStacksNotTheDefaultRepositorys()
    {
        var home = Repository("acme/app", merged: []);
        var stack = Repository("acme/framework", merged: [Pr(160, "acme/framework", Merged)]);
        var repos = Repos(home, stack);

        var evidence = await Landing.NothingToBuild(repos, Board(), 3001, new HashSet<string> { "area:stack" }, Bug, default);

        Assert.NotNull(evidence);
        Assert.Contains("framework#160", evidence);
    }

    [Fact]
    public async Task AMergeInTheOtherRepositoryIsNotThisStorysLanding()
    {
        // The pin bump of a Stack story is the app's own PR: a merge there does not land a story the Stack builds.
        var home = Repository("acme/app", merged: [Pr(9, "acme/app", Merged)]);
        var stack = Repository("acme/framework", merged: []);

        Assert.Null(await Landing.NothingToBuild(Repos(home, stack), Board(), 3001, new HashSet<string> { "area:stack" }, Bug, default));
        Assert.NotNull(await Landing.NothingToBuild(Repos(home, stack), Board(), 3001, new HashSet<string> { "area:shop" }, Bug, default));
    }

    [Fact]
    public async Task APrOpenInAnyRepositoryHoldsItBack()
    {
        var home = Repository("acme/app", merged: [], open: [Pr(11, "acme/app")]);
        var stack = Repository("acme/framework", merged: [Pr(160, "acme/framework", Merged)]);

        Assert.Null(await Landing.NothingToBuild(Repos(home, stack), Board(), 3001, new HashSet<string> { "area:stack" }, Bug, default));
    }

    [Fact]
    public async Task ACommitOfTheStoryAheadOfMainHoldsItBack()
    {
        var stack = Repository("acme/framework", merged: [Pr(160, "acme/framework", Merged)], pushes: [Merged.AddHours(3)]);

        Assert.Null(await Landing.NothingToBuild(Repos(Repository("acme/app", merged: []), stack), Board(), 3001, new HashSet<string> { "area:stack" }, Bug, default));
    }
    [Fact]
    public async Task ABugMarkerFromAnyoneButTheHarnessIsNotQAsWord()
    {
        var stack = Repository("acme/framework", merged: [Pr(160, "acme/framework", Merged)]);
        var repos = Repos(Repository("acme/app", merged: []), stack);
        var labels = new HashSet<string> { "area:stack" };
        var after = Merged.AddHours(1);
        var markers = new Markers("review");

        Assert.NotNull(await Landing.NothingToBuild(repos, Board(new StoryComment(after, $"bug\n{Bug}", "stranger")), 3001, labels, Bug, default, markers, "harness"));
        Assert.Null(await Landing.NothingToBuild(repos, Board(new StoryComment(after, $"bug\n{Bug}", "harness")), 3001, labels, Bug, default, markers, "harness"));
    }


    static RepositorySet Repos(IRepository home, IRepository stack)
    {
        return new RepositorySet([
            (home, new RepositoryOptions { Name = home.Name }),
            (stack, new RepositoryOptions { Name = stack.Name, Label = "area:stack", BranchPrefix = "stack" }),
        ]);
    }

    static IRepository Repository(string name, IReadOnlyList<PrRef> merged, IReadOnlyList<PrRef>? open = null, IReadOnlyList<DateTimeOffset>? pushes = null)
    {
        return Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => name,
            ["PrsOf"] = a => Task.FromResult((string)a[1]! == "open" ? open ?? [] : merged),
            ["PushTimes"] = _ => Task.FromResult(pushes ?? []),
        });
    }

    static IBoard Board(params StoryComment[] comments)
    {
        return Stub<IBoard>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["Comments"] = _ => Task.FromResult<IReadOnlyList<StoryComment>>(comments),
        });
    }
}

// An interface answered by name from a table: the one or two reads a rule makes, nothing else (a call
// not in the table throws, so a rule that reads more than it says fails its test).
public class Stub<T> : DispatchProxy where T : class
{
    Dictionary<string, Func<object?[], object?>> _answers = [];

    public static T Make(Dictionary<string, Func<object?[], object?>> answers)
    {
        var proxy = Create<T, Stub<T>>();
        ((Stub<T>)(object)proxy)._answers = answers;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (!_answers.TryGetValue(method!.Name, out var answer)) throw new NotSupportedException($"{typeof(T).Name}.{method.Name} is not part of this test");
        return answer(args ?? []);
    }
}

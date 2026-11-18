// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging.Abstractions;
using NSail.Harness.Board;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A story whose work another story's merge already did has no PR of its own: its analysis names the
// commits, and they stand for its merge wherever one is read — the red fix's cut, the shipped answer,
// the carry and the walk.
public class NamedFixTests : IDisposable
{
    const string ShopRed = "Regression red on main (Shop)";

    const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-fix-" + Guid.NewGuid().ToString("N"));

    string Remote => Path.Combine(_root, "remote.git");

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    static readonly Markers Marks = new("review", ["fleet"]);

    static StoryComment Said(string body, string author = "harness", int minutesAgo = 0)
    {
        return new StoryComment(DateTimeOffset.UtcNow.AddMinutes(-minutesAgo), body, author);
    }

    [Fact]
    public void TheFixIsReadWholeFromTheHarnesssOwnWordAndNobodyElses()
    {
        var own = Marks.FixOf([Said($"Nothing to build.\n\n{Markers.Fix([A, B])}")], "harness");
        var fleet = Marks.FixOf([Said(Markers.Fix([A]), author: "fleet")], "harness");
        var forged = Marks.FixOf([Said(Markers.Fix([A]), author: "mallory")], "harness");
        var quoted = Marks.FixOf([Said($"The marker is `{Markers.Fix([A])}`.")], "harness");
        var shortened = Marks.FixOf([Said("<!-- harness:fix aaaaaaa -->")], "harness");

        Assert.Equal([A, B], own!.Shas);
        Assert.Equal([A], fleet!.Shas);
        Assert.Null(forged);
        Assert.Null(quoted);
        Assert.Null(shortened);
    }

    [Fact]
    public void TheNewestWordIsTheFix()
    {
        var newest = Said(Markers.Fix([B]), minutesAgo: 1);

        var fix = Marks.FixOf([Said(Markers.Fix([A]), minutesAgo: 30), newest, Said("A later comment that names none.")], "harness");

        Assert.Equal([B], fix!.Shas);
        Assert.Equal(newest.At, fix.At);
    }

    [Fact]
    public void ASeatsWordsCarryNoFixMarker()
    {
        var words = $"Done.\n{Markers.Fix([A])}\n<!--harness:fix {B} -->";

        Assert.DoesNotContain("harness:fix", Markers.StripFix(words));
        Assert.DoesNotContain("harness:fix", Marks.Strip(words));
    }

    // The reader's half, over a Merged red story with no merge of its own.
    static async Task<(List<ReleaseCandidate> Candidates, HashSet<int> Shipped)> RedFixes(StoryComment? word, params Release[] releases)
    {
        var options = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        options.Workers["release-shop"] = new WorkerOptions { Type = "Release", Enabled = true, Product = "Shop", Workflow = "release-shop.yml" };
        var repo = Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["MergeOf"] = _ => Task.FromResult<Merge?>(null),
            ["RunsSince"] = _ => Task.FromResult(0),
            ["Login"] = _ => Task.FromResult("harness"),
        });
        var board = new MemoryBoard();
        var card = board.Add(1, title: ShopRed, stage: Stages.Merged);
        if (word is not null) card.Comments.Add(word);
        var reader = new SnapshotReader(board, new RepositorySet([(repo, options.Repositories[0])]), null!, options);
        return await reader.ReadRedFixes((await board.Stories()).ToList(), Flight.Empty, releases, CancellationToken.None);
    }

    [Fact]
    public async Task ARedStoryWhoseFixAnotherStoryMergedIsCutOnTheCommitItsAnalysisNamed()
    {
        var (candidates, shipped) = await RedFixes(Said(Markers.Fix([A, B]), minutesAgo: 10));

        Assert.Equal(new ReleaseCandidate(1, "release-shop.yml", B), Assert.Single(candidates));
        Assert.Empty(shipped);
    }

    [Fact]
    public async Task AMergedRedStoryThatNamesNoFixHasNothingToCut()
    {
        var (candidates, shipped) = await RedFixes(Said("Nothing to build: main already holds it."));

        Assert.Empty(candidates);
        Assert.Empty(shipped);
    }

    [Fact]
    public async Task OnlyAReleasePublishedAfterTheAnalysisNamedTheFixShipsIt()
    {
        var word = Said(Markers.Fix([A]), minutesAgo: 60);

        var before = await RedFixes(word, new Release("shop/v1.3.0", word.At.AddMinutes(-5)));
        var after = await RedFixes(word, new Release("shop/v1.4.0", word.At.AddMinutes(5)));

        Assert.Empty(before.Shipped);
        Assert.Equal([1], before.Candidates.Select(c => c.Story));
        Assert.Equal([1], after.Shipped);
        Assert.Empty(after.Candidates);
    }

    [Fact]
    public void TheCarryTakesTheCommitsNamedByShaBesideTheStorysOwnMerges()
    {
        var log = $"1111111111111111111111111111111111111111 another story (#40)\n{A} the fix, another story's (#41)\n2222222222222222222222222222222222222222 this story's own (#42)\n{B} the guide (#43)";

        Assert.Equal([A, "2222222222222222222222222222222222222222", B], ReleaseJob.FixesIn(log, [42], [B, A]));
        Assert.Equal(["2222222222222222222222222222222222222222"], ReleaseJob.FixesIn(log, [42]));
    }

    async Task<string> Commit(string seed, string message)
    {
        await Shell.Git(seed, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", message);
        return (await Shell.Git(seed, "rev-parse", "HEAD")).Trim();
    }

    [Fact]
    public async Task TheWalkMovesAStoryWithNoPrWhoseNamedCommitsTheReleaseHolds()
    {
        Directory.CreateDirectory(_root);
        await Shell.Git(_root, "init", "-q", "--bare", "-b", "main", Remote);
        var seed = Path.Combine(_root, "seed");
        await Shell.Git(_root, "clone", "-q", Remote, seed);
        await Shell.Git(seed, "checkout", "-q", "-b", "main");
        await Commit(seed, "the start");
        await Shell.Git(seed, "tag", "shop/v1.0.0");
        var fix = await Commit(seed, "another story's merge, which is the fix");
        await Shell.Git(seed, "tag", "shop/v1.1.0");
        var later = await Commit(seed, "main's newer work");
        await Shell.Git(seed, "push", "-q", "origin", "main", "--tags");

        var options = new HarnessOptions
        {
            Machine = "box",
            WorkRoot = Path.Combine(_root, "work"),
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Workers =
            {
                ["release-shop"] = new WorkerOptions { Type = "Release", Enabled = true, Product = "Shop", Workflow = "release-shop.yml" },
                ["release-clinic"] = new WorkerOptions { Type = "Release", Enabled = true, Product = "Clinic", Workflow = "release-clinic.yml" },
            },
        };
        var board = new MemoryBoard();
        board.Add(1, title: ShopRed, stage: Stages.Merged).Comments.Add(Said(Markers.Fix([fix])));
        board.Add(2, stage: Stages.Merged).Comments.Add(Said(Markers.Fix([fix, later])));
        board.Add(3, title: "Regression red on main (Clinic)", stage: Stages.Merged).Comments.Add(Said(Markers.Fix([fix])));
        board.Add(4, stage: Stages.Merged);
        var repo = new DevJobRunTests.FileRepository(Remote);
        var workspace = new Workspace(Path.Combine(_root, "ws"), Remote, null, 1, "Gru", "gru@example.com");
        var ctx = new JobContext(board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace> { [repo.Name] = workspace },
            new NoAgent(), options, [], NullLoggerFactory.Instance, new BuildLeases(1));
        var job = new ShipJob(new WorkItem(WorkKind.Ship, null, null, "release", "shop/v1.1.0 published", "release-shop.yml", Worker: "release-shop", ProductName: "Shop", Tag: "shop/v1.1.0"), ctx);

        await job.Run(1, Path.Combine(_root, "job.log"), CancellationToken.None);

        Assert.Equal(Stages.Released, board.Cards[1].Stage);
        Assert.Equal(Stages.Merged, board.Cards[2].Stage);
        Assert.Equal(Stages.Merged, board.Cards[3].Stage);
        Assert.Equal(Stages.Merged, board.Cards[4].Stage);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Agents;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A whole analysis against a file:// remote: the seat reads its prompt and leaves its verdict (Ready,
// with or without a protected.txt; a ruling; a split into parts), and what reaches the board is read back.
public class AnalyzeJobRunTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-analyze-" + Guid.NewGuid().ToString("N"));

    string Remote => Path.Combine(_root, "remote.git");

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    // A seat that writes a Ready verdict and, when given, the paths its story's change must touch.
    sealed class Analyst(string? protectedPaths, string verdict = "ready", string? labels = null) : IAgent
    {
        public string Prompt { get; private set; } = "";

        // The parts of a split, in order: each one's files by name.
        public List<Dictionary<string, string>> Parts { get; } = [];

        public string Driver => "test";

        public async Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            Prompt = run.Prompt;
            var dir = Path.Combine(run.WorkDir, ".analysis");
            await File.WriteAllTextAsync(Path.Combine(dir, "verdict.txt"), $"VERDICT: {verdict}\n", ct);
            await File.WriteAllTextAsync(Path.Combine(dir, "body.md"), "The shaped body.", ct);
            await File.WriteAllTextAsync(Path.Combine(dir, "comment.md"), "Claim, Done, Readers.", ct);
            if (protectedPaths is not null) await File.WriteAllTextAsync(Path.Combine(dir, "protected.txt"), protectedPaths, ct);
            if (labels is not null) await File.WriteAllTextAsync(Path.Combine(dir, "labels.txt"), labels, ct);
            for (var i = 0; i < Parts.Count; i++)
            {
                var part = Directory.CreateDirectory(Path.Combine(dir, "parts", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))).FullName;
                foreach (var (name, text) in Parts[i]) await File.WriteAllTextAsync(Path.Combine(part, name), text, ct);
            }
            return new AgentResult(0, false, null, false, null);
        }

        public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
        {
            return new Dictionary<string, string>();
        }

        public (bool Refused, DateTimeOffset? Until) Quota(string output)
        {
            return (false, null);
        }
    }

    async Task<MemoryBoard> Analyze(Analyst seat, params string[] labels)
    {
        var (board, outcome) = await Run(seat, "a story", labels);
        Assert.Equal("Ready", outcome.Event);
        return board;
    }

    Task<(MemoryBoard Board, JobOutcome Outcome)> Run(Analyst seat, string title, params string[] labels)
    {
        return Run(seat, title, null, labels);
    }

    async Task<(MemoryBoard Board, JobOutcome Outcome)> Run(Analyst seat, string title, Action<MemoryBoard>? arrange, params string[] labels)
    {
        if (!Directory.Exists(Remote))
        {
            Directory.CreateDirectory(_root);
            await Shell.Git(_root, "init", "-q", "--bare", "-b", "main", Remote);
            var seed = Path.Combine(_root, "seed");
            await Shell.Git(_root, "clone", "-q", Remote, seed);
            await Shell.Git(seed, "checkout", "-q", "-b", "main");
            // The repository ignores the analysis's folder, as a product's does: cleaning the slot leaves it.
            await File.WriteAllTextAsync(Path.Combine(seed, ".gitignore"), ".analysis/\n");
            await Shell.Git(seed, "add", ".gitignore");
            await Shell.Git(seed, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "-m", "M");
            await Shell.Git(seed, "push", "-q", "origin", "main");
        }

        var board = new MemoryBoard();
        board.Add(1, title: title, stage: Stages.Analyze, labels: labels);
        board.Add(2, stage: Stages.Closed, labels: [Labels.NoWalk, Labels.P1, "area:shop", "other"]);
        arrange?.Invoke(board);
        var options = new HarnessOptions
        {
            WorkRoot = Path.Combine(_root, "work"),
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            ProtectedPaths = [".github/workflows/**", "scripts/ci/*.sh"],
            Seats = { ["timo"] = new AgentOptions { Signature = "— Timo", Model = "m", Prompt = "analyze __ISSUE__; the screen holds __PROTECTED__" } },
            Workers = { ["analyze"] = new WorkerOptions { Type = "Analyze", Seat = "timo" } },
        };
        var repo = new DevJobRunTests.FileRepository(Remote);
        var workspace = new Workspace(Path.Combine(_root, "ws"), Remote, null, 1, "Timo", "timo@example.com");
        var ctx = new JobContext(board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace> { [repo.Name] = workspace },
            seat, options, [], new DevJobRunTests.Lines(), new BuildLeases(1));
        var job = new AnalyzeJob(new WorkItem(WorkKind.Analyze, 1, null, "rest", "rest", Worker: "analyze"), ctx, new JobHistory(Path.Combine(_root, "history")));
        return (board, await job.Run(1, Path.Combine(_root, "job.log"), CancellationToken.None));
    }

    [Fact]
    public async Task TheAnalystMarksAStoryNoWalkAndNeverAHandsLabel()
    {
        var board = await Analyze(new Analyst(null, labels: "no-walk\narea:shop\np1\nother\n"));

        Assert.Superset(new HashSet<string> { Labels.NoWalk, "area:shop" }, board.Cards[1].Labels);
        Assert.DoesNotContain(Labels.P1, board.Cards[1].Labels);
        Assert.DoesNotContain("other", board.Cards[1].Labels);
    }

    [Fact]
    public async Task TheAnalystsPromptNamesTheConfiguredProtectedPaths()
    {
        var seat = new Analyst(null);

        await Analyze(seat);

        Assert.Contains("the screen holds `.github/workflows/**`, `scripts/ci/*.sh`", seat.Prompt);
    }

    [Fact]
    public async Task AShapedStoryThatMustTouchAProtectedPathWaitsForAllowProtected()
    {
        var board = await Analyze(new Analyst(".github/workflows/ci.yml\nsrc/App/Program.cs\n"));

        Assert.Superset(new HashSet<string> { Labels.Ruling, Labels.Screened }, board.Cards[1].Labels);
        var asked = board.Cards[1].Comments.Last().Body;
        Assert.Contains("`.github/workflows/ci.yml`", asked);
        Assert.DoesNotContain("Program.cs", asked);
        Assert.Contains($"`{Labels.AllowProtected}`", asked);
    }

    [Fact]
    public async Task AStoryThatTouchesNoProtectedPathEndsAsBefore()
    {
        var board = await Analyze(new Analyst("src/App/Program.cs\n"));

        Assert.DoesNotContain(Labels.Ruling, board.Cards[1].Labels);
        Assert.DoesNotContain(Labels.Screened, board.Cards[1].Labels);
        Assert.Equal("Claim, Done, Readers.", Assert.Single(board.Cards[1].Comments).Body);
    }

    [Fact]
    public async Task WhatAnEarlierAnalysisLeftInTheSlotIsNotReadAsThisOnes()
    {
        await Analyze(new Analyst(".github/workflows/ci.yml\n"));

        var board = await Analyze(new Analyst(null));

        Assert.DoesNotContain(Labels.Ruling, board.Cards[1].Labels);
        Assert.Single(board.Cards[1].Comments);
    }

    [Fact]
    public async Task AStoryAlreadyAllowedEndsAsBefore()
    {
        var board = await Analyze(new Analyst(".github/workflows/ci.yml\n"), Labels.AllowProtected);

        Assert.DoesNotContain(Labels.Ruling, board.Cards[1].Labels);
        Assert.Single(board.Cards[1].Comments);
    }

    [Fact]
    public async Task AReleaseRedsAnalysisIsNeverARulingAndTheAskIsCountedAsADeath()
    {
        var (board, outcome) = await Run(new Analyst(null, "ruling"), "Regression red on main (Acme)");

        Assert.NotEqual("Ruling", outcome.Event);
        Assert.DoesNotContain(Labels.Ruling, board.Cards[1].Labels);
        Assert.Contains(board.Cards[1].Comments, c => c.Body.Contains("never `ruling`"));
        Assert.Equal(Stages.Analyze, board.Cards[1].Stage);
    }

    [Fact]
    public async Task AnOrdinaryStorysRulingStillWaitsForAHand()
    {
        var (board, outcome) = await Run(new Analyst(null, "ruling"), "a story");

        Assert.Equal("Ruling", outcome.Event);
    }

    // A change in the library and its use in the product: the second waits on the first.
    static Analyst Splitter()
    {
        var seat = new Analyst(null, "split");
        seat.Parts.Add(new() { ["title.txt"] = "The library part\n", ["body.md"] = "Add the seam.", ["comment.md"] = "Claim, Done, Readers.", ["labels.txt"] = "no-walk\np1\nother\n" });
        seat.Parts.Add(new() { ["title.txt"] = "The product part", ["body.md"] = "Use the seam. Blocked by __PART_1__." });
        return seat;
    }

    [Fact]
    public async Task ASplitFilesItsPartsUnderTheStoryEachWhereItsNextJobReadsIt()
    {
        var (board, outcome) = await Run(Splitter(), "a story", "area:shop");

        Assert.Equal("Split", outcome.Event);
        Assert.Equal([100, 101], board.Cards[1].Children);
        var first = board.Cards[100];
        Assert.Equal("The library part", first.Title);
        Assert.Equal(Stages.Ready, first.Stage);
        Assert.Equal("Claim, Done, Readers.", Assert.Single(first.Comments).Body);
        Assert.Equal(new HashSet<string> { "area:shop", Labels.NoWalk }, first.Labels);
        var second = board.Cards[101];
        Assert.Equal("Use the seam. Blocked by #100.", second.Body);
        Assert.Equal(Stages.Backlog, second.Stage);
        Assert.Equal([100], second.Blockers);
        Assert.Contains(Labels.Blocked, second.Labels);
        Assert.Contains("area:shop", second.Labels);
        Assert.Equal("Claim, Done, Readers.", board.Cards[1].Comments[0].Body);
        Assert.Contains("#100, #101", board.Cards[1].Comments[1].Body);
    }

    [Fact]
    public async Task APartWithNoRefineryCommentAndNothingToWaitOnIsAnalyzedAsAnyFiledStory()
    {
        var seat = Splitter();
        seat.Parts[1]["body.md"] = "Use the seam.";

        var (board, _) = await Run(seat, "a story");

        Assert.Equal(Stages.Analyze, board.Cards[101].Stage);
        Assert.DoesNotContain(Labels.Blocked, board.Cards[101].Labels);
    }

    [Fact]
    public async Task AShapedPartThatMustTouchAProtectedPathWaitsForAllowProtected()
    {
        var seat = Splitter();
        seat.Parts[0]["protected.txt"] = ".github/workflows/ci.yml\n";

        var (board, _) = await Run(seat, "a story");

        Assert.Superset(new HashSet<string> { Labels.Ruling, Labels.Screened }, board.Cards[100].Labels);
        Assert.Equal(Stages.Ready, board.Cards[100].Stage);
    }

    [Theory]
    [InlineData("Use the seam of __PART_2__.")]
    [InlineData("Use the seam of __PART_3__.")]
    public async Task APartThatNamesItselfOrALaterOneIsNoSplit(string body)
    {
        var seat = Splitter();
        seat.Parts[1]["body.md"] = body;

        var (board, outcome) = await Run(seat, "a story");

        Assert.True(outcome.Failed);
        Assert.Empty(board.Cards[1].Children);
        Assert.DoesNotContain(100, board.Cards.Keys);
    }

    [Fact]
    public async Task OnePartIsNoSplit()
    {
        var seat = Splitter();
        seat.Parts.RemoveAt(1);

        var (board, outcome) = await Run(seat, "a story");

        Assert.True(outcome.Failed);
        Assert.DoesNotContain(100, board.Cards.Keys);
    }

    [Fact]
    public async Task AReleaseRedIsNeverSplit()
    {
        var (board, outcome) = await Run(Splitter(), "Regression red on main (Acme)");

        Assert.NotEqual("Split", outcome.Event);
        Assert.DoesNotContain(100, board.Cards.Keys);
    }

    [Fact]
    public async Task AStalledStoryIsNeverSplit()
    {
        var (board, outcome) = await Run(Splitter(), "a story", Labels.Stalled);

        Assert.NotEqual("Split", outcome.Event);
        Assert.DoesNotContain(100, board.Cards.Keys);
    }

    [Fact]
    public async Task ASplitCutShortWaitsForAHandInsteadOfBeingFiledAgain()
    {
        var (board, outcome) = await Run(Splitter(), "a story", b => b.CreatesLeft = 1);

        Assert.True(outcome.Failed);
        Assert.Null(outcome.Event);
        Assert.Superset(new HashSet<string> { Labels.Stalled, Labels.Ruling }, board.Cards[1].Labels);
        Assert.Contains("#100", Assert.Single(board.Cards[1].Comments).Body);
        Assert.Equal([100], board.Cards[1].Children);
    }
}

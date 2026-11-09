// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Agents;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A whole analysis against a file:// remote: the seat reads its prompt and leaves a Ready verdict,
// with or without a protected.txt, and what reaches the board is read back.
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
    sealed class Analyst(string? protectedPaths) : IAgent
    {
        public string Prompt { get; private set; } = "";

        public string Driver => "test";

        public async Task<AgentResult> Run(AgentRun run, CancellationToken ct)
        {
            Prompt = run.Prompt;
            var dir = Path.Combine(run.WorkDir, ".analysis");
            await File.WriteAllTextAsync(Path.Combine(dir, "verdict.txt"), "VERDICT: ready\n", ct);
            await File.WriteAllTextAsync(Path.Combine(dir, "body.md"), "The shaped body.", ct);
            await File.WriteAllTextAsync(Path.Combine(dir, "comment.md"), "Claim, Done, Readers.", ct);
            if (protectedPaths is not null) await File.WriteAllTextAsync(Path.Combine(dir, "protected.txt"), protectedPaths, ct);
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
        Directory.CreateDirectory(_root);
        await Shell.Git(_root, "init", "-q", "--bare", "-b", "main", Remote);
        var seed = Path.Combine(_root, "seed");
        await Shell.Git(_root, "clone", "-q", Remote, seed);
        await Shell.Git(seed, "checkout", "-q", "-b", "main");
        await Shell.Git(seed, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", "M");
        await Shell.Git(seed, "push", "-q", "origin", "main");

        var board = new MemoryBoard();
        board.Add(1, title: "a story", stage: Stages.Analyze, labels: labels);
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
        var outcome = await job.Run(1, Path.Combine(_root, "job.log"), CancellationToken.None);
        Assert.Equal("Ready", outcome.Event);
        return board;
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
    public async Task AStoryAlreadyAllowedEndsAsBefore()
    {
        var board = await Analyze(new Analyst(".github/workflows/ci.yml\n"), Labels.AllowProtected);

        Assert.DoesNotContain(Labels.Ruling, board.Cards[1].Labels);
        Assert.Single(board.Cards[1].Comments);
    }
}

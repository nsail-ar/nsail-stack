// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Agents;
using NSail.Harness.Jobs;
using NSail.Harness.Lanes;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// What the reviewer's verdict file does to the PR, the story and the board.
public class ReviewDecideTests : IDisposable
{
    const string Sha = "abc1234";
    readonly string _root = Path.Combine(Path.GetTempPath(), "harness-decide-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    sealed class Repo(string remote) : DevJobRunTests.FileRepository(remote)
    {
        public List<PrNote> Before { get; } = [];

        public List<int> Merges { get; } = [];

        public string? HeadSha { get; set; }

        public override Task<string> Login(CancellationToken ct = default)
        {
            return Task.FromResult("bot");
        }

        public override Task<IReadOnlyList<PrNote>> Thread(int pr, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PrNote>>([.. Before, .. Said.Select(s => new PrNote(DateTimeOffset.UtcNow, s.Body, "bot"))]);
        }

        public override Task<PullRequest> Pr(int number, CancellationToken ct = default)
        {
            return Task.FromResult(new PullRequest(number, "a story", true, "story/app-1", HeadSha ?? Sha, false));
        }

        public override Task Merge(int pr, string sha, CancellationToken ct = default)
        {
            Merges.Add(pr);
            return Task.CompletedTask;
        }
    }

    (ReviewJob Job, Repo Repo, MemoryBoard Board) Arrange()
    {
        Directory.CreateDirectory(_root);
        var options = new HarnessOptions
        {
            WorkRoot = Path.Combine(_root, "work"),
            Repositories = [new RepositoryOptions { Name = "acme/app" }],
            Seats = { ["vigia"] = new AgentOptions { Signature = "— Vigía", Model = "m", Prompt = "review" } },
            Workers = { ["review"] = new WorkerOptions { Type = "Review", Seat = "vigia" } },
        };
        var repo = new Repo(Path.Combine(_root, "remote.git"));
        var board = new MemoryBoard();
        board.Add(1, title: "a story", stage: Stages.InProgress, labels: [Labels.Review, Labels.Claimed]);
        var workspace = new Workspace(Path.Combine(_root, "ws"), repo.CloneUrl, null, 1, "Gru", "gru@example.com");
        var ctx = new JobContext(board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace> { [repo.Name] = workspace },
            new DevJobRunTests.NoSeat(), options, [], new DevJobRunTests.Lines(), new BuildLeases(1));
        var job = new ReviewJob(new WorkItem(WorkKind.Review, 1, 7, "rest", "rest", Worker: "review"), ctx, new JobHistory(Path.Combine(_root, "history")));
        return (job, repo, board);
    }

    async Task<Ending> Decide(ReviewJob job, string? verdict, AgentResult? result = null)
    {
        var file = Path.Combine(_root, ".review.md");
        if (verdict is not null) await File.WriteAllTextAsync(file, verdict);
        return await job.Decide(7, 1, Sha, "story/app-1", file, result ?? new AgentResult(0, false, null, false, null), new DevJobRunTests.Lines().CreateLogger("t"), CancellationToken.None);
    }

    [Fact]
    public async Task NoVerdictFileStallsTheStoryAndMergesNothing()
    {
        var (job, repo, board) = Arrange();

        var ending = await Decide(job, null);

        Assert.True(ending.Failed);
        Assert.Empty(repo.Merges);
        Assert.Contains(Labels.Stalled, board.Cards[1].Labels);
        Assert.DoesNotContain(Labels.Review, board.Cards[1].Labels);
        Assert.Equal(Stages.Analyze, board.Cards[1].Stage);
    }

    [Fact]
    public async Task AQuotaRefusalWithoutAVerdictFreesTheStoryWithoutStallingIt()
    {
        var (job, repo, board) = Arrange();

        var ending = await Decide(job, null, new AgentResult(1, false, null, true, null));

        Assert.False(ending.Failed);
        Assert.Empty(repo.Merges);
        Assert.DoesNotContain(Labels.Stalled, board.Cards[1].Labels);
        Assert.DoesNotContain(Labels.Review, board.Cards[1].Labels);
    }

    [Fact]
    public async Task CleanMergesTheReadHeadAndEndsClean()
    {
        var (job, repo, board) = Arrange();

        var ending = await Decide(job, "VERDICT: clean\nAll good.");

        Assert.Equal("Clean", ending.Event);
        Assert.Equal([7], repo.Merges);
        Assert.Single(repo.Said);
        Assert.Contains($"harness:review:clean:{Sha}", repo.Said[0].Body);
        Assert.DoesNotContain("VERDICT", repo.Said[0].Body);
        Assert.DoesNotContain(Labels.Review, board.Cards[1].Labels);
    }

    [Fact]
    public async Task CleanOnAHeadThatMovedDuringTheReadMergesNothing()
    {
        var (job, repo, _) = Arrange();
        repo.HeadSha = "def5678";

        var ending = await Decide(job, "VERDICT: clean\nAll good.");

        Assert.Null(ending.Event);
        Assert.Empty(repo.Merges);
    }

    [Fact]
    public async Task FindingsSendTheStoryToAnotherRound()
    {
        var (job, repo, board) = Arrange();

        var ending = await Decide(job, "VERDICT: findings\n1. a bug");

        Assert.Equal("Findings", ending.Event);
        Assert.False(ending.Failed);
        Assert.Empty(repo.Merges);
        Assert.DoesNotContain(Labels.Review, board.Cards[1].Labels);
    }

    [Fact]
    public async Task FindingsOnTheFourthReadStallTheStory()
    {
        var (job, repo, board) = Arrange();
        foreach (var sha in new[] { "a1", "b2", "c3" }) repo.Before.Add(new PrNote(DateTimeOffset.UtcNow.AddHours(-1), $"x\n\n<!-- harness:review:findings:{sha} -->", "bot"));

        var ending = await Decide(job, "VERDICT: findings\n1. still a bug");

        Assert.True(ending.Failed);
        Assert.Contains(Labels.Stalled, board.Cards[1].Labels);
        Assert.Equal(Stages.Analyze, board.Cards[1].Stage);
    }

    [Fact]
    public async Task ARulingSendsTheStoryBackToAnalyzeWithTheWordsOnIt()
    {
        var (job, repo, board) = Arrange();

        var ending = await Decide(job, "VERDICT: ruling\nThe criteria contradict.");

        Assert.Equal("Ruling", ending.Event);
        Assert.Empty(repo.Merges);
        Assert.Contains(board.Cards[1].Comments, c => c.Body.Contains("The criteria contradict."));
    }

    [Fact]
    public async Task AnUnknownFirstLineIsNoVerdict()
    {
        var (job, repo, board) = Arrange();

        var ending = await Decide(job, "looks fine to me");

        Assert.True(ending.Failed);
        Assert.Empty(repo.Merges);
        Assert.Contains(Labels.Stalled, board.Cards[1].Labels);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

// main's head is judged whenever it moves past what the gate last judged, before anything else; its
// red reopens a landed red-main story only at a head that holds the story's merge.
public class MainGateTests
{
    const string Red = "CI red on main: Foo";

    [Fact]
    public void AMovedMainIsGatedFirst()
    {
        var board = new Fixture().Story(1, "Ready", "p1").Build() with { MainSha = "bbb", MainGated = "aaa" };

        var plan = SampleWorkers.Plan(board, new PlannerOptions(Gate: true));

        Assert.Equal(WorkKind.Main, plan.Items[0].Kind);
        Assert.Equal("bbb", plan.Items[0].Sha);
    }

    [Fact]
    public void AJudgedHeadIsNotGatedAgain()
    {
        var board = new Fixture().Build() with { MainSha = "aaa", MainGated = "aaa" };

        Assert.DoesNotContain(SampleWorkers.Plan(board, new PlannerOptions(Gate: true)).Items, i => i.Kind == WorkKind.Main);
    }

    [Fact]
    public void AGateOfMainAlreadyRunningIsNotSentTwice()
    {
        var board = new Fixture().Build() with { MainSha = "bbb", MainGated = "aaa" };
        board = board with { Flight = board.Flight with { MainGating = true } };

        Assert.DoesNotContain(SampleWorkers.Plan(board, new PlannerOptions(Gate: true)).Items, i => i.Kind == WorkKind.Main);
    }

    static MainGateJob Job(MemoryBoard board, DevJobRunTests.FileRepository repo, string head, DevJobRunTests.Lines? logs = null)
    {
        var options = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        var ctx = new JobContext(board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace>(),
            new NoAgent(), options, [], logs ?? new DevJobRunTests.Lines(), new BuildLeases(1));
        return new MainGateJob(new WorkItem(WorkKind.Main, null, null, "main", "main moved", Sha: head), ctx);
    }

    static async Task ReportRed(MemoryBoard board, MainHistory main, Merge? merge, string head)
    {
        var job = Job(board, new DevJobRunTests.FileRepository(main.Dir) { Landed = merge }, head);
        await job.ReportRed(head, main.Dir, null, "red", "Foo", "gate.log", CancellationToken.None);
    }

    static async Task<JobOutcome> Green(MemoryBoard board, MainHistory main, DevJobRunTests.FileRepository repo, string head, DevJobRunTests.Lines? logs = null)
    {
        return await Job(board, repo, head, logs).Green(head, main.Dir, CancellationToken.None);
    }

    static int Closings(MemoryBoard board, int story)
    {
        var said = new HarnessTexts().MainRedFixed.Split('{')[0];
        return board.Cards[story].Comments.Count(c => c.Body.Contains(said, StringComparison.Ordinal));
    }

    static ProviderException Refused()
    {
        return new ProviderException("GitHub 403: API rate limit exceeded", 403, 0, TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(Stages.Merged)]
    [InlineData(Stages.Released)]
    [InlineData(Stages.Qa)]
    public async Task ARedAtAHeadOlderThanTheFixOnlyComments(string stage)
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();
        board.Add(1, title: Red, stage: stage);

        await ReportRed(board, main, main.Merge, main.Parent);

        var comment = Assert.Single(board.Cards[1].Comments);
        Assert.Contains($"does not hold this story's merge `{main.Fix[..8]}`", comment.Body, StringComparison.Ordinal);
        Assert.Equal(stage, board.Cards[1].Stage);
    }

    [Fact]
    public async Task ARedAtAHeadThatHoldsTheFixReopensTheStory()
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();
        board.Add(1, title: Red, stage: Stages.Merged);

        await ReportRed(board, main, main.Merge, main.Fix);

        var comment = Assert.Single(board.Cards[1].Comments);
        Assert.DoesNotContain("does not hold", comment.Body, StringComparison.Ordinal);
        Assert.Equal(Stages.Ready, board.Cards[1].Stage);
    }

    [Fact]
    public async Task ARedOnAStoryWithNoMergeReopensIt()
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();
        board.Add(1, title: Red, stage: Stages.Qa);

        await ReportRed(board, main, null, main.Parent);

        Assert.Equal(Stages.Ready, board.Cards[1].Stage);
    }

    [Fact]
    public async Task ARedWithNoOpenStoryFilesOneInReady()
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();

        await ReportRed(board, main, null, main.Fix);

        var (_, card) = Assert.Single(board.Cards);
        Assert.StartsWith(Red, card.Title);
        Assert.Equal(Stages.Ready, card.Stage);
    }

    [Fact]
    public async Task AGreenHeadThatHoldsTheFixClosesTheStoryOnce()
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();
        board.Add(1, title: Red, stage: Stages.Merged);

        var outcome = await Green(board, main, new DevJobRunTests.FileRepository(main.Dir) { Landed = main.Merge }, main.Fix);

        Assert.False(outcome.Failed);
        Assert.Equal(Stages.Closed, board.Cards[1].Stage);
        Assert.NotNull(board.Cards[1].ClosedAt);
        Assert.Equal(1, Closings(board, 1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AClosePartwayRefusedIsFinishedByTheNextGreenGateWithoutASecondComment(bool stageWrite)
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();
        board.Add(1, title: Red, stage: Stages.Merged);
        if (stageWrite) board.NextSetStageThrows = Refused();
        else board.NextCloseThrows = Refused();
        var repo = new DevJobRunTests.FileRepository(main.Dir) { Landed = main.Merge };
        var logs = new DevJobRunTests.Lines();

        var first = await Green(board, main, repo, main.Fix, logs);

        Assert.True(first.Failed);
        Assert.Contains("could not close #1", first.Summary, StringComparison.Ordinal);
        Assert.Contains(logs.Written, l => l.Level == LogLevel.Error && l.Text.Contains("red main fixed but not closed", StringComparison.Ordinal));
        Assert.Null(board.Cards[1].ClosedAt);

        var second = await Green(board, main, repo, main.Fix);

        Assert.False(second.Failed);
        Assert.Equal(Stages.Closed, board.Cards[1].Stage);
        Assert.NotNull(board.Cards[1].ClosedAt);
        Assert.Equal(1, Closings(board, 1));
    }

    [Fact]
    public async Task ANewFixAfterAPartialCloseIsSaidAgain()
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();
        board.Add(1, title: Red, stage: Stages.Merged);
        board.NextCloseThrows = Refused();
        var repo = new DevJobRunTests.FileRepository(main.Dir) { Landed = main.Merge };
        await Green(board, main, repo, main.Fix);

        repo.Landed = new Merge(main.Fix, DateTimeOffset.UtcNow.AddMinutes(1));
        await Green(board, main, repo, main.Fix);

        Assert.Equal(2, Closings(board, 1));
        Assert.NotNull(board.Cards[1].ClosedAt);
    }

    public static TheoryData<string> Unfixed => ["open PR", "no merge", "head without the merge"];

    [Theory]
    [MemberData(nameof(Unfixed))]
    public async Task ARedStoryWithoutTheFixOnTheGreenHeadStaysOpenAndUntouched(string why)
    {
        using var main = await MainHistory.Create();
        var board = new MemoryBoard();
        board.Add(1, title: Red, stage: Stages.Merged);
        var repo = new DevJobRunTests.FileRepository(main.Dir) { Landed = why == "no merge" ? null : main.Merge };
        if (why == "open PR") repo.Open.Add(new PrRef(7, "nsail#1: fix", "story/nsail-1", null, "acme/app"));

        var outcome = await Green(board, main, repo, why == "head without the merge" ? main.Parent : main.Fix);

        Assert.False(outcome.Failed);
        Assert.Null(board.Cards[1].ClosedAt);
        Assert.Equal(Stages.Merged, board.Cards[1].Stage);
        Assert.Empty(board.Cards[1].Comments);
    }
}

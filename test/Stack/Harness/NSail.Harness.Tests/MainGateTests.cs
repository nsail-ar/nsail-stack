// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    static async Task ReportRed(MemoryBoard board, MainHistory main, Merge? merge, string head)
    {
        var options = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/app" }] };
        var repo = new DevJobRunTests.FileRepository(main.Dir) { Landed = merge };
        var ctx = new JobContext(board, new RepositorySet([(repo, options.Repositories[0])]), new Dictionary<string, Workspace>(),
            new NoAgent(), options, [], new DevJobRunTests.Lines(), new BuildLeases(1));
        var job = new MainGateJob(new WorkItem(WorkKind.Main, null, null, "red", "main moved", Sha: head), ctx);
        await job.ReportRed(head, main.Dir, null, "red", "Foo", "gate.log", CancellationToken.None);
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
}

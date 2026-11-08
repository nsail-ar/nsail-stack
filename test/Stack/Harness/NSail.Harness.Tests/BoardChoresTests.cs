// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// A keeper's move is planned from the pass's read and written later: a column a hand set between
// the two stays, and the story gets nothing else that pass.
public class BoardChoresTests
{
    sealed class Lines : ILogger
    {
        public List<string> Written { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Written.Add(formatter(state, exception));
        }
    }

    // The pass's read is the fixture; the board and the open PRs are what the chores find when they
    // are written.
    static async Task<Lines> Run(Fixture read, MemoryBoard board, params OpenPr[] opened)
    {
        var snapshot = read.Build();
        var plan = SampleWorkers.Plan(snapshot);
        var log = new Lines();
        var reader = new SnapshotReader(board, Repos(opened), null!, SampleWorkers.Options);
        await BoardChores.Do(board, plan.Chores, BoardChores.ReadOf(snapshot.Issues), reader.OpenPrOf, "— harness", log, CancellationToken.None);
        return log;
    }

    static RepositorySet Repos(OpenPr[] opened)
    {
        return new RepositorySet(SampleWorkers.Options.Repositories.Select(o => (Repository(o.Name, opened.Where(p => p.Repo == o.Name).ToList()), o)).ToList());
    }

    static IRepository Repository(string name, IReadOnlyList<OpenPr> open)
    {
        return Stub<IRepository>.Make(new Dictionary<string, Func<object?[], object?>>
        {
            ["get_Name"] = _ => name,
            ["OpenPrs"] = _ => Task.FromResult(open),
        });
    }

    static readonly TimeSpan Quiet = TimeSpan.FromMinutes(30);

    [Fact]
    public async Task AStoryAHandMovedSinceTheReadKeepsItsColumn()
    {
        var board = new MemoryBoard();
        board.Add(3518, stage: "Analyze");

        var log = await Run(new Fixture().Story(3518, "-"), board);

        Assert.Equal("Analyze", board.Cards[3518].Stage);
        var line = Assert.Single(log.Written, l => l.Contains("dropped"));
        Assert.Contains("#3518", line);
        Assert.Contains("Analyze", line);
        Assert.Contains("Backlog", line);
        Assert.DoesNotContain(log.Written, l => l.StartsWith("chore:") && l.Contains("#3518"));
    }

    [Fact]
    public async Task AnyKeepersMoveIsCheckedAndItsCommentIsNotSaid()
    {
        var board = new MemoryBoard();
        board.Add(7, stage: "Merged");

        await Run(new Fixture().Story(7, "In-Progress"), board);

        Assert.Equal("Merged", board.Cards[7].Stage);
        Assert.Empty(board.Cards[7].Comments);
    }

    [Fact]
    public async Task AStoryLeftWithNoColumnLandsInBacklog()
    {
        var board = new MemoryBoard();
        board.Add(1, stage: "-");

        await Run(new Fixture().Story(1, "-"), board);

        Assert.Equal("Backlog", board.Cards[1].Stage);
    }

    [Fact]
    public async Task AnOrphanLeftInProgressGoesBackToReadyWithItsComment()
    {
        var board = new MemoryBoard();
        board.Add(7, stage: "In-Progress");
        var other = new OpenPr(239, "app#8: another story", "stack/app-8", "sha", CiState.Green, Fixture.Stack, "now", HeadRepo: Fixture.Stack);

        await Run(new Fixture().Story(7, "In-Progress", quiet: Quiet), board, other);

        Assert.Equal("Ready", board.Cards[7].Stage);
        Assert.Single(board.Cards[7].Comments);
    }

    [Fact]
    public async Task AnOrphanWithAMergedPrGoesToTheAnalystWithItsComment()
    {
        var board = new MemoryBoard();
        board.Add(7, stage: "In-Progress");

        await Run(new Fixture().Story(7, "In-Progress", quiet: Quiet).MergedPr(7), board);

        Assert.Equal("Analyze", board.Cards[7].Stage);
        Assert.Single(board.Cards[7].Comments);
    }

    // The read saw no PR; by the write, the job that built the story elsewhere has opened one.
    [Fact]
    public async Task AStoryWhosePrOpenedAfterTheReadStaysInProgressWithNoComment()
    {
        var board = new MemoryBoard();
        board.Add(7, stage: "In-Progress");
        var opened = new OpenPr(239, "app#7: the fix", "stack/app-7", "sha", CiState.Pending, Fixture.Stack, "now", HeadRepo: Fixture.Stack);

        var log = await Run(new Fixture().Story(7, "In-Progress", quiet: Quiet), board, opened);

        Assert.Equal("In-Progress", board.Cards[7].Stage);
        Assert.Empty(board.Cards[7].Comments);
        var line = Assert.Single(log.Written, l => l.Contains("dropped"));
        Assert.Contains("#7", line);
        Assert.Contains($"{Fixture.Stack} PR #239", line);
        Assert.Contains("Ready", line);
        Assert.DoesNotContain(log.Written, l => l.StartsWith("chore:") && l.Contains("#7"));
    }

    [Fact]
    public async Task AStoryWhoseJobJustEndedStaysInProgressWithNoComment()
    {
        var board = new MemoryBoard();
        board.Add(7, stage: "In-Progress");

        await Run(new Fixture().Story(7, "In-Progress", quiet: TimeSpan.FromSeconds(10)), board);

        Assert.Equal("In-Progress", board.Cards[7].Stage);
        Assert.Empty(board.Cards[7].Comments);
    }

    [Fact]
    public async Task ABlockedStoryStillInBacklogIsLiftedToAnalyze()
    {
        var board = new MemoryBoard();
        board.Add(1, stage: "Backlog", labels: "blocked");
        board.Add(2, stage: "Merged");

        await Run(new Fixture().Story(1, "Backlog", "blocked", blockers: [2]).Story(2, "Merged"), board);

        Assert.Equal("Analyze", board.Cards[1].Stage);
        Assert.DoesNotContain("blocked", board.Cards[1].Labels);
    }

    [Fact]
    public async Task TwoMovesOfOneStoryInAPassBothLand()
    {
        var board = new MemoryBoard();
        board.Add(1, stage: "-", labels: "blocked");
        board.Add(2, stage: "Merged");

        await Run(new Fixture().Story(1, "-", "blocked", blockers: [2]).Story(2, "Merged"), board);

        Assert.Equal("Analyze", board.Cards[1].Stage);
    }

    [Fact]
    public async Task AWorkersStartingMoveIsWrittenAsPlanned()
    {
        var board = new MemoryBoard();
        board.Add(900, stage: "Backlog", labels: "stalled");

        var log = await Run(new Fixture().Story(900, "Ready", "stalled"), board);

        Assert.Equal("Analyze", board.Cards[900].Stage);
        Assert.DoesNotContain(log.Written, l => l.Contains("dropped"));
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using NSail.Harness.Planning;

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

    // The pass's read is the fixture; the board is what the chores find when they are written.
    static async Task<Lines> Run(Fixture read, MemoryBoard board)
    {
        var snapshot = read.Build();
        var plan = SampleWorkers.Plan(snapshot);
        var log = new Lines();
        await BoardChores.Do(board, plan.Chores, snapshot.Issues.ToDictionary(i => i.Number, i => i.Stage), "— harness", log, CancellationToken.None);
        return log;
    }

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

        await Run(new Fixture().Story(7, "In-Progress"), board);

        Assert.Equal("Ready", board.Cards[7].Stage);
        Assert.Single(board.Cards[7].Comments);
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
}

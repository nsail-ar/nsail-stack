// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;

namespace NSail.Harness.Tests;

public class MappedBoardTests
{
    // One story on a board that names things its own way.
    sealed class TeamBoard : IBoard
    {
        public HashSet<string> Worn { get; } = ["component/sales", "On fire", "bug"];
        public string Column { get; set; } = "Doing";

        public string Name => "team";

        public Task<IReadOnlyList<Issue>> Stories(CancellationToken ct = default)
        {
            IReadOnlyList<Issue> stories = [new Issue(1, "a story", Worn.ToHashSet(), Column, 0, [], -1, false, 0, 0)];
            return Task.FromResult(stories);
        }

        public Task<string> StageOf(int story, CancellationToken ct = default)
        {
            return Task.FromResult(Column);
        }

        public Task SetStage(int story, string stage, CancellationToken ct = default)
        {
            Column = stage;
            return Task.CompletedTask;
        }

        public Task<bool> TrySetStage(int story, string stage, ILogger log, CancellationToken ct = default)
        {
            Column = stage;
            return Task.FromResult(true);
        }

        public Task<IReadOnlySet<string>> Labels(int story, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlySet<string>>(Worn.ToHashSet());
        }

        public Task AddLabels(int story, params string[] labels)
        {
            Worn.UnionWith(labels);
            return Task.CompletedTask;
        }

        public Task RemoveLabel(int story, string label)
        {
            Worn.Remove(label);
            return Task.CompletedTask;
        }

        public Task<StoryText> Story(int story, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<string>> KnownLabels(CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<string, DateTimeOffset>> LabeledAt(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<DateTimeOffset?> LastLift(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task Comment(int story, string body)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<StoryComment>> Comments(int story, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateBody(int story, string body, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task Close(int story, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CreateStory(string title, string body, IReadOnlyCollection<string> labels, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<int?> FindOpen(string title, IReadOnlyCollection<string> labels, bool exact = true, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> Children(int story, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<(int Number, string Stage)>> ClosedSince(DateTimeOffset since, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }
    }

    static readonly Dictionary<string, string> Columns = new() { [Stages.InProgress] = "Doing", [Stages.Ready] = "To do" };
    static readonly Dictionary<string, string> Tags = new() { [NSail.Harness.Labels.P1] = "On fire", ["area:"] = "component/" };

    [Fact]
    public async Task TheHarnessReadsItsOwnNames()
    {
        var board = new MappedBoard(new TeamBoard(), Columns, Tags);

        var story = (await board.Stories()).Single();

        Assert.Equal(Stages.InProgress, story.Stage);
        Assert.True(story.Has(NSail.Harness.Labels.P1));
        Assert.True(story.Has("area:sales"));
        Assert.True(story.Has("bug"));
    }

    [Fact]
    public async Task WhatTheHarnessWritesLandsInTheBoardsNames()
    {
        var team = new TeamBoard();
        var board = new MappedBoard(team, Columns, Tags);

        await board.SetStage(1, Stages.Ready);
        await board.RemoveLabel(1, NSail.Harness.Labels.P1);
        await board.AddLabels(1, "area:stock", NSail.Harness.Labels.Stalled);

        Assert.Equal("To do", team.Column);
        Assert.DoesNotContain("On fire", team.Worn);
        Assert.Contains("component/stock", team.Worn);
        Assert.Contains("stalled", team.Worn);
    }
}

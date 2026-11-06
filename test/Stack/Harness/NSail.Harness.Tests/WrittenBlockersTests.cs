// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

// A blocker named in words gets the board's link and holds the story while it is open.
public class WrittenBlockersTests
{
    [Theory]
    [InlineData("This is blocked by #12.", new[] { 12 })]
    [InlineData("It waits on #12 and #13; the rest is free.", new[] { 12, 13 })]
    [InlineData("Waits for app#12, #14", new[] { 12, 14 })]
    [InlineData("depends on #9 — then the screen", new[] { 9 })]
    [InlineData("**Waits:** #3198, #3201", new[] { 3198, 3201 })]
    [InlineData("Waits: nothing", new int[0])]
    [InlineData("See #12 for context; it is not blocked.", new int[0])]
    [InlineData("blocked by other#12 and acme/app#13", new int[0])]
    public void TheBlockersAStoryNamesInWordsAreRead(string text, int[] named)
    {
        Assert.Equal(named, WrittenBlockers.Named(text, "app"));
    }

    [Fact]
    public async Task AnOpenBlockerIsLinkedAndHoldsTheStory()
    {
        var board = new MemoryBoard();
        board.Add(1, body: "Waits on #2 and #3.");
        board.Add(2, stage: "Ready");
        board.Add(3, stage: "Merged");

        var open = await WrittenBlockers.Link(board, 1, board.Cards[1].Body, "app");

        Assert.Equal([2], open);
        Assert.Equal([2, 3], board.Cards[1].Blockers);
        Assert.Contains("blocked", board.Cards[1].Labels);
    }

    [Fact]
    public async Task LandedBlockersAreLinkedAndHoldNothing()
    {
        var board = new MemoryBoard();
        board.Add(1, body: "blocked by #3");
        board.Add(3, stage: "Merged");

        Assert.Empty(await WrittenBlockers.Link(board, 1, board.Cards[1].Body, "app"));
        Assert.Equal([3], board.Cards[1].Blockers);
        Assert.DoesNotContain("blocked", board.Cards[1].Labels);
    }

    [Fact]
    public async Task ALinkAlreadyThereIsNotMadeTwiceAndANumberWithNoStoryBlocksNothing()
    {
        var board = new MemoryBoard();
        board.Add(1, body: "blocked by #2 and #404");
        board.Add(2, stage: "Ready");
        board.Cards[1].Blockers.Add(2);

        Assert.Equal([2], await WrittenBlockers.Link(board, 1, board.Cards[1].Body, "app"));
        Assert.Equal([2], board.Cards[1].Blockers);
    }
}

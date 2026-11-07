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
    [InlineData("**Waits** — nothing. #3263 (how long reports are kept) touches the same reports but not this read", new int[0])]
    [InlineData("Waits: none", new int[0])]
    [InlineData("Waits — nada", new int[0])]
    [InlineData("**Waits:** None.", new int[0])]
    [InlineData("Waits — ninguna", new int[0])]
    [InlineData("WAITS: Ninguno, though it depends on #7 and is blocked by #8", new int[0])]
    [InlineData("**Waits** — nothing. #3263 is near.\nThis is blocked by #12.", new[] { 12 })]
    [InlineData("Waits — #12 (its seam)", new[] { 12 })]
    [InlineData("Waits on #12 and #14", new[] { 12, 14 })]
    [InlineData("Waits — #12, though nothing else", new[] { 12 })]
    [InlineData("See #12 for context; it is not blocked.", new int[0])]
    [InlineData("blocked by other#12 and acme/app#13", new int[0])]
    public void TheBlockersAStoryNamesInWordsAreRead(string text, int[] named)
    {
        Assert.Equal(named, WrittenBlockers.Named(text, "app"));
    }

    // The refinery comment's own form, as the analysis wrote it on stories it then held.
    [Theory]
    [InlineData("**Waits** — nothing. #3263 (how long reports are kept) touches the same reports but not this read")]
    public void AWaitsLineThatAnswersNothingInTheAnalystsFormNamesNoBlocker(string line)
    {
        var comment = $"**Claim** — the read.\n\n{line}\n\n**Dev notes**\n- the seam.";

        Assert.Empty(WrittenBlockers.Named(comment, "nsail"));
    }

    [Fact]
    public async Task AWaitsLineThatAnswersNothingLinksNoStoryAndHoldsNothing()
    {
        var board = new MemoryBoard();
        board.Add(1, body: "**Waits** — nothing. #2 touches the same reports, and depends on #2 for nothing here.");
        board.Add(2, stage: "Ready");

        Assert.Empty(await WrittenBlockers.Link(board, 1, board.Cards[1].Body, "app"));
        Assert.Empty(board.Cards[1].Blockers);
        Assert.DoesNotContain("blocked", board.Cards[1].Labels);
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

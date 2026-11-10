// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

// A number that does not fit an int names no story, and never stops a pass.
public class StoryNamesTests
{
    private readonly StoryNames _names = new("app", ["story", "stack"]);

    [Theory]
    [InlineData("app#99999999999: x", "feature/x", null)]
    [InlineData("app#99999999999: x", "story/app-12", 12)]
    [InlineData("x", "story/app-99999999999", null)]
    [InlineData("x", "stack/app-99999999999-fix", null)]
    [InlineData("app#12: x", "feature/x", 12)]
    [InlineData("x", "stack/app-7-fix", 7)]
    public void AStoryOfAPrIsReadOnlyWhenItFitsAnInt(string title, string head, int? story)
    {
        Assert.Equal(story, _names.StoryOf(title, head));
    }

    [Fact]
    public void AnOutOfRangeReferenceInTextIsSkipped()
    {
        Assert.Equal([12], WrittenBlockers.Named("blocked by #99999999999 and #12", "app"));
        Assert.Equal([12], ShipJob.Named("app#99999999999 app#12", "app"));
        Assert.Empty(ReleaseJob.FixesIn("aaa fix: x (#99999999999)\n", [2917]));
    }
}

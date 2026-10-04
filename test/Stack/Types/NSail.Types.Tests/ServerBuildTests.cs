// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Builds;

namespace NSail.Types.Tests;

// What raises the offer to reload and, mostly, what does not: a bar that appears on a developer's
// every request, or on a host that predates the stamp, has failed this rule as badly as one that
// never appears at all (nsail#1452).
public sealed class ServerBuildTests
{
    [Fact]
    public void ABuildThatIsNotThisOneMovesItOnce()
    {
        var build = new ServerBuild();
        var raised = 0;

        build.Changed += () => raised++;

        build.Answered("1.2.3");
        build.Answered("1.2.4");

        Assert.True(build.Moved);
        Assert.Equal(1, raised);
    }

    // The developer's case, and it is the common one: nothing stamps a version outside the
    // container build, so both halves read the same default and must compare equal.
    [Fact]
    public void TheBuildThisProcessRunsIsSilence()
    {
        var build = new ServerBuild();
        var raised = 0;

        build.Changed += () => raised++;

        build.Answered(BuildVersion.Current);

        Assert.False(build.Moved);
        Assert.Equal(0, raised);
    }

    // A host that answers without the header at all, and one that answers with nothing in it:
    // neither says the server moved, so neither is allowed to say anything.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoWordIsNotADifferentWord(string? version)
    {
        var build = new ServerBuild();
        var raised = 0;

        build.Changed += () => raised++;

        build.Answered(version);

        Assert.False(build.Moved);
        Assert.Equal(0, raised);
    }

    // The version is an opaque token, not a number: the client compares what it booted with
    // against what answered, and reads no order into either.
    [Fact]
    public void AnOlderBuildAnsweringIsStillAMove()
    {
        var build = new ServerBuild();

        build.Answered("0.0.1-rollback");

        Assert.True(build.Moved);
    }

    [Fact]
    public void TheBuildThisProcessRunsIsNeverBlank()
    {
        Assert.False(string.IsNullOrWhiteSpace(BuildVersion.Current));
    }
}

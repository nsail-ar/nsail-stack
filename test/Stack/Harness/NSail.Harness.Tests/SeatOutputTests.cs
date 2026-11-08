// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

public class SeatOutputTests
{
    [Fact]
    public void ALabelOutsideTheKnownSetOrOneASeatMayNotSetIsDropped()
    {
        var known = new[] { "area:shop", "claude:opus", "p1", "claimed", "hand", "stalled" };
        var allowed = SeatOutput.AllowedLabels(["area:shop", "p1", "claimed", "hand", "stalled", "made-up", " claude:opus "], known);
        Assert.Equal(["area:shop", "claude:opus"], allowed);
    }

    [Theory]
    [InlineData(".github/workflows/ci.yml", true)]
    [InlineData(".github/workflows/sub/x.yml", true)]
    [InlineData(".github/voyage.sh", false)]
    [InlineData("src/.github/workflows/ci.yml", false)]
    public void AProtectedGlobMatchesItsPaths(string file, bool protectedPath)
    {
        Assert.Equal(protectedPath ? 1 : 0, SeatOutput.Protected([file], [".github/workflows/**"]).Count);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// Which open PRs the planner takes for a story's: only heads in the repository itself, by a listed author.
public class CrewPrTests
{
    static OpenPr Pr(string headRepo, string author)
    {
        return new OpenPr(1, "app#5: x", "story/app-5", "abc", CiState.Green, "acme/app", "t", headRepo, author);
    }

    [Fact]
    public void AForksPrIsNotTheCrews()
    {
        Assert.False(SnapshotReader.IsCrews(Pr("evil/app", "evil"), "acme/app", []));
    }

    [Fact]
    public void AnOwnHeadIsTheCrewsWhenNoAuthorsAreListed()
    {
        Assert.True(SnapshotReader.IsCrews(Pr("acme/app", "gru"), "acme/app", []));
    }

    [Fact]
    public void AnAuthorOffTheListIsNotTheCrews()
    {
        Assert.False(SnapshotReader.IsCrews(Pr("acme/app", "stranger"), "acme/app", ["gru"]));
        Assert.True(SnapshotReader.IsCrews(Pr("acme/app", "GRU"), "acme/app", ["gru"]));
    }
}

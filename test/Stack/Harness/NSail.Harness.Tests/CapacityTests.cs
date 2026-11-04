// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

public class CapacityTests
{
    static WorkItem Dev(int n)
    {
        return new WorkItem(WorkKind.Dev, n, null, "rest", "rest");
    }

    static WorkItem Release()
    {
        return new WorkItem(WorkKind.Release, 1, null, "release", "red fix", "release-shop.yml", "abc");
    }

    static WorkItem Qa()
    {
        return new WorkItem(WorkKind.Qa, null, null, "qa", "qa");
    }

    [Fact]
    public void NewStoriesFillTheAgentsButTheLastAndTheRestWaits()
    {
        var picked = Capacity.Pick([Dev(1), Dev(2), Dev(3)], new Dictionary<Resource, int>(), new Limits(3, 3, 1));

        Assert.Equal([1, 2], picked.Select(p => p.Story!.Value));
    }

    [Fact]
    public void TheLastAgentIsKeptForAReviewThatComesAfterTheStoriesStarted()
    {
        var review = new WorkItem(WorkKind.Review, 9, 90, "review", "green, unread");

        var picked = Capacity.Pick([review, Dev(1)], new Dictionary<Resource, int> { [Resource.Agent] = 2 }, new Limits(3, 3, 1));

        Assert.Equal([WorkKind.Review], picked.Select(p => p.Kind));
    }

    [Fact]
    public void WithTheAgentsFullAnItemThatNeedsNoneStillGoes()
    {
        var picked = Capacity.Pick([Dev(1), Release()], new Dictionary<Resource, int> { [Resource.Agent] = 5 }, new Limits(5, 3, 1));

        Assert.Equal([WorkKind.Release], picked.Select(p => p.Kind));
    }

    [Fact]
    public void AReleaseWalkNeedsNothingAndGoesWithEverythingFull()
    {
        var ship = new WorkItem(WorkKind.Ship, null, null, "release", "shop/v1.2.0 published", "release-shop.yml", Tag: "shop/v1.2.0");

        var picked = Capacity.Pick([Dev(1), ship], new Dictionary<Resource, int> { [Resource.Agent] = 5, [Resource.Build] = 3, [Resource.Browser] = 1 }, new Limits(5, 3, 1));

        Assert.Equal([WorkKind.Ship], picked.Select(p => p.Kind));
    }

    [Fact]
    public void WithTheBuildsFullTheNextAgentItemGoes()
    {
        var picked = Capacity.Pick([Release(), Dev(2)], new Dictionary<Resource, int> { [Resource.Build] = 3 }, new Limits(5, 3, 1));

        Assert.Equal([WorkKind.Dev], picked.Select(p => p.Kind));
    }

    [Fact]
    public void OneBrowserServesOneJobAtATime()
    {
        var picked = Capacity.Pick([Release(), Qa()], new Dictionary<Resource, int>(), new Limits(5, 3, 1));

        Assert.Equal([WorkKind.Release], picked.Select(p => p.Kind));
    }
}

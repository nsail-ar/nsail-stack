// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;

namespace NSail.Harness.Tests;

// Which stories a release walk reads: the ones its commits name, and the ones no other product's
// release is owed.
public class ShipTests
{
    static readonly HashSet<string> Products = ["shop", "clinic", "admin"];

    [Theory]
    [InlineData("area:shop,area:sales", false)]
    [InlineData("area:shop,area:clinic", false)]
    [InlineData("area:clinic", true)]
    [InlineData("area:stack,area:accounting", false)]
    [InlineData("", false)]
    public void AStoryWaitsOnlyForTheProductsItWearsAndThisIsNotOne(string labels, bool waits)
    {
        var worn = labels.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        Assert.Equal(waits, ShipJob.Waits(worn, "shop", Products));
    }

    [Fact]
    public void TheStoriesNamedAreReadOnceEachAndNoOtherKeyCounts()
    {
        var log = "app#12: feat: one (#40)\n\nfrom app#7's ruling\n\napp#12: fix (#41)\nother#3 and xapp#9";

        Assert.Equal([7, 12], ShipJob.Named(log, "app"));
    }
}

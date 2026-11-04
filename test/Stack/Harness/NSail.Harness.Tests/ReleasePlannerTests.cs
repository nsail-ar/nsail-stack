// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// When a product's cut is planned: its day's cut once due, a red fix's at once, never twice.
public class ReleasePlannerTests
{
    [Fact]
    public void ADueCutGoesBeforeEveryStory()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(1, "Ready", "p1").Due("release-shop.yml").Build());

        Assert.Equal(WorkKind.Release, plan.Items[0].Kind);
        Assert.Equal("shop", plan.Items[0].Product);
        Assert.Null(plan.Items[0].Story);
    }

    [Fact]
    public void ACutAlreadyInFlightIsNotPlannedAgain()
    {
        var plan = SampleWorkers.Plan(new Fixture().Due("release-shop.yml").Cutting("release-shop.yml").Build());

        Assert.DoesNotContain(plan.Items, i => i.Kind == WorkKind.Release);
    }

    [Fact]
    public void ARedFixAndTheDaysCutOfOneProductAreOneCut()
    {
        var plan = SampleWorkers.Plan(new Fixture().Story(5, "Merged").Release(5).Due("release-shop.yml").Due("release-clinic.yml").Build());

        var cuts = plan.Items.Where(i => i.Kind == WorkKind.Release).ToList();
        Assert.Equal(["shop", "clinic"], cuts.Select(c => c.Product));
        Assert.Equal(5, cuts[0].Story);
    }

    [Fact]
    public void APublishedReleaseIsWalkedEvenWhileItsProductCuts()
    {
        var plan = SampleWorkers.Plan(new Fixture().Published("shop", "shop/v1.2.0").Cutting("release-shop.yml").Build());

        var ship = Assert.Single(plan.Items, i => i.Kind == WorkKind.Ship);
        Assert.Equal("shop/v1.2.0", ship.Tag);
        Assert.DoesNotContain(plan.Items, i => i.Kind == WorkKind.Release);
    }

    [Fact]
    public void AReleaseBeingWalkedIsNotPlannedAgain()
    {
        var plan = SampleWorkers.Plan(new Fixture().Published("shop", "shop/v1.2.0").Shipping("shop/v1.2.0").Build());

        Assert.DoesNotContain(plan.Items, i => i.Kind == WorkKind.Ship);
    }
}

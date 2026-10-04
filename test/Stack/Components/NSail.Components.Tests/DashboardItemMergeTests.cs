// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Components.Tests;

// The drawer's rule on the home (nsail#1008): a kit plants a card and the app renames, reorders
// or hides it by naming it and only the fields it changes. NavMenuItemMergeTests is the sibling
// and the vocabulary is deliberately the same one.
public sealed class DashboardItemMergeTests
{
    [Fact]
    public void KeepsACardWithNoOverrideUnchanged()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem
            {
                Name = "TodayAgenda",
                CardType = typeof(DashboardItemMergeTests),
                Icon = new Glyph("today"),
                PageType = typeof(DashboardItem),
                Weight = 5,
                Tall = true
            }
        ]);

        var card = Assert.Single(merged);
        Assert.Equal("TodayAgenda", card.Name);
        Assert.Equal(typeof(DashboardItemMergeTests), card.CardType);
        Assert.Equal(new Glyph("today"), card.Icon);
        Assert.Equal(typeof(DashboardItem), card.PageType);
        Assert.Equal(5, card.Weight);
        Assert.True(card.Tall);
    }

    // The defect the merge exists to end: two contributors naming one card used to render it
    // twice, because Collect concatenated and sorted.
    [Fact]
    public void RendersOneCardPerName()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "Tills", CardType = typeof(DashboardItemMergeTests), Weight = 15 },
            new DashboardItem { Name = "Tills", Weight = 40 }
        ]);

        Assert.Single(merged);
    }

    [Fact]
    public void TakesTheLastContributedWeight()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "Tills", CardType = typeof(DashboardItemMergeTests), Weight = 15 },
            new DashboardItem { Name = "Tills", Weight = 40 }
        ]);

        Assert.Equal(40, Assert.Single(merged).Weight);
    }

    [Fact]
    public void TakesTheLastContributedIcon()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem
            {
                Name = "Tills",
                CardType = typeof(DashboardItemMergeTests),
                Icon = new Glyph("accounting")
            },
            new DashboardItem { Name = "Tills", Icon = new Glyph("lens") }
        ]);

        Assert.Equal(new Glyph("lens"), Assert.Single(merged).Icon);
    }

    [Fact]
    public void TakesTheLastContributedTall()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "TodayAgenda", CardType = typeof(DashboardItemMergeTests), Tall = true },
            new DashboardItem { Name = "TodayAgenda", Tall = false }
        ]);

        Assert.False(Assert.Single(merged).Tall);
    }

    // Null is "no opinion", not "back to the default": an override that only hides says nothing
    // about the weight, the icon or the height the kit chose.
    [Fact]
    public void LeavesTheKitsFieldsAloneWhenAnOverrideNamesNone()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem
            {
                Name = "TodayAgenda",
                CardType = typeof(DashboardItemMergeTests),
                Icon = new Glyph("today"),
                PageType = typeof(DashboardItem),
                Weight = 5,
                Tall = true
            },
            new DashboardItem { Name = "TodayAgenda", Visible = true }
        ]);

        var card = Assert.Single(merged);
        Assert.Equal(new Glyph("today"), card.Icon);
        Assert.Equal(typeof(DashboardItem), card.PageType);
        Assert.Equal(5, card.Weight);
        Assert.True(card.Tall);
    }

    // CardType and PageType are what the card IS, not where it sits, so they keep the FIRST
    // non-null — which is what lets an override name the card and nothing else.
    [Fact]
    public void KeepsTheFirstContributedCardTypeAndPageType()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem
            {
                Name = "Tills",
                CardType = typeof(DashboardItemMergeTests),
                PageType = typeof(DashboardItem)
            },
            new DashboardItem
            {
                Name = "Tills",
                CardType = typeof(NavMenuItemMergeTests),
                PageType = typeof(NavMenuItem)
            }
        ]);

        var card = Assert.Single(merged);
        Assert.Equal(typeof(DashboardItemMergeTests), card.CardType);
        Assert.Equal(typeof(DashboardItem), card.PageType);
    }

    [Fact]
    public void AnOverrideThatOnlyReordersNeedsNoCardType()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "Early", CardType = typeof(DashboardItemMergeTests), Weight = 5 },
            new DashboardItem { Name = "Late", CardType = typeof(DashboardItem), Weight = 60 },
            new DashboardItem { Name = "Late", Weight = 1 }
        ]);

        Assert.Equal(["Late", "Early"], merged.Select(card => card.Name));
    }

    [Fact]
    public void VisibleFalseDropsTheCard()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "TodayAgenda", CardType = typeof(DashboardItemMergeTests), Weight = 5 },
            new DashboardItem { Name = "Tills", CardType = typeof(DashboardItem), Weight = 15 },
            new DashboardItem { Name = "TodayAgenda", Visible = false }
        ]);

        Assert.Equal(["Tills"], merged.Select(card => card.Name));
    }

    // Last non-null, the same as every other overridable field: a contributor after the one that
    // hid the card puts it back.
    [Fact]
    public void ALaterContributorPutsAHiddenCardBack()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "TodayAgenda", CardType = typeof(DashboardItemMergeTests) },
            new DashboardItem { Name = "TodayAgenda", Visible = false },
            new DashboardItem { Name = "TodayAgenda", Visible = true }
        ]);

        Assert.Equal(["TodayAgenda"], merged.Select(card => card.Name));
    }

    // An override is told from a card by the absence of a CardType, so a name nobody plants is
    // a dead instruction the home would otherwise carry out in silence.
    [Fact]
    public void ThrowsWhenAnOverrideNamesACardNobodyContributes()
    {
        var error = Assert.Throws<InvalidOperationException>(() => DashboardItem.Merge(
        [
            new DashboardItem { Name = "Tills", CardType = typeof(DashboardItemMergeTests) },
            new DashboardItem { Name = "TodayAgenda", Visible = false }
        ]));

        Assert.Contains("TodayAgenda", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdersCardsByWeightAscending()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "Late", CardType = typeof(DashboardItemMergeTests), Weight = 60 },
            new DashboardItem { Name = "Early", CardType = typeof(DashboardItem), Weight = 5 },
            new DashboardItem { Name = "Middle", CardType = typeof(NavMenuItem), Weight = 30 }
        ]);

        Assert.Equal(["Early", "Middle", "Late"], merged.Select(card => card.Name));
    }

    [Fact]
    public void KeepsContributionOrderBetweenEqualWeights()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "First", CardType = typeof(DashboardItemMergeTests), Weight = 20 },
            new DashboardItem { Name = "Second", CardType = typeof(DashboardItem), Weight = 20 }
        ]);

        Assert.Equal(["First", "Second"], merged.Select(card => card.Name));
    }

    // A card nobody weighted sorts where a zero would, so an override that only hides a sibling
    // cannot move it.
    [Fact]
    public void AnUnweightedCardSortsWhereAZeroWould()
    {
        var merged = DashboardItem.Merge(
        [
            new DashboardItem { Name = "Weighted", CardType = typeof(DashboardItemMergeTests), Weight = 5 },
            new DashboardItem { Name = "Unweighted", CardType = typeof(DashboardItem) }
        ]);

        Assert.Equal(["Unweighted", "Weighted"], merged.Select(card => card.Name));
    }
}

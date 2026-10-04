// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Components.Tests;

/// <summary>An app's arrangement is a flat list of values, so what a stored one would have to
/// carry is exactly what the declaration carries: a name, a parent, an order, whether it shows,
/// and what a leaf needs to open its page. It stays flat through Resolve too — NavMenuItem.Merge
/// is what nests it, because a row can only override a kit's entry of the same Name if the two
/// reach the merge at one level.</summary>
public sealed class NavMenuEntryTests
{
    [Fact]
    public void CarriesEachRowsParentThroughForTheMergeToNest()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Contactology", Order = 25, Icon = new Glyph("lens") },
            new() { Name = "Calendar", Parent = "Contactology", Order = 10, PageType = typeof(NavMenuEntryTests) },
            new() { Name = "Holidays", Parent = "Contactology", Order = 20, PageType = typeof(NavMenuEntry) }
        ];

        var items = NavMenuEntry.Resolve(arrangement);

        Assert.Equal(["Calendar", "Holidays", "Contactology"], items.Select(item => item.Name));
        Assert.Equal([(int?)10, 20, 25], items.Select(item => item.Weight));
        Assert.Equal(["Contactology", "Contactology", null], items.Select(item => item.Parent));
    }

    [Fact]
    public void TheMergeNestsWhatResolveHandsIt()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Contactology", Order = 25, Icon = new Glyph("lens") },
            new() { Name = "Calendar", Parent = "Contactology", Order = 10, PageType = typeof(NavMenuEntryTests) },
            new() { Name = "Holidays", Parent = "Contactology", Order = 20, PageType = typeof(NavMenuEntry) }
        ];

        var items = NavMenuItem.Merge(NavMenuEntry.Resolve(arrangement));

        var group = Assert.Single(items);
        Assert.Equal("Contactology", group.Name);
        Assert.Equal(25, group.Weight);
        Assert.Equal(new Glyph("lens"), group.Icon);
        Assert.Equal(["Calendar", "Holidays"], group.Items.Select(item => item.Name));
    }

    [Fact]
    public void OrdersSiblingsByTheirOrder()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Clients", Order = 5, PageType = typeof(NavMenuEntryTests) },
            new() { Name = "Home", Order = 1, PageType = typeof(NavMenuEntry) }
        ];

        var items = NavMenuEntry.Resolve(arrangement);

        Assert.Equal(["Home", "Clients"], items.Select(item => item.Name));
    }

    /// <summary>Shown is the row's answer only when it is NO. An arrangement says what it
    /// changes, so a row that shows leaves a kit's own decision alone, and a row that does not
    /// arrives marked rather than dropped — which is what takes the KIT's entry of that Name
    /// out of the drawer with it.</summary>
    [Fact]
    public void AnEntryThatDoesNotShowArrivesMarkedAndTheRestSayNothing()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Contactology", Order = 25, Shown = false },
            new() { Name = "Calendar", Parent = "Contactology", Order = 10, PageType = typeof(NavMenuEntryTests) },
            new() { Name = "Home", Order = 1, PageType = typeof(NavMenuEntry) }
        ];

        var items = NavMenuEntry.Resolve(arrangement);

        Assert.False(items.Single(item => item.Name == "Contactology").Visible);
        Assert.Null(items.Single(item => item.Name == "Home").Visible);
        Assert.Null(items.Single(item => item.Name == "Calendar").Visible);
    }

    [Fact]
    public void ASeparatorResolvesToOne()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Home", Order = 1, PageType = typeof(NavMenuEntryTests) },
            NavMenuEntry.Separator(2),
            new() { Name = "Clients", Order = 5, PageType = typeof(NavMenuEntry) }
        ];

        var items = NavMenuEntry.Resolve(arrangement);

        Assert.True(items[1].IsSeparator);
        Assert.Equal(2, items[1].Weight);
    }

    /// <summary>A row may name a group no row of the app's declares — a kit's, which is the
    /// whole point of an override — so the parent is checked where the whole tree is known
    /// and not here (NavMenuItemMergeTests).</summary>
    [Fact]
    public void AParentTheArrangementDoesNotDeclareResolvesAndIsLeftToTheMerge()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Calendar", Parent = "Scheduling", Order = 10, PageType = typeof(NavMenuEntryTests) }
        ];

        var item = Assert.Single(NavMenuEntry.Resolve(arrangement));

        Assert.Equal("Scheduling", item.Parent);
    }

    [Fact]
    public void OneNameDeclaredTwiceThrows()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Home", Order = 1, PageType = typeof(NavMenuEntryTests) },
            new() { Name = "Home", Order = 2, PageType = typeof(NavMenuEntry) }
        ];

        var error = Assert.Throws<InvalidOperationException>(() => NavMenuEntry.Resolve(arrangement));

        Assert.Contains("Home", error.Message);
    }

    [Fact]
    public void TwoSeparatorsAreNotOneNameDeclaredTwice()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "Home", Order = 1, PageType = typeof(NavMenuEntryTests) },
            NavMenuEntry.Separator(2),
            new() { Name = "Clients", Order = 5, PageType = typeof(NavMenuEntry) },
            NavMenuEntry.Separator(6)
        ];

        var items = NavMenuEntry.Resolve(arrangement);

        Assert.Equal(2, items.Count(item => item.IsSeparator));
    }

    /// <summary>A declared row may name the permission that shows its door, which is how an app
    /// tightens a kit's entry — and it reaches the merge as a field on the item, since the
    /// drawer is what asks it.</summary>
    [Fact]
    public void ADeclaredRowCarriesThePermissionThatShowsIt()
    {
        IReadOnlyList<NavMenuEntry> arrangement =
        [
            new() { Name = "WorkOrders", Order = 9, PageType = typeof(NavMenuEntryTests), Permission = typeof(NavMenuEntry) },
            new() { Name = "Home", Order = 1, PageType = typeof(NavMenuEntry) }
        ];

        var items = NavMenuEntry.Resolve(arrangement);

        Assert.Equal(typeof(NavMenuEntry), items.Single(item => item.Name == "WorkOrders").Permission);
        Assert.Null(items.Single(item => item.Name == "Home").Permission);
    }
}

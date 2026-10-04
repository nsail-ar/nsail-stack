// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

/// <summary>A separator is an item somebody placed, so two of them are two lines. Merge unites
/// by Name and a separator has none, which is exactly the fold that would have swallowed one of
/// the two — it passes through by identity instead.</summary>
public sealed class NavMenuSeparatorTests
{
    [Fact]
    public void ASeparatorCarriesNoNameAndNoPage()
    {
        var separator = NavMenuItem.Separator(3);

        Assert.True(separator.IsSeparator);
        Assert.Equal(string.Empty, separator.Name);
        Assert.Null(separator.PageType);
        Assert.Empty(separator.Items);
        Assert.Equal(3, separator.Weight);
    }

    [Fact]
    public void TwoSeparatorsStayTwoLines()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Home", Weight = 1, PageType = typeof(NavMenuSeparatorTests) },
            NavMenuItem.Separator(2),
            new() { Name = "Clients", Weight = 5, PageType = typeof(NavMenuItem) },
            NavMenuItem.Separator(6)
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal(4, merged.Count);
        Assert.Equal(2, merged.Count(item => item.IsSeparator));
    }

    [Fact]
    public void ASeparatorTakesItsPlaceByWeight()
    {
        var items = new List<NavMenuItem>
        {
            new() { Name = "Clients", Weight = 5, PageType = typeof(NavMenuItem) },
            new() { Name = "Home", Weight = 1, PageType = typeof(NavMenuSeparatorTests) },
            NavMenuItem.Separator(2)
        };

        var merged = NavMenuItem.Merge(items);

        Assert.Equal("Home", merged[0].Name);
        Assert.True(merged[1].IsSeparator);
        Assert.Equal("Clients", merged[2].Name);
    }

    [Fact]
    public void ASeparatorPassesThroughByIdentity()
    {
        var separator = NavMenuItem.Separator(2);

        var merged = NavMenuItem.Merge([separator]);

        Assert.Same(separator, Assert.Single(merged));
    }

    [Fact]
    public void ASeparatorIsNotAPageAndFindIgnoresIt()
    {
        var items = new List<NavMenuItem>
        {
            NavMenuItem.Separator(1),
            new() { Name = "Home", Weight = 2, PageType = typeof(NavMenuSeparatorTests) }
        };

        var found = NavMenuItem.Find(items, typeof(NavMenuSeparatorTests));

        Assert.Equal("Home", found?.Name);
    }
}

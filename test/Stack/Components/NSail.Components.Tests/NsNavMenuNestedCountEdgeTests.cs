// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#2970: a count rides the row's right edge at every depth. The vendor zeroes a
/// nested entry's end padding, so the drawer states one end padding for every row and the
/// count cannot leave the menu.</summary>
public sealed class NsNavMenuNestedCountEdgeTests
{
    [Fact]
    public void EveryDrawerRowKeepsItsEndPaddingAtAnyDepth()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        Assert.Matches(
            @"\.ns-nav-drawer \.mud-nav-link\s*\{[^}]*padding-inline-end:\s*var\(--ns-nav-inset\)\s*!important;",
            css);
    }

    /// <summary>nsail#3641: a group draws a <c>.mud-navmenu</c> inside the outer one, so the
    /// drawer's inset must not land on it a second time.</summary>
    [Fact]
    public void ANestedMenuAddsNoInsetOfItsOwn()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        Assert.Matches(
            @"\.ns-nav-drawer \.mud-navmenu \.mud-navmenu\s*\{[^}]*margin-inline:\s*0;",
            css);
    }

    /// <summary>The start padding that lines a top-level icon up with the gutter is the outer
    /// menu's only; on a nested row it would erase the indent under its group.</summary>
    [Fact]
    public void TheGutterStartPaddingReachesOnlyTheOuterMenusRows()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        var rules = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var rule = Assert.Single(Regex.Matches(rules, @"(?<selectors>[^{}]*)\{[^}]*padding-inline-start:\s*calc\(var\(--ns-nav-gutter\) - var\(--ns-nav-inset\)\)"));

        foreach (var selector in rule.Groups["selectors"].Value.Split(','))
        {
            Assert.Contains(".mud-navmenu:not(.mud-navmenu .mud-navmenu) >", selector);
        }
    }

    /// <summary>With the house zeroing a nested menu's margin and padding, a nested row's indent
    /// is the vendor's start padding per depth alone. Read verbatim, so a vendor upgrade that
    /// drops or flattens that chain cannot set a group's rows on the top-level line in silence.</summary>
    [Fact]
    public void TheVendorIndentsANestedEntryFurtherAtEveryDepth()
    {
        var vendor = VendorStylesheet.Read();

        var top = Regex.Match(vendor, @"(?:^|\})\.mud-nav-link\{[^}]*padding:\s*\S+\s+\S+\s+\S+\s+(?<start>\d+)px");
        Assert.True(top.Success, "The vendor's .mud-nav-link no longer states its padding as four lengths.");

        var depths = Regex
            .Matches(vendor, @"\}(?<selector>\.mud-nav-group \* \.mud-navmenu[^{}]*\.mud-nav-item \.mud-nav-link)\{[^}]*padding-inline-start:\s*(?<start>\d+)px")
            .Select(match => (
                Depth: Regex.Count(match.Groups["selector"].Value, @"\.mud-navmenu"),
                Start: int.Parse(match.Groups["start"].Value)))
            .OrderBy(rule => rule.Depth)
            .ToList();

        Assert.True(depths.Count >= 2, $"The vendor indents nested entries at {depths.Count} depth(s).");

        var previous = int.Parse(top.Groups["start"].Value);

        foreach (var (depth, start) in depths)
        {
            Assert.True(start > previous, $"A nested entry at depth {depth} starts at {start}px, not past {previous}px.");
            previous = start;
        }
    }

    static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

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

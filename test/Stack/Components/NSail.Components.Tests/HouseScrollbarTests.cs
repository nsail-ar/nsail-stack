// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Doctrine (ui/styling.md, the house draws one scrollbar): every scroller wears a thin
/// bar whose thumb is mixed from the lit palette, so it reads dark on dark and light on light. bUnit
/// paints no scrollbar, so the promise is read from the stylesheet the app ships.</summary>
public sealed class HouseScrollbarTests
{
    static readonly string Sheet = HouseStylesheet.Read();

    [Fact]
    public void TheRootThumbIsAPaletteColourMixedThinAgainstATransparentTrack()
    {
        var root = Body(":root");
        var color = Regex.Match(root, @"scrollbar-color:\s*var\((?<thumb>--ns-scrollbar-thumb)\)\s+transparent;");

        Assert.True(color.Success, ":root declares no scrollbar-color of the house thumb on a transparent track.");

        var thumb = Regex.Match(root, @"--ns-scrollbar-thumb:\s*color-mix\(in srgb, var\(--mud-palette-[a-z-]+\) (?<weight>\d+)%, transparent\);");

        Assert.True(thumb.Success, "The house thumb is not a mix of a --mud-palette-* colour against transparent.");
        Assert.InRange(int.Parse(thumb.Groups["weight"].Value), 1, 50);
    }

    [Fact]
    public void TheRailsThumbIsMixedFromTheBandsInkNotTheContents()
    {
        var rail = Body(@"\.ns-nav-drawer\.mud-drawer");

        Assert.Matches(@"--ns-scrollbar-thumb:\s*color-mix\(in srgb, var\(--mud-palette-drawer-text\) \d+%, transparent\);", rail);
        Assert.Matches(@"scrollbar-color:\s*var\(--ns-scrollbar-thumb\)\s+transparent;", rail);
    }

    [Fact]
    public void EveryElementDrawsTheThinBar()
    {
        Assert.Contains("scrollbar-width: thin;", Body(@"\*"));
    }

    [Fact]
    public void EveryWebkitScrollbarRuleIsOnlyTheFallbackForABrowserWithoutScrollbarColor()
    {
        const string Guard = "@supports not (scrollbar-color: auto) {";
        var start = Sheet.IndexOf(Guard, StringComparison.Ordinal);

        Assert.True(start >= 0, "ns-mud.css has no @supports not (scrollbar-color: auto) block.");

        var end = BlockEnd(start + Guard.Length);
        var outside = Sheet[..start] + Sheet[end..];

        Assert.Contains("::-webkit-scrollbar-thumb", Sheet[start..end]);
        Assert.DoesNotContain("::-webkit-scrollbar", outside);
        Assert.Single(Regex.Matches(Sheet, Regex.Escape(Guard)));
    }

    [Fact]
    public void TheOldInnerOnlyBarIsGone()
    {
        Assert.DoesNotContain("ns-scroll-thin", Sheet);
    }

    // Every top-level rule for the selector, joined: the sheet opens :root in several places.
    static string Body(string selector)
    {
        var rules = Regex.Matches(Sheet, $@"(?m)^{selector}\s*\{{(?<body>[^}}]*)\}}");

        Assert.NotEmpty(rules);

        return string.Join("\n", rules.Select(rule => rule.Groups["body"].Value));
    }

    static int BlockEnd(int from)
    {
        var depth = 1;

        for (var i = from; i < Sheet.Length; i++)
        {
            if (Sheet[i] == '{')
            {
                depth++;
            }
            else if (Sheet[i] == '}' && --depth == 0)
            {
                return i + 1;
            }
        }

        throw new InvalidOperationException("The @supports block never closes.");
    }
}

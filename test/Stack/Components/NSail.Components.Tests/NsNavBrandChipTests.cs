// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;

namespace NSail.Components.Tests;

/// <summary>A chip in the drawer's brand row keeps its natural width up to half the brand row
/// itself, not half of whatever box a product's Brand wraps around it, its painted edge — not the
/// vendor's margin — sits on the header's right line, and the name beside it takes the rest and
/// wraps.</summary>
public sealed class NsNavBrandChipTests : BunitContext
{
    public NsNavBrandChipTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Fact]
    public void TheBrandRowIsTheBoxItsChipsCapMeasures()
    {
        var css = HouseStylesheet.Read();

        Assert.Matches(@"(?:^|\})\s*\.ns-nav-brand\s*\{[^}]*container-type:\s*inline-size;", Uncommented(css));
        Assert.Matches(
            @"\.ns-nav-brand \.ns-chip-ellipsis\.mud-chip\s*\{[^}]*max-width:\s*min\(100%,\s*50cqi\);[^}]*margin-inline-end:\s*0;",
            css);
    }

    /// <summary>AC1, K1: the row's block padding is half of its floor less one title line, so a
    /// wrapped name keeps the one-line row's space above and below it and one line is the floor.</summary>
    [Fact]
    public void AWrappedBrandKeepsTheOneLineRowsSpaceAboveAndBelowIt()
    {
        var brand = Regex.Match(Uncommented(HouseStylesheet.Read()), @"(?:^|\})\s*\.ns-nav-brand\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        Assert.Matches(@"min-height:\s*var\(--mud-appbar-height\);", brand);
        Assert.Matches(
            @"padding-block:\s*calc\(\(var\(--mud-appbar-height\)\s*-\s*var\(--mud-typography-h6-size\)\s*\*\s*var\(--mud-typography-h6-lineheight\)\)\s*/\s*2\);",
            brand);
        Assert.DoesNotMatch(@"box-sizing", brand);
    }

    /// <summary>Every NsChip wears the ellipsis, so only the brand row's rule may move a chip's
    /// size or margin.</summary>
    [Fact]
    public void OnlyTheBrandRowMovesAChipsWidthOrMargin()
    {
        var rules = Regex.Matches(Uncommented(HouseStylesheet.Read()), @"(?<selectors>[^{}]*\.ns-chip-ellipsis\.mud-chip[^{}]*)\{(?<body>[^}]*)\}");

        foreach (Match rule in rules)
        {
            if (Regex.IsMatch(rule.Groups["body"].Value, @"max-width|margin"))
            {
                Assert.Contains(".ns-nav-brand ", rule.Groups["selectors"].Value);
            }
        }
    }

    /// <summary>The ellipsis clips the label's own span, so the glyph must render outside it:
    /// inside, a short label would lose its letters to the icon's width.</summary>
    [Fact]
    public void TheChipsGlyphSitsOutsideTheLabelTheEllipsisClips()
    {
        var cut = Render<NsChip>(p => p
            .Add(x => x.Text, "Prueba")
            .Add(x => x.Icon, NsIcons.Check));

        var label = cut.Find(".mud-chip-content > .ns-chip-label");

        Assert.Empty(label.QuerySelectorAll("svg"));
        Assert.NotEmpty(cut.Find(".mud-chip").QuerySelectorAll("svg"));
        Assert.Equal("Prueba", label.TextContent);
    }

    [Fact]
    public void AStretchedChipWearsTheSpanClassAndAPlainOneDoesNot()
    {
        var stretched = Render<NsChip>(p => p.Add(x => x.Text, "Prueba").Add(x => x.Stretch, true));
        var plain = Render<NsChip>(p => p.Add(x => x.Text, "Prueba"));

        Assert.Contains("ns-chip-stretch", stretched.Find(".mud-chip").ClassList);
        Assert.DoesNotContain("ns-chip-stretch", plain.Find(".mud-chip").ClassList);
    }

    [Fact]
    public void AStretchedChipSpansTheBrandRowPastTheHalfRowCap()
    {
        var css = Uncommented(HouseStylesheet.Read());

        Assert.Matches(@"\.ns-chip-stretch\.mud-chip\s*\{[^}]*width:\s*100%;", css);
        Assert.Matches(@"\.ns-nav-brand \.ns-chip-stretch\.mud-chip\s*\{[^}]*max-width:\s*100%;", css);
        Assert.Matches(@"\.ns-nav-brand :has\(\.ns-chip-stretch\)\s*\{[^}]*max-width:\s*none;", css);
    }

    [Fact]
    public void AClosableChipsCloseButtonSitsOutsideTheLabel()
    {
        var cut = Render<NsChip>(p => p
            .Add(x => x.Text, "Prueba")
            .Add(x => x.OnClose, () => { }));

        Assert.Equal("Prueba", cut.Find(".ns-chip-label").TextContent);
        Assert.Empty(cut.Find(".ns-chip-label").QuerySelectorAll("button"));
        Assert.NotEmpty(cut.Find(".mud-chip").QuerySelectorAll("button"));
    }

    /// <summary>K2: <c>.mud-chip-content</c> is the vendor's inline-flex that centers the label
    /// in the pill, so no rule of ours gives it another display; the clip is the label's.</summary>
    [Fact]
    public void TheContentKeepsTheVendorsCenteringAndTheLabelClips()
    {
        var css = Uncommented(HouseStylesheet.Read());

        foreach (Match rule in Regex.Matches(css, @"(?<selectors>[^{}]*\.mud-chip-content[^{}]*)\{(?<body>[^}]*)\}"))
        {
            Assert.DoesNotMatch(@"display|align-items|overflow|text-overflow", rule.Groups["body"].Value);
        }

        Assert.Matches(
            @"\.ns-chip-ellipsis \.ns-chip-label\s*\{[^}]*min-width:\s*0;[^}]*overflow:\s*hidden;[^}]*text-overflow:\s*ellipsis;[^}]*white-space:\s*nowrap;",
            css);
    }

    /// <summary>AC1: every box between the row and the chip but the chip's own side of the row,
    /// and the one beside it that holds the name, may shrink to nothing and break its text, while
    /// the chip and its side are held: the name wraps instead of pushing the row past the
    /// drawer.</summary>
    [Fact]
    public void TheNameWrapsBesideABrandChipThatKeepsItsWidth()
    {
        var css = Uncommented(HouseStylesheet.Read());

        Assert.Matches(@"\.ns-nav-brand :has\(\.ns-chip-ellipsis\)\s*\{[^}]*min-width:\s*0;", css);
        Assert.Matches(@"\.ns-nav-brand \.ns-chip-ellipsis\.mud-chip\s*\{[^}]*flex-shrink:\s*0;", css);
        Assert.Matches(
            @"\.ns-nav-brand:has\(\.ns-chip-ellipsis\) > :not\(:has\(\.ns-chip-ellipsis\), \.ns-chip-ellipsis, img, svg\),\s*"
            + @"\.ns-nav-brand :has\(\.ns-chip-ellipsis\) > :not\(:has\(\.ns-chip-ellipsis\), \.ns-chip-ellipsis, img, svg\)\s*"
            + @"\{[^}]*min-width:\s*0;[^}]*overflow-wrap:\s*anywhere;",
            css);
        var held = Regex.Matches(css, @"(?<selectors>[^{}]*\.ns-nav-brand[^{}]*)\{(?<body>[^}]*)\}")
            .Where(rule => Regex.IsMatch(rule.Groups["body"].Value, @"flex-shrink:\s*0"))
            .Select(rule => rule.Groups["selectors"].Value.Trim());
        Assert.All(held, selectors => Assert.Contains(selectors, new[]
        {
            ".ns-nav-brand .ns-chip-ellipsis.mud-chip",
            ".ns-nav-brand:has(.ns-chip-ellipsis) :is(img, svg):not(.ns-chip-ellipsis *)",
            ".ns-nav-brand:has(.ns-chip-ellipsis) .ns-remote-image:not(.ns-chip-ellipsis *)",
            ".ns-nav-brand * ~ :has(.ns-chip-ellipsis),\n.ns-nav-brand :has(.ns-chip-ellipsis):has(~ *)",
        }));
    }

    /// <summary>AC1: a box the Brand wraps around the chip beside the name is sized to its
    /// content, where the chip's 100% resolves to nothing, so that box is held at half the row
    /// itself; the Brand's wrapper around both name and chip has no sibling and is not.</summary>
    [Fact]
    public void TheBoxBesideTheNameThatHoldsTheChipIsCappedAtHalfTheRow()
    {
        var css = Uncommented(HouseStylesheet.Read());

        Assert.Matches(
            @"\.ns-nav-brand \* ~ :has\(\.ns-chip-ellipsis\),\s*\.ns-nav-brand :has\(\.ns-chip-ellipsis\):has\(~ \*\)\s*"
            + @"\{[^}]*flex-shrink:\s*0;[^}]*max-width:\s*50cqi;",
            css);
    }

    /// <summary>AC2: a logo or product mark, beside the chip or inside the name's box, keeps its
    /// size while the name wraps; the chip's own glyph is the vendor's.</summary>
    [Fact]
    public void TheImagesBesideTheNameKeepTheirSize()
    {
        var css = Uncommented(HouseStylesheet.Read());

        Assert.Matches(
            @"\.ns-nav-brand:has\(\.ns-chip-ellipsis\) :is\(img, svg\):not\(\.ns-chip-ellipsis \*\)\s*\{[^}]*flex-shrink:\s*0;",
            css);
    }

    /// <summary>AC1: a remote image's flex item is the span it wraps its mark in, and the
    /// span's max-width leaves it no min-content floor, so the brand row holds the span itself
    /// and a 1.46:1 mark at 18 px keeps its 26 px beside a long name and the chip.</summary>
    [Fact]
    public void ARemoteImageBesideTheNameKeepsItsSize()
    {
        var css = HouseStylesheet.Read();

        Assert.Matches(
            @"/\*[^*]*flex item is its span, not the svg[^*]*\*/\s*\.ns-nav-brand:has\(\.ns-chip-ellipsis\) \.ns-remote-image:not\(\.ns-chip-ellipsis \*\)\s*\{[^}]*flex-shrink:\s*0;",
            css);
    }

    static string Uncommented(string css)
    {
        return Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
    }
}

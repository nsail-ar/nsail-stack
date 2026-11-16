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
        var css = ReadStylesheet();

        Assert.Matches(@"(?:^|\})\s*\.ns-nav-brand\s*\{[^}]*container-type:\s*inline-size;", Uncommented(css));
        Assert.Matches(
            @"\.ns-nav-brand \.ns-chip-ellipsis\.mud-chip\s*\{[^}]*max-width:\s*min\(100%,\s*50cqi\);[^}]*margin-inline-end:\s*0;",
            css);
    }

    /// <summary>Every NsChip wears the ellipsis, so only the brand row's rule may move a chip's
    /// size or margin.</summary>
    [Fact]
    public void OnlyTheBrandRowMovesAChipsWidthOrMargin()
    {
        var rules = Regex.Matches(Uncommented(ReadStylesheet()), @"(?<selectors>[^{}]*\.ns-chip-ellipsis\.mud-chip[^{}]*)\{(?<body>[^}]*)\}");

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
        var css = Uncommented(ReadStylesheet());

        foreach (Match rule in Regex.Matches(css, @"(?<selectors>[^{}]*\.mud-chip-content[^{}]*)\{(?<body>[^}]*)\}"))
        {
            Assert.DoesNotMatch(@"display|align-items|overflow|text-overflow", rule.Groups["body"].Value);
        }

        Assert.Matches(
            @"\.ns-chip-ellipsis \.ns-chip-label\s*\{[^}]*min-width:\s*0;[^}]*overflow:\s*hidden;[^}]*text-overflow:\s*ellipsis;[^}]*white-space:\s*nowrap;",
            css);
    }

    /// <summary>AC1: every box between the row and the chip, and the one beside it that holds the
    /// name, may shrink to nothing and break its text, while the chip keeps its width: the name
    /// wraps instead of pushing the row past the drawer.</summary>
    [Fact]
    public void TheNameWrapsBesideABrandChipThatKeepsItsWidth()
    {
        var css = Uncommented(ReadStylesheet());

        Assert.Matches(@"\.ns-nav-brand :has\(\.ns-chip-ellipsis\)\s*\{[^}]*min-width:\s*0;", css);
        Assert.Matches(@"\.ns-nav-brand \.ns-chip-ellipsis\.mud-chip\s*\{[^}]*flex-shrink:\s*0;", css);
        Assert.Matches(
            @"\.ns-nav-brand:has\(\.ns-chip-ellipsis\) > :not\(:has\(\.ns-chip-ellipsis\), \.ns-chip-ellipsis, img, svg\),\s*"
            + @"\.ns-nav-brand :has\(\.ns-chip-ellipsis\) > :not\(:has\(\.ns-chip-ellipsis\), \.ns-chip-ellipsis, img, svg\)\s*"
            + @"\{[^}]*min-width:\s*0;[^}]*overflow-wrap:\s*anywhere;",
            css);
        Assert.DoesNotMatch(@"\.ns-nav-brand[^{}]*:not\(:has\(\.ns-chip-ellipsis\)[^{}]*\{[^}]*flex-shrink:\s*0;", css);
    }

    /// <summary>AC2: a logo or product mark, beside the chip or inside the name's box, keeps its
    /// size while the name wraps; the chip's own glyph is the vendor's.</summary>
    [Fact]
    public void TheImagesBesideTheNameKeepTheirSize()
    {
        var css = Uncommented(ReadStylesheet());

        Assert.Matches(
            @"\.ns-nav-brand:has\(\.ns-chip-ellipsis\) :is\(img, svg\):not\(\.ns-chip-ellipsis \*\)\s*\{[^}]*flex-shrink:\s*0;",
            css);
    }

    static string Uncommented(string css)
    {
        return Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
    }

    static string ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}

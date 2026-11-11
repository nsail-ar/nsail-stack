// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;

namespace NSail.Components.Tests;

/// <summary>nsail#3515: a chip in the drawer's brand row is capped at half the brand row itself,
/// not at half of whatever box a product's Brand wraps around it, and its painted edge — not the
/// vendor's margin — sits on the header's right line.</summary>
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

    /// <summary>The ellipsis clips <c>.mud-chip-content</c>, so the glyph must render outside it:
    /// inside, a short label would lose its letters to the icon's width.</summary>
    [Fact]
    public void TheChipsGlyphSitsOutsideTheContentTheEllipsisClips()
    {
        var cut = Render<NsChip>(p => p
            .Add(x => x.Text, "Prueba")
            .Add(x => x.Icon, NsIcons.Check));

        var content = cut.Find(".mud-chip-content");

        Assert.Empty(content.QuerySelectorAll("svg"));
        Assert.NotEmpty(cut.Find(".mud-chip").QuerySelectorAll("svg"));
        Assert.Equal("Prueba", content.TextContent.Trim());
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

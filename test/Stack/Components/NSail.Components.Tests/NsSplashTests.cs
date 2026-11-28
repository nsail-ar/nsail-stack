// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

public sealed class NsSplashTests : BunitContext
{
    public NsSplashTests()
    {
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    [Fact]
    public void WithoutALogoNoMarkIsDrawnAndTheSplashStillStands()
    {
        var cut = Render<NsSplash>();

        Assert.Empty(cut.FindAll("#ns-splash-logo"));
        Assert.NotNull(cut.Find("#ns-splash"));
        Assert.NotNull(cut.Find(".ns-splash-track"));
    }

    [Fact]
    public void TheSvgAllowlistTheScriptFiltersWithIsInjectedFromTheSanitizer()
    {
        var cut = Render<NsSplash>();
        var script = cut.Find("script").TextContent;

        foreach (var element in NSail.Icons.SvgSanitizer.AllowedElements)
        {
            Assert.Contains("\"" + element.ToLowerInvariant() + "\"", script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheInlinedMarkIsABlockSoNoBaselineGapSitsUnderIt()
    {
        var cut = Render<NsSplash>(p => p.Add(c => c.LogoUrl, "api/branding/logo"));
        var script = cut.Find("script").TextContent;

        Assert.Contains("root.style.display = \"block\";", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("root.style.display", StringComparison.Ordinal) < script.IndexOf("window.nsapp.fitMark(root)", StringComparison.Ordinal),
            "the svg must be a block before fitMark measures it");
    }

    [Fact]
    public void TheMarkIsDrawnInABandOfTheSharedHeight()
    {
        var cut = Render<NsSplash>(p => p.Add(c => c.LogoUrl, "api/branding/logo"));
        var band = cut.Find("#ns-splash-logo").GetAttribute("style");

        Assert.Contains($"height:{NsSplash.MarkHeight}px", band, StringComparison.Ordinal);
        Assert.Contains("align-items:flex-end", band, StringComparison.Ordinal);
    }

    // The mark's box is its artwork: a wide lockup is as tall as the block's width leaves, never a
    // band-high box with the drawing letterboxed inside it, which is the gap nsail#3109 measures.
    [Fact]
    public void TheMarkKeepsItsOwnRatioInsideTheBand()
    {
        var cut = Render<NsSplash>(p => p.Add(c => c.LogoUrl, "api/branding/logo"));
        var script = cut.Find("script").TextContent;
        var image = cut.Find("#ns-splash-logo img").GetAttribute("style");

        Assert.Contains("height:auto", image, StringComparison.Ordinal);
        Assert.Contains($"max-height:{NsSplash.MarkHeight}px", image, StringComparison.Ordinal);
        Assert.Contains("max-width:100%", image, StringComparison.Ordinal);
        Assert.Contains("root.style.aspectRatio = art.width + \" / \" + art.height;", script, StringComparison.Ordinal);
        Assert.Contains("root.style.height = \"auto\";", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFirstFrameIsABlockToo()
    {
        var cut = Render<NsSplash>(p => p.Add(c => c.LogoUrl, "api/branding/logo"));

        Assert.Contains("display:block", cut.Find("#ns-splash-logo > picture").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("display:block", cut.Find("#ns-splash-logo img").GetAttribute("style"), StringComparison.Ordinal);
    }
}

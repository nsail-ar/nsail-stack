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
        var script = cut.Find("script").TextContent;

        Assert.Contains($"height:{NsSplash.MarkHeight}px", cut.Find("#ns-splash-logo").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("height:100%", cut.Find("#ns-splash-logo img").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("root.style.height = \"100%\";", script, StringComparison.Ordinal);
        Assert.Contains("root.style.width = \"auto\";", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFirstFrameIsABlockToo()
    {
        var cut = Render<NsSplash>(p => p.Add(c => c.LogoUrl, "api/branding/logo"));

        Assert.Contains("display:block", cut.Find("#ns-splash-logo > picture").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("display:block", cut.Find("#ns-splash-logo img").GetAttribute("style"), StringComparison.Ordinal);
    }
}

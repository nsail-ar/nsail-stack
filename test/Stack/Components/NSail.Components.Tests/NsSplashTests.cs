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
    public void TheLogoAndTheBrandNameCarryTheRoutesTheCallerNamed()
    {
        var cut = Render<NsSplash>(ps => ps
            .Add(p => p.LogoUrl, "/api/logo")
            .Add(p => p.DarkLogoUrl, "/api/logo-dark")
            .Add(p => p.BrandUrl, "/api/brand"));

        var logo = cut.Find("#ns-splash-logo");

        Assert.Equal("/api/logo", logo.GetAttribute("data-logo"));
        Assert.Equal("/api/logo-dark", logo.GetAttribute("data-dark-logo"));
        Assert.Equal("/api/logo", logo.QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal("/api/logo-dark", logo.QuerySelector("source")!.GetAttribute("srcset"));
        Assert.Equal("/api/brand", cut.Find("#ns-splash-name").GetAttribute("data-brand-url"));
    }

    [Fact]
    public void WithoutADarkLogoTheOneLogoServesBothGrounds()
    {
        var cut = Render<NsSplash>(ps => ps.Add(p => p.LogoUrl, "/api/logo"));

        Assert.Empty(cut.FindAll("#ns-splash-logo source"));
        Assert.NotNull(cut.Find("#ns-splash-logo img"));
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
}

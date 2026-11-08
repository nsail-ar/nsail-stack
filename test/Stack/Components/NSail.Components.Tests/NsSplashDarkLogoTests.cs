// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The splash is drawn before any theme exists, so a brand's dark mark reaches it by the
/// OS scheme on the first frame and by the splash's own painted ground once the script has read it.</summary>
public sealed class NsSplashDarkLogoTests : BunitContext
{
    void Setup()
    {
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Loading"] = "Cargando"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    [Fact]
    public void ADarkMarkRidesTheFirstFrameAndTheScript()
    {
        Setup();

        var cut = Render<NsSplash>(parameters => parameters
            .Add(splash => splash.LogoUrl, "/api/branding/logo")
            .Add(splash => splash.DarkLogoUrl, "/api/branding/logo?dark=true"));

        var source = cut.Find("picture > source");

        Assert.Equal("(prefers-color-scheme: dark)", source.GetAttribute("media"));
        Assert.Equal("/api/branding/logo?dark=true", source.GetAttribute("srcset"));
        Assert.Equal("/api/branding/logo?dark=true", cut.Find("#ns-splash-logo").GetAttribute("data-dark-logo"));
        Assert.Equal("/api/branding/logo", cut.Find("picture > img").GetAttribute("src"));
    }

    [Fact]
    public void WithoutADarkMarkTheOneLogoServesBothGrounds()
    {
        Setup();

        var cut = Render<NsSplash>(parameters => parameters.Add(splash => splash.LogoUrl, "/api/branding/logo"));

        Assert.Empty(cut.FindAll("picture > source"));
        Assert.Null(cut.Find("#ns-splash-logo").GetAttribute("data-dark-logo"));
    }
}

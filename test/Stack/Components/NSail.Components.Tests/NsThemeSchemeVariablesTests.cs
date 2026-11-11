// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Tones;

namespace NSail.Components.Tests;

/// <summary>nsail#2744: the elevation ring and the filled info alert both answer to which SCHEME
/// is lit rather than to a free pick's luminance, so unlike --ns-accent-ink they are not swept
/// across the accent cube — the house's own two schemes are the whole domain. This pins what
/// <see cref="NsTheme"/> emits for each; the stylesheet actually reading it is <see
/// cref="LightPaletteFollowupTests"/>.</summary>
public sealed class NsThemeSchemeVariablesTests : BunitContext
{
    static readonly Brand TwoSchemeBrand = new();

    public NsThemeSchemeVariablesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Fact]
    public void DarkPaintsTheWhiteRingAndRestatesTheVendorsFlatInfoFill()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(false);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, true));

        var dark = BrandTheme.DefaultDark();

        Assert.Contains("--ns-elevation-ring:rgba(255,255,255,.06);", cut.Markup);
        Assert.Contains($"--ns-alert-info-bg:{dark.Info};", cut.Markup);
        Assert.Contains("--ns-alert-info-border:transparent;", cut.Markup);
        Assert.Contains($"--ns-alert-info-ink:{dark.TextOnInfo};", cut.Markup);
    }

    [Fact]
    public void LightPaintsTheBlackRingAndATintedInfoFace()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(true);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, false));

        var light = BrandTheme.DefaultLight();
        var expectedBg = BrandTone.Mix(light.Surface, light.Info, NsTheme.AlertInfoTintWeight);

        Assert.Contains("--ns-elevation-ring:rgba(0,0,0,.08);", cut.Markup);
        Assert.Contains($"--ns-alert-info-bg:{expectedBg};", cut.Markup);
        Assert.Contains($"--ns-alert-info-border:{light.Info};", cut.Markup);
        Assert.Contains($"--ns-alert-info-ink:{light.TextPrimary};", cut.Markup);

        // Never the raw fill nor the vendor's own ink: a theme that painted the dark pair in
        // light would be invisible on the canvas rather than merely loud.
        Assert.DoesNotContain($"--ns-alert-info-bg:{light.Info};", cut.Markup);
    }

    [Fact]
    public void TheColorSchemeFollowsTheLitSchemeAndFlipsWithIt()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(false);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, true));

        Assert.Contains("color-scheme:dark;", cut.Markup);

        cut.Render(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, false));

        Assert.Contains("color-scheme:light;", cut.Markup);
        Assert.DoesNotContain("color-scheme:dark;", cut.Markup);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheVendorsOwnScrollbarIsOffSoEveryScrollerInheritsTheHouses(bool dark)
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(dark);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, dark));

        Assert.DoesNotContain("scrollbar-color", cut.Markup);
        Assert.DoesNotContain("scrollbar-width", cut.Markup);
        Assert.DoesNotContain("::-webkit-scrollbar", cut.Markup);
        Assert.DoesNotContain("#c4c4c4", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
    // nsail#3655: a Brand's colours are written into a page-wide <style>, so one that is not a
    // colour paints as its scheme's default and nothing of it reaches either stylesheet.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AColourThatIsNotOnePaintsAsTheSchemesDefault(bool dark)
    {
        const string planted = "red;} body{background:url(//x)}";

        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(dark);

        var brand = new Brand();

        foreach (var scheme in new[] { brand.Light, brand.Dark })
        {
            scheme.Info = planted;
            scheme.TextOnInfo = planted;
            scheme.TextOnChrome = planted;
            scheme.Accent = planted;
        }

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, brand)
            .Add(x => x.Dark, dark));

        var expected = dark ? BrandTheme.DefaultDark() : BrandTheme.DefaultLight();

        Assert.DoesNotContain("url(//x)", cut.Markup);
        Assert.DoesNotContain("red;", cut.Markup);
        Assert.Contains($"--ns-accent-ink:{expected.AccentInk};", cut.Markup);
        Assert.Contains($"--ns-accent-on-chrome:{expected.AccentOnChrome};", cut.Markup);
        Assert.Contains(dark ? $"--ns-alert-info-ink:{expected.TextOnInfo};" : $"--ns-alert-info-border:{expected.Info};", cut.Markup);
    }

    // Unset preference and a device that has not answered: the dark block the stylesheet
    // relights with carries the vendor's declarations too.
    [Fact]
    public void AColourThatIsNotOneReachesNotEvenTheDevicesDarkBlock()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true);

        var brand = new Brand();

        brand.Dark.Info = "red;} body{background:url(//x)}";
        brand.Dark.RowHover = "#fff;} *{display:none}";

        var cut = Render<NsTheme>(p => p.Add(x => x.Brand, brand));

        Assert.Contains("@media (prefers-color-scheme: dark)", cut.Markup);
        Assert.DoesNotContain("url(//x)", cut.Markup);
        Assert.DoesNotContain("display:none", cut.Markup);
    }

    // K1: every hex shape a Brand carries today, the eight-digit RowHover included, as written.
    [Fact]
    public void EveryHexABrandCarriesPaintsAsWritten()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(true);

        var brand = new Brand();

        brand.Dark.Info = "#4af";
        brand.Dark.TextOnInfo = "#000a";

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, brand)
            .Add(x => x.Dark, true));

        Assert.Contains("--ns-alert-info-bg:#4af;", cut.Markup);
        Assert.Contains("--ns-alert-info-ink:#000a;", cut.Markup);
    }
}

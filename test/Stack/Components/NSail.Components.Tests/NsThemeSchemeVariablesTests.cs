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
        var expectedBg = BrandTone.Mix(light.Surface, light.Info, 0.12);

        Assert.Contains("--ns-elevation-ring:rgba(0,0,0,.08);", cut.Markup);
        Assert.Contains($"--ns-alert-info-bg:{expectedBg};", cut.Markup);
        Assert.Contains($"--ns-alert-info-border:{light.Info};", cut.Markup);
        Assert.Contains($"--ns-alert-info-ink:{light.TextPrimary};", cut.Markup);

        // Never the raw fill nor the vendor's own ink: a theme that painted the dark pair in
        // light would be invisible on the canvas rather than merely loud.
        Assert.DoesNotContain($"--ns-alert-info-bg:{light.Info};", cut.Markup);
    }
}

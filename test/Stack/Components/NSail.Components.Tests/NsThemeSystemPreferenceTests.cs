// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using MudBlazor.Utilities;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>A page with no saved preference — an anonymous visitor, or a signed-in reader who
/// never touched Mi Cuenta's Apariencia — paints the device's scheme, never the brand's
/// <see cref="Brand.DefaultDark"/>. Until a client answers, the stylesheet reads the device
/// itself (<c>prefers-color-scheme</c>); the OS query (<c>window.matchMedia</c>) is asked once
/// after the first interactive render and kept current by the vendor's own change watcher. A
/// saved Claro/Oscuro wins outright.</summary>
public sealed class NsThemeSystemPreferenceTests : BunitContext
{
    static readonly Brand TwoSchemeBrand = new();

    public NsThemeSystemPreferenceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Fact]
    public void UnsetPreferenceFollowsTheSystemSchemeOnceTheClientAnswers()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(false);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, (bool?)null));

        cut.WaitForAssertion(() => Assert.DoesNotContain("prefers-color-scheme", cut.Markup));
        Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", cut.Markup);
    }

    [Fact]
    public void ASavedPreferenceIgnoresWhatTheOsReports()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(false);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, true));

        cut.WaitForAssertion(() => Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultDark().AccentInk};", cut.Markup));
        Assert.DoesNotContain($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", cut.Markup);
    }

    [Fact]
    public void AutomaticoFollowsALiveSchemeChange()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true).SetResult(true);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, (bool?)null));

        cut.WaitForAssertion(() => Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultDark().AccentInk};", cut.Markup));

        var provider = cut.FindComponent<MudThemeProvider>().Instance;

        _ = provider.SystemDarkModeChangedAsync(false);

        cut.WaitForAssertion(() => Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", cut.Markup));
    }

    // The window between mounting and the client's first answer — a prerender, where nothing
    // ever answers at all, stays here forever. The gate holds the JS call open exactly the way
    // GatedBrandProvider holds a brand read open in NsSetupBrandTests, so the un-resolved state is
    // actually observed rather than assumed from timing.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnUnansweredDeviceIsReadByTheStylesheetWhateverTheBrandsDefault(bool brandDefaultDark)
    {
        var gate = JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, new Brand { DefaultDark = brandDefaultDark })
            .Add(x => x.Dark, (bool?)null));

        var device = Regex.Match(cut.Markup, @"@media \(prefers-color-scheme: dark\)\{(?<dark>.*?\})\}", RegexOptions.Singleline);

        Assert.True(device.Success, "An unset preference with no answer yet carries no prefers-color-scheme block.");

        var lit = cut.Markup[..device.Index];
        var dark = device.Groups["dark"].Value;

        Assert.Contains("color-scheme:light;", lit);
        Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", lit);
        Assert.Contains($"--mud-palette-background: {Rgba(BrandTheme.DefaultLight().Background)};", lit);
        Assert.Contains("color-scheme:dark;", dark);
        Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultDark().AccentInk};", dark);
        Assert.Contains($"--mud-palette-background: {Rgba(BrandTheme.DefaultDark().Background)};", dark);
        Assert.Contains("--mud-native-html-color-scheme: dark;", dark);

        gate.SetResult(false);

        cut.WaitForAssertion(() => Assert.DoesNotContain("prefers-color-scheme", cut.Markup));
        Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", cut.Markup);
    }

    [Fact]
    public void ASavedPreferenceCarriesNoDeviceBlock()
    {
        JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true);

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, TwoSchemeBrand)
            .Add(x => x.Dark, false));

        Assert.DoesNotContain("prefers-color-scheme", cut.Markup);
    }

    static string Rgba(string hex)
    {
        return new MudColor(hex).ToString(MudColorOutputFormats.RGBA);
    }
}

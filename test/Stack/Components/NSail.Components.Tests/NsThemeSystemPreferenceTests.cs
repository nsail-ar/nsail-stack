// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>nsail#2744 (folded from #2730): a page with no saved preference — an anonymous
/// visitor, or a signed-in reader who never touched Mi Cuenta's Apariencia — used to paint the
/// brand's own <see cref="BrandTheme.IsDark"/> rather than the visitor's own OS scheme, because
/// NsTheme only ever read <see cref="NsTheme.Dark"/> (null) against that default. The OS query is
/// a client fact (<c>window.matchMedia</c>), so it is asked once after the first interactive
/// render and kept current by the vendor's own change watcher — a saved Claro/Oscuro still wins
/// outright, and a prerender with no client to ask yet keeps the brand's own default until one
/// answers.</summary>
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

        cut.WaitForAssertion(() => Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", cut.Markup));
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
    [Fact]
    public void QueryUnavailableKeepsTheBrandsOwnDefaultUntilItResolves()
    {
        var gate = JSInterop.Setup<bool>("mudThemeProvider.isDarkMode", _ => true);

        var brand = new Brand { DefaultDark = true };

        var cut = Render<NsTheme>(p => p
            .Add(x => x.Brand, brand)
            .Add(x => x.Dark, (bool?)null));

        Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultDark().AccentInk};", cut.Markup);

        gate.SetResult(false);

        cut.WaitForAssertion(() => Assert.Contains($"--ns-accent-ink:{BrandTheme.DefaultLight().AccentInk};", cut.Markup));
    }
}

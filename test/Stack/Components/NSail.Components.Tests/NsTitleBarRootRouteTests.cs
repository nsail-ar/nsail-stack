// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>NsTitleBar derives its icon from the page it is standing on, and standing on the app
/// root — Optical's home, every app's home — the address it reads is the empty string, which
/// RouteTable.Match used to refuse outright: every root-routed page deriving an icon walked into
/// an ArgumentException. The root is a route like any other, and these pin both halves of that —
/// it does not throw, and it resolves the root page's own menu glyph rather than nothing. On the
/// main surface that glyph reaches the eye through the announcement the app bar draws
/// (nsail#38), which is where it is read from here.</summary>
public sealed class NsTitleBarRootRouteTests : BunitContext
{
    static readonly Glyph HomeGlyph = NsIcons.Badge;

    sealed class FakeJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }
    }

    SurfaceContext Setup()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsTitleBarRootRouteTests).Assembly, []));
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Home", Icon = HomeGlyph, PageType = typeof(HomeTestPage) }));
        Services.AddScoped<NavMenu>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        // The app root: bUnit's navigation manager starts at the base address, so
        // ToBaseRelativePath answers the empty string — exactly what a user on "/" produces.
        navigation.NavigateTo("/");

        return new SurfaceContext(null, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    [Fact]
    public void APageRoutedAtTheRootDerivesItsMenuGlyph()
    {
        var surface = Setup();

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Inicio"));

        // Read out of the page's own bar: the minimal top bar draws the same announcement at
        // the widths the drawer is hidden at, so the host holds the one slot twice over.
        var icon = Assert.Single(cut.FindComponent<NsTitleBar>().FindComponents<NsIcon>());

        Assert.Equal(HomeGlyph, icon.Instance.Icon);
    }
}

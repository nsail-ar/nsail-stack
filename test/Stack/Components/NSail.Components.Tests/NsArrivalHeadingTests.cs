// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

/// <summary>nsail#2338: the arrival fallback is a selector ("h1", NsArrivalFocus), and a selector
/// finds nothing unless something draws the element — no screen component drew one, so every
/// screen that marks no field of its own took no arrival focus at all and a screen reader never
/// heard the page's name. The screen's own title row draws it, focusable, and only the row that
/// is the screen's subject does: a page carries ONE top-level heading. The focus itself happens
/// in ns.js and is proven by the Optical E2E; what is pinned here is the markup it reads.</summary>
public sealed class NsArrivalHeadingTests : BunitContext, IAsyncLifetime
{
    static readonly Glyph PageGlyph = NsIcons.Badge;

    // AddMudServices registers the vendor's popover service, which only implements
    // IAsyncDisposable — a synchronous teardown throws on it by design.
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

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

    SurfaceContext Setup(Surface? name = null)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Loading"] = "Cargando",
            ["Common.Close"] = "Cerrar",
            ["Common.Menu"] = "Menú"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(NsArrivalHeadingTests).Assembly, []));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        return new SurfaceContext(name, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    [Fact]
    public void TheScreensOwnRowNamesItWithTheOneTopLevelHeading()
    {
        var surface = Setup();

        var cut = Render<TitleBarSurfaceHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Personas")
            .Add(x => x.Icon, PageGlyph));

        var heading = Assert.Single(cut.FindAll("h1"));

        Assert.Equal("Personas", heading.TextContent);
    }

    /// <summary>What the fallback needs beyond the element: focus() moves the cursor only onto
    /// something that can hold it, so a heading with no tabindex leaves the call a no-op — the
    /// same price the shell's skip link pays for the region it lands in (NsMainLayout).</summary>
    [Fact]
    public void TheHeadingCanHoldTheCursorTheArrivalHandsIt()
    {
        var surface = Setup();

        var cut = Render<TitleBarSurfaceHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Personas")
            .Add(x => x.Icon, PageGlyph));

        Assert.Equal("-1", cut.Find("h1").GetAttribute("tabindex"));
    }

    /// <summary>The heading rank is the element alone: the row is set in the same type scale it
    /// always was, so nothing under the eye changes size for an accessibility fix.</summary>
    [Fact]
    public void TheHeadingKeepsTheTitleRowsOwnTypeScale()
    {
        var surface = Setup();

        var cut = Render<TitleBarSurfaceHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Personas")
            .Add(x => x.Icon, PageGlyph));

        Assert.Contains("mud-typography-h6", cut.Find("h1").ClassList);
    }

    /// <summary>A panel below the screen's subject names itself too (PasswordPage, Contraseña
    /// above Segundo factor) — and a second top-level heading would name the screen twice, which
    /// is why the heading rides the row that claimed the surface and no other.</summary>
    [Fact]
    public void ASectionsRowIsNoSecondTopLevelHeading()
    {
        var surface = Setup();

        var cut = Render<TwoTitleRowsHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, new NsLayoutState()));

        var heading = Assert.Single(cut.FindAll("h1"));

        Assert.Equal("Contraseña", heading.TextContent);

        var section = cut.FindComponents<NsTitleBar>()[1];

        Assert.Empty(section.FindAll("h1"));
        Assert.Contains("Segundo factor", section.Find(".mud-typography-h6").TextContent, StringComparison.Ordinal);
    }

    /// <summary>An overlay's row names what opened OVER the screen, not the screen: the page
    /// underneath keeps its own name, and the arrival focus of a navigation into an overlay is
    /// the form's first field anyway (ui/focus.md).</summary>
    [Fact]
    public void AnOverlaysRowDrawsNoTopLevelHeading()
    {
        var surface = Setup(Surfaces.Aside);

        var cut = Render<TitleBarSurfaceHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Nueva Persona")
            .Add(x => x.Icon, PageGlyph));

        Assert.Empty(cut.FindAll("h1"));

        var title = cut.Find(".mud-typography-h6");

        Assert.Contains("Nueva Persona", title.TextContent, StringComparison.Ordinal);

        // And it is not made focusable either: a row that is not the page's heading is text, and
        // a tabindex on it would be a cursor stop nothing asked for.
        Assert.False(title.HasAttribute("tabindex"));
    }
}

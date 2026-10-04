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

// The family: a list with a door of its own, and the create, edit and detail screens that hang
// under its address with no door anywhere — the shape of every "Nuevo X" and "Editar X" in the
// app (/optical/clients/new, /directory/parties/{id}/edit).
[Route("/clients")]
public sealed class ClientsListTestPage : ComponentBase;

[Route("/clients/new")]
public sealed class NewClientTestPage : ComponentBase;

[Route("/clients/{Id:guid}/edit")]
public sealed class EditClientTestPage : ComponentBase;

// Routed under nothing that carries a glyph: the walk has to end at null rather than at the
// app root's.
[Route("/orphans/new")]
public sealed class OrphanTestPage : ComponentBase;

/// <summary>A page's title glyph is derived from the ADDRESS it was opened at, not from the page
/// being a menu entry: a create, an edit or a detail hanging under a list wears that list's door
/// glyph, so every "Nuevo Cliente" aside stopped opening with an empty icon slot (nsail#1865).
/// The walk stops before the app root — the home's glyph on a page that derived none would be a
/// default, and icons are chosen (intentional-ui.md).</summary>
public sealed class NsTitleBarDerivedIconTests : BunitContext, IAsyncLifetime
{
    static readonly Glyph ClientsGlyph = NsIcons.People;

    static readonly Glyph HomeGlyph = NsIcons.Badge;

    // An aside's bar draws its own close icon, whose tooltip pulls in a MudBlazor service that
    // is IAsyncDisposable-only — bUnit's synchronous teardown cannot dispose it, so teardown
    // is routed to the async one (the note every popover-hosting fixture here carries).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
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

    // Only the list has a door, and the home has one so a walk that fell through to the root
    // would be visible as the root's own glyph rather than as nothing.
    SurfaceContext Standing(string address)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsTitleBarDerivedIconTests).Assembly, []));
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Home", Icon = HomeGlyph, PageType = typeof(HomeTestPage) },
            new NavMenuItem { Name = "Clients", Icon = ClientsGlyph, PageType = typeof(ClientsListTestPage) }));
        Services.AddScoped<NavMenu>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(address);

        return new SurfaceContext(null, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    Glyph? Derived(SurfaceContext surface, Glyph? icon = null)
    {
        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Cliente")
            .Add(x => x.Icon, icon));

        return cut
            .FindComponent<NsTitleBar>()
            .FindComponents<NsIcon>()
            .Select(rendered => rendered.Instance.Icon)
            .FirstOrDefault();
    }

    [Fact]
    public void ACreateUnderAListWearsTheListsGlyph()
    {
        Assert.Equal(ClientsGlyph, Derived(Standing("/clients/new")));
    }

    // The edit's own segment is below a route token, so the walk passes an address that matches
    // no page at all on its way up — /clients/{id} is nobody's route here.
    [Fact]
    public void AnEditUnderARecordWearsTheListsGlyph()
    {
        Assert.Equal(ClientsGlyph, Derived(Standing($"/clients/{Guid.NewGuid()}/edit")));
    }

    // An aside is routed by the QUERY and not by the browser's path, which still names the
    // screen standing behind the overlay — so the aside over a screen outside the family wears
    // the glyph of its own address, and the surface under it derives none.
    [Fact]
    public void AnAsideDerivesFromItsOwnAddressAndNotTheScreenBehindIt()
    {
        var behind = Standing("/orphans/new?aside=/clients/new");

        var aside = new SurfaceContext(
            new Surface("aside"),
            Services.GetRequiredService<NavigationManager>(),
            Services.GetRequiredService<RouteTable>(),
            new FakeJs(),
            new SurfaceHistory());

        Assert.Equal(ClientsGlyph, Derived(aside));
        Assert.Null(Derived(behind));
    }

    // The list itself still answers for its own door — the walk's first step is the address as
    // it stands, so nothing about an entry's own page changed.
    [Fact]
    public void AListStillWearsItsOwnDoorsGlyph()
    {
        Assert.Equal(ClientsGlyph, Derived(Standing("/clients")));
    }

    [Fact]
    public void APageSetsItsOwnGlyphAndKeepsIt()
    {
        Assert.Equal(NsIcons.Person, Derived(Standing("/clients/new"), NsIcons.Person));
    }

    // Nothing above it carries a glyph, and the root's own is not borrowed to fill the slot.
    [Fact]
    public void APageUnderNoDoorDerivesNothing()
    {
        Assert.Null(Derived(Standing("/orphans/new")));
    }
}

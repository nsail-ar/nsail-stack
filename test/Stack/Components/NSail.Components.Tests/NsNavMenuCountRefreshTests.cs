// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/counted/inbox")]
public sealed class CountedInboxProbePage : ComponentBase;

[Route("/counted/elsewhere")]
public sealed class CountedElsewhereProbePage : ComponentBase;

/// <summary>nsail#1479. The number beside a door used to be asked again only when somebody
/// moved through the app, which is a refresh policy that cannot see work ARRIVING: a person
/// sitting on one screen watched a stale number all afternoon. NavMenuCountsChanged is the
/// Stack's generic word for "the numbers moved, ask again" — generic because the drawer may
/// not learn what any module counts, so the only thing it can be told is to ask.</summary>
public sealed class NsNavMenuCountRefreshTests : BunitContext
{
    readonly CapturingMediator _mediator = new();

    readonly CountingNavCount _count = new("Inbox");

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IRenderedComponent<NsNavMenu> RenderMenu()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuCountRefreshTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(_mediator);
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Inbox", PageType = typeof(CountedInboxProbePage) }));
        Services.AddSingleton<INavMenuCount>(_count);
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization().SetAuthorized("counted-probe");

        return Render<NsNavMenu>();
    }

    /// <summary>The headline: nobody navigated, nobody touched the drawer, and the number on
    /// screen is the new one.</summary>
    [Fact]
    public async Task TheNumberIsAskedAgainWhenSomethingSaysTheCountsMoved()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal("2", menu.Find(".ns-nav-count").TextContent));

        _count.Answer = 5;

        await _mediator.Publish(new NavMenuCountsChanged());

        menu.WaitForAssertion(() => Assert.Equal("5", menu.Find(".ns-nav-count").TextContent));
    }

    /// <summary>And the redraw is the event's own work, not a render somebody else happened to
    /// cause: a handler that only assigns a field leaves the change invisible (NsPartial's own
    /// note), which is the defect this hand-rolled subscription has to avoid without NsPartial.
    /// Asserted by counting asks — the recount really ran — beside the number above.</summary>
    [Fact]
    public async Task TheAskItselfHappensOnTheEventAndNotOnTheNextRender()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal(1, _count.Calls));

        await _mediator.Publish(new NavMenuCountsChanged());

        menu.WaitForAssertion(() => Assert.Equal(2, _count.Calls));
    }

    /// <summary>The drawer's other moment: somebody arrived somewhere, which is the moment they
    /// look at the map again and the one a page's own reads are being paid for anyway.</summary>
    [Fact]
    public void AnArrivalSomewhereElseIsCountedAgain()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal(1, _count.Calls));

        Navigation.NavigateTo("/counted/inbox");

        menu.WaitForAssertion(() => Assert.Equal(2, _count.Calls));

        Navigation.NavigateTo("/counted/elsewhere");

        menu.WaitForAssertion(() => Assert.Equal(3, _count.Calls));
    }

    /// <summary>nsail#1934. A surface writes itself onto the address of the page the person is
    /// already standing at, so an aside opening and closing is two location changes in which
    /// nobody went anywhere — and the drawer was paying a round trip per contributor for each,
    /// inside the window of every save taken in an overlay. What moved the work says so itself
    /// (NavMenuCountsChanged), which is why dropping this costs no freshness.</summary>
    [Fact]
    public void ASurfaceOpeningOverTheSamePageIsNotAnArrival()
    {
        var menu = RenderMenu();

        Navigation.NavigateTo("/counted/inbox");

        menu.WaitForAssertion(() => Assert.Equal(2, _count.Calls));

        Navigation.NavigateTo("/counted/inbox?aside=counted%2Fsomething%2Fnew");
        Navigation.NavigateTo("/counted/inbox");

        // The drawer still redraws — the active entry and its expansion are a function of the
        // query too — so this is the ask being dropped and not the render.
        menu.WaitForAssertion(() => Assert.NotNull(menu.Find(".ns-nav-count")));

        Assert.Equal(2, _count.Calls);
    }

    /// <summary>A drawer that went away stops asking. The subscription is the component's, so a
    /// Dispose that forgot it would leave a torn-down tree being recounted on every arrival for
    /// the rest of the session.</summary>
    [Fact]
    public async Task ADisposedDrawerIsNotRecounted()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal(1, _count.Calls));

        await DisposeComponentsAsync();

        await _mediator.Publish(new NavMenuCountsChanged());

        Assert.Equal(1, _count.Calls);
    }
}

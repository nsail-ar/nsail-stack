// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/hidden-count/elsewhere")]
[Authorize<OwnRead>]
public sealed class HiddenCountElsewhereProbePage : ComponentBase;

/// <summary>nsail#4331: a count belongs to the door it is drawn beside. A patient-portal session
/// was never drawn Tickets, and every load still asked its open count — a 403 and a warning in
/// the console. Each way the drawer has of leaving a door out is one hidden counter here, and
/// none of them may be asked at any of the drawer's moments.</summary>
public sealed class NsNavMenuHiddenCountTests : BunitContext, IAsyncLifetime
{
    // The vendor's popover service AddMudServices registers is IAsyncDisposable only — a
    // synchronous dispose of the container throws on it.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    readonly CapturingMediator _mediator = new();

    readonly CountingNavCount _drawn = new("Drawn");

    readonly CountingNavCount[] _hidden =
    [
        new("RefusedPermission"),
        new("ClosedPage"),
        new("GoneGroup"),
        new("GoneChild"),
        new("Arranged")
    ];

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IRenderedComponent<NsNavMenu> RenderMenu(params INavMenuCount[] extra)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuHiddenCountTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(_mediator);
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Drawn", PageType = typeof(PermittedAppointmentsProbePage) },
            new NavMenuItem { Name = "Throwing", PageType = typeof(PermittedMeasuresProbePage) },
            new NavMenuItem
            {
                Name = "RefusedPermission",
                PageType = typeof(PermittedAppointmentsProbePage),
                Permission = typeof(CounterRead)
            },
            new NavMenuItem { Name = "ClosedPage", PageType = typeof(PermittedCounterProbePage) },
            new NavMenuItem
            {
                Name = "GoneGroup",
                Items = [new NavMenuItem { Name = "GoneChild", PageType = typeof(PermittedCounterProbePage) }]
            },
            new NavMenuItem { Name = "Arranged", PageType = typeof(PermittedAppointmentsProbePage), Visible = false }));
        Services.AddSingleton<INavMenuCount>(_drawn);

        foreach (var count in _hidden.Concat(extra))
        {
            Services.AddSingleton<INavMenuCount>(count);
        }

        JSInterop.Mode = JSRuntimeMode.Loose;

        // The real fabrication path, as NsNavMenuEntryPermissionTests takes it: the session holds
        // OwnRead and not CounterRead.
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<AuthenticationStateProvider>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationPolicyProvider, MessagePolicyProvider>();
        Services.AddSingleton<IAuthorizationHandler>(new GrantedMessages(typeof(OwnRead)));
        Services.AddSingleton<AuthenticationStateProvider>(new ProbeAuthenticationStateProvider());

        return Render<NsNavMenu>();
    }

    [Fact]
    public void AHiddenEntrysCountIsNotAskedWhenTheDrawerBuilds()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal(1, _drawn.Calls));

        Assert.All(_hidden, count => Assert.Equal(0, count.Calls));
    }

    [Fact]
    public void AHiddenEntrysCountIsNotAskedOnAnArrival()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal(1, _drawn.Calls));

        Navigation.NavigateTo("/hidden-count/elsewhere");

        menu.WaitForAssertion(() => Assert.Equal(2, _drawn.Calls));

        Assert.All(_hidden, count => Assert.Equal(0, count.Calls));
    }

    [Fact]
    public async Task AHiddenEntrysCountIsNotAskedWhenTheCountsMoved()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal(1, _drawn.Calls));

        await _mediator.Publish(new NavMenuCountsChanged());

        menu.WaitForAssertion(() => Assert.Equal(2, _drawn.Calls));

        Assert.All(_hidden, count => Assert.Equal(0, count.Calls));
    }

    /// <summary>AC2: the door that is drawn is asked as before and carries its number.</summary>
    [Fact]
    public void ADrawnEntrysCountIsAskedAndDrawnBesideIt()
    {
        var menu = RenderMenu();

        menu.WaitForAssertion(() => Assert.Equal("2", menu.Find(".ns-nav-count").TextContent));

        Assert.Single(menu.FindAll(".ns-nav-count"));
    }

    /// <summary>K3: a drawn door whose count throws is left out, and the drawer still draws the
    /// map and the number that did answer.</summary>
    [Fact]
    public void ADrawnEntrysThrowingCountIsLeftOutAndTheDrawerDraws()
    {
        var menu = RenderMenu(new ThrowingNavCount("Throwing"));

        menu.WaitForAssertion(() => Assert.Equal("2", menu.Find(".ns-nav-count").TextContent));

        Assert.Contains("Throwing", menu.Markup);
        Assert.Single(menu.FindAll(".ns-nav-count"));
    }

    sealed class ThrowingNavCount(string name) : INavMenuCount
    {
        public string Name { get; } = name;

        public Task<int> GetCount()
        {
            throw new InvalidOperationException("down");
        }
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/purchasing/orders")]
public sealed class PurchaseOrdersProbePage : ComponentBase;

[Route("/purchasing/receipts")]
public sealed class ReceiptsProbePage : ComponentBase;

/// <summary>Leonardo, 2026-08-10, with the drawer in front of him: "me gustaba el menu compras,
/// que vuelva". Compras holds one screen today (Órdenes de Compra), and the single-child collapse
/// had flattened it onto the top level — the section's name gone from the map, and with it the
/// place recepciones and proveedores are going to be born in. The collapse keeps the case that
/// bore it (a settings group is a list of screens, so a fold around one screen is ceremony) and
/// loses the main nav, which is what these render.</summary>
public sealed class NsNavMenuGroupScopeTests : BunitContext
{
    void Setup(params NavMenuItem[] items)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuGroupScopeTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(items));
        JSInterop.Mode = JSRuntimeMode.Loose;

        this.AddAuthorization();
    }

    static NavMenuItem Purchasing(params NavMenuItem[] children)
    {
        return new NavMenuItem
        {
            Name = "Purchasing",
            Icon = new Glyph("receipt"),
            Weight = 20,
            Items = children
        };
    }

    static NavMenuItem PurchaseOrders()
    {
        return new NavMenuItem { Name = "PurchaseOrders", PageType = typeof(PurchaseOrdersProbePage) };
    }

    /// <summary>The regression itself: Compras renders as a group with its one screen inside,
    /// not as that screen sitting at the top level under its own name.</summary>
    [Fact]
    public void ANavGroupHoldingOneScreenStillRendersAsAGroup()
    {
        Setup(Purchasing(PurchaseOrders()));

        var cut = Render<NsNavMenu>();

        var group = cut.Find(".mud-nav-group");

        Assert.Contains("Purchasing", group.TextContent);
        Assert.NotNull(group.QuerySelector("a[href='/purchasing/orders']"));
    }

    /// <summary>And the entry is inside the fold rather than beside it: before the scope, the
    /// link was the top-level node itself and there was no group to be inside of.</summary>
    [Fact]
    public void TheOneScreenIsNotAlsoATopLevelEntry()
    {
        Setup(Purchasing(PurchaseOrders()));

        var cut = Render<NsNavMenu>();

        var menu = cut.Find(".mud-navmenu");

        var topLevel = menu.Children.Where(child => child.TagName == "A").ToList();

        Assert.Empty(topLevel);
    }

    /// <summary>Nothing about a group with two members moved — the same render either way.</summary>
    [Fact]
    public void ANavGroupHoldingTwoScreensRendersAsAGroupToo()
    {
        Setup(Purchasing(
            PurchaseOrders(),
            new NavMenuItem { Name = "Receipts", PageType = typeof(ReceiptsProbePage) }));

        var cut = Render<NsNavMenu>();

        var group = cut.Find(".mud-nav-group");

        Assert.NotNull(group.QuerySelector("a[href='/purchasing/orders']"));
        Assert.NotNull(group.QuerySelector("a[href='/purchasing/receipts']"));
    }
}

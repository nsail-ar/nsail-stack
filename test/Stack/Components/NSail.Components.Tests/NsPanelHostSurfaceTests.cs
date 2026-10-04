// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Messaging.Runtime;

namespace NSail.Components.Tests;

/// <summary>The scroll shadow's covers paint --ns-panel-surface, so the var has to equal the
/// background its host actually paints — equal it, and the cover is invisible and hides the
/// shadow at rest; miss it, and the panel draws a band of a foreign colour across the top and
/// bottom of every scrolling region (Leonardo, dark theme: "panel con fondo negro?" on Nueva
/// Dirección, a Background band over a dialog; and, measured in Chromium on the light theme,
/// a Background band over the drawer's white).
///
/// ns-mud.css binds the var per host on the selector that carries the PAINT — .mud-drawer,
/// .mud-dialog, .ns-dialog — never on a house class riding along with it, and what these
/// tests pin is the half a render can see: that for each host, the element matching that
/// selector really is an ancestor of the panel's .ns-scroll-shadow region. Both drifts that
/// produced Leonardo's bands were exactly that ancestry breaking under a selector that had
/// stopped naming the painting box — .ns-drawer never reached the nav drawer, and
/// .mud-dialog.mud-paper names a compound MudBlazor does not render at all. What no bUnit
/// render can reach is the computed colour itself; that stays a live probe.</summary>
public sealed class NsPanelHostSurfaceTests : BunitContext, IAsyncLifetime
{
    public NsPanelHostSurfaceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddScoped<IBrowserViewportService>(_ => new FixedBreakpointViewportService(Breakpoint.Lg));

        // Registered after AddComponentServices so this one wins: the table it builds is the
        // host app's, and the probe pages the nav contributor names live in this assembly.
        Services.AddSingleton(new RouteTable(typeof(NsPanelHostSurfaceTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(
            new NavMenuItem { Name = "Parties", PageType = typeof(NavMenuProbePage) }));

        this.AddAuthorization();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    /// <summary>An ephemeral dialog (DialogManager.Open — LocationForm's own host). The paint
    /// and the var both hang off .mud-dialog: MudBlazor's dialog is not a paper, so the
    /// compound that named one matched nothing and the panel inside fell through to the root
    /// default — Background, which over a dialog in the dark palette reads as black.</summary>
    [Fact]
    public void ADialogHostedPanel_ScrollsInsideTheBoxThatPaintsTheDialog()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogPanelProbeBody>("Dialog title"));

        var dialog = host.WaitForElement(".mud-dialog");

        Assert.NotNull(dialog.QuerySelector(".ns-scroll-shadow"));

        // The half that broke: .mud-paper is not among the dialog's classes, so any selector
        // demanding both matches nothing MudBlazor renders.
        Assert.DoesNotContain("mud-paper", dialog.ClassList);
    }

    /// <summary>The nav drawer (NsNavMenu became an NsPanel in 8c0f65ac). Its host is a
    /// MudDrawer like the aside's, and the class the house puts on it — ns-nav-drawer — is not
    /// the aside's, which is how the var scoped to .ns-drawer stopped reaching it.</summary>
    [Fact]
    public void TheNavDrawersPanel_ScrollsInsideTheBoxThatPaintsTheDrawer()
    {
        var host = Render<NavDrawerPanelHost>();

        var drawer = host.Find(".mud-drawer");

        Assert.NotNull(drawer.QuerySelector(".ns-scroll-shadow"));

        // The house class that used to carry the var is on this drawer's sibling, not on it.
        Assert.DoesNotContain("ns-drawer", drawer.ClassList);
    }

    /// <summary>The aside. Same vendor box as the nav drawer, and it carries its own paint —
    /// the content surface, where the rail carries the brand's chrome — so what the var is
    /// bound to is the box that paints, whichever of the two it turns out to be.</summary>
    [Fact]
    public void AnAsideHostedPanel_ScrollsInsideTheBoxThatPaintsTheDrawer()
    {
        var host = Render<AsidePanelHost>(parameters => parameters
            .Add(x => x.RouteTable, new RouteTable(typeof(NsPanelHostSurfaceTests).Assembly, [])));

        var drawer = host.Find(".mud-drawer");

        Assert.NotNull(drawer.QuerySelector(".ns-scroll-shadow"));
    }

    /// <summary>The main surface is the case with no host of its own: nothing between the
    /// panel and the document paints, so the var stays at its :root value and the cover paints
    /// the body's own Background.</summary>
    [Fact]
    public void AMainSurfacePanel_SitsUnderNoPaintingHostAtAll()
    {
        var cut = Render<DialogPanelProbeBody>();

        var scroller = cut.Find(".ns-scroll-shadow");

        Assert.Null(scroller.Closest(".mud-drawer"));
        Assert.Null(scroller.Closest(".mud-dialog"));
        Assert.Null(scroller.Closest(".ns-dialog"));
    }
}

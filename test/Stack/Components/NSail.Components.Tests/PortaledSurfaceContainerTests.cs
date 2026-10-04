// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>Emmanuel, 2026-08-11 (a screenshot of the Turno dialog at ~960px: Confirmar,
/// Reprogramar and Cancelar rendered as bare glyphs). The d-c-* utilities are container
/// queries, and a container query with no container-type ancestor matches NOTHING — which
/// leaves the mobile-first half of the pair standing on its own: d-c-none wins at every width,
/// so NsResponsive's collapsed label was hidden forever and every icon+label button inside a
/// dialog was icon-only for good. The only ns-container declarations in the house are
/// NsPanel's and NsCard's, and a dialog hosting a flat form (AppointmentActionsForm) has
/// neither above it — a dialog is portaled out of the page's tree entirely.
/// <para>The cure is that every surface that leaves the layout's tree declares the container
/// its own content is measured in. bUnit runs no container query, so what is pinned here is
/// the declaration: which box carries ns-container in each surface, and — for the aside — the
/// deliberate absence.</para></summary>
public sealed class PortaledSurfaceContainerTests : BunitContext, IAsyncLifetime
{
    public PortaledSurfaceContainerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddScoped<IBrowserViewportService>(_ => new FixedBreakpointViewportService(Breakpoint.Lg));
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    /// <summary>The reproduction's own surface: DialogManager.Open hands the component to
    /// MudDialogProvider, a sibling of the router, so nothing of the page is above it.</summary>
    [Fact]
    public void AHostDialogsContentBox_IsTheContainerItsOwnControlsMeasureAgainst()
    {
        var host = Render<DialogHostFixture>();
        var dialogs = Services.GetRequiredService<DialogManager>();

        // Open completes when the dialog CLOSES, so awaiting it here would hang the test.
        _ = host.InvokeAsync(() => dialogs.Open<DialogProbeBody>("Dialog title"));

        var content = host.WaitForElement(".mud-dialog-content");

        Assert.Contains("ns-container", content.ClassList);

        // The flex column 02c6f834 put on the same box stays: that fix is what lets a hosted
        // NsPanel's footer stay pinned, and a container declaration must not cost it.
        Assert.Contains("d-flex", content.ClassList);
        Assert.Contains("flex-column", content.ClassList);
    }

    /// <summary>The routed modal is not portaled by a provider, but it is a fixed box over the
    /// page: the layout chrome above it declares no container either (NsContainer centres the
    /// main surface, it does not measure it), so a modal hosting a flat form was just as
    /// blind.</summary>
    [Fact]
    public void TheRoutedModalsContentBox_DeclaresTheSameContainer()
    {
        var host = RenderStack();

        var content = host.Find(".ns-dialog .ns-container");

        Assert.NotNull(content.QuerySelector(".probe-modal"));

        // The measuring reference is not the scroll owner: the dialog frame is a fixed box
        // and does not scroll its own content, only NsPanel does — min-h-0 is what lets a
        // hosted NsPanel's own Content div shrink to the room this box actually has.
        Assert.DoesNotContain("overflow-y-auto", content.ClassList);
        Assert.Contains("min-h-0", content.ClassList);
    }

    /// <summary>The aside's verdict, and it is "nothing to do" — recorded rather than assumed.
    /// A drawer that declared itself a container would measure the panel's own padding into the
    /// width the panel then measures against (NsFormGridTests pins that the element a form's
    /// grid resolves against is the hosted NsPanel, and that the drawer is not it). What an
    /// aside hosts is a routed page, and a page inside a surface composes NsPanel by doctrine —
    /// so the container is already there, one level in.</summary>
    [Fact]
    public void TheAsideDeclaresNoContainerOfItsOwn_becauseTheHostedPanelIsTheOne()
    {
        var host = RenderStack();

        Assert.DoesNotContain("ns-container", host.Find(".ns-drawer").ClassList);
        Assert.Empty(host.Find(".ns-drawer").QuerySelectorAll(".ns-container"));

        var panelHost = Render<AsidePanelHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(PortaledSurfaceContainerTests).Assembly, [])));

        // And with the page's own panel in it, the aside measures — one declaration, inside the
        // drawer, exactly where a hosted screen puts it.
        Assert.NotEmpty(panelHost.Find(".ns-drawer").QuerySelectorAll(".ns-container"));
    }

    IRenderedComponent<SurfaceStackHost> RenderStack()
    {
        return Render<SurfaceStackHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(PortaledSurfaceContainerTests).Assembly, [])));
    }
}

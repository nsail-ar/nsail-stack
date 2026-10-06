// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;
using MudBlazor.Services;

namespace NSail.Components.Tests;

/// <summary>The link is how an aside is actually opened — "cargar receta" from the OT form's
/// prescription lookup is an NsLink, and so is every New and every row Edit in a list. Its href
/// is the address the page is already on plus the surface's query parameter, so the click is a
/// surface transition and the link has to be the one to decide how it stacks
/// (SurfaceContext.Follow): opening a closed surface pushes, moving an open one replaces, and
/// an open the link marked NoHistory replaces. Blazor pushes every anchor it intercepts and
/// skips a click whose default was prevented, so the link that drives itself wraps its chrome
/// in the element that prevents it — including the two chromes whose anchor is rendered by the
/// vendor. A link that changes the page is a place the user went: no wrapper, and Blazor pushes
/// it as always.</summary>
public sealed class NsLinkHistoryTests : BunitContext
{
    public NsLinkHistoryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(NsLinkHistoryTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    // bUnit's History is newest first, and a replacing write drops the entry it replaced — so
    // Last() reads as "the click's write" only for as long as every write replaces, and stops
    // meaning anything the moment one of them pushes.
    NavigationHistory LastWrite
    {
        get { return Navigation.History.First(); }
    }

    IRenderedComponent<SurfaceLinkHost> RenderLink(
        string href,
        Surface? target,
        Surface? surface = null,
        NsAs chrome = NsAs.Inline,
        string? label = "Open",
        bool iconOnly = false)
    {
        Navigation.NavigateTo("/optical/work-orders/new");

        return Render<SurfaceLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Name, surface)
            .Add(x => x.Href, href)
            .Add(x => x.As, chrome)
            .Add(x => x.Icon, iconOnly ? NsIcons.Edit : null)
            .Add(x => x.Breakpoint, iconOnly ? NsBreakpoint.Never : NsBreakpoint.Sm)
            .Add(x => x.Label, label)
            .Add(x => x.Target, target));
    }

    /// <summary>Closed → open, through the door a user actually uses. The aside is a window
    /// somebody opened, so it takes an entry of its own and Back closes it — the click landing
    /// here rather than on Blazor's own interception is also what records the push, which is
    /// what the close reads to know it may pop.</summary>
    [Fact]
    public async Task FollowingALinkThatOpensAnAside_PushesAHistoryEntry()
    {
        var link = RenderLink("optical/prescriptions/new", Surfaces.Aside);

        // The wrapper is what stands Blazor's own interception down — it carries the class the
        // document listener prevents the default on — and its absence is what leaves an
        // ordinary link alone: ALinkToAnotherPage asserts the wrapper is missing there, so that
        // test discriminates.
        Assert.NotEmpty(link.FindAll("span.ns-link-intercept"));

        // Clicking the anchor, not the wrapper: a click prevented anywhere on its way up is
        // prevented for the whole event, and that bubbling is what the vendor chromes rely on.
        await link.InvokeAsync(() => link.Find("a").Click());

        var write = LastWrite;

        Assert.Contains("aside=optical%2Fprescriptions%2Fnew", write.Uri, StringComparison.Ordinal);
        Assert.False(write.Options.ReplaceHistoryEntry);
    }

    /// <summary>Closed → open, refused: the lookup's inline create is the one open that is not
    /// a place — a satellite feeding a value back into the form still standing underneath — so
    /// it replaces, and the entry the form is sitting on is what Back returns to.</summary>
    [Fact]
    public async Task FollowingALinkThatOpensAnAsideWithNoHistory_ReplacesTheCurrentHistoryEntry()
    {
        Navigation.NavigateTo("/optical/work-orders/new");

        var link = Render<SurfaceLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Href, "optical/prescriptions/new")
            .Add(x => x.Target, Surfaces.Aside)
            .Add(x => x.NoHistory, true)
            .Add(x => x.Label, "Open"));

        await link.InvokeAsync(() => link.Find("a").Click());

        var write = LastWrite;

        Assert.Contains("aside=optical%2Fprescriptions%2Fnew", write.Uri, StringComparison.Ordinal);
        Assert.True(write.Options.ReplaceHistoryEntry);
    }

    /// <summary>Open → open: a second row clicked while the aside is already showing the first
    /// is a move inside one place, and no flag makes it anything else — a back-stack growing
    /// per click inside an open aside is what this pins against.</summary>
    [Fact]
    public async Task FollowingALinkThatMovesAnAlreadyOpenAside_ReplacesTheCurrentHistoryEntry()
    {
        Navigation.NavigateTo("/optical/work-orders?aside=optical%2Fwork-orders%2F1%2Fedit");

        var link = Render<SurfaceLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Href, "optical/work-orders/2/edit")
            .Add(x => x.Target, Surfaces.Aside)
            .Add(x => x.Label, "Open"));

        await link.InvokeAsync(() => link.Find("a").Click());

        var write = LastWrite;

        Assert.Contains("aside=optical%2Fwork-orders%2F2%2Fedit", write.Uri, StringComparison.Ordinal);
        Assert.True(write.Options.ReplaceHistoryEntry);
    }

    /// <summary>ctrl+click is the user addressing the browser, not the app: it means "open this
    /// address in a new tab", and Blazor's own link interception lets exactly those clicks
    /// through for that reason. The intercepting wrapper has to agree — its default is left
    /// standing (ns.js decides per click, which markup cannot) and this handler stands down on
    /// the same flags, so the tab opens and the page behind it does not move as well.</summary>
    [Theory]
    [InlineData("ctrl")]
    [InlineData("shift")]
    [InlineData("alt")]
    [InlineData("meta")]
    [InlineData("middle")]
    public async Task AModifiedClickOnASurfaceLink_MovesNothingInPlace(string modifier)
    {
        var link = RenderLink("optical/prescriptions/new", Surfaces.Aside);

        await link.InvokeAsync(() => link.Find("a").Click(new MouseEventArgs
        {
            Button = modifier == "middle" ? 1 : 0,
            CtrlKey = modifier == "ctrl",
            ShiftKey = modifier == "shift",
            AltKey = modifier == "alt",
            MetaKey = modifier == "meta"
        }));

        // The address the page stands at, not how anything stacked: the click was the browser's,
        // this handler stood down, and the page behind the tab that opened never moved at all.
        Assert.DoesNotContain("aside=", Navigation.Uri, StringComparison.Ordinal);
        Assert.EndsWith("/optical/work-orders/new", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>The guard on that guard: a plain primary click is still the app's, and it is
    /// the one the whole interception exists for.</summary>
    [Fact]
    public async Task APlainClickOnASurfaceLink_StillMovesTheSurface()
    {
        var link = RenderLink("optical/prescriptions/new", Surfaces.Aside);

        await link.InvokeAsync(() => link.Find("a").Click(new MouseEventArgs()));

        Assert.Contains("aside=optical%2Fprescriptions%2Fnew", Navigation.Uri, StringComparison.Ordinal);
    }

    /// <summary>A grid row wired with its own OnRowClick (Clientes, stories.md "las acciones
    /// van a su acción") sits one ancestor above this wrapper: a click the wrapper only
    /// prevents the default of still reaches that ancestor's handler, which runs a beat after
    /// Follow() and wins the navigation — every row action bouncing to the row's own
    /// destination instead of its own, regardless of which one was clicked. Stopping
    /// propagation here is the one-place fix; NsActionToolbar already carries the identical
    /// guard for the same reason.</summary>
    [Fact]
    public void TheInterceptingWrapper_AlsoStopsPropagation()
    {
        var link = RenderLink("optical/prescriptions/new", Surfaces.Aside);

        Assert.True(link.Find("span.ns-link-intercept").HasAttribute("blazor:onclick:stopPropagation"));
    }

    /// <summary>The two chromes that matter most in the wild — every list's New button and
    /// every row's Edit icon are MudBlazor components that render their own anchor and expose
    /// no way to prevent a click. They are why the interception sits on a wrapper.</summary>
    [Theory]
    [InlineData(NsAs.Main, "Open", false)]
    [InlineData(NsAs.Inline, null, true)]
    public async Task FollowingAChromedLinkThatOpensAnAside_PushesAHistoryEntry(
        NsAs chrome,
        string? label,
        bool iconOnly)
    {
        var link = RenderLink(
            "optical/prescriptions/new",
            Surfaces.Aside,
            chrome: chrome,
            label: label,
            iconOnly: iconOnly);

        Assert.NotEmpty(link.FindAll("span.ns-link-intercept"));

        await link.InvokeAsync(() => link.Find("a").Click());

        var write = LastWrite;

        Assert.Contains("aside=optical%2Fprescriptions%2Fnew", write.Uri, StringComparison.Ordinal);
        Assert.False(write.Options.ReplaceHistoryEntry);

        // The vendor's own anchor still carries the address: chrome that lost nothing is the
        // point of wrapping rather than trading Href away for an OnClick.
        Assert.Equal(
            "/optical/work-orders/new?aside=optical%2Fprescriptions%2Fnew",
            link.Find("a").GetAttribute("href"));
    }

    /// <summary>Deep-linking is what replace must not touch: the anchor still carries the whole
    /// address, so copying it, prerendering it or pasting it fresh opens the aside as before —
    /// only how the click stacks in history changed.</summary>
    [Fact]
    public void ALinkThatOpensAnAside_StillCarriesTheDeepLinkableAddress()
    {
        var link = RenderLink("optical/prescriptions/new", Surfaces.Aside);

        var href = link.Find("a").GetAttribute("href");

        Assert.Equal("/optical/work-orders/new?aside=optical%2Fprescriptions%2Fnew", href);
    }

    /// <summary>A link to another page is a place the user went, and Back must return to it:
    /// nothing is intercepted, so the anchor stays a plain anchor and Blazor pushes it.</summary>
    [Fact]
    public void ALinkToAnotherPage_IsLeftToBlazorsOwnInterception()
    {
        var link = RenderLink("optical/work-orders", null);

        var anchor = link.Find("a");

        Assert.Equal("/optical/work-orders", anchor.GetAttribute("href"));
        Assert.False(anchor.HasAttribute("blazor:onclick"));
        Assert.Empty(link.FindAll("span.ns-link-intercept"));
    }

    /// <summary>Closing the aside by leaving for the page underneath is a place change too, so
    /// it keeps its entry — the same move seen from inside the overlay.</summary>
    [Fact]
    public void FollowingALinkOutOfAnAsideToTheMainSurface_IsLeftToBlazorsOwnInterception()
    {
        Navigation.NavigateTo("/optical/work-orders/new?aside=optical%2Fprescriptions%2Fnew");

        var link = Render<SurfaceLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Name, Surfaces.Aside)
            .Add(x => x.Href, "optical/work-orders")
            .Add(x => x.Target, Surfaces.Main));

        var anchor = link.Find("a");

        Assert.Equal("/optical/work-orders", anchor.GetAttribute("href"));
        Assert.False(anchor.HasAttribute("blazor:onclick"));
        Assert.Empty(link.FindAll("span.ns-link-intercept"));
    }

    /// <summary>The guide's section link, through the door a reader uses: the same page plus a
    /// heading's slug. Same path, so the link intercepts and nothing opens — and it is still a
    /// place, because the section the reader was on is what Back owes them. This used to fall
    /// into the same-path arm and replace, which is how Back left the manual altogether.</summary>
    [Fact]
    public async Task FollowingASectionLinkOnTheSamePage_PushesAHistoryEntry()
    {
        Navigation.NavigateTo("/optical/work-orders/new");

        var link = Render<SurfaceLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Href, "optical/work-orders/new")
            .Add(x => x.Fragment, "medir-la-graduacion")
            .Add(x => x.Label, "Medir la graduación"));

        Assert.NotEmpty(link.FindAll("span.ns-link-intercept"));

        await link.InvokeAsync(() => link.Find("a").Click());

        var write = LastWrite;

        Assert.EndsWith("#medir-la-graduacion", write.Uri, StringComparison.Ordinal);
        Assert.False(write.Options.ReplaceHistoryEntry);
    }

    /// <summary>A browser target is the one destination this app does not route at all, so the
    /// click belongs to the browser — a printed PDF opening in a new tab is not a surface move
    /// and must never have its default prevented.</summary>
    [Fact]
    public void ALinkThatOpensInTheBrowser_IsNeverIntercepted()
    {
        var link = RenderLink("optical/work-orders/1/pdf", Surfaces.Blank);

        Assert.Empty(link.FindAll("span.ns-link-intercept"));
    }

    /// <summary>The reported defect's exact shape: the Turno dialog's lapiz is an
    /// NsPageLink Target="@Auto" rendered inside AppointmentActionsForm, which is hosted by
    /// OpenDialog — a host-managed surface (Surfaces.Dialog), not a queryable one. Before the
    /// fix, Auto resolved to the dialog's own name and wrote a "?dialog=" query nobody reads
    /// — the click "did nothing" because the href it followed genuinely led nowhere.</summary>
    [Fact]
    public void ALinkTargetingAutoFromAHostManagedSurface_EscalatesToTheAsideInstead()
    {
        Navigation.NavigateTo("/optical/work-orders/new");

        var link = Render<SurfaceLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Name, Surfaces.Dialog)
            .Add(x => x.Close, () => { })
            .Add(x => x.Href, "optical/prescriptions/new")
            .Add(x => x.Target, Surfaces.Auto));

        var href = link.Find("a").GetAttribute("href");

        Assert.Equal("/optical/work-orders/new?aside=optical%2Fprescriptions%2Fnew", href);
        Assert.DoesNotContain("dialog=", href);
    }

    /// <summary>The second half of the same defect: even a correctly resolved href left the
    /// open dialog sitting on top of the page it had just sent the user to. Clicking the link
    /// must close the host — the same close DialogManager.Open wires up for the X and Escape —
    /// so the destination is what the user actually sees.</summary>
    [Fact]
    public async Task FollowingALinkOutOfAHostManagedSurface_ClosesIt()
    {
        Navigation.NavigateTo("/optical/work-orders/new");
        var closed = false;

        var link = Render<SurfaceLinkHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Name, Surfaces.Dialog)
            .Add(x => x.Close, () => closed = true)
            .Add(x => x.Href, "optical/prescriptions/new")
            .Add(x => x.Target, Surfaces.Auto));

        await link.InvokeAsync(() => link.Find("a").Click());

        Assert.True(closed);
    }
}

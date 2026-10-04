// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>NsTabs riding the query seam (nsail#180): a callback that comes back to a screen
/// can land the user on the tab it belongs to, and switching a tab leaves an address worth
/// sending to somebody. The binding is opt-in per screen — most tabs deep-link into nothing and
/// would only dirty the URL — and the value is the tab's own invariant Name, because a
/// localized Title identifies nothing across two languages.</summary>
public sealed class NsTabsQueryTests : BunitContext
{
    public NsTabsQueryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    IRenderedComponent<TabsQueryHost> RenderTabs(
        string uri,
        bool bindQuery = true,
        Surface? surface = null,
        Action? close = null,
        bool unnamed = false)
    {
        Navigation.NavigateTo(uri);

        return Render<TabsQueryHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(NsTabsQueryTests).Assembly, Array.Empty<System.Reflection.Assembly>()))
            .Add(x => x.Name, surface)
            .Add(x => x.Close, close)
            .Add(x => x.BindQuery, bindQuery)
            .Add(x => x.Unnamed, unnamed));
    }

    // Which panel the vendor is actually painting: every panel is mounted (KeepPanelsAlive) and
    // the one showing is the one carrying .mud-tab-panel-active, which the vendor's own
    // stylesheet draws as display:contents while its siblings are display:none —
    // NsTabKeepAliveTests measures that pair. Asserting on the DOM rather than on the bound
    // field is what makes "the tab is active" a statement about what the user sees.
    static int ShownPanel(IRenderedComponent<TabsQueryHost> tabs)
    {
        var panels = tabs.FindAll(".mud-tabs-panels > .mud-tab-panel");

        for (var index = 0; index < panels.Count; index++)
        {
            if (panels[index].ClassList.Contains("mud-tab-panel-active"))
            {
                return index;
            }
        }

        return -1;
    }

    static Task Switch(IRenderedComponent<TabsQueryHost> tabs, int index)
    {
        return tabs.InvokeAsync(() => tabs.FindAll(".mud-tabs-tabbar .mud-tab")[index].Click());
    }

    /// <summary>The default, which is every tabbed screen in the app until one opts in: tabs are
    /// a view of the document, not a place, and switching one leaves the address alone.</summary>
    [Fact]
    public async Task TabsThatDidNotOptIn_LeaveTheAddressAlone()
    {
        var tabs = RenderTabs("/iam/profile", bindQuery: false);

        await Switch(tabs, 2);

        Assert.Equal(2, ShownPanel(tabs));
        Assert.Equal("http://localhost/iam/profile", Navigation.Uri);
    }

    [Fact]
    public async Task OptedIn_SwitchingATabWritesItsNameOnTheAddress()
    {
        var tabs = RenderTabs("/iam/profile");

        await Switch(tabs, 2);

        Assert.Equal("http://localhost/iam/profile?tab=accounts", Navigation.Uri);
    }

    /// <summary>A tab is a view of the place the user is at, so the write replaces the history
    /// entry — the seam's own rule (nsail#163), pinned from its first component rider: Back
    /// leaves the screen instead of walking every tab that was touched on the way in.</summary>
    [Fact]
    public async Task OptedIn_TheWriteReplacesTheHistoryEntryRatherThanPushingOne()
    {
        var tabs = RenderTabs("/iam/profile");

        await Switch(tabs, 1);
        await Switch(tabs, 2);

        Assert.All(Navigation.History.TakeLast(2), entry => Assert.True(entry.Options.ReplaceHistoryEntry));
    }

    /// <summary>The motivating scenario: Google hands the browser back to My Profile and the
    /// screen has to open on Cuentas, not on Identidad.</summary>
    [Fact]
    public void ArrivingWithTheTabNamed_OpensOnThatTab()
    {
        var tabs = RenderTabs("/iam/profile?tab=accounts");

        Assert.Equal(2, ShownPanel(tabs));
        Assert.Equal(2, tabs.Instance.ActiveTab);
    }

    /// <summary>Arriving is not switching: a screen that opens on the tab it was asked for must
    /// not echo that back as a navigation, and one that was asked for nothing keeps the clean
    /// address it was given.</summary>
    [Fact]
    public void Arriving_NeverWritesTheAddressBack()
    {
        var before = Navigation.History.Count;

        RenderTabs("/iam/profile?tab=accounts");

        Assert.Equal("http://localhost/iam/profile?tab=accounts", Navigation.Uri);
        Assert.Equal(before + 1, Navigation.History.Count);

        var clean = Navigation.History.Count;

        RenderTabs("/iam/profile");

        Assert.Equal("http://localhost/iam/profile", Navigation.Uri);
        Assert.Equal(clean + 1, Navigation.History.Count);
    }

    /// <summary>A name nobody carries is a hand-typed address, and the honest answer to it is
    /// the tab the screen would have opened on anyway.</summary>
    [Fact]
    public void ArrivingWithAnUnknownName_LeavesTheFirstTabShowing()
    {
        var tabs = RenderTabs("/iam/profile?tab=nowhere");

        Assert.Equal(0, ShownPanel(tabs));
    }

    /// <summary>In the browser a switch's write lands a JS round trip after the switch, and a page
    /// bound to ActivePanelIndex re-renders inside that window — Nueva Venta's Artículos tab
    /// wrote ?tab=counter and snapped back to Trabajos. The address being left is not an order to
    /// return to it.</summary>
    [Fact]
    public async Task ASwitchSurvivesTheRenderBeforeItsAddressLands()
    {
        var navigation = new LaggingNavigationManager("http://localhost/iam/profile?tab=identity");

        Services.AddSingleton<NavigationManager>(navigation);

        var tabs = Render<TabsQueryHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(NsTabsQueryTests).Assembly, Array.Empty<System.Reflection.Assembly>()))
            .Add(x => x.BindQuery, true));

        await Switch(tabs, 2);

        Assert.Equal(2, ShownPanel(tabs));
        Assert.Equal(2, tabs.Instance.ActiveTab);

        await tabs.InvokeAsync(navigation.Land);

        Assert.Equal("http://localhost/iam/profile?tab=accounts", navigation.Uri);
        Assert.Equal(2, ShownPanel(tabs));

        // And once it has landed, the address still leads: a switch back is one more write.
        await Switch(tabs, 0);

        Assert.Equal(0, ShownPanel(tabs));
        Assert.Equal(0, tabs.Instance.ActiveTab);
    }

    // The browser's own NavigationManager, as far as timing goes: a navigation is recorded now and
    // reported only when the test says it landed.
    sealed class LaggingNavigationManager : NavigationManager
    {
        string? _pending;

        public LaggingNavigationManager(string uri)
        {
            Initialize("http://localhost/", uri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            _pending = ToAbsoluteUri(uri).ToString();
        }

        // The surface's NavigationLock registers a handler; nothing here ever asks it.
        protected override void SetNavigationLockState(bool value)
        {
        }

        public void Land()
        {
            if (_pending is null)
            {
                return;
            }

            Uri = _pending;
            _pending = null;

            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    // Unnamed tabs: the index is the fallback identity, so a screen that opts in without naming
    // anything still round-trips — it just deep-links by position.

    [Fact]
    public async Task UnnamedTabs_WriteTheirIndex()
    {
        var tabs = RenderTabs("/iam/profile", unnamed: true);

        await Switch(tabs, 2);

        Assert.Equal("http://localhost/iam/profile?tab=2", Navigation.Uri);
    }

    [Fact]
    public void UnnamedTabs_OpenOnTheIndexTheyWereAskedFor()
    {
        var tabs = RenderTabs("/iam/profile?tab=1", unnamed: true);

        Assert.Equal(1, ShownPanel(tabs));
    }

    [Fact]
    public void AnIndexPastTheLastTab_LeavesTheFirstTabShowing()
    {
        var tabs = RenderTabs("/iam/profile?tab=7", unnamed: true);

        Assert.Equal(0, ShownPanel(tabs));
    }

    // The aside: the screen's own query travels inside the value of ?aside=, and the binding
    // reads and writes there without knowing it does — the whole point of asking the surface.

    [Fact]
    public void InsideAnAside_ArrivingWithTheTabNamed_OpensOnThatTab()
    {
        var inner = Uri.EscapeDataString("directory/parties/edit?tab=accounts");
        var tabs = RenderTabs($"/optical/clients?aside={inner}", surface: Surfaces.Aside);

        Assert.Equal(2, ShownPanel(tabs));
    }

    [Fact]
    public async Task InsideAnAside_SwitchingWritesIntoTheAsidesOwnRoute()
    {
        var inner = Uri.EscapeDataString("directory/parties/edit");
        var tabs = RenderTabs($"/optical/clients?aside={inner}", surface: Surfaces.Aside);

        await Switch(tabs, 1);

        Assert.Equal(
            "http://localhost/optical/clients?aside=directory%2Fparties%2Fedit%3Ftab%3Dlocations",
            Navigation.Uri);
    }

    // The dialog: routed by nothing, carrying no query. The binding is inert there rather than
    // throwing or writing something nobody can read back.

    [Fact]
    public async Task HostedInADialog_SwitchingWritesNothing()
    {
        var tabs = RenderTabs("/iam/profile", surface: Surfaces.Dialog, close: () => { });

        await Switch(tabs, 2);

        Assert.Equal(2, ShownPanel(tabs));
        Assert.Equal("http://localhost/iam/profile", Navigation.Uri);
    }

    /// <summary>And it reads nothing either: the tab of the page UNDERNEATH the dialog is not
    /// the dialog's — a dialog is ephemeral and opens where it was opened.</summary>
    [Fact]
    public void HostedInADialog_IgnoresTheTabOfThePageUnderneath()
    {
        var tabs = RenderTabs("/iam/profile?tab=accounts", surface: Surfaces.Dialog, close: () => { });

        Assert.Equal(0, ShownPanel(tabs));
    }
}

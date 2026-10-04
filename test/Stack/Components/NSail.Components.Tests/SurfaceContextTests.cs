// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NSail.Components;

namespace NSail.Components.Tests;

public sealed class SurfaceContextTests
{
    private sealed class FakeNavigation : NavigationManager
    {
        public List<string> Navigations { get; } = [];

        public FakeNavigation(string uri = "https://app.test/parties")
            => Initialize("https://app.test/", uri);

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Navigations.Add(ToAbsoluteUri(uri).ToString());
        }

        /// <summary>The half a recorder leaves out: a navigation that COMMITS, which is what
        /// LocationChanged answers for. Requested and landed are two different moments — a
        /// LocationChanging handler stands between them and may refuse — so a double that
        /// only records the request cannot be asked what happens on arrival.</summary>
        public void Arrive()
        {
            Uri = Navigations[^1];
            NotifyLocationChanged(false);
        }
    }

    private sealed class FakeJs : IJSRuntime
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

    private static SurfaceContext Build(string? name = null, FakeNavigation? navigation = null)
    {
        navigation ??= new FakeNavigation();
        var routes = new RouteTable(typeof(SurfaceContextTests).Assembly, Array.Empty<System.Reflection.Assembly>());

        return new SurfaceContext(name is null ? null : new Surface(name), navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    [Fact]
    public void SetSize_raises_StateChanged_once_per_change()
    {
        var surface = Build();
        var raised = 0;
        surface.StateChanged += () => raised++;

        surface.SetSize(NsSize.Xl);
        surface.SetSize(NsSize.Xl);

        Assert.Equal(NsSize.Xl, surface.Size);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetFloating_raises_StateChanged_once_per_change()
    {
        var surface = Build();
        var raised = 0;
        surface.StateChanged += () => raised++;

        surface.SetFloating(true);
        surface.SetFloating(true);

        Assert.True(surface.Floating);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ResetView_restores_defaults_with_a_single_notification()
    {
        var surface = Build();
        surface.SetSize(NsSize.Xl);
        surface.SetFloating(true);

        var raised = 0;
        surface.StateChanged += () => raised++;

        surface.ResetView();

        Assert.Equal(NsSize.Md, surface.Size);
        Assert.False(surface.Floating);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ResetView_is_a_no_op_at_defaults()
    {
        var surface = Build();
        var raised = 0;
        surface.StateChanged += () => raised++;

        surface.ResetView();

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ResetView_drops_what_the_previous_page_announced()
    {
        var surface = Build();
        surface.Announce("Personas", null, null);

        surface.ResetView();

        Assert.Null(surface.Title);
    }

    [Fact]
    public void HasChanges_tracks_multiple_dirty_sources()
    {
        var surface = Build();
        var a = new object();
        var b = new object();
        var raised = 0;
        surface.StateChanged += () => raised++;

        surface.SetDirty(a);
        surface.SetDirty(b);
        Assert.True(surface.HasChanges);
        Assert.Equal(1, raised);

        surface.SetUnchanged(a);
        Assert.True(surface.HasChanges);
        Assert.Equal(1, raised);

        surface.SetUnchanged(b);
        Assert.False(surface.HasChanges);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void A_retired_source_spends_its_report_and_takes_no_later_one()
    {
        var surface = Build();
        var gone = new object();
        var staying = new object();

        surface.SetDirty(gone);
        surface.Retire(gone);
        Assert.False(surface.HasChanges);

        surface.SetDirty(gone);
        Assert.False(surface.HasChanges);

        surface.SetDirty(staying);
        Assert.True(surface.HasChanges);
    }

    // A surface outlives every page on it and each of their components retires on the way out, so
    // an entry that survived its key would make the retired table a list of every component the
    // circuit ever rendered — a leak nothing on screen could show. A plain set cannot hold this
    // rule, and neither can a weak table whose value is reachable from outside it.
    [Fact]
    public void A_retired_source_is_not_held_alive_by_the_surface()
    {
        var surface = Build();
        var probe = RetireAndForget(surface);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(probe.TryGetTarget(out _));

        GC.KeepAlive(surface);
    }

    // Its own frame, never inlined: a local of the test method is still rooted at the collect
    // above, and the assertion would then read the JIT's liveness instead of the table's.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> RetireAndForget(SurfaceContext surface)
    {
        var source = new object();

        surface.SetDirty(source);
        surface.Retire(source);

        return new WeakReference<object>(source);
    }

    [Fact]
    public void HasWork_tracks_nested_Enter_Exit()
    {
        var surface = Build();
        var raised = 0;
        surface.StateChanged += () => raised++;

        surface.Enter();
        surface.Enter();
        Assert.True(surface.HasWork);
        Assert.Equal(1, raised);

        surface.Exit();
        Assert.True(surface.HasWork);

        surface.Exit();
        Assert.False(surface.HasWork);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void GetHref_on_the_default_surface_is_root_relative()
    {
        var surface = Build();

        Assert.Equal("/directory/parties", surface.GetHref("directory/parties"));
        Assert.Equal("/directory/parties", surface.GetHref("/directory/parties"));
    }

    [Fact]
    public void GetHref_on_a_named_surface_targets_its_query_parameter()
    {
        var surface = Build("aside");

        var href = surface.GetHref("directory/parties/new");

        Assert.StartsWith("/parties?aside=", href);
        Assert.Contains("directory%2Fparties%2Fnew", href);
    }

    [Fact]
    public void GetHref_without_a_target_navigates_this_surface()
    {
        Assert.Equal("/directory/parties", Build().GetHref("directory/parties", null));
        Assert.StartsWith("/parties?aside=", Build("aside").GetHref("x", null));
    }

    // A browser target LEAVES our surfaces: the tab it opens has no aside and no modal, so a
    // surface query resolved against this document arrives there as the app booting at the
    // same address -- the reported defect, a Print button that reloaded the page instead of
    // fetching the PDF (nsail#584).

    [Theory]
    [InlineData(null)]
    [InlineData("aside")]
    [InlineData("modal")]
    [InlineData("panel")]
    public void A_browser_target_resolves_to_the_rooted_route_from_every_surface(string? from)
    {
        var navigation = new FakeNavigation("https://app.test/accounting/vouchers?aside=accounting%2Fvouchers%2F1");

        var href = Build(from, navigation).GetHref("api/accounting/vouchers/1/pdf", Surfaces.Blank);

        Assert.Equal("/api/accounting/vouchers/1/pdf", href);
    }

    [Fact]
    public void A_browser_target_from_an_overlay_gives_the_address_Main_would()
    {
        var navigation = new FakeNavigation("https://app.test/accounting/vouchers?aside=accounting%2Fvouchers%2F1");
        var aside = Build("aside", navigation);

        Assert.Equal(
            aside.GetHref("api/accounting/vouchers/1/pdf", Surfaces.Main),
            aside.GetHref("api/accounting/vouchers/1/pdf", Surfaces.Blank));
    }

    [Fact]
    public void A_browser_target_from_a_host_managed_surface_carries_no_surface_query()
    {
        var navigation = new FakeNavigation("https://app.test/scheduling/agenda");
        var routes = new RouteTable(typeof(SurfaceContextTests).Assembly, Array.Empty<System.Reflection.Assembly>());
        var surface = new SurfaceContext(Surfaces.Dialog, navigation, routes, new FakeJs(), new SurfaceHistory(), close: () => { });

        Assert.Equal("/api/accounting/vouchers/1/pdf", surface.GetHref("api/accounting/vouchers/1/pdf", Surfaces.Blank));
    }

    [Fact]
    public void GetHref_with_a_named_target_uses_that_surface()
    {
        var href = Build().GetHref("directory/parties/new", Surfaces.Modal);

        Assert.Contains("modal=directory%2Fparties%2Fnew", href);
    }

    [Theory]
    [InlineData(null, "aside")]
    [InlineData("aside", "modal")]
    [InlineData("modal", "modal")]
    public void Auto_opens_one_surface_further_out(string? from, string expected)
    {
        var href = Build(from).GetHref("directory/parties/new", Surfaces.Auto);

        Assert.Contains($"{expected}=directory%2Fparties%2Fnew", href);
    }

    [Fact]
    public void Auto_from_a_product_surface_navigates_that_surface()
    {
        var href = Build("panel").GetHref("directory/parties/new", Surfaces.Auto);

        Assert.Contains("panel=directory%2Fparties%2Fnew", href);
    }

    [Fact]
    public void Auto_from_a_host_managed_surface_escalates_like_Main()
    {
        // The reported defect's mechanism: Surfaces.Dialog answers to no query key of its
        // own ("nothing links to it" -- Surfaces.cs) -- unlike a product's own surface
        // (Auto_from_a_product_surface_navigates_that_surface above), so writing its own
        // name into the href built a link nobody could ever follow (a lapiz that "no hace
        // nada"). It escalates the same way Main does: to the Chain's first rung.
        var navigation = new FakeNavigation("https://app.test/scheduling/agenda");
        var routes = new RouteTable(typeof(SurfaceContextTests).Assembly, Array.Empty<System.Reflection.Assembly>());
        var surface = new SurfaceContext(Surfaces.Dialog, navigation, routes, new FakeJs(), new SurfaceHistory(), close: () => { });

        var href = surface.GetHref("scheduling/appointments/new/edit", Surfaces.Auto);

        Assert.Contains("aside=scheduling%2Fappointments%2Fnew%2Fedit", href);
        Assert.DoesNotContain("dialog=", href);
    }

    [Fact]
    public void Follow_closes_a_host_managed_surface_when_the_navigation_lands()
    {
        // A dialog carries no route of its own to keep representing once a link inside it
        // has sent the user elsewhere -- the second half of the reported defect: even a
        // correctly resolved href left the open dialog sitting on top of the destination.
        // Closing once is what lets every dialog-hosted link stay a plain NsLink with no
        // Surface?.Close() wiring of its own -- but on ARRIVAL, never on the line after the
        // request: the unsaved-changes guard stands between the two and may refuse
        // (DialogFormExitGuardTests).
        var navigation = new FakeNavigation("https://app.test/scheduling/agenda");
        var routes = new RouteTable(typeof(SurfaceContextTests).Assembly, Array.Empty<System.Reflection.Assembly>());
        var closed = 0;
        var surface = new SurfaceContext(Surfaces.Dialog, navigation, routes, new FakeJs(), new SurfaceHistory(), close: () => closed++);

        surface.Follow(surface.GetHref("scheduling/appointments/new/edit", Surfaces.Auto));

        Assert.Equal(0, closed);
        Assert.Single(navigation.Navigations);

        navigation.Arrive();

        Assert.Equal(1, closed);
    }

    [Fact]
    public void Follow_closes_a_host_managed_surface_only_for_its_own_navigation()
    {
        // The armed close is spent by the first arrival and then gone: a dialog does not
        // hold a subscription that a later, unrelated navigation would cash in.
        var navigation = new FakeNavigation("https://app.test/scheduling/agenda");
        var routes = new RouteTable(typeof(SurfaceContextTests).Assembly, Array.Empty<System.Reflection.Assembly>());
        var closed = 0;
        var surface = new SurfaceContext(Surfaces.Dialog, navigation, routes, new FakeJs(), new SurfaceHistory(), close: () => closed++);

        surface.Follow(surface.GetHref("scheduling/appointments/new/edit", Surfaces.Auto));
        navigation.Arrive();
        navigation.Arrive();

        Assert.Equal(1, closed);
    }

    [Fact]
    public void Follow_does_not_close_a_named_surface()
    {
        var navigation = new FakeNavigation();
        var surface = Build("aside", navigation);

        surface.Follow(surface.GetHref("directory/parties/new"));

        Assert.Single(navigation.Navigations);
    }

    [Fact]
    public void ZIndex_on_the_default_surface_is_the_base_tier()
    {
        Assert.Equal(1300, Build().ZIndex);
    }

    [Theory]
    [InlineData("aside", 1400)]
    [InlineData("modal", 1500)]
    public void ZIndex_follows_the_same_Chain_order_Auto_escalates_through(string name, int expected)
    {
        Assert.Equal(expected, Build(name).ZIndex);
    }

    [Fact]
    public void ZIndex_for_a_products_own_surface_outranks_the_whole_Chain()
    {
        // Nothing escalates past a product's own surface (Auto_from_a_product_surface_...
        // above), so it takes the outermost tier rather than colliding with Modal's.
        Assert.True(Build("panel").ZIndex > Build("modal").ZIndex);
    }

    [Fact]
    public void VendorZIndex_outranks_every_surface_in_the_Chain()
    {
        // A Confirm/Alert or DialogManager.Open host can be triggered from Main, Aside or
        // Modal alike -- VendorZIndex must beat all three without knowing which one it was.
        Assert.True(SurfaceContext.VendorZIndex > Build().ZIndex);
        Assert.True(SurfaceContext.VendorZIndex > Build("aside").ZIndex);
        Assert.True(SurfaceContext.VendorZIndex > Build("modal").ZIndex);
    }

    [Fact]
    public void VendorZIndex_matches_the_Dialog_surfaces_own_tier()
    {
        // Surfaces.Dialog (NsOpenDialog's own SurfaceContext) is outside the Chain, so it
        // already lands on this same outermost tier via ZIndex -- VendorZIndex is that exact
        // number, not a second guess at it, which is what keeps NsOpenDialog's cascaded
        // Surface and the vendor chrome hosting it from ever disagreeing.
        Assert.Equal(Build("dialog").ZIndex, SurfaceContext.VendorZIndex);
    }

    [Fact]
    public void Main_drops_the_query_string_so_the_overlays_close()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx");

        var href = Build("aside", navigation).GetHref("directory/parties", Surfaces.Main);

        Assert.Equal("/directory/parties", href);
        Assert.DoesNotContain("aside=", href);
    }

    [Fact]
    public void Close_is_a_no_op_on_the_default_surface()
    {
        var navigation = new FakeNavigation();
        var surface = Build(name: null, navigation);

        surface.Close();

        Assert.Empty(navigation.Navigations);
    }

    [Fact]
    public void Close_removes_the_named_surface_query_parameter()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx");
        var surface = Build("aside", navigation);

        surface.Close();

        var target = Assert.Single(navigation.Navigations);
        Assert.DoesNotContain("aside=", target);
    }

    [Fact]
    public void Open_navigates_setting_the_target_surface_parameter()
    {
        var navigation = new FakeNavigation();
        var surface = Build(name: null, navigation);

        surface.Open(Surfaces.Modal, "some/route");

        var target = Assert.Single(navigation.Navigations);
        Assert.Contains("modal=some%2Froute", target);
    }

    // Leonardo's rule, verbatim: the unsaved-changes guard fires ONLY when the form's own
    // surface actually leaves, never because an aside or popup opens on top of it. Opening
    // an aside IS an internal navigation (it writes ?aside= into the address), which is why
    // every surface's NavigationLock used to hear it and the page underneath asked about
    // changes it was not about to lose.

    [Fact]
    public void Opening_an_aside_is_not_a_departure_of_the_page_underneath()
    {
        var navigation = new FakeNavigation("https://app.test/optical/work-orders/new");
        var surface = Build(name: null, navigation);

        Assert.False(surface.Departs("https://app.test/optical/work-orders/new?aside=optical%2Fprescriptions%2Fnew"));
    }

    [Fact]
    public void Closing_an_aside_is_not_a_departure_of_the_page_underneath()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx");
        var surface = Build(name: null, navigation);

        Assert.False(surface.Departs("https://app.test/parties"));
    }

    [Fact]
    public void A_different_page_is_a_departure_of_the_main_surface()
    {
        var navigation = new FakeNavigation("https://app.test/optical/work-orders/new");
        var surface = Build(name: null, navigation);

        Assert.True(surface.Departs("https://app.test/parties"));
    }

    [Fact]
    public void An_aside_departs_when_its_own_route_changes()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx");
        var surface = Build("aside", navigation);

        Assert.True(surface.Departs("https://app.test/parties?aside=directory%2Fy"));
        Assert.True(surface.Departs("https://app.test/parties"));
    }

    // The path is what identity lives in (ruled 2026-08-26), so a named surface reads its own
    // query the way the main surface has always read the address bar's: as filter or component
    // state, not as another document.

    [Fact]
    public void An_aside_stays_when_its_own_query_changes()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx");
        var surface = Build("aside", navigation);

        Assert.False(surface.Departs("https://app.test/parties?aside=directory%2Fx%3Ftab%3Dy"));
    }

    [Fact]
    public void An_aside_stays_when_its_own_query_is_rewritten_or_dropped()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx%3Ftab%3Dy");
        var surface = Build("aside", navigation);

        Assert.False(surface.Departs("https://app.test/parties?aside=directory%2Fx%3Ftab%3Dz"));
        Assert.False(surface.Departs("https://app.test/parties?aside=directory%2Fx"));
    }

    [Fact]
    public void An_aside_departs_when_its_path_changes_under_the_same_query()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx%3Ftab%3Dy");
        var surface = Build("aside", navigation);

        Assert.True(surface.Departs("https://app.test/parties?aside=directory%2Fz%3Ftab%3Dy"));
    }

    [Fact]
    public void An_aside_stays_when_a_modal_opens_further_out()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fx");
        var surface = Build("aside", navigation);

        Assert.False(surface.Departs("https://app.test/parties?aside=directory%2Fx&modal=directory%2Fz"));
    }

    [Fact]
    public void A_host_managed_surface_departs_on_any_navigation()
    {
        // A dialog carries no route in the address, so there is nothing to compare: the only
        // navigation it can witness is the page underneath moving, which closes the host.
        var navigation = new FakeNavigation("https://app.test/parties");
        var routes = new RouteTable(typeof(SurfaceContextTests).Assembly, Array.Empty<System.Reflection.Assembly>());
        var surface = new SurfaceContext(Surfaces.Dialog, navigation, routes, new FakeJs(), new SurfaceHistory(), close: () => { });

        Assert.True(surface.Departs("https://app.test/parties"));
    }

    [Fact]
    public void Navigate_on_the_default_surface_goes_full_page()
    {
        var navigation = new FakeNavigation();
        var surface = Build(name: null, navigation);

        surface.Navigate("directory/parties");

        var target = Assert.Single(navigation.Navigations);
        Assert.Equal("https://app.test/directory/parties", target);
    }

    // The reported defect, reduced to its mechanism: a page opened in an aside is routed by
    // the INNER route, whose query travels inside the VALUE of ?aside= and so never becomes
    // the browser's own. Blazor's [SupplyParameterFromQuery] binds from the real URI, which is
    // why PartyAccountPage reached from the Ficha's lupa arrived with PartyId null and listed
    // everybody. A surface knows which string it is routed by; these ask it.

    const string PartyId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    static FakeNavigation Hosting(string innerRoute)
    {
        return new FakeNavigation($"https://app.test/optical/clients?aside={Uri.EscapeDataString(innerRoute)}");
    }

    [Fact]
    public void GetQuery_on_a_hosted_surface_reads_the_inner_routes_own_query()
    {
        var surface = Build("aside", Hosting($"accounting/books/party-account?PartyId={PartyId}"));

        Assert.Equal(Guid.Parse(PartyId), surface.GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void GetQuery_on_the_main_surface_reads_the_address_bars_query()
    {
        var navigation = new FakeNavigation($"https://app.test/accounting/books/party-account?PartyId={PartyId}");

        Assert.Equal(Guid.Parse(PartyId), Build(name: null, navigation).GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void The_main_surface_does_not_read_a_query_belonging_to_the_route_hosted_over_it()
    {
        var surface = Build(name: null, Hosting($"accounting/books/party-account?PartyId={PartyId}"));

        Assert.Null(surface.GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void A_hosted_surface_does_not_read_the_query_of_the_page_underneath()
    {
        var navigation = new FakeNavigation($"https://app.test/parties?PartyId={PartyId}&aside=directory%2Fx");

        Assert.Null(Build("aside", navigation).GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void An_absent_key_is_the_default_and_says_so()
    {
        var surface = Build("aside", Hosting("accounting/books/party-account"));

        Assert.Null(surface.GetQuery<Guid?>("PartyId"));
        Assert.Equal(Guid.Empty, surface.GetQuery<Guid>("PartyId"));
        Assert.False(surface.TryGetQuery("PartyId", out var value));
        Assert.Null(value);
        Assert.Empty(surface.GetQueryValues("PartyId"));
    }

    [Fact]
    public void A_key_carried_without_a_value_is_present_and_still_reads_as_the_default()
    {
        var surface = Build("aside", Hosting("accounting/books/party-account?PartyId="));

        Assert.True(surface.TryGetQuery("PartyId", out var value));
        Assert.Equal(string.Empty, value);
        Assert.Null(surface.GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void An_unreadable_value_is_the_default_rather_than_a_throw()
    {
        var surface = Build("aside", Hosting("accounting/books/party-account?PartyId=not-a-guid"));

        Assert.Null(surface.GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void A_host_managed_surface_is_routed_by_nothing_and_carries_no_query()
    {
        var navigation = Hosting($"accounting/books/party-account?PartyId={PartyId}");
        var routes = new RouteTable(typeof(SurfaceContextTests).Assembly, Array.Empty<System.Reflection.Assembly>());
        var surface = new SurfaceContext(Surfaces.Dialog, navigation, routes, new FakeJs(), new SurfaceHistory(), close: () => { });

        Assert.Null(surface.GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void A_repeated_key_reads_as_an_array()
    {
        var other = "6f9619ff-8b86-d011-b42d-00c04fc964ff";
        var surface = Build("aside", Hosting($"directory/parties?RoleIds={PartyId}&RoleIds={other}"));

        Assert.Equal(new[] { Guid.Parse(PartyId), Guid.Parse(other) }, surface.GetQuery<Guid[]>("RoleIds"));
        Assert.Equal(new[] { PartyId, other }, surface.GetQueryValues("RoleIds"));
    }

    [Fact]
    public void An_absent_array_key_is_null_rather_than_an_empty_array()
    {
        // PartiesPage tells "no filter" from "an empty filter" by the null, and its menu
        // shortcuts depend on the difference.
        Assert.Null(Build("aside", Hosting("directory/parties")).GetQuery<Guid[]>("RoleIds"));
    }

    [Fact]
    public void A_scalar_key_repeated_answers_with_its_last_value()
    {
        var surface = Build("aside", Hosting($"directory/parties?PartyId={Guid.Empty}&PartyId={PartyId}"));

        Assert.Equal(Guid.Parse(PartyId), surface.GetQuery<Guid?>("PartyId"));
    }

    [Fact]
    public void Conversion_is_culture_invariant_like_the_URL_builder_on_the_other_end()
    {
        // Both ends of a URL are invariant (RouteTable.Format writes these shapes); reading
        // them any other way builds a filter the writer never meant.
        var surface = Build("aside", Hosting(
            "accounting/journal-entries?From=2026-08-05&Total=1234.56&Page=3&Closed=true&Size=Xl&At=2026-08-05T14:30:00"));

        Assert.Equal(new DateOnly(2026, 8, 5), surface.GetQuery<DateOnly?>("From"));
        Assert.Equal(1234.56m, surface.GetQuery<decimal>("Total"));
        Assert.Equal(3, surface.GetQuery<int?>("Page"));
        Assert.True(surface.GetQuery<bool>("Closed"));
        Assert.Equal(NsSize.Xl, surface.GetQuery<NsSize?>("Size"));
        Assert.Equal(new DateTime(2026, 8, 5, 14, 30, 0), surface.GetQuery<DateTime?>("At"));
        Assert.Equal(DateTimeKind.Unspecified, surface.GetQuery<DateTime?>("At")!.Value.Kind);
    }

    [Fact]
    public void A_value_that_had_to_be_escaped_twice_survives_the_trip()
    {
        // RouteTable escapes each query value, then the whole route is escaped again as one
        // value of ?aside= — two rounds in, two rounds out, or the ampersand splits the pair
        // it belongs to.
        var surface = Build("aside", Hosting($"directory/parties?Search={Uri.EscapeDataString("Pérez & Cía")}"));

        Assert.Equal("Pérez & Cía", surface.GetQuery<string>("Search"));
    }
}

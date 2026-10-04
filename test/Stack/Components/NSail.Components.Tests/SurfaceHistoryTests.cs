// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace NSail.Components.Tests;

/// <summary>A surface is a place: opening one is a navigation the user made, so Back closes it
/// and leaves the screen they opened it from standing (Leonardo, 2026-09-05/06). Three
/// transitions carry that, and every test below says which one it pins — closed→open pushes,
/// open→open replaces, open→closed pops the entry its own open pushed.
///
/// Leonardo's 2026-08-05 resurrection — "hago pa atrás y vuelvo a 'nueva receta'" — is still
/// real and is not answered by refusing every surface an entry. It has one shape: the lookup's
/// inline create, a window opened to hand a value back to the form standing underneath, which
/// the save then closes for the user. That open is marked NoHistory where it is written
/// (NsAutocomplete), and NoHistory is the whole exception: it replaces on the way in and on the
/// way out, exactly as every surface used to.
///
/// The three contexts are three objects on purpose. The open is written by the surface
/// UNDERNEATH, the overlay's own context is keyed by the route inside it and rebuilt whenever
/// that route moves (NsSurface), and the close runs on whichever one is standing then — so how
/// the open landed is remembered in the SurfaceHistory they share, and a run with nothing
/// recorded is a pasted address or a reload.</summary>
public sealed class SurfaceHistoryTests
{
    sealed class FakeNavigation : NavigationManager
    {
        public List<(string Uri, bool Replace)> Navigations { get; } = [];

        public FakeNavigation(string uri = "https://app.test/parties")
        {
            Initialize("https://app.test/", uri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Navigations.Add((ToAbsoluteUri(uri).ToString(), options.ReplaceHistoryEntry));
        }

        /// <summary>A pop that committed, delivered the way the browser delivers one: the
        /// address is already the popped one by the time anybody is told. Blazor raises
        /// LocationChanged only for a navigation that actually landed, which is why it is the
        /// signal a surface reads to know its own pop is spent.</summary>
        public void Land(string uri)
        {
            Uri = ToAbsoluteUri(uri).ToString();

            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    // Recording rather than swallowing: a pop is a JS call and nothing else, so the call IS the
    // only evidence a test can read that a close spent an entry instead of writing one.
    sealed class FakeJs : IJSRuntime
    {
        public List<string> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add(identifier);

            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add(identifier);

            return default;
        }
    }

    static SurfaceContext Build(
        string? name,
        FakeNavigation navigation,
        SurfaceHistory? history = null,
        FakeJs? js = null)
    {
        var routes = new RouteTable(typeof(SurfaceHistoryTests).Assembly, Array.Empty<System.Reflection.Assembly>());

        return new SurfaceContext(
            name is null ? null : new Surface(name),
            navigation,
            routes,
            js ?? new FakeJs(),
            history ?? new SurfaceHistory());
    }

    /// <summary>Closed → open. The aside's key was absent and now is: a place the user
    /// navigated to, so it takes an entry of its own and Back closes it.</summary>
    [Fact]
    public void Opening_a_closed_surface_pushes_a_history_entry()
    {
        var navigation = new FakeNavigation("https://app.test/optical/work-orders/new");
        var surface = Build(null, navigation);

        surface.Open(Surfaces.Aside, "optical/prescriptions/new");

        var write = Assert.Single(navigation.Navigations);
        Assert.Contains("aside=optical%2Fprescriptions%2Fnew", write.Uri, StringComparison.Ordinal);
        Assert.False(write.Replace);
    }

    /// <summary>Open → closed, with nothing recorded: the surface was reached cold — a pasted
    /// address, an F5 — so there is no entry of its own underneath it and popping would walk
    /// the user out of the app. The address is rewritten in place instead.</summary>
    [Fact]
    public void Closing_a_surface_reached_cold_replaces_the_current_history_entry()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fparties%2Fnew");
        var js = new FakeJs();

        Build("aside", navigation, js: js).Close();

        var write = Assert.Single(navigation.Navigations);
        Assert.DoesNotContain("aside=", write.Uri, StringComparison.Ordinal);
        Assert.True(write.Replace);
        Assert.Empty(js.Invocations);
    }

    /// <summary>Open → open. The create→edit handoff every create page performs on save, seen
    /// from inside an aside: it moves the aside's own route, which is the same place in the
    /// address bar. (6485206e drops this move entirely when the save is closing the surface —
    /// this is the path that survives, a page navigating its own overlay for any other
    /// reason.)</summary>
    [Fact]
    public void Navigating_inside_a_named_surface_replaces_the_current_history_entry()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fparties%2Fnew");
        var surface = Build("aside", navigation);

        surface.Navigate("directory/parties/7/edit");

        var write = Assert.Single(navigation.Navigations);
        Assert.Contains("parties%2F7%2Fedit", write.Uri, StringComparison.Ordinal);
        Assert.True(write.Replace);
    }

    /// <summary>Not one of the three: the main surface is where the user actually goes, and a
    /// create page navigating in place there is a place change that keeps its history entry —
    /// Back from the edit page has always returned to the list and still does.</summary>
    [Fact]
    public void Navigating_the_main_surface_pushes_a_history_entry()
    {
        var navigation = new FakeNavigation("https://app.test/parties");
        var surface = Build(null, navigation);

        surface.Navigate("directory/parties/7/edit");

        var write = Assert.Single(navigation.Navigations);
        Assert.False(write.Replace);
    }

    /// <summary>Closed → open, through the door a user actually uses: every New and every row
    /// Edit is a link carrying a Target. Same transition, same answer.</summary>
    [Fact]
    public void Following_a_link_that_only_opens_a_surface_pushes()
    {
        var navigation = new FakeNavigation("https://app.test/parties");
        var surface = Build(null, navigation);

        surface.Follow(surface.GetHref("directory/parties/new", Surfaces.Aside), Surfaces.Aside);

        var write = Assert.Single(navigation.Navigations);
        Assert.Contains("aside=", write.Uri, StringComparison.Ordinal);
        Assert.False(write.Replace);
    }

    /// <summary>Closed → open, refused: the one opener that says so. Marked at NsAutocomplete's
    /// own create link and nowhere else, it replaces on the way in — and, because nothing was
    /// recorded as pushed, on the way out too.</summary>
    [Fact]
    public void An_open_marked_NoHistory_replaces_and_so_does_its_close()
    {
        const string page = "https://app.test/optical/work-orders/new";

        var history = new SurfaceHistory();
        var opening = new FakeNavigation(page);
        var opener = Build(null, opening, history);

        opener.Follow(
            opener.GetHref("optical/prescriptions/new", Surfaces.Aside),
            Surfaces.Aside,
            noHistory: true);

        var opened = Assert.Single(opening.Navigations);

        Assert.True(opened.Replace);

        var closing = new FakeNavigation(opened.Uri);
        var js = new FakeJs();

        Build("aside", closing, history, js).Close();

        var closed = Assert.Single(closing.Navigations);

        Assert.Empty(js.Invocations);
        Assert.True(closed.Replace);
        Assert.Equal(page, closed.Uri);
    }

    /// <summary>Not one of the three: a different path is a different place, whatever the
    /// surfaces on either side of it were doing.</summary>
    [Fact]
    public void Following_a_link_to_another_page_pushes()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fparties%2Fnew");
        var surface = Build("aside", navigation);

        surface.Follow(surface.GetHref("optical/work-orders", Surfaces.Main), Surfaces.Main);

        var write = Assert.Single(navigation.Navigations);
        Assert.Equal("https://app.test/optical/work-orders", write.Uri);
        Assert.False(write.Replace);
    }

    /// <summary>Closed → open, from inside another surface. Escalating an aside to a modal is
    /// ?modal= absent and then present: the modal is a place of its own even though the aside
    /// underneath it never moved, so Back closes the modal and leaves the aside standing. Two
    /// overlays over one page are two entries, and peeling them one at a time is the rule
    /// applied honestly, not a leak.</summary>
    [Fact]
    public void Escalating_from_an_aside_to_a_modal_pushes()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fparties%2F7%2Fedit");
        var surface = Build("aside", navigation);

        surface.Follow(surface.GetHref("directory/roles", Surfaces.Auto), Surfaces.Auto);

        var write = Assert.Single(navigation.Navigations);
        Assert.Contains("modal=directory%2Froles", write.Uri, StringComparison.Ordinal);
        Assert.False(write.Replace);
    }

    /// <summary>Open → open, from the page underneath: a second row clicked while the aside is
    /// already showing the first. The key is present on both sides, so no flag and no target
    /// makes this an open — a back-stack that grew by one per click inside an open aside would
    /// take four Backs to leave a document the user visited once.</summary>
    [Fact]
    public void Pointing_an_open_surface_at_another_route_replaces()
    {
        var navigation = new FakeNavigation("https://app.test/parties?aside=directory%2Fparties%2F7%2Fedit");
        var surface = Build(null, navigation);

        surface.Follow(surface.GetHref("directory/parties/9/edit", Surfaces.Aside), Surfaces.Aside);

        var write = Assert.Single(navigation.Navigations);
        Assert.Contains("aside=directory%2Fparties%2F9%2Fedit", write.Uri, StringComparison.Ordinal);
        Assert.True(write.Replace);
    }

    /// <summary>How a write stacks changes nothing about what the address says — the routing
    /// side of a surface is untouched, so an address pasted fresh with ?aside= still opens the
    /// aside.</summary>
    [Fact]
    public void Opening_leaves_the_address_it_wrote_intact()
    {
        var navigation = new FakeNavigation("https://app.test/parties");
        var surface = Build(null, navigation);

        surface.Open(Surfaces.Aside, "directory/parties/new");

        var written = new Uri(Assert.Single(navigation.Navigations).Uri);

        Assert.Equal("directory/parties/new", SurfaceQuery.Parse(written.Query)["aside"]);
    }

    /// <summary>The whole rule in one gesture, on the three objects it actually runs on: the
    /// page underneath opens, the page underneath moves what is open, and the aside's own
    /// context closes — three SurfaceContexts, one SurfaceHistory, which is the only thing
    /// that can carry the answer between them.</summary>
    [Fact]
    public void The_three_transitions_push_replace_and_pop()
    {
        var history = new SurfaceHistory();

        var page = new FakeNavigation("https://app.test/directory/parties");

        Build(null, page, history).Open(Surfaces.Aside, "directory/parties/new");

        var opened = Assert.Single(page.Navigations);

        Assert.False(opened.Replace);

        var moving = new FakeNavigation(opened.Uri);
        var underneath = Build(null, moving, history);

        underneath.Follow(underneath.GetHref("directory/parties/7/edit", Surfaces.Aside), Surfaces.Aside);

        var moved = Assert.Single(moving.Navigations);

        Assert.True(moved.Replace);

        var closing = new FakeNavigation(moved.Uri);
        var js = new FakeJs();

        Build("aside", closing, history, js).Close();

        // Popping, and nothing else: a rewrite here would stack a second entry over the one the
        // open pushed rather than spend it, and Back would reopen what was just finished.
        Assert.Equal("history.back", Assert.Single(js.Invocations));
        Assert.Empty(closing.Navigations);
    }

    /// <summary>Open → closed, on the aside's own context and at the address the open wrote —
    /// which is how it actually happens: NsSurface keys its content by that route, so the
    /// object that closes never saw the open.</summary>
    [Fact]
    public void Closing_a_surface_whose_open_pushed_pops_that_entry()
    {
        var history = new SurfaceHistory();
        var opening = new FakeNavigation("https://app.test/optical/work-orders/new");

        Build(null, opening, history).Open(Surfaces.Aside, "optical/prescriptions/new");

        var opened = Assert.Single(opening.Navigations);
        var closing = new FakeNavigation(opened.Uri);
        var js = new FakeJs();

        Build("aside", closing, history, js).Close();

        Assert.Equal("history.back", Assert.Single(js.Invocations));
        Assert.Empty(closing.Navigations);
    }

    // An aside standing on an entry its own open pushed — the state every test below closes
    // from, built the way it is actually reached: the page underneath opens, and the aside's
    // own context is a different object at the address that open wrote.
    static (SurfaceContext Aside, FakeNavigation Navigation, FakeJs Js) Opened()
    {
        var history = new SurfaceHistory();
        var opening = new FakeNavigation("https://app.test/optical/work-orders/new");

        Build(null, opening, history).Open(Surfaces.Aside, "optical/prescriptions/new");

        var closing = new FakeNavigation(Assert.Single(opening.Navigations).Uri);
        var js = new FakeJs();

        return (Build("aside", closing, history, js), closing, js);
    }

    /// <summary>One gesture spends one entry. The pop is a round trip — JS interop, popstate,
    /// circuit, the location-changing handlers, unmount — and the X, Escape and the backdrop are
    /// three un-debounced handlers on the same surface, so a second Close() inside that window
    /// is ordinary rather than exotic. The record cannot answer it: SurfaceHistory describes the
    /// browser's stack, not this gesture, and still says the entry is there. Two pops for one
    /// click land one screen past the one the aside was opened from — and outside the app when
    /// that screen was the first in-app entry, which is the exact over-pop this story exists to
    /// prevent, reached by re-entrancy instead of a cold link.</summary>
    [Fact]
    public void Two_closes_inside_one_gesture_spend_one_history_entry()
    {
        var (aside, navigation, js) = Opened();

        aside.Close();
        aside.Close();

        Assert.Equal("history.back", Assert.Single(js.Invocations));
        Assert.Empty(navigation.Navigations);
    }

    /// <summary>The other end of the same gesture, and the reason the suppression cannot be a
    /// latch: a pop is guarded. Blazor restores the history position before it asks the
    /// location-changing handlers and leaves it restored if one refuses, so a dirty surface
    /// whose person answers "no" is still open and its X still has to close it — and the refusal
    /// raises no LocationChanged, so the guard that refused says so itself (NsSurfaceContext).
    /// Suppression that outlived a refusal would trade an over-pop for a dead X.</summary>
    [Fact]
    public void A_pop_the_guard_refused_leaves_the_surface_closable()
    {
        var (aside, navigation, js) = Opened();

        aside.Close();
        aside.EndPop();
        aside.Close();

        Assert.Equal(["history.back", "history.back"], js.Invocations);
        Assert.Empty(navigation.Navigations);
    }

    /// <summary>And the end a pop reaches when nothing refuses it: the entry is spent, the
    /// gesture is over, and nothing about this surface is held shut behind it. Nothing is
    /// cleared from the record on close, on purpose — it describes the browser's stack, so a
    /// surface standing again through Forward still pops — and the suppression must not be what
    /// quietly takes that back.</summary>
    [Fact]
    public void A_pop_that_landed_ends_the_suppression_with_it()
    {
        var (aside, navigation, js) = Opened();

        aside.Close();
        navigation.Land("https://app.test/optical/work-orders/new");
        aside.Close();

        Assert.Equal(["history.back", "history.back"], js.Invocations);
    }

    /// <summary>The 212e2a0f guard reads the address it is handed, never how that address was
    /// stacked, so the three transitions leave it exactly as it was: an open is still not a
    /// departure of the page underneath, and a close is still a departure of the aside itself
    /// (the X over a dirty form asks, exactly as it did).
    ///
    /// A pop-close is the same question through a different door and gets the same answer:
    /// Blazor hands the location-changing handlers the popped address — the one the open wrote
    /// over — before it lets the pop commit, and leaves the history position restored if a
    /// handler refuses (blazor.web.js, onBrowserInitiatedPopState). That address is this
    /// test's second assertion.</summary>
    [Fact]
    public void How_a_write_stacks_does_not_change_what_the_departure_guard_sees()
    {
        var navigation = new FakeNavigation("https://app.test/optical/work-orders/new");
        var page = Build(null, navigation);

        page.Open(Surfaces.Aside, "optical/prescriptions/new");

        var opened = Assert.Single(navigation.Navigations).Uri;

        Assert.False(page.Departs(opened));

        var aside = Build("aside", new FakeNavigation(opened));

        Assert.True(aside.Departs("https://app.test/optical/work-orders/new"));
    }
}

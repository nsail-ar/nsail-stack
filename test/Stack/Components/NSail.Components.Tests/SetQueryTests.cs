// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace NSail.Components.Tests;

/// <summary>The write half of the query seam (nsail#163), pinned surface by surface. GetQuery
/// asks which string the page is routed by; SetQuery has to write onto that same string, which
/// is the address bar on the main surface and the inner route inside ?aside= in an overlay —
/// two different places behind one door. The composition of that second one is where nsail#78
/// lives: a route resolved twice nests a query inside an aside, so the round trip here is
/// asserted byte for byte rather than by "contains".</summary>
public sealed class SetQueryTests
{
    const string PartyId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    sealed class MovingNavigation : NavigationManager
    {
        public List<string> Navigations { get; } = [];

        public List<NavigationOptions> Options { get; } = [];

        public MovingNavigation(string uri = "https://app.test/parties")
        {
            Initialize("https://app.test/", uri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            // The address has to actually MOVE: every pin about writing twice, or reading back
            // what was written, is a statement about the second trip starting where the first
            // one landed.
            var target = ToAbsoluteUri(uri).ToString();

            Navigations.Add(target);
            Options.Add(options);
            Uri = target;
        }
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

    static SurfaceContext Build(string? name, MovingNavigation navigation)
    {
        var routes = new RouteTable(typeof(SetQueryTests).Assembly, Array.Empty<System.Reflection.Assembly>());

        return new SurfaceContext(name is null ? null : new Surface(name), navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    static MovingNavigation Hosting(string innerRoute)
    {
        return new MovingNavigation($"https://app.test/optical/clients?aside={Uri.EscapeDataString(innerRoute)}");
    }

    // The main surface: the browser's own query is the one the page is routed by.

    [Fact]
    public void SetQuery_on_the_main_surface_merges_into_the_address_it_is_already_at()
    {
        var navigation = new MovingNavigation($"https://app.test/directory/parties?RoleIds={PartyId}");

        Build(name: null, navigation).SetQuery("Tab", "contacts");

        var target = Assert.Single(navigation.Navigations);
        Assert.Equal($"https://app.test/directory/parties?RoleIds={PartyId}&Tab=contacts", target);
    }

    [Fact]
    public void A_write_replaces_the_history_entry_rather_than_pushing_one()
    {
        // A tab or a filter is a view of the place the user is at, not a place of its own:
        // pushing would make Back walk every tab they touched instead of leaving the screen.
        var navigation = new MovingNavigation();

        Build(name: null, navigation).SetQuery("Tab", "contacts");

        Assert.True(Assert.Single(navigation.Options).ReplaceHistoryEntry);
    }

    [Fact]
    public void An_existing_key_is_rewritten_in_place_rather_than_repeated()
    {
        var navigation = new MovingNavigation("https://app.test/directory/parties?Tab=general&Page=3");

        Build(name: null, navigation).SetQuery("Tab", "contacts");

        Assert.Equal("https://app.test/directory/parties?Tab=contacts&Page=3", Assert.Single(navigation.Navigations));
    }

    [Fact]
    public void A_null_value_removes_the_parameter()
    {
        var navigation = new MovingNavigation("https://app.test/directory/parties?Tab=contacts&Page=3");

        Build(name: null, navigation).SetQuery("Tab", null);

        Assert.Equal("https://app.test/directory/parties?Page=3", Assert.Single(navigation.Navigations));
    }

    [Fact]
    public void Removing_the_last_parameter_leaves_no_question_mark_behind()
    {
        var navigation = new MovingNavigation("https://app.test/directory/parties?Tab=contacts");

        Build(name: null, navigation).SetQuery("Tab", null);

        Assert.Equal("https://app.test/directory/parties", Assert.Single(navigation.Navigations));
    }

    [Fact]
    public void A_write_leaves_an_open_surface_where_it_was()
    {
        // The page underneath writing its own filter must not close the aside over it — the
        // aside's whole route lives in the same query string being merged.
        var navigation = Hosting("directory/parties/new");

        Build(name: null, navigation).SetQuery("Tab", "contacts");

        Assert.Equal(
            "https://app.test/optical/clients?aside=directory%2Fparties%2Fnew&Tab=contacts",
            Assert.Single(navigation.Navigations));
    }

    [Fact]
    public void What_is_written_is_what_GetQuery_reads_back()
    {
        // Both ends of a URL are invariant, and here they are literally the same formatter:
        // a culture that writes 30/07/2026 would build a value the reader hands back as null.
        var surface = Build(name: null, new MovingNavigation("https://app.test/accounting/journal-entries"));

        surface.SetQuery("From", new DateOnly(2026, 8, 5));
        surface.SetQuery("Total", 1234.56m);
        surface.SetQuery("Page", 3);
        surface.SetQuery("Closed", true);
        surface.SetQuery("Size", NsSize.Xl);
        surface.SetQuery("At", new DateTime(2026, 8, 5, 14, 30, 0));
        surface.SetQuery("Search", "Pérez & Cía");

        Assert.Equal(new DateOnly(2026, 8, 5), surface.GetQuery<DateOnly?>("From"));
        Assert.Equal(1234.56m, surface.GetQuery<decimal>("Total"));
        Assert.Equal(3, surface.GetQuery<int?>("Page"));
        Assert.True(surface.GetQuery<bool>("Closed"));
        Assert.Equal(NsSize.Xl, surface.GetQuery<NsSize?>("Size"));
        Assert.Equal(new DateTime(2026, 8, 5, 14, 30, 0), surface.GetQuery<DateTime?>("At"));
        Assert.Equal("Pérez & Cía", surface.GetQuery<string>("Search"));
    }

    // Arrays: GetQuery<Guid[]> already collects a repeated key on the read side (nsail#183 is
    // the first writer that needs the other direction — PartiesPage's role filter).

    [Fact]
    public void An_array_value_writes_one_pair_per_element_and_GetQuery_reads_it_back()
    {
        var roleA = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        var roleB = Guid.Parse("d3d24b20-8a1e-4d5c-9b0f-1a2b3c4d5e6f");
        var navigation = new MovingNavigation("https://app.test/directory/parties");
        var surface = Build(name: null, navigation);

        surface.SetQuery("RoleIds", new[] { roleA, roleB });

        Assert.Equal(
            $"https://app.test/directory/parties?RoleIds={roleA}&RoleIds={roleB}",
            Assert.Single(navigation.Navigations));
        Assert.Equal(new[] { roleA, roleB }, surface.GetQuery<Guid[]>("RoleIds"));
    }

    [Fact]
    public void An_empty_array_removes_the_key()
    {
        var navigation = new MovingNavigation("https://app.test/directory/parties?RoleIds=3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        Build(name: null, navigation).SetQuery("RoleIds", Array.Empty<Guid>());

        Assert.Equal("https://app.test/directory/parties", Assert.Single(navigation.Navigations));
    }

    [Fact]
    public void Writing_an_array_the_same_length_twice_gives_back_the_same_address()
    {
        var roleA = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        var roleB = Guid.Parse("d3d24b20-8a1e-4d5c-9b0f-1a2b3c4d5e6f");
        var navigation = new MovingNavigation("https://app.test/directory/parties?Page=3");
        var surface = Build(name: null, navigation);

        surface.SetQuery("RoleIds", new[] { roleA, roleB });
        surface.SetQuery("RoleIds", new[] { roleA, roleB });

        Assert.Equal(2, navigation.Navigations.Count);
        Assert.Equal(navigation.Navigations[0], navigation.Navigations[1]);
        Assert.Equal($"https://app.test/directory/parties?Page=3&RoleIds={roleA}&RoleIds={roleB}", navigation.Navigations[1]);
    }

    [Fact]
    public void A_shorter_array_drops_the_leftover_occurrences_of_the_key()
    {
        var roleA = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        var navigation = new MovingNavigation(
            $"https://app.test/directory/parties?RoleIds={roleA}&RoleIds=d3d24b20-8a1e-4d5c-9b0f-1a2b3c4d5e6f");

        Build(name: null, navigation).SetQuery("RoleIds", new[] { roleA });

        Assert.Equal($"https://app.test/directory/parties?RoleIds={roleA}", Assert.Single(navigation.Navigations));
    }

    // The aside: the page's own route travels as the VALUE of ?aside=, so the write has to
    // reach inside that value and come back out escaped exactly once.

    [Fact]
    public void SetQuery_inside_an_aside_rewrites_the_asides_own_route_in_place()
    {
        var navigation = Hosting("directory/parties");

        Build("aside", navigation).SetQuery("Tab", "contacts");

        Assert.Equal(
            "https://app.test/optical/clients?aside=directory%2Fparties%3FTab%3Dcontacts",
            Assert.Single(navigation.Navigations));
    }

    [Fact]
    public void An_aside_merges_into_the_inner_query_it_already_carries()
    {
        var navigation = Hosting("directory/parties?Tab=general&Page=3");

        Build("aside", navigation).SetQuery("Tab", "contacts");

        var surface = Build("aside", navigation);

        Assert.Equal("contacts", surface.GetQuery<string>("Tab"));
        Assert.Equal(3, surface.GetQuery<int?>("Page"));
    }

    [Fact]
    public void An_aside_writing_its_own_query_does_not_touch_the_page_underneath()
    {
        var navigation = new MovingNavigation(
            $"https://app.test/optical/clients?Tab=underneath&aside={Uri.EscapeDataString("directory/parties")}");

        Build("aside", navigation).SetQuery("Tab", "contacts");

        Assert.Equal("contacts", Build("aside", navigation).GetQuery<string>("Tab"));
        Assert.Equal("underneath", Build(name: null, navigation).GetQuery<string>("Tab"));
    }

    [Fact]
    public void A_null_value_inside_an_aside_removes_only_that_key_from_the_inner_route()
    {
        var navigation = Hosting("directory/parties?Tab=contacts");

        Build("aside", navigation).SetQuery("Tab", null);

        Assert.Equal(
            "https://app.test/optical/clients?aside=directory%2Fparties",
            Assert.Single(navigation.Navigations));
    }

    /// <summary>nsail#78's mechanism, pinned from the write side: an already-resolved route sent
    /// back through the resolver nests the query inside the aside and escapes it again. SetQuery
    /// composes from the route it READ, never from an href it resolved, so writing the same value
    /// twice has to give back the same address byte for byte — an extra round of escaping or an
    /// extra '?' would grow the URL on every write.</summary>
    [Fact]
    public void Writing_the_same_value_twice_gives_back_the_same_address()
    {
        var navigation = Hosting("directory/parties?Page=3");
        var surface = Build("aside", navigation);

        surface.SetQuery("Tab", "contacts");
        surface.SetQuery("Tab", "contacts");
        surface.SetQuery("Tab", "contacts");

        Assert.Equal(3, navigation.Navigations.Count);
        Assert.Equal(navigation.Navigations[0], navigation.Navigations[1]);
        Assert.Equal(navigation.Navigations[1], navigation.Navigations[2]);
    }

    [Fact]
    public void The_composed_address_carries_one_query_and_one_round_of_escaping()
    {
        var navigation = Hosting("directory/parties");
        var surface = Build("aside", navigation);

        surface.SetQuery("Tab", "contacts");
        surface.SetQuery("Search", "Pérez & Cía");

        var target = navigation.Navigations[^1];

        // One '?' — the browser's own. A nested query would bring a second live one, and a
        // second round of escaping would show up as an escaped percent sign.
        Assert.Equal(1, target.Count(character => character == '?'));
        Assert.DoesNotContain("%253F", target, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%2525", target, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aside=aside", target, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("contacts", surface.GetQuery<string>("Tab"));
        Assert.Equal("Pérez & Cía", surface.GetQuery<string>("Search"));
    }

    [Fact]
    public void An_aside_that_is_routed_by_nothing_has_no_own_route_to_write_onto()
    {
        var navigation = new MovingNavigation("https://app.test/optical/clients");

        Build("aside", navigation).SetQuery("Tab", "contacts");

        Assert.Empty(navigation.Navigations);
    }

    // The dialog: routed by nothing, carries no query — the honest answer is nothing at all.

    [Fact]
    public void SetQuery_on_a_host_managed_surface_neither_throws_nor_navigates()
    {
        var navigation = Hosting($"accounting/books/party-account?PartyId={PartyId}");
        var routes = new RouteTable(typeof(SetQueryTests).Assembly, Array.Empty<System.Reflection.Assembly>());
        var surface = new SurfaceContext(Surfaces.Dialog, navigation, routes, new FakeJs(), new SurfaceHistory(), close: () => { });

        surface.SetQuery("Tab", "contacts");
        surface.SetQuery("Tab", null);

        Assert.Empty(navigation.Navigations);
    }

    [Fact]
    public void An_empty_name_is_refused_on_every_surface()
    {
        Assert.Throws<ArgumentException>(() => Build(name: null, new MovingNavigation()).SetQuery(string.Empty, "x"));
        Assert.Throws<ArgumentException>(() => Build("aside", Hosting("directory/parties")).SetQuery(string.Empty, "x"));
    }
}

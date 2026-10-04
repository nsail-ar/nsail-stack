// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Components;
using System.Globalization;

namespace NSail.Components.Tests;

[Route("/parties")]
public sealed class PartiesTestPage : ComponentBase;

[Route("/parties/new")]
[Route("/parties/{Id}/edit")]
public sealed class PartyDetailTestPage : ComponentBase;

[Route("/parties/{Id}")]
public sealed class PartyViewTestPage : ComponentBase;

[Route("/files/{*Path}")]
public sealed class FilesTestPage : ComponentBase;

// A page serving both a bare list route and a scoped one (MembershipsPage): the same
// component renders full-screen and inside a surface.
[Route("/memberships")]
[Route("/parties/{PartyId:guid}/memberships")]
public sealed class MembershipsTestPage : ComponentBase;

// A page opened prefilled with a wall clock (the agenda's free slot into the intake form):
// both templates on one component, the tokens carrying resource and time.
[Route("/slots/new")]
[Route("/slots/new/{ResourceId:guid}/{StartsLocal:datetime}")]
public sealed class SlotTestPage : ComponentBase;

// The app root — Optical's home is one of these. NsTitleBar derives its icon from whatever
// ToBaseRelativePath answers, and standing on this page that answer is the empty string.
[Route("/")]
public sealed class HomeTestPage : ComponentBase;

public sealed class UnroutedTestPage : ComponentBase;

public sealed class RouteTableTests
{
    private static RouteTable Build()
    {
        return new(typeof(RouteTableTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    [Fact]
    public void Match_prefers_literal_segments_over_parameters()
    {
        var table = Build();

        var match = table.Match("/parties/new");

        Assert.NotNull(match);
        Assert.Equal(typeof(PartyDetailTestPage), match.PageType);
    }

    [Fact]
    public void Match_extracts_route_values()
    {
        var table = Build();

        var match = table.Match("/parties/42");

        Assert.NotNull(match);
        Assert.Equal(typeof(PartyViewTestPage), match.PageType);
        Assert.Equal("42", match.RouteValues["Id"]);
    }

    [Fact]
    public void Match_ignores_query_and_fragment()
    {
        var table = Build();

        var match = table.Match("parties/42?x=1#top");

        Assert.NotNull(match);
        Assert.Equal(typeof(PartyViewTestPage), match.PageType);
    }

    [Fact]
    public void Match_joins_catch_all_segments()
    {
        var table = Build();

        var match = table.Match("/files/a/b/c");

        Assert.NotNull(match);
        Assert.Equal(typeof(FilesTestPage), match.PageType);
        Assert.Equal("a/b/c", match.RouteValues["Path"]);
    }

    [Fact]
    public void Match_returns_null_when_nothing_matches()
    {
        var table = Build();

        Assert.Null(table.Match("/nowhere/at/all"));
    }

    // What NavigationManager.ToBaseRelativePath hands a caller standing on the app root, and
    // what NsTitleBar therefore hands this: the empty string is the root's own address, not a
    // missing argument. NsNavMenu.FindActive already reads it that way.
    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void Match_reads_the_empty_path_as_the_root_route(string route)
    {
        var table = Build();

        var match = table.Match(route);

        Assert.NotNull(match);
        Assert.Equal(typeof(HomeTestPage), match.PageType);
    }

    [Fact]
    public void Match_still_refuses_a_null_route()
    {
        var table = Build();

        Assert.Throws<ArgumentNullException>(() => table.Match(null!));
    }

    [Fact]
    public void GetUrl_returns_the_single_template()
    {
        var table = Build();

        Assert.Equal("/parties", table.GetUrl<PartiesTestPage>());
    }

    [Fact]
    public void GetUrl_picks_the_template_binding_most_parameters()
    {
        var table = Build();

        Assert.Equal("/parties/5/edit", table.GetUrl<PartyDetailTestPage>(new { Id = 5 }));
        Assert.Equal("/parties/new", table.GetUrl<PartyDetailTestPage>());
    }

    [Fact]
    public void GetUrl_escapes_parameter_values()
    {
        var table = Build();

        Assert.Equal("/parties/a%20b", table.GetUrl<PartyViewTestPage>(new { Id = "a b" }));
    }

    [Fact]
    public void GetUrl_throws_for_components_without_routes()
    {
        var table = Build();

        Assert.Throws<InvalidOperationException>(() => table.GetUrl<UnroutedTestPage>());
    }

    [Fact]
    public void GetUrl_picks_the_scoped_template_when_the_scope_is_supplied()
    {
        var table = Build();
        var partyId = Guid.NewGuid();

        Assert.Equal($"/parties/{partyId}/memberships", table.GetUrl<MembershipsTestPage>(new { partyId }));
        Assert.Equal("/memberships", table.GetUrl<MembershipsTestPage>());
    }

    [Fact]
    public void Match_binds_a_typed_route_value_for_the_scoped_template()
    {
        var table = Build();
        var partyId = Guid.NewGuid();

        var match = table.Match($"/parties/{partyId}/memberships");

        Assert.NotNull(match);
        Assert.Equal(typeof(MembershipsTestPage), match.PageType);
        Assert.Equal(partyId, match.RouteValues["PartyId"]);
    }

    [Fact]
    public void Match_binds_no_route_values_for_the_bare_template()
    {
        var table = Build();

        var match = table.Match("/memberships");

        Assert.NotNull(match);
        Assert.Equal(typeof(MembershipsTestPage), match.PageType);
        Assert.Empty(match.RouteValues);
    }

    [Fact]
    public void GetUrl_writes_a_wall_clock_sortably()
    {
        var table = Build();
        var resourceId = Guid.NewGuid();

        var url = table.GetUrl<SlotTestPage>(new { resourceId, startsLocal = new DateTime(2026, 7, 30, 9, 30, 0) });

        Assert.Equal($"/slots/new/{resourceId}/2026-07-30T09%3A30%3A00", url);
    }

    [Fact]
    public void Match_binds_a_datetime_route_value_with_no_kind()
    {
        var table = Build();
        var resourceId = Guid.NewGuid();

        var match = table.Match(table.GetUrl<SlotTestPage>(
            new { resourceId, startsLocal = new DateTime(2026, 7, 30, 9, 30, 0) }));

        Assert.NotNull(match);
        Assert.Equal(typeof(SlotTestPage), match.PageType);
        Assert.Equal(resourceId, match.RouteValues["ResourceId"]);

        var starts = Assert.IsType<DateTime>(match.RouteValues["StartsLocal"]);

        Assert.Equal(new DateTime(2026, 7, 30, 9, 30, 0), starts);
        Assert.Equal(DateTimeKind.Unspecified, starts.Kind);
    }

    // The regression the invariant formatting exists for: a culture that writes 30/07/2026
    // used to build a URL its own reader could not match, and the surface rendered nothing.
    [Fact]
    public void The_round_trip_survives_a_day_first_culture()
    {
        var table = Build();
        var resourceId = Guid.NewGuid();
        var culture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-AR");

            var match = table.Match(table.GetUrl<SlotTestPage>(
                new { resourceId, startsLocal = new DateTime(2026, 7, 30, 9, 30, 0) }));

            Assert.NotNull(match);
            Assert.Equal(new DateTime(2026, 7, 30, 9, 30, 0), match.RouteValues["StartsLocal"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Match_falls_back_to_the_bare_template_for_a_malformed_datetime()
    {
        var table = Build();

        var match = table.Match($"/slots/new/{Guid.NewGuid()}/not-a-time");

        Assert.Null(match);
    }

    [Fact]
    public void GetUrl_writes_a_date_filter_sortably()
    {
        var table = Build();
        var culture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-AR");

            Assert.Equal(
                "/parties?from=2026-07-30",
                table.GetUrl<PartiesTestPage>(new { from = new DateOnly(2026, 7, 30) }));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}

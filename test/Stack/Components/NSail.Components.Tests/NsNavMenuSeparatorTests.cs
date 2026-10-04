// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/probe/home")]
public sealed class SeparatorHomeProbePage : ComponentBase;

[Route("/probe/clients")]
public sealed class SeparatorClientsProbePage : ComponentBase;

[Route("/probe/separator/admin")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
public sealed class SeparatorAdminProbePage : ComponentBase;

[Route("/probe/section/inbox")]
public sealed class SeparatorSectionInboxProbePage : ComponentBase;

[Route("/probe/section/archive")]
public sealed class SeparatorSectionArchiveProbePage : ComponentBase;

/// <summary>The drawer's thin line used to be deduced from the markup — a border above any
/// top-level group that was not first — which said "a group starts here", something the chevron
/// already said, and left an app unable to remove a line it never asked for (nsail#839). It is
/// an item now: drawn where a contributor placed it, presentational, and gated by nothing —
/// which is why nothing collapses a run of them on its own, and why NsNavMenu's render, not
/// Merge, is what applies "a rule between nothing and nothing" (the search-filter case above)
/// to a permission's empty band and to a level's own edges (nsail#1052).</summary>
public sealed class NsNavMenuSeparatorTests : BunitContext
{
    BunitAuthorizationContext Setup(params NavMenuItem[] items)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsNavMenuSeparatorTests).Assembly, []));
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<INavMenuContributor>(new FixedNavContributor(items));
        JSInterop.Mode = JSRuntimeMode.Loose;

        return this.AddAuthorization();
    }

    static NavMenuItem[] HomeLineClients()
    {
        return
        [
            new NavMenuItem { Name = "Home", Weight = 1, PageType = typeof(SeparatorHomeProbePage) },
            NavMenuItem.Separator(2),
            new NavMenuItem { Name = "Clients", Weight = 5, PageType = typeof(SeparatorClientsProbePage) }
        ];
    }

    [Fact]
    public void ASeparatorRendersAsARoleSeparatorWithNoTabStop()
    {
        Setup(HomeLineClients());

        var cut = Render<NsNavMenu>();

        var separator = cut.Find(".mud-navmenu [role='separator']");

        Assert.Empty(separator.TextContent);
        Assert.Null(separator.GetAttribute("tabindex"));

        // Where the contributor put it: after the first entry and before the second, read off
        // the menu's own rows rather than off a tag name the vendor picks.
        var rows = cut.Find(".mud-navmenu").Children.ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("separator", rows[1].GetAttribute("role"));
        Assert.Contains("/probe/home", rows[0].OuterHtml);
        Assert.Contains("/probe/clients", rows[2].OuterHtml);
    }

    /// <summary>The gate answers about pages, and a separator names none: the old Hide read a
    /// null PageType as "denied" and would have taken every line out of the drawer.</summary>
    [Fact]
    public void ASeparatorSurvivesThePermissionGate()
    {
        Setup(HomeLineClients());

        var cut = Render<NsNavMenu>();

        Assert.Single(cut.FindAll(".mud-navmenu [role='separator']"));
    }

    /// <summary>Ruled with the item: a filtered drawer is a list of matches and not the map, so
    /// the families a line stood between are gone and the line goes with them. The alternative —
    /// keeping it — draws a rule between nothing and nothing.</summary>
    [Fact]
    public async Task ASeparatorGoesWhileTheSearchIsFiltering()
    {
        Setup(HomeLineClients());

        var cut = Render<NsNavMenu>();

        await Search(cut, "Cli");

        cut.WaitForAssertion(
            () =>
            {
                Assert.Empty(cut.FindAll(".mud-navmenu [role='separator']"));
                Assert.Single(cut.FindAll("a[href='/probe/clients']"));
            },
            TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task TheSeparatorComesBackWhenTheSearchIsCleared()
    {
        Setup(HomeLineClients());

        var cut = Render<NsNavMenu>();

        await Search(cut, "Cli");

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll(".mud-navmenu [role='separator']")),
            TimeSpan.FromSeconds(10));

        await Search(cut, string.Empty);

        cut.WaitForAssertion(
            () => Assert.Single(cut.FindAll(".mud-navmenu [role='separator']")),
            TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void TwoConsecutiveSeparatorsDrawOneLine()
    {
        Setup(
            new NavMenuItem { Name = "Home", Weight = 1, PageType = typeof(SeparatorHomeProbePage) },
            NavMenuItem.Separator(2),
            NavMenuItem.Separator(3),
            new NavMenuItem { Name = "Clients", Weight = 5, PageType = typeof(SeparatorClientsProbePage) });

        var cut = Render<NsNavMenu>();

        Assert.Single(cut.FindAll(".mud-navmenu [role='separator']"));
    }

    [Fact]
    public void ASeparatorLeadingTheLevelDrawsNoLine()
    {
        Setup(
            NavMenuItem.Separator(0),
            new NavMenuItem { Name = "Home", Weight = 1, PageType = typeof(SeparatorHomeProbePage) },
            new NavMenuItem { Name = "Clients", Weight = 5, PageType = typeof(SeparatorClientsProbePage) });

        var cut = Render<NsNavMenu>();

        Assert.Empty(cut.FindAll(".mud-navmenu [role='separator']"));
    }

    [Fact]
    public void ASeparatorTrailingTheLevelDrawsNoLine()
    {
        Setup(
            new NavMenuItem { Name = "Home", Weight = 1, PageType = typeof(SeparatorHomeProbePage) },
            new NavMenuItem { Name = "Clients", Weight = 5, PageType = typeof(SeparatorClientsProbePage) },
            NavMenuItem.Separator(9));

        var cut = Render<NsNavMenu>();

        Assert.Empty(cut.FindAll(".mud-navmenu [role='separator']"));
    }

    /// <summary>The case the story names: a session whose permissions empty a whole band would
    /// otherwise see the two lines that used to bracket it stand side by side with nothing
    /// between them. The gate runs first (Hide), so by the time Shown collapses the level the
    /// denied entry never entered the sequence at all — this pins the outcome, not the
    /// mechanism ASeparatorSurvivesThePermissionGate already does.</summary>
    [Fact]
    public void APermissionThatEmptiesABandLeavesOneLineWhereTwoWouldStand()
    {
        var auth = Setup(
            new NavMenuItem { Name = "Home", Weight = 1, PageType = typeof(SeparatorHomeProbePage) },
            NavMenuItem.Separator(2),
            new NavMenuItem { Name = "Admin", Weight = 4, PageType = typeof(SeparatorAdminProbePage) },
            NavMenuItem.Separator(6),
            new NavMenuItem { Name = "Clients", Weight = 8, PageType = typeof(SeparatorClientsProbePage) });

        auth.SetAuthorized("clerk");
        auth.SetRoles("clerk");

        var cut = Render<NsNavMenu>();

        Assert.Single(cut.FindAll(".mud-navmenu [role='separator']"));
        Assert.DoesNotContain("/probe/separator/admin", cut.Markup);
    }

    /// <summary>The story's other case: a session denied everything below a line used to leave
    /// it hanging off the end with nothing left to separate. Trailing is trailing whether the
    /// entry after it was never contributed or was contributed and then denied.</summary>
    [Fact]
    public void APermissionThatEmptiesTheRestOfTheLevelLeavesNoLineHangingOffTheEnd()
    {
        var auth = Setup(
            new NavMenuItem { Name = "Home", Weight = 1, PageType = typeof(SeparatorHomeProbePage) },
            NavMenuItem.Separator(2),
            new NavMenuItem { Name = "Admin", Weight = 4, PageType = typeof(SeparatorAdminProbePage) },
            NavMenuItem.Separator(6));

        auth.SetAuthorized("clerk");
        auth.SetRoles("clerk");

        var cut = Render<NsNavMenu>();

        Assert.Empty(cut.FindAll(".mud-navmenu [role='separator']"));
    }

    /// <summary>The rule applies per level, not once over the whole tree: a group's own children
    /// collapse their own run independently of whatever the top level is doing.</summary>
    [Fact]
    public void AGroupCollapsesARunAmongItsOwnChildren()
    {
        Setup(
            new NavMenuItem
            {
                Name = "Section",
                Weight = 1,
                Items =
                [
                    new NavMenuItem { Name = "Inbox", Weight = 1, PageType = typeof(SeparatorSectionInboxProbePage) },
                    NavMenuItem.Separator(2),
                    NavMenuItem.Separator(3),
                    new NavMenuItem { Name = "Archive", Weight = 4, PageType = typeof(SeparatorSectionArchiveProbePage) }
                ]
            });

        var cut = Render<NsNavMenu>();

        var group = cut.Find(".mud-nav-group");

        Assert.Single(group.QuerySelectorAll("[role='separator']"));
    }

    // NsSearchField debounces (Immediate, no onchange), so the filter lands a moment after the
    // keystroke — every search assertion waits for it rather than reading the DOM straight
    // after the input (NsTreeTests carries the same note and the same generous timeout).
    static async Task Search(IRenderedComponent<NsNavMenu> cut, string term)
    {
        await cut.InvokeAsync(() => cut.Find(".mud-input-slot").Input(term));
    }
}

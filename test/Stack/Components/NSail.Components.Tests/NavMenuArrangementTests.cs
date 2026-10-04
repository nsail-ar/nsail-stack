// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Settings;

namespace NSail.Components.Tests;

[Route("/arrangement/agenda")]
public sealed class ArrangedAgendaPage : ComponentBase;

[Route("/arrangement/calendar")]
public sealed class ArrangedCalendarPage : ComponentBase;

/// <summary>An install arranges its own menu: order, label and whether an entry shows, read out
/// of stored data in place of an override somebody would otherwise have to write in code. The
/// layer sits ON TOP of the modules' contributions, so what it holds are the changes a shop
/// made — never the menu itself, which is why an entry no row names is the case with the most
/// tests here.</summary>
public sealed class NavMenuArrangementTests
{
    static readonly NavMenuItem Agenda = new()
    {
        Name = "Scheduling",
        Icon = new Glyph("calendar"),
        Weight = 5,
        PageType = typeof(ArrangedAgendaPage)
    };

    static readonly NavMenuItem Calendar = new()
    {
        Name = "Calendar",
        Icon = new Glyph("today"),
        Weight = 2,
        PageType = typeof(ArrangedCalendarPage)
    };

    static Task<AuthenticationState> Session()
    {
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    static NavMenu Menu(INavMenuArrangement arrangement, string language = "es", params NavMenuItem[] contributed)
    {
        return new NavMenu(
            [new FixedNavContributor(contributed.Length > 0 ? contributed : [Calendar, Agenda])],
            [],
            [arrangement],
            new LanguageProvider { Current = language });
    }

    /// <summary>The three fields the story is about, on one stored row, over a kit's entry that
    /// knows nothing about them.</summary>
    [Fact]
    public async Task OrderLabelAndVisibilityComeOutOfTheStoredRow()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry
            {
                Name = "Scheduling",
                Order = 25,
                Labels = new Dictionary<string, string> { ["es"] = "Contactología" }
            },
            new NavMenuArrangementEntry { Name = "Calendar", Shown = false }));

        var items = await menu.GetItems(Session());
        var agenda = items.Single(item => item.Name == "Scheduling");

        Assert.Equal(25, agenda.Weight);
        Assert.Equal("Contactología", agenda.Label);
        Assert.False(items.Single(item => item.Name == "Calendar").Visible);
    }

    /// <summary>The kit's own contribution stands: a row carries what it changes, so the icon
    /// and the page of an entry a row only renames are the ones the module planted.</summary>
    [Fact]
    public async Task ARowSaysOnlyWhatItChanges()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry
            {
                Name = "Scheduling",
                Labels = new Dictionary<string, string> { ["es"] = "Contactología" }
            }));

        var agenda = (await menu.GetItems(Session())).Single(item => item.Name == "Scheduling");

        Assert.Equal(new Glyph("calendar"), agenda.Icon);
        Assert.Equal(typeof(ArrangedAgendaPage), agenda.PageType);
        Assert.Equal(5, agenda.Weight);
    }

    /// <summary>A stored icon replaces the module's, which is what an install that gave a door
    /// its own glyph is saying.</summary>
    [Fact]
    public async Task AStoredIconIsTheOneDrawn()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry { Name = "Scheduling", Icon = new Glyph("lens") }));

        var agenda = (await menu.GetItems(Session())).Single(item => item.Name == "Scheduling");

        Assert.Equal(new Glyph("lens"), agenda.Icon);
    }

    /// <summary>The whole of AC 4: a release adds a screen, the install arranged its menu long
    /// before, and the new door is exactly where its module put it. An arrangement is merged
    /// onto the contributions and never substituted for them.</summary>
    [Fact]
    public async Task AnEntryNoRowNamesKeepsThePlaceItsModuleGaveIt()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry { Name = "Scheduling", Order = 25 }));

        var calendar = (await menu.GetItems(Session())).Single(item => item.Name == "Calendar");

        Assert.Equal(2, calendar.Weight);
        Assert.Equal(new Glyph("today"), calendar.Icon);
        Assert.Null(calendar.Visible);
        Assert.Null(calendar.Label);
    }

    /// <summary>A row naming an entry nothing contributes is ignored rather than drawn or
    /// thrown: a stored row outlives the release that could fix it, so an unmounted kit must not
    /// leave a drawer that cannot be built at all — and must not leave a door to nowhere either.</summary>
    [Fact]
    public async Task ARowNamingNothingContributedIsIgnored()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry { Name = "Payroll", Order = 1 },
            new NavMenuArrangementEntry { Name = "Scheduling", Order = 25 }));

        var items = await menu.GetItems(Session());

        Assert.Equal(["Calendar", "Scheduling"], items.Select(item => item.Name));
    }

    /// <summary>A stored Parent nests one module's first-level door under another's group, which
    /// is what Optical's Calendario row does — and the entry keeps its own page and icon.</summary>
    [Fact]
    public async Task AStoredParentNestsTheEntryUnderTheNamedGroup()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry { Name = "Calendar", Parent = "Scheduling", Order = 10 }));

        var items = await menu.GetItems(Session());
        var agenda = Assert.Single(items);

        Assert.Equal("Scheduling", agenda.Name);

        var calendar = Assert.Single(agenda.Items);

        Assert.Equal("Calendar", calendar.Name);
        Assert.Equal(typeof(ArrangedCalendarPage), calendar.PageType);
    }

    /// <summary>A Parent nothing answers to leaves the entry where it was contributed — the same
    /// forgiveness the row itself gets, and for the same reason.</summary>
    [Fact]
    public async Task AStoredParentNothingContributesLeavesTheEntryWhereItWas()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry { Name = "Calendar", Parent = "Payroll" }));

        var items = await menu.GetItems(Session());

        Assert.Equal(["Calendar", "Scheduling"], items.Select(item => item.Name));
    }

    /// <summary>Two rows hanging under each other would throw in the merge and take the drawer
    /// with them, and an install cannot reach its own editor through a drawer that does not
    /// draw.</summary>
    [Fact]
    public async Task RowsThatHangUnderEachOtherDoNotTakeTheDrawerDown()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry { Name = "Calendar", Parent = "Scheduling" },
            new NavMenuArrangementEntry { Name = "Scheduling", Parent = "Calendar" }));

        var items = await menu.GetItems(Session());

        Assert.NotEmpty(items);
    }

    /// <summary>Hiding is not a permission: the entry leaves the drawer and stays in the tree,
    /// so a page reached some other way keeps the glyph on its own title bar and its own
    /// authorize attribute is still the only thing deciding whether it opens.</summary>
    [Fact]
    public async Task AHiddenEntryStaysInTheTreeForTheTitleBar()
    {
        var menu = Menu(new FixedNavArrangement(
            new NavMenuArrangementEntry { Name = "Calendar", Shown = false }));

        var items = await menu.GetItems(Session());

        Assert.Equal(typeof(ArrangedCalendarPage), NavMenuItem.Find(items, typeof(ArrangedCalendarPage))?.PageType);
    }

    /// <summary>The shop's own noun is stored per language, and a language it wrote no word for
    /// reads the module's string — never another language's word, which would be a translation
    /// nobody made.</summary>
    [Theory]
    [InlineData("es", "Contactología")]
    [InlineData("en", "Contactology")]
    [InlineData("pt", null)]
    public async Task TheLabelIsTheOneStoredForThisLanguage(string language, string? expected)
    {
        var menu = Menu(
            new FixedNavArrangement(new NavMenuArrangementEntry
            {
                Name = "Scheduling",
                Labels = new Dictionary<string, string> { ["es"] = "Contactología", ["en"] = "Contactology" }
            }),
            language);

        var agenda = (await menu.GetItems(Session())).Single(item => item.Name == "Scheduling");

        Assert.Equal(expected, agenda.Label);
    }

    /// <summary>AC 3, at the seam: the same code over different stored data is a different
    /// drawer. Nothing here is a deployment.</summary>
    [Fact]
    public async Task ChangingTheStoredRowsChangesTheDrawerWithNoCodeChange()
    {
        var before = await Menu(new FixedNavArrangement()).GetItems(Session());

        var after = await Menu(new FixedNavArrangement(
                new NavMenuArrangementEntry { Name = "Calendar", Parent = "Scheduling" },
                new NavMenuArrangementEntry
                {
                    Name = "Scheduling",
                    Order = 25,
                    Labels = new Dictionary<string, string> { ["es"] = "Agenda de la casa" }
                }))
            .GetItems(Session());

        Assert.Equal(["Calendar", "Scheduling"], before.Select(item => item.Name));
        Assert.Equal(["Scheduling"], after.Select(item => item.Name));
        Assert.Equal("Agenda de la casa", after.Single().Label);
    }

    /// <summary>The arrangement is not a contributor and cannot be registered before one: it is
    /// read after every module has spoken, so a module that contributes an entry LAST still
    /// finds the install's word over its own.</summary>
    [Fact]
    public async Task TheInstallsWordStandsOverAModuleContributingLast()
    {
        var menu = new NavMenu(
            [
                new FixedNavContributor(Agenda),
                new FixedNavContributor(new NavMenuItem { Name = "Scheduling", Weight = 90, Icon = new Glyph("late") })
            ],
            [],
            [
                new FixedNavArrangement(new NavMenuArrangementEntry
                {
                    Name = "Scheduling",
                    Order = 25,
                    Icon = new Glyph("lens")
                })
            ],
            new LanguageProvider { Current = "es" });

        var agenda = Assert.Single(await menu.GetItems(Session()));

        Assert.Equal(25, agenda.Weight);
        Assert.Equal(new Glyph("lens"), agenda.Icon);
    }

    /// <summary>An install arranges the doors its modules planted and cannot author a gate: the
    /// stored row has no field for a permission, so a door a module gated stays gated however the
    /// shop reorders or renames it.</summary>
    [Fact]
    public async Task AStoredRowCannotTouchADoorsPermission()
    {
        var gated = new NavMenuItem
        {
            Name = "Scheduling",
            Weight = 5,
            PageType = typeof(ArrangedAgendaPage),
            Permission = typeof(NavMenuArrangementTests)
        };

        var menu = Menu(
            new FixedNavArrangement(new NavMenuArrangementEntry { Name = "Scheduling", Order = 25 }),
            "es",
            gated);

        var agenda = (await menu.GetItems(Session())).Single(item => item.Name == "Scheduling");

        Assert.Equal(typeof(NavMenuArrangementTests), agenda.Permission);
        Assert.Equal(25, agenda.Weight);
    }
}

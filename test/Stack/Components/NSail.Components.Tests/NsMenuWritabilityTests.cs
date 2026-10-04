// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1323: the act's own word (ActionItem.Writes, nsail#1321) reaches the third
/// chrome an ActionItem has a mouth in — a menu row. A kit that draws its own NsMenu inside an
/// NsForm handed a way in to a form that refuses writes, and NsForm cascades Disabled ||
/// IsRunning, so it was open on every submit of every screen that has one (OpticalJobCard's
/// Comparar, which writes the very field the NsSelect beside it greys out).
///
/// Two answers, because a menu is two things. A row answers for its own act — and can only do
/// it because the menu carries the form's word across the vendor's portal, which the body of a
/// popover renders outside. The FACE answers for what it opens: a menu is a way in unless it
/// says otherwise, so a trigger that would open a list of withheld rows is not drawn at all.
/// The toolbar's two menus declare the opposite and keep their faces — their rows passed the
/// same word before they were counted, and asking twice would take the kebab away from a
/// read-only row that still has links to offer.</summary>
public sealed class NsMenuWritabilityTests : BunitContext, IAsyncLifetime
{
    public NsMenuWritabilityTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Compare"] = "Presupuestos",
            ["Actions.Assign"] = "Asignar",
            ["Actions.Archive"] = "Archivar",
            ["Actions.Contact"] = "Contactar",
            ["Actions.Print"] = "Imprimir",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsMenuWritabilityTests).Assembly, []));
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsActWritabilityTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static async Task<IReadOnlyList<string>> Open(IRenderedComponent<MenuFormHost> cut, string trigger)
    {
        await Face(cut, trigger).ClickAsync(new MouseEventArgs());

        return Rows(cut);
    }

    // A trigger is named by its own text where it has one and by aria-label where it is a
    // glyph, and both shapes stand in this fixture.
    static IElement Face(IRenderedComponent<MenuFormHost> cut, string trigger)
    {
        return cut
            .FindAll(".mud-menu button")
            .Single(button => $"{button.GetAttribute("aria-label")} {button.TextContent}".Contains(trigger, StringComparison.Ordinal));
    }

    static IReadOnlyList<string> Rows(IRenderedComponent<MenuFormHost> cut)
    {
        return cut.FindAll(".mud-menu-item").Select(row => row.TextContent.Trim()).ToList();
    }

    static IReadOnlyList<IElement> Menus(IRenderedComponent<MenuFormHost> cut)
    {
        return cut.Find(".ns-action-toolbar").QuerySelectorAll(".mud-menu").ToList();
    }

    [Fact]
    public async Task AWritableFormDrawsEveryMenuAndEveryRowInThem()
    {
        var cut = Render<MenuFormHost>();

        Assert.Contains("Comparar", cut.Markup, StringComparison.Ordinal);

        // The contributed act that offers a choice, the one beside it, and the kebab the cap
        // pushed the rest into.
        Assert.Equal(3, Menus(cut).Count);

        var rows = await Open(cut, "Comparar");

        Assert.Contains("Taller Barato", rows);
        Assert.Contains("Taller Caro", rows);
    }

    [Fact]
    public void AReadOnlyFormWithholdsTheMenuWhoseRowsWrite()
    {
        AssertNoWayIn(Render<MenuFormHost>(p => p.Add(x => x.ReadOnly, true)));
    }

    /// <summary>Disabled is the state every submit puts on a form for as long as it runs, so
    /// this is not the read-only screen nobody has mounted yet — it is Guardar, on the screen
    /// the bug was found on. The menu that survives is greyed by the vendor's own read of the
    /// same cascade name (MudBlazor 9.10), which is what a value does and not what a way in
    /// does: this rule withholds.</summary>
    [Fact]
    public void ADisabledFormWithholdsTheMenuWhoseRowsWrite()
    {
        var cut = Render<MenuFormHost>(p => p.Add(x => x.Disabled, true));

        AssertNoWayIn(cut);

        Assert.NotNull(Face(cut, "Mixta").GetAttribute("disabled"));
    }

    static void AssertNoWayIn(IRenderedComponent<MenuFormHost> cut)
    {
        // Face and all: a trigger that opens a list of withheld rows is a button that does
        // nothing, which is worse than no button.
        Assert.DoesNotContain("Comparar", cut.Markup, StringComparison.Ordinal);

        // What the form takes away is the way IN, not the menu — one whose rows answer for
        // themselves keeps its face.
        Assert.Contains("Mixta", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The row's own answer, and the only reason it can give one: a menu's body is the
    /// vendor's popover content, rendered outside the tree the form cascaded into, so the menu
    /// carries the two names across and a row reads them there exactly as an act reads them on
    /// the page.</summary>
    [Fact]
    public async Task AMenuThatSurvivesStillWithholdsTheRowsThatWrite()
    {
        var cut = Render<MenuFormHost>(p => p.Add(x => x.ReadOnly, true));

        var rows = await Open(cut, "Mixta");

        Assert.Contains("Ver el presupuesto", rows);
        Assert.DoesNotContain("Taller Caro", rows);
    }

    /// <summary>The toolbar drops what the form withholds before it counts (nsail#1321), so its
    /// own two menus are drawn over rows that already passed: a second filter on the face would
    /// take the contribution and the overflow away from a form that still has both to offer.</summary>
    [Fact]
    public async Task TheToolbarKeepsTheMenusItAlreadyFiltered()
    {
        var cut = Render<MenuFormHost>(p => p.Add(x => x.ReadOnly, true));

        // Asignar writes, so the toolbar never renders it; Presupuestos declared it does not
        // and stands beside the kebab.
        Assert.Equal(2, Menus(cut).Count);
        Assert.DoesNotContain("Asignar", cut.Markup, StringComparison.Ordinal);

        Assert.Contains("Ver el presupuesto", await Open(cut, "Presupuestos"));
    }

    [Fact]
    public async Task TheOverflowStillOpensOnWhatSurvivedTheForm()
    {
        var cut = Render<MenuFormHost>(p => p.Add(x => x.ReadOnly, true));

        var kebab = Menus(cut)[^1].QuerySelector("button")!;

        await kebab.ClickAsync(new MouseEventArgs());

        Assert.Contains("Imprimir", Rows(cut));
        Assert.DoesNotContain("Archivar", Rows(cut));
    }

    /// <summary>A form is disabled for the length of every submit, so the menu does not wait for
    /// a screen that mounts ReadOnly to be taken away and put back — it answers a cascade that
    /// moves under a mounted component.</summary>
    [Fact]
    public void AFormThatTurnsReadOnlyTakesTheMenuAndGivesItBack()
    {
        var cut = Render<MenuFormHost>();

        cut.Render(p => p.Add(x => x.ReadOnly, true));

        Assert.DoesNotContain("Comparar", cut.Markup, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.ReadOnly, false));

        Assert.Contains("Comparar", cut.Markup, StringComparison.Ordinal);
    }
}

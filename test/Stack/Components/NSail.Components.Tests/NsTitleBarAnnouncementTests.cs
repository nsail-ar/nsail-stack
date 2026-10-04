// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#38 handed the main surface's title to the app bar; nsail#517 took the app bar
/// away from every width the drawer docks at, and nsail#561 took it away entirely: the page's own
/// title bar is the one that draws the screen — glyph slot, name and utilities — at every width,
/// it carries the hidden drawer's toggle, and the announcement still travels because the surface
/// is what tells anything else what screen is on. Acts stay at the foot of the page; the X belongs to an overlay alone, which keeps
/// its own chrome untouched because it has no frame to hand anything to.</summary>
public sealed class NsTitleBarAnnouncementTests : BunitContext, IAsyncLifetime
{
    static readonly Glyph PageGlyph = NsIcons.Badge;

    // The overlay's X is a MudTooltip, which pulls MudBlazor's popover service into the
    // container, and that one only implements IAsyncDisposable — a synchronous teardown
    // throws on it by design. Async teardown sidesteps it instead of working around it.
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
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

    SurfaceContext Setup(Surface? name = null)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Loading"] = "Cargando",
            ["Common.Close"] = "Cerrar",
            ["Common.Enabled"] = "Habilitado"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(NsTitleBarAnnouncementTests).Assembly, []));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        return new SurfaceContext(name, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    static RenderFragment Utility()
    {
        return builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "probe-utility");
            builder.AddContent(2, "Imprimir");
            builder.CloseElement();
        };
    }

    [Fact]
    public void OnTheMainSurfaceThePagesOwnBarNamesItAndStillAnnouncesIt()
    {
        var surface = Setup();

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Personas")
            .Add(x => x.Icon, PageGlyph));

        var bar = cut.FindComponent<NsTitleBar>();

        // The page's own row draws the name at EVERY width: with no app bar in the shell
        // (nsail#561) there is nowhere else for a screen to be named.
        Assert.Contains("Personas", bar.Markup, StringComparison.Ordinal);

        var announcement = bar.Find(".mud-typography-h6").ParentElement!.ParentElement!;

        Assert.DoesNotContain("d-none", announcement.ClassList);
        Assert.Contains("d-flex", announcement.ClassList);

        // Nothing to close on the surface the app is already at.
        Assert.Empty(bar.FindComponents<NsClose>());

        // And the announcement still travels: the surface is what carries a screen's name to
        // whatever else asks for it.
        Assert.Equal("Personas", surface.Title);
        Assert.Equal(PageGlyph, surface.Icon);
    }

    [Fact]
    public async Task TheHiddenDrawersToggleRidesThePagesOwnTitleRow()
    {
        var surface = Setup();
        var state = new NsLayoutState();

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, state)
            .Add(x => x.Title, "Personas"));

        var bar = cut.FindComponent<NsTitleBar>();

        // The one thing the vanished bar owned that no page can own: the way back to a drawer
        // that is behind a hamburger. It exists only at the widths the drawer is not docked at.
        var toggle = bar.Find("button");

        Assert.Contains("d-md-none", toggle.ParentElement!.ClassList);

        await cut.InvokeAsync(() => toggle.Click());

        Assert.True(state.DrawerIsOpen);
    }

    /// <summary>nsail#1460: this row is drawn inside whatever the page wrapped its panel in, and
    /// that is usually an NsForm — whose cascade MudBaseButton reads for itself. The drawer is
    /// the shell's: reaching the menu writes nothing, so a save the hamburger has no part in must
    /// not take the way to it away.</summary>
    [Fact]
    public async Task TheDrawersToggleKeepsWorkingWhileTheFormAroundItSaves()
    {
        var surface = Setup();
        var state = new NsLayoutState();
        var running = new TaskCompletionSource();

        var cut = Render<TitleRowInFormHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, state)
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Held, running.Task));

        var bar = cut.FindComponent<NsTitleBar>();

        // Discarded on purpose: the submit's own Task only completes when `running` is released
        // below, so awaiting the dispatch here would deadlock — the WaitForAssertion that
        // follows is the wait that stands in for it (NsLoadTests' note).
        _ = cut.InvokeAsync(() => cut.Find("form").Submit());

        cut.WaitForAssertion(() => Assert.True(cut.Find("button.hero").HasAttribute("disabled")));

        var toggle = bar.Find("button");

        Assert.False(toggle.HasAttribute("disabled"));

        await cut.InvokeAsync(() => toggle.Click());

        Assert.True(state.DrawerIsOpen);

        running.SetResult();

        cut.WaitForAssertion(() => Assert.False(cut.Find("button.hero").HasAttribute("disabled")));
    }

    /// <summary>nsail#1910: Contraseña drew a title row in each of its two panels and a phone
    /// showed two hamburgers. The way back to a hidden drawer is the SURFACE's, so the screen
    /// offers it once however many rows it draws — the first row to ask holds the claim.</summary>
    [Fact]
    public void TwoTitleRowsOnOneScreenShowOneHamburgerBetweenThem()
    {
        var surface = Setup();
        var state = new NsLayoutState();

        var cut = Render<TwoTitleRowsHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, state));

        var rows = cut.FindComponents<NsTitleBar>();

        Assert.Equal(2, rows.Count);

        // One across the whole screen, and it is the row that names the screen — the second
        // panel is a section of it, not a screen of its own.
        var toggle = Assert.Single(cut.FindAll("button"));

        Assert.Contains("d-md-none", toggle.ParentElement!.ClassList);
        Assert.Contains("Contraseña", rows[0].Markup, StringComparison.Ordinal);
        Assert.Empty(rows[1].FindAll("button"));
    }

    /// <summary>The main surface outlives every page on it, so the row that held the claim hands
    /// it back when it goes: a claim left standing would be a phone with no way to the drawer on
    /// every screen reached after this one.</summary>
    [Fact]
    public void TheToggleIsHandedBackWhenTheRowHoldingItLeaves()
    {
        var surface = Setup();
        var state = new NsLayoutState();

        var cut = Render<TwoTitleRowsHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, state));

        cut.Render(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, state)
            .Add(x => x.Subject, false));

        // The release lands on the departing row's disposal, which Blazor runs AFTER the render
        // that removed it — so the row left standing takes the claim on the render the release
        // asks for, not on that one.
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button")));
    }

    /// <summary>A screen whose step replaces its panel draws its second row before the first one
    /// is disposed, so the claim is refused on that render and taken on the one the release asks
    /// for. The toggle arrives with the step either way: a step that lost it would be a phone
    /// stuck on the screen it is on.</summary>
    [Fact]
    public async Task TheToggleArrivesWithTheStepThatReplacesTheRowCarryingIt()
    {
        var surface = Setup();
        var state = new NsLayoutState();

        var cut = Render<SteppedTitleRowHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, state));

        Assert.Single(cut.FindAll("button"));

        cut.Render(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.LayoutState, state)
            .Add(x => x.SecondStep, true));

        cut.WaitForAssertion(() => Assert.Contains("Escribir el código", cut.Markup, StringComparison.Ordinal));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button")));

        // And it is the drawer's own, not a leftover: the row that holds it now is the step's.
        await cut.InvokeAsync(() => cut.Find("button").Click());

        Assert.True(state.DrawerIsOpen);
    }

    [Fact]
    public void OutsideAShellThereIsNoToggleToDraw()
    {
        var surface = Setup();

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Personas"));

        Assert.Empty(cut.FindComponent<NsTitleBar>().FindAll("button"));
    }

    [Fact]
    public void TheUtilitiesAreAnnouncedAndDrawnByThePagesOwnBar()
    {
        var surface = Setup();

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Orden de Trabajo")
            .Add(x => x.Utilities, Utility()));

        // Announced all the same — the fragment travels whether or not the bar below md has a
        // row to put it on — but DRAWN by the page's own bar, at every width, which is what
        // keeps a utility reachable on a phone now that the top bar carries the hamburger and
        // the announcement and nothing else (nsail#517).
        Assert.NotNull(surface.Utilities);

        var button = cut.Find(".probe-utility");

        Assert.Equal("Imprimir", button.TextContent);
        Assert.Contains("probe-utility", cut.FindComponent<NsTitleBar>().Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("probe-utility", cut.FindComponent<NsAppBar>().Markup, StringComparison.Ordinal);
    }

    /// <summary>nsail#200, Leonardo 2026-08-14: "movamos todos los enables al form como un campo
    /// normal." The record's liveness used to be the one piece the bar could not hand over, so it
    /// rendered alone on the main surface — ruled a defect: standing alone above the panel is
    /// chrome, not a field. NsTitleBar carries no Enabled concept anymore (the parameter is gone,
    /// so nothing can bind one back), and NsRecordState renders as an ordinary NsFieldBase, at
    /// home in whichever NsFormGrid an editor places it.</summary>
    [Fact]
    public void TheBarCarriesNoRecordStateAndTheFieldStandsOnItsOwn()
    {
        var surface = Setup();

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Editar Rubro"));

        var bar = cut.FindComponent<NsTitleBar>();

        Assert.Empty(bar.FindComponents<NsRecordState>());
        Assert.Equal("Editar Rubro", surface.Title);

        var field = Render<NsRecordState>(p => p.Add(x => x.Value, true));

        Assert.Contains("Habilitado", field.Markup, StringComparison.Ordinal);
        Assert.Contains("ns-record-state", field.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOverlayKeepsItsOwnChromeAndAnnouncesNothing()
    {
        var surface = Setup(Surfaces.Aside);

        var cut = Render<TitleBarSurfaceHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Nueva Persona")
            .Add(x => x.Icon, PageGlyph)
            .Add(x => x.Utilities, Utility()));

        var bar = cut.FindComponent<NsTitleBar>();

        Assert.Contains("Nueva Persona", bar.Markup, StringComparison.Ordinal);
        Assert.Contains("probe-utility", bar.Markup, StringComparison.Ordinal);

        // The way out an overlay owes the user, still drawn by the bar itself.
        Assert.NotEmpty(cut.FindComponent<NsClose>().Markup.Trim());

        Assert.Null(surface.Title);
        Assert.Null(surface.Icon);
        Assert.Null(surface.Utilities);
    }

    /// <summary>The frame redraws when the page announces, and only the frame: an announcement
    /// that raised StateChanged would re-render the page that made it (NsPage subscribes), whose
    /// render announces again.</summary>
    [Fact]
    public void AnnouncingNotifiesTheFrameWithoutWakingThePage()
    {
        var surface = Setup();
        var announcements = 0;
        var states = 0;

        surface.AnnouncementChanged += () => announcements++;
        surface.StateChanged += () => states++;

        surface.Announce("Personas", PageGlyph, null);
        surface.Announce("Personas", PageGlyph, null);

        Assert.Equal(1, announcements);
        Assert.Equal(0, states);
    }
}

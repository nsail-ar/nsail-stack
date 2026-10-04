// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Leonardo's rule, verbatim: the unsaved-changes guard fires ONLY when the form's
/// own surface actually leaves — never because an aside or popup opens on top of it. The
/// OT create form opening "cargar receta" in an aside was being asked to confirm losing a
/// document it was not leaving.
///
/// The other half of the same sentence: a named surface DOES leave when its own query key
/// is pointed at another route (modal=a/1 → modal=a/2), so the protest is owed there too —
/// a create opened from inside a create is asked, never refused. The replace is legal
/// navigation and nothing is forbidden; the guard is what stands between it and silence.</summary>
public sealed class UnsavedChangesGuardTests : BunitContext, IAsyncLifetime
{
    readonly CountingDialogManager _dialogs = new();

    public UnsavedChangesGuardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton(BuildRouteTable());
        Services.AddScoped<DialogManager>(_ => _dialogs);

        AddAuthorization().SetAuthorized("probe");
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(UnsavedChangesGuardTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    async Task<IRenderedComponent<CheckBoxTrackingHost>> RenderDirtyForm()
    {
        var host = Render<CheckBoxTrackingHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Model, new CheckBoxTrackingModel()));

        // A real edit through a real DOM event, so the surface is dirty the way a person
        // makes it dirty.
        await host.InvokeAsync(() => host.Find("input[type=checkbox]").Change(true));

        Assert.True(host.Instance.Surface!.HasChanges);

        return host;
    }

    [Fact]
    public async Task OpeningAnAsideOverADirtyForm_NeverAsksAboutUnsavedChanges()
    {
        var host = await RenderDirtyForm();
        var navigation = Services.GetRequiredService<NavigationManager>();

        await host.InvokeAsync(() => host.Instance.Surface!.Open(Surfaces.Aside, "optical/prescriptions/new"));

        Assert.Equal(0, _dialogs.Confirms);
        Assert.Contains("aside=", navigation.Uri);
    }

    [Fact]
    public async Task LeavingThePageWithADirtyForm_AsksAboutUnsavedChanges()
    {
        var host = await RenderDirtyForm();
        var navigation = Services.GetRequiredService<NavigationManager>();

        await host.InvokeAsync(() => navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(1, _dialogs.Confirms);
    }

    // A create page on the main surface, ending its submit the way every one of them does: it
    // navigates to what it just wrote (create-thin-then-enrich). ReportsWhileSaving is the echo —
    // an editor that reports the SURFACE re-entered by a render the handler's own publishes
    // provoked, from inside the window that navigation lives in.
    IRenderedComponent<SurfaceFormHost> RenderCreatePage(
        Action<ComponentParameterCollectionBuilder<SurfaceFormHost>>? extra = null)
    {
        Navigation.NavigateTo("/optical/sales/new");

        return Render<SurfaceFormHost>(p =>
        {
            p.Add(x => x.RouteTable, BuildRouteTable());
            p.Add(x => x.Model, new SubmitTrackingModel());
            p.Add(x => x.NavigatesTo, "sales/counter-sales/7");
            extra?.Invoke(p);
        });
    }

    /// <summary>nsail#1450: the save's own echo re-armed the guard, and the guard answered the
    /// navigation the save itself caused — the operator asked to confirm abandoning a document
    /// already on disk. The spend before the handler cannot cover it: the report is filed while
    /// the handler runs, and the handler is where the navigation is.</summary>
    [Fact]
    public async Task ASaveWhoseOwnEchoReportsTheSurface_AsksNothingAndLandsOnWhatItWrote()
    {
        var host = RenderCreatePage(p => p.Add(x => x.ReportsWhileSaving, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.EndsWith("/sales/counter-sales/7", Navigation.Uri, StringComparison.Ordinal);
        Assert.False(host.Instance.Surface!.HasChanges);
    }

    /// <summary>nsail#1471: the echo can be filed EARLIER than the handler — by the render the
    /// spend's own answer provokes. The surface going clean is a StateChanged, NsPage turns that
    /// into a StateHasChanged, and an editor re-entered by that render reports itself while the
    /// submit is still two statements from opening its window: nothing held it and nothing would
    /// ever spend it, so the guard answered the navigation the handler ends with. The window opens
    /// BEFORE the spend now, so the spend's own answer lands inside it.</summary>
    [Fact]
    public async Task ASaveWhoseEchoTheSpendItselfProvoked_AsksNothingAndLandsOnWhatItWrote()
    {
        var host = RenderCreatePage(p => p.Add(x => x.ReportsOnTheSpend, true));

        // Something for the spend to spend: the surface going clean is what announces itself, and
        // a surface that was never dirty announces nothing.
        host.Instance.Probe!.MarkDirty();

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.EndsWith("/sales/counter-sales/7", Navigation.Uri, StringComparison.Ordinal);
        Assert.False(host.Instance.Surface!.HasChanges);
    }

    /// <summary>And the guard's question no longer depends on that ordering at all: a report
    /// standing on the surface when a window opened is still the report of a save that is about to
    /// navigate, so it protests nothing that save does. Driven at the seam because no pair of
    /// gestures can file one there — which is the point: the exit guard must not be the place a
    /// missed ordering surfaces (nsail#1471).</summary>
    [Fact]
    public async Task AReportStandingWhenASavesWindowOpened_ProtestsNothingThatSaveDoes()
    {
        var host = RenderCreatePage();
        var surface = host.Instance.Surface!;

        host.Instance.Probe!.MarkDirty();

        var window = surface.BeginSave(host.Instance.Form!);

        await host.InvokeAsync(() => surface.Navigate("sales/counter-sales/7"));

        Assert.Equal(0, _dialogs.Confirms);
        Assert.EndsWith("/sales/counter-sales/7", Navigation.Uri, StringComparison.Ordinal);

        // And the save gone, the report is somebody's unsaved work again: the window answered for
        // it, it did not spend it.
        surface.EndSave(window);

        await host.InvokeAsync(() => Navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(1, _dialogs.Confirms);
    }

    /// <summary>And the half that costs: a save answers for its OWN reports, never for the second
    /// document's. A person typing into the settings page's other form while this one saves is
    /// still asked before the navigation this handler makes carries their edit off the screen —
    /// the report is nothing this submit spent, held or caused (SurfaceContext.Spendable).</summary>
    [Fact]
    public async Task ASaveThatNavigatesWhileASiblingFormHoldsAnEdit_StillAsksAboutIt()
    {
        var host = RenderCreatePage(p => p
            .Add(x => x.Sibling, true)
            .Add(x => x.SiblingReportsWhileSaving, true));

        await host.InvokeAsync(() => host.FindAll("form")[0].Submit());

        Assert.Equal(1, _dialogs.Confirms);
    }

    /// <summary>The half the fix above must not buy: a refused save saved nothing, so what the
    /// window held goes back and leaving the screen still asks. An echo is dropped only by a save
    /// that took.</summary>
    [Fact]
    public async Task LeavingThePageAfterARefusedSaveThatEchoed_StillAsksAboutUnsavedChanges()
    {
        var host = RenderCreatePage(p => p
            .Add(x => x.ReportsWhileSaving, true)
            .Add(x => x.Aborts, true));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => Navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(1, _dialogs.Confirms);
    }

    /// <summary>And the save's window outlives a sibling's submit landing inside it: a settings
    /// page's second Guardar, or the untracked Probar beside it — nothing disables either while
    /// the first form saves, only that form's own fields freeze. The echo this save files AFTER
    /// the sibling finished is still held, so the navigation the handler ends with is not
    /// protested; a window a sibling could close puts nsail#1450 back for the rest of the
    /// handler.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASaveWhoseSiblingSubmittedInsideIt_AsksNothingAboutItsOwnLaterEcho(bool untracked)
    {
        var host = RenderCreatePage(p => p
            .Add(x => x.Sibling, true)
            .Add(x => x.SiblingUntracked, untracked)
            .Add(x => x.WhileSaving, async self =>
            {
                await self.SiblingForm!.Submit();

                self.Probe!.MarkDirty();
            }));

        await host.InvokeAsync(() => host.FindAll("form")[0].Submit());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.EndsWith("/sales/counter-sales/7", Navigation.Uri, StringComparison.Ordinal);
        Assert.False(host.Instance.Surface!.HasChanges);
    }

    /// <summary>The second gesture on the SAME document, which nothing on screen prevents:
    /// CreateOpticalSalePage draws Presupuestar as a plain button beside Continuar and both submit
    /// the one form, so a press while the first save is in flight re-enters HandleSubmit — which
    /// answers it with the save already running: refused, sending nothing, and opening no window
    /// of its own. The echo this save files afterwards is still held; a window the second press
    /// could close puts nsail#1450 back for the rest of the handler.</summary>
    [Fact]
    public async Task ASaveItsOwnButtonWasPressedAgainInside_AsksNothingAboutItsOwnLaterEcho()
    {
        var host = RenderCreatePage(p => p
            .Add(x => x.WhileSaving, async self =>
            {
                Assert.False(await self.Form!.Submit());

                self.Probe!.MarkDirty();
            }));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.EndsWith("/sales/counter-sales/7", Navigation.Uri, StringComparison.Ordinal);
        Assert.False(host.Instance.Surface!.HasChanges);
    }

    /// <summary>A refusal inside another save's window owes its report back, and that other save
    /// is not the one that may drop it — it neither spent nor caused it. It reaches the surface
    /// without arming the guard against the departure the outer handler is about to make (the
    /// outer's window inherits it and answers at its own close), and the outer's success leaves it
    /// standing: the work the inner form refused is still guarded on the way out.</summary>
    [Fact]
    public async Task ARefusalInsideASaveThatTook_LeavesTheWorkItRefusedGuardedAndProtestsNoNavigation()
    {
        var host = RenderCreatePage(p => p
            .Add(x => x.Sibling, true)
            .Add(x => x.WhileSaving, async self => await self.SiblingForm!.Submit())
            .Add(x => x.WhileSiblingSaving, (self, args) =>
            {
                self.Probe!.MarkDirty();
                args.Abort();

                return Task.CompletedTask;
            }));

        await host.InvokeAsync(() => host.FindAll("form")[0].Submit());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.EndsWith("/sales/counter-sales/7", Navigation.Uri, StringComparison.Ordinal);
        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => Navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(1, _dialogs.Confirms);
    }

    /// <summary>And the report nobody saved at all — the rows a collection editor holds, which no
    /// EditContext sees — still guards the way out. This is the door the echo drop is narrowest
    /// against: the same source, the same call, told outside a submit.</summary>
    [Fact]
    public async Task LeavingThePageWithAnEditorsUnsavedRows_AsksAboutUnsavedChanges()
    {
        var host = RenderCreatePage();

        await host.InvokeAsync(() => host.Instance.Probe!.MarkDirty());

        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => Navigation.NavigateTo("/somewhere/else"));

        Assert.Equal(1, _dialogs.Confirms);
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    static string At(string surface, string route)
    {
        return $"/probe?{surface}={Uri.EscapeDataString(route)}";
    }

    // The whole tree the replace happens in — the real router, the layout that declares both
    // named surfaces, and a routed page hosted inside one of them. The surface under test is
    // dirtied through a real DOM event on the page the surface actually mounted, so what the
    // replace is about to discard is a document the machinery itself is holding.
    async Task<IRenderedComponent<NsRouter>> RenderDirtySurface(string surface)
    {
        Navigation.NavigateTo(At(surface, "probe/form"));

        var app = Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(ProbeFormPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));

        await app.InvokeAsync(() => app.Find("input[type=text]").Change("typed by hand"));

        return app;
    }

    [Theory]
    [InlineData("aside")]
    [InlineData("modal")]
    public async Task ReplacingTheRouteOfADirtySurface_AsksAboutUnsavedChanges(string surface)
    {
        var app = await RenderDirtySurface(surface);

        await app.InvokeAsync(() => Navigation.NavigateTo(At(surface, "probe/other")));

        Assert.Equal(1, _dialogs.Confirms);
    }

    [Theory]
    [InlineData("aside")]
    [InlineData("modal")]
    public async Task ReplacingTheRouteOfACleanSurface_AsksNothing(string surface)
    {
        Navigation.NavigateTo(At(surface, "probe/form"));

        var app = Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(ProbeFormPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(ProbeLayout)));

        await app.InvokeAsync(() => Navigation.NavigateTo(At(surface, "probe/other")));

        Assert.Equal(0, _dialogs.Confirms);
    }

    /// <summary>The probe the story asks for: declining keeps the surface AND what was typed
    /// into it. The value is read back off the DOM rather than off the model, because the
    /// question is whether the hosted page was torn down — a fresh mount would render the
    /// page's own empty model.</summary>
    [Theory]
    [InlineData("aside")]
    [InlineData("modal")]
    public async Task DecliningTheProtest_KeepsTheSurfaceAndWhatWasTypedIntoIt(string surface)
    {
        _dialogs.Answer = false;

        var app = await RenderDirtySurface(surface);

        await app.InvokeAsync(() => Navigation.NavigateTo(At(surface, "probe/other")));

        Assert.Equal(At(surface, "probe/form"), Navigation.Uri[Navigation.BaseUri.TrimEnd('/').Length..]);
        Assert.Equal("typed by hand", app.Find("input[type=text]").GetAttribute("value"));
    }

    [Theory]
    [InlineData("aside")]
    [InlineData("modal")]
    public async Task AcceptingTheProtest_ReplacesTheRoute(string surface)
    {
        var app = await RenderDirtySurface(surface);

        await app.InvokeAsync(() => Navigation.NavigateTo(At(surface, "probe/other")));

        Assert.Equal(At(surface, "probe/other"), Navigation.Uri[Navigation.BaseUri.TrimEnd('/').Length..]);
        Assert.Empty(app.FindAll("input[type=text]"));
    }

    /// <summary>The guard on the door the pop-close comes through, which is the same door by a
    /// different route: closing a surface whose open pushed spends that entry with history.back,
    /// Blazor restores the history position before it asks the handlers and leaves it restored
    /// when one refuses — so declining here keeps the surface, and the X over it has to keep
    /// working. Close() suppresses a second pop while the first is in flight (one gesture, one
    /// entry) and a refusal raises no LocationChanged to end that flight, so the refusal itself
    /// is what ends it; without that, the second click is a dead X.
    ///
    /// The popstate is simulated by navigating to the popped address, because bUnit raises no
    /// browser history events — what is real here is the tree: the surface's own
    /// NsSurfaceContext, its NavigationLock, and the refusal that reaches it.</summary>
    [Fact]
    public async Task DecliningTheProtestOnAPopClose_LeavesTheSurfaceClosable()
    {
        _dialogs.Answer = false;

        Navigation.NavigateTo(At("aside", "probe/form"));

        // What the open would have written from the page underneath: this aside is standing on
        // an entry of its own, so its close pops instead of rewriting the address.
        Services.GetRequiredService<SurfaceHistory>().Opened(Surfaces.Aside, pushed: true);

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.Name, Surfaces.Aside)
            .Add(x => x.Model, new SubmitTrackingModel()));

        await host.InvokeAsync(() => host.Find("input[type=text]").Change("typed by hand"));

        Assert.True(host.Instance.Surface!.HasChanges);

        await host.InvokeAsync(() => host.Instance.Surface!.Close());

        Assert.Single(JSInterop.Invocations["history.back"]);

        await host.InvokeAsync(() => Navigation.NavigateTo("/probe"));

        Assert.Equal(1, _dialogs.Confirms);
        Assert.Equal(At("aside", "probe/form"), Navigation.Uri[Navigation.BaseUri.TrimEnd('/').Length..]);

        await host.InvokeAsync(() => host.Instance.Surface!.Close());

        Assert.Equal(2, JSInterop.Invocations["history.back"].Count);
    }

    // The other half of the law, ruled 2026-08-26: identity is the PATH of the route a surface
    // is routed by, and its query is filter or component state. A page writing its own query —
    // a tab, a search — has not moved, so neither the guard nor the surface's key may read that
    // write as the surface leaving.

    /// <summary>The whole loop as a person drives it: the click switches the tab, the strip
    /// writes the query through the surface, and the navigation that comes back must ask
    /// nothing, keep the page mounted with what was typed in it, and land on the other
    /// panel. Both halves are pinned in one row on purpose — a green "asks nothing" that
    /// stopped switching tabs is not the fix.</summary>
    [Theory]
    [InlineData("aside")]
    [InlineData("modal")]
    public async Task SwitchingATabInsideADirtySurface_AsksNothingAndKeepsThePageMounted(string surface)
    {
        var app = await RenderDirtySurface(surface);

        await app.InvokeAsync(() => app.FindAll(".mud-tabs-tabbar .mud-tab")[1].Click());

        Assert.Equal(0, _dialogs.Confirms);
        Assert.Equal(At(surface, "probe/form?tab=other"), Navigation.Uri[Navigation.BaseUri.TrimEnd('/').Length..]);
        Assert.Equal("typed by hand", app.Find("input[type=text]").GetAttribute("value"));
        Assert.Equal(1, ShownPanel(app));
    }

    /// <summary>And the same silence for any other write onto a named surface's own query — a
    /// filter, a search — which reaches the guard as a bare navigation with no strip
    /// behind it.</summary>
    [Theory]
    [InlineData("aside")]
    [InlineData("modal")]
    public async Task WritingAFilterOntoADirtySurfacesOwnQuery_AsksNothingAndKeepsThePageMounted(string surface)
    {
        var app = await RenderDirtySurface(surface);

        await app.InvokeAsync(() => Navigation.NavigateTo(At(surface, "probe/form?search=ana")));

        Assert.Equal(0, _dialogs.Confirms);
        Assert.Equal("typed by hand", app.Find("input[type=text]").GetAttribute("value"));
    }

    /// <summary>nsail#395 unmoved: a key pointed at another route still changes the path, so the
    /// protest is still owed — including when the route it leaves carries a query of its
    /// own.</summary>
    [Theory]
    [InlineData("aside")]
    [InlineData("modal")]
    public async Task ReplacingTheRouteOfADirtySurfaceThatCarriesAQuery_StillAsks(string surface)
    {
        var app = await RenderDirtySurface(surface);

        await app.InvokeAsync(() => app.FindAll(".mud-tabs-tabbar .mud-tab")[1].Click());
        await app.InvokeAsync(() => Navigation.NavigateTo(At(surface, "probe/other?tab=other")));

        Assert.Equal(1, _dialogs.Confirms);
    }

    // Which panel the vendor is actually painting: every panel is mounted (KeepPanelsAlive) and
    // the one showing is the one carrying .mud-tab-panel-active, the same class NsTabsQueryTests
    // reads.
    static int ShownPanel(IRenderedComponent<NsRouter> app)
    {
        var panels = app.FindAll(".mud-tabs-panels > .mud-tab-panel");

        for (var index = 0; index < panels.Count; index++)
        {
            if (panels[index].ClassList.Contains("mud-tab-panel-active"))
            {
                return index;
            }
        }

        return -1;
    }
}

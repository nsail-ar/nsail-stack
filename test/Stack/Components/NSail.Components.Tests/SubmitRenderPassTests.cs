// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1987. A save taken in an aside blocks the browser about two seconds drawing,
/// and the two `/api/` calls inside that window account for well under a fifth of it — so what
/// the window costs is render passes nobody had counted. This is the count, and the gate.
///
/// A count and never a duration. Milliseconds are not assertable here — the box swings 4-8x on
/// identical work, and nsail#1934's own before/after pair removed a whole round trip and
/// measured a HIGHER blocked total — so the box-independent half is the only one that carries
/// (PageGateTests is the same bargain on the same window).
///
/// Through the shipped composition, because the fact is about who hears one announcement: the
/// real router, the shell's own slots (PassLayout), a routed list on the main surface and a
/// routed create screen inside the aside's NsDrawer. The readers of the aside's surface are all
/// four of them in a tree like this one — its own NsSurfaceContext, the page it hosts, the
/// drawer and the submit — and a fixture that leaves any of them out counts a save nobody takes.
///
/// One Guardar announces its surface three times: the document going clean (ClearChanges), the
/// work starting (Enter) and the work ending (Exit). What each of them is allowed to redraw is
/// what the facts below pin, and the counts are read at the step the submit's own handler has
/// finished — the teardown after it is the vendor's drawer leaving its container, and pinning its
/// churn would be pinning MudBlazor's version. The one cut that only shows after that step is
/// taken on a standing aside instead, on the same announcement from the quiet side.</summary>
public sealed class SubmitRenderPassTests : BunitContext, IAsyncLifetime
{
    readonly RenderPasses _passes = new();

    public SubmitRenderPassTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton(_passes);

        // NsDrawer's IsDocked asks the viewport, and MudBlazor's own service is JS-backed: the
        // fake answers a wide breakpoint synchronously, so the drawer is docked the way a
        // desktop renders it (NsDrawerXlTests, SurfaceStackLayeringTests).
        Services.AddScoped<IBrowserViewportService>(_ => new FixedBreakpointViewportService(Breakpoint.Lg));

        // NsPage resolves its RouteTable from DI, and the one AddRouteTable builds is the entry
        // assembly's — a test host's, which carries none of these fixtures' routes.
        Services.AddSingleton(new RouteTable(typeof(PassListPage).Assembly, []));

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

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    /// <summary>The list on the main surface with the create screen open over it in the aside —
    /// the address a person is at when they press Guardar.</summary>
    IRenderedComponent<NsRouter> RenderSaveWindow()
    {
        Navigation.NavigateTo($"/pass?aside={Uri.EscapeDataString("pass/new")}");

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(PassListPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(PassLayout)));
    }

    /// <summary>Everything a person does before the click: one field typed, and the editor
    /// beside the fields holding a row of its own. Both are what a real document arrives at the
    /// submit with, and both are reports the submit has to spend.</summary>
    async Task<IRenderedComponent<NsRouter>> Saved()
    {
        var app = RenderSaveWindow();

        await app.InvokeAsync(() => app.Find("input[type=text]").Input("Marisa"));
        await app.InvokeAsync(() => app.FindComponent<PassCreatePage>().Instance.Probe!.MarkDirty());

        _passes.Reset();

        await app.Find("form").SubmitAsync();

        return app;
    }

    /// <summary>What the submit's own handler had cost by the time it returned — read there and
    /// not at the end, so the vendor's own teardown churn is outside every count below.</summary>
    RenderPasses.Step Submitted()
    {
        return _passes.Steps.Last(step => step.Name == "listed");
    }

    /// <summary>Three passes for the page in the aside, where it used to take five — and not one
    /// per announcement: the two the submit makes before its first await (the document going
    /// clean and the work starting) land in the same batch and cost ONE pass, and the other two
    /// fall after the handler returned, the page drawn first with the save in flight and then
    /// with the work ended. The pair that is gone is the one the form's own refusal report
    /// provoked: it reports from the after-render of the pass that decided it, and that report
    /// came through StateChanged, so a render provoked a render to say a word only a dialog's X
    /// reads.</summary>
    [Fact]
    public async Task TheSave_RedrawsThePageInTheAsideOnceForEachStateItIsDrawnWith()
    {
        await Saved();

        Assert.Equal(3, _passes.Count("create"));
    }

    /// <summary>The aside's own chrome — the surface host above the drawer, the drawer and the
    /// container between them — redraws for the one announcement it reads and not for the other
    /// two. The host's render reads HasChanges (the NavigationLock) and the drawer's reads the
    /// width its size translates to and whether it docks: a save starting and a save ending move
    /// none of them, and redrawing for them rebuilt the whole hosted tree to emit what was
    /// already on screen.</summary>
    [Fact]
    public async Task TheSave_RedrawsTheAsidesChromeOnlyForTheAnnouncementItReads()
    {
        await Saved();

        Assert.Equal(1, Submitted().Count("aside"));
    }

    /// <summary>And the shell the aside stands over is not redrawn at all. The drawer poked its
    /// container on every announcement — and that container is the LAYOUT, so the poke reached
    /// the nav, the bar and the content region behind the aside for a surface none of them is
    /// on.</summary>
    [Fact]
    public async Task TheSave_RedrawsNoneOfTheShellTheAsideStandsOver()
    {
        await Saved();

        Assert.Equal(0, Submitted().Count("shell"));
    }

    /// <summary>An announcement the surface host does not read redraws nothing it hosts. Taken
    /// on a standing aside and not inside the save, because the pass this one saves falls on the
    /// work ENDING — after the handler returned, among the vendor's teardown churn, where no
    /// count above can see it. Work STARTING is the same announcement from the host's side: it
    /// moves no HasChanges, which is the only thing this host's own markup reads off the surface
    /// (the NavigationLock). The page on the surface does redraw for it — it reads HasWork — so
    /// this is a count of a host that stayed still while the announcement reached its readers,
    /// not of an announcement nobody heard.</summary>
    [Fact]
    public async Task AnAnnouncementTheSurfaceHostDoesNotRead_RedrawsNothingItHosts()
    {
        var app = RenderSaveWindow();
        var aside = app.FindComponent<PassCreatePage>().Instance.Surface!;

        _passes.Reset();

        await app.InvokeAsync(aside.Enter);

        Assert.Equal(1, _passes.Count("create"));
        Assert.Equal(0, _passes.Count("host"));
    }

    /// <summary>The page behind keeps the one pass it has a reason for: the event the handler
    /// publishes, which is what reloads the list. It is the floor here and not a cut — a save
    /// that did not redraw the list behind it would be showing a row that is not there.</summary>
    [Fact]
    public async Task TheSave_RedrawsThePageBehindTheAsideOnceForTheEventItPublishes()
    {
        await Saved();

        Assert.Equal(1, Submitted().Count("list"));
    }

    /// <summary>The window the counts above are of: a click that ends with the aside closed, so
    /// none of them is a count of a save that never took.</summary>
    [Fact]
    public async Task TheSave_ClosesTheAside()
    {
        await Saved();

        Assert.DoesNotContain("aside=", Navigation.Uri, StringComparison.Ordinal);
    }
}

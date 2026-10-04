// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Leonardo, 2026-08-11 screenshot (Nueva OT, Paciente): creating a Persona from "+
/// Crear Persona…" assigns the new row to the lookup (the PartySaved it already listens for)
/// and leaves the dropdown open, still showing the entry that was just clicked. His ruling:
/// the click closes the menu, no subtlety.
///
/// What actually closes it here is not the click itself. The entry is an NsLink, and NsLink
/// owns its click all the way to Follow(), stopping it from bubbling to any ancestor (its own
/// history-vs-surface guard) — nothing wrapping the entry ever sees that event. Closing on
/// mousedown was tried and measured wrong: MudAutocomplete unmounts the popover's whole
/// content the instant Open turns false, which can remove the very anchor the browser is
/// mid-click on before the click ever fires — a race that can cost the create flow its own
/// navigation. What NsAutocomplete reacts to instead is the one thing every one of those
/// clicks produces regardless of what it opens: Follow() takes the surface through
/// NavigationManager. A LocationChanged subscription closes the popover once it is open,
/// landing alongside the navigation rather than racing the DOM that produces it. The belt —
/// a Value arriving with no navigation of its own, the create flow's saved-event assignment
/// among them — is covered by the same OnParametersSet path a resolved value already takes.</summary>
public sealed class NsLookupCreateMenuClosesTests : BunitContext, IAsyncLifetime
{
    public NsLookupCreateMenuClosesTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IStringSource>(new FixedStrings(new Dictionary<string, string>
        {
            [Concept] = "Personas",
            ["Common.CreateNew"] = "Crear {0}…",
        }));
        Services.AddSingleton<StringCatalog>();
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsLookupCreateMenuClosesTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    static readonly string Concept = new MetadataProvider().KeyFor(typeof(SelectRef));

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (NsLookupCreateEntryTests'
    // own note, same fixture).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static readonly SelectRef[] Two =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alpha"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beta"),
    ];

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    // The create entry only intercepts its own click (NsLink's own guard, NsLinkHistoryTests)
    // when following it stays on the current PATH — same page, a surface query added, which is
    // what a create route targeting a surface resolves to. A route that lands on a different
    // path is a genuine departure, which Blazor's own interception handles and this story never
    // touches.
    async Task<IRenderedComponent<LookupCreateHost>> OpenOnCurrentPath(string createRoute)
    {
        Navigation.NavigateTo("/directory/parties/new");

        var cut = Render<LookupCreateHost>(p => p
            .Add(x => x.Items, Two)
            .Add(x => x.CreateRoute, createRoute)
            .Add(x => x.CreateTarget, Surfaces.Aside));

        // OpenOnFocus: the same state the create entry has to survive in every other test of
        // this lookup (NsLookupCreateEntryTests).
        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.Render();

        Assert.NotEmpty(cut.FindAll(".mud-popover-open"));

        return cut;
    }

    // The route alone: what the entry's own link resolves against the surface it stands on —
    // here the main one, which turns an Aside target into the ?aside= query below.
    const string CreateRoute = "/directory/parties/create";

    [Fact]
    public async Task ClickingTheCreateEntryStillOpensItsDestination()
    {
        var cut = await OpenOnCurrentPath(CreateRoute);

        await cut.InvokeAsync(() => cut.Find(".mud-autocomplete-after-items .ns-lookup-create a").Click());

        Assert.Contains("aside=directory%2Fparties%2Fcreate", Navigation.History.Last().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClickingTheCreateEntryClosesTheMenu()
    {
        var cut = await OpenOnCurrentPath(CreateRoute);

        await cut.InvokeAsync(() => cut.Find(".mud-autocomplete-after-items .ns-lookup-create a").Click());

        // WaitForAssertion, not a bare assert: what closes the popover is the LocationChanged
        // subscription reacting to Follow()'s navigation, and that render lands on its own
        // schedule rather than inside the click. Asserting immediately passes alone and fails
        // now and then when the whole solution runs and the thread pool is busy — measured
        // 2026-08-13, three failures in four full runs, always green in isolation. The wait is
        // the cure for a test that was racing its own subject; a sleep would only hide it.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")));
    }

    [Fact]
    public async Task AValueArrivingWithNoNavigationOfItsOwnClosesTheMenuToo()
    {
        var cut = await OpenOnCurrentPath(CreateRoute);

        // The belt: NsLookupBase's own TSaved subscription assigns Value once a create form
        // saves. This simulates a caller that reaches the same OnParametersSet path directly,
        // with no navigation for the LocationChanged cure above to react to.
        cut.Render(p => p.Add(x => x.Value, Two[0].Id));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")));
    }
}

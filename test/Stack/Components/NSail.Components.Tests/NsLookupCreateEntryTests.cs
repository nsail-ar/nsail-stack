// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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

/// <summary>The dropdown's create entry is the list's permanent last ITEM, and an item is
/// never smaller than the rows it follows (intentional-ui-cases-controls.md, "Why the create
/// entry restates its own geometry": "never smaller than the rows above it"). MudAutocomplete
/// renders an option through .mud-list-item — 16px gutters, 8px above and below, a body1
/// paragraph adding 4px each way — but renders the two seams the create entry lives in as bare divs padded 4px, with the
/// anchor inside inheriting the body's own smaller type. The size therefore cannot come from
/// where the entry is mounted; it is restated on the entry itself.
///
/// What is reachable from here is the HOOK, on both seams: the class the geometry hangs off
/// and the list class the seam's own padding is neutralized through. The pixels those classes
/// carry are CSS and are verified against the served stylesheet and by eye — a bUnit DOM has
/// no layout.</summary>
public sealed class NsLookupCreateEntryTests : BunitContext, IAsyncLifetime
{
    public NsLookupCreateEntryTests()
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
        Services.AddSingleton(new RouteTable(typeof(NsLookupCreateEntryTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // The concept key the entry composes its label from, asked of the same provider the
    // component asks rather than written out: a change to the naming convention moves both.
    static readonly string Concept = new MetadataProvider().KeyFor(typeof(SelectRef));

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (NsSelectTests' note).
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

    async Task<IRenderedComponent<LookupCreateHost>> Open(IEnumerable<SelectRef> items)
    {
        var cut = Render<LookupCreateHost>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.CreateRoute, "/directory/parties/create"));

        // OpenOnFocus: the list opens on focus with an empty box, which is the state a
        // non-Lazy lookup is browsed in and the one the create entry has to survive.
        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.Render();

        return cut;
    }

    [Fact]
    public async Task TheOpenListCarriesTheClassTheSeamsPaddingIsNeutralizedThrough()
    {
        var cut = await Open(Two);

        // The popover is a portal — a sibling of this component's own subtree — so ListClass
        // is the only handle a rule reaching the seam inside it can be scoped under. Without
        // it the vendor's own 4px seam padding stands and the entry keeps its small indent.
        Assert.Contains("ns-lookup-list", cut.Find(".mud-list").ClassName);
    }

    [Fact]
    public async Task TheCreateEntryCarriesTheSizingHookBesideRealResults()
    {
        var cut = await Open(Two);

        var entry = cut.Find(".mud-autocomplete-after-items .ns-lookup-create");

        Assert.Contains("Crear Personas…", entry.TextContent);

        // The rows it has to measure up to are right there in the same list: the vendor's
        // own item, whose geometry ns-lookup-create restates.
        Assert.Equal(2, cut.FindAll(".mud-list-item").Count);
    }

    [Fact]
    public async Task TheCreateEntryCarriesTheSameHookWhenNothingMatched()
    {
        var cut = await Open([]);

        var entry = cut.Find(".mud-autocomplete-no-items .ns-lookup-create");

        Assert.Contains("Crear Personas…", entry.TextContent);

        // Permanence is the older ruling (a create entry that only appears on "no results"
        // strands the user whose match is merely absent); what this adds is that the empty
        // state gets the same size as the populated one — two vendor seams, one presentation.
        Assert.Empty(cut.FindAll(".mud-list-item"));
    }

    /// <summary>The one open in the app that is not a place, and the reason NoHistory exists:
    /// the create entry opens a window whose whole job is to hand a value back to the form
    /// still standing underneath, which the save then closes for the user — an entry of its own
    /// would leave that finished window one Back away from the form it just fed. It is marked
    /// here, on NsAutocomplete's own link: no lookup wrapper passes it and NsPageLink does not
    /// forward it, so no lookup and no future lookup has to remember anything.</summary>
    [Fact]
    public async Task TheCreateEntryOpensItsSurfaceWithNoHistoryEntryOfItsOwn()
    {
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/optical/work-orders/new");

        var cut = Render<LookupCreateHost>(p => p
            .Add(x => x.Items, Two)
            .Add(x => x.CreateRoute, "/directory/parties/create")
            .Add(x => x.CreateTarget, Surfaces.Aside));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.Render();

        await cut.InvokeAsync(() => cut.Find(".ns-lookup-create a").Click());

        // bUnit's History is newest first, and a replacing write drops the entry it replaced.
        var write = navigation.History.First();

        Assert.Contains("aside=directory%2Fparties%2Fcreate", write.Uri, StringComparison.Ordinal);
        Assert.True(write.Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task NoCreateRouteLeavesNoEntryAtAll()
    {
        var cut = Render<LookupCreateHost>(p => p.Add(x => x.Items, Two));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.Render();

        Assert.Empty(cut.FindAll(".ns-lookup-create"));
    }
}

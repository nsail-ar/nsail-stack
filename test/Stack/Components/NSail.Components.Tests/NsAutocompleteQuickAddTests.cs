// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1184: the quick add offers the typed text as a row to make, and its own
/// contract says it is not offered when an option already reads that text — which only the
/// search for that text can answer. Offered while that search is still out, the entry quoted a
/// term nobody had looked up yet, and a click on it asked the field's Runner to start the alta
/// while the search still held it: "Runner is already running", no dialog, the receta left
/// waiting on an alta that never opened.</summary>
public sealed class NsAutocompleteQuickAddTests : BunitContext, IAsyncLifetime
{
    public NsAutocompleteQuickAddTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsAutocompleteQuickAddTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so
    // bUnit's synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static readonly SelectRef[] Rows =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Gómez"),
    ];

    static IEnumerable<AngleSharp.Dom.IElement> Entries(IRenderedComponent<SearchLookupHost> cut)
    {
        return cut.FindAll(".ns-lookup-create button");
    }

    [Fact]
    public async Task The_quick_add_waits_for_the_search_of_the_text_it_would_add()
    {
        var gates = new ConcurrentDictionary<string, TaskCompletionSource>();

        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Rows)
            .Add(x => x.Lazy, true)
            .Add(x => x.Gate, term => gates
                .GetOrAdd(term ?? string.Empty, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                .Task)
            .Add(x => x.OnQuickAdd, EventCallback.Factory.Create<QuickAddEventArgs<SelectRef>>(this, _ => { })));

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        // Not awaited: the search is deliberately left out, the round trip a loaded box pays.
        var typed = cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Zeta" });

        // The term reaching the sender is what says the field has already redrawn itself busy:
        // the Runner marks itself running, and redraws, before it raises the search.
        cut.WaitForAssertion(() => Assert.Equal("Zeta", cut.Instance.Asked[^1]), TimeSpan.FromSeconds(5));

        Assert.Empty(Entries(cut));

        gates["Zeta"].TrySetResult();

        cut.WaitForAssertion(() => Assert.Single(Entries(cut)), TimeSpan.FromSeconds(5));

        await typed;
    }

    [Fact]
    public async Task A_text_an_option_already_reads_is_never_offered_to_add()
    {
        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Rows)
            .Add(x => x.Lazy, true)
            .Add(x => x.OnQuickAdd, EventCallback.Factory.Create<QuickAddEventArgs<SelectRef>>(this, _ => { })));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Gómez" });

        cut.WaitForAssertion(() => Assert.Contains("Gómez", cut.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())), TimeSpan.FromSeconds(5));

        Assert.Empty(Entries(cut));
    }
}

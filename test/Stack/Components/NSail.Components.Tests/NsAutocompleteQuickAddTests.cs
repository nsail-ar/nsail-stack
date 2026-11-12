// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    // The keyboard's own way to the quick add: it is a row of the list, reached past the matches
    // and taken by Enter as its click takes it (nsail#3684).

    static readonly SelectRef[] Gomez =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Gómez"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Gómora"),
    ];

    readonly List<string> _added = [];

    readonly List<Guid> _changes = [];

    async Task<IRenderedComponent<SearchLookupHost>> Walkable(string text, IReadOnlyList<SelectRef>? rows = null)
    {
        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, rows ?? Gomez)
            .Add(x => x.Lazy, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<Guid>(this, value => _changes.Add(value)))
            .Add(x => x.OnQuickAdd, EventCallback.Factory.Create<QuickAddEventArgs<SelectRef>>(this, args => _added.Add(args.Text))));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = text });

        cut.WaitForAssertion(() => Assert.Single(Entries(cut)), TimeSpan.FromSeconds(5));

        return cut;
    }

    static Task Press(IRenderedComponent<SearchLookupHost> cut, string key)
    {
        return cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });
    }

    static bool OnQuickAdd(IRenderedComponent<SearchLookupHost> cut)
    {
        return cut.FindAll(".ns-lookup-create-active button").Count == 1;
    }

    static string[] Selected(IRenderedComponent<SearchLookupHost> cut)
    {
        return [.. cut.FindAll(".mud-list-item").Select(item => item.GetAttribute("aria-selected")!)];
    }

    [Fact]
    public async Task The_quick_add_is_the_only_row_of_an_empty_answer_and_Enter_adds_the_text()
    {
        var cut = await Walkable("Nueva");

        Assert.Empty(cut.FindAll(".mud-list-item"));

        await Press(cut, "ArrowDown");

        Assert.True(OnQuickAdd(cut));

        await Press(cut, "Enter");

        Assert.Equal(["Nueva"], _added);
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task ArrowDown_past_the_last_row_reaches_the_quick_add_and_ArrowUp_returns()
    {
        var cut = await Walkable("Góm");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));

        await Press(cut, "ArrowDown");

        Assert.False(OnQuickAdd(cut));
        Assert.Equal(["false", "true"], Selected(cut));

        await Press(cut, "ArrowDown");

        Assert.True(OnQuickAdd(cut));
        Assert.Equal(["false", "false"], Selected(cut));

        await Press(cut, "ArrowDown");

        Assert.True(OnQuickAdd(cut));
        Assert.Empty(_changes);

        await Press(cut, "ArrowUp");

        Assert.False(OnQuickAdd(cut));
        Assert.Equal(["false", "true"], Selected(cut));
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task ArrowUp_from_the_quick_add_of_an_empty_answer_stays_on_it()
    {
        var cut = await Walkable("Nueva");

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowUp");

        Assert.True(OnQuickAdd(cut));
    }

    [Fact]
    public async Task Escape_on_the_quick_add_closes_the_list_with_nothing_added()
    {
        var cut = await Walkable("Nueva");

        await Press(cut, "ArrowDown");
        await Press(cut, "Escape");
        await cut.Find("input").KeyUpAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        Assert.Empty(_added);
        Assert.Empty(_changes);
    }

    // K5: the reached entry's Enter is the field's, so a form around it is not sent.
    [Fact]
    public async Task A_reached_quick_add_takes_the_Enter_from_the_form()
    {
        var cut = await Walkable("Nueva");

        await Press(cut, "ArrowDown");

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-commits"));
    }
}

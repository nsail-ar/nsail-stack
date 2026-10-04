// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#858: typing "Caja" into Forma de Pago › Cuenta was reported listing every
/// posting account. The message and the handler are not it — the handler narrows over the wire
/// — and AccountSelect is the only non-Lazy lookup with an OnSearch in the tree, so the browsed
/// path is the one that had never been exercised by anything. It has none of its own tests
/// today, which is the whole reason a report about it could not be settled by reading.
///
/// What a search owes the field is one promise: the options on screen are what the sender
/// answered for the term in the box. It is pinned in the three states the field is met in — a
/// blank create form, a create form whose focus search is still in flight when the first
/// keystroke lands, and an edit form born holding a value — and in both postures, which differ
/// only in what an EMPTY box means.</summary>
public sealed class NsAutocompleteSearchTests : BunitContext, IAsyncLifetime
{
    public NsAutocompleteSearchTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsAutocompleteSearchTests).Assembly, []));
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

    static readonly Guid Held = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // A chart of accounts in miniature: one row whose name carries the term, three that do not.
    static readonly SelectRef[] Chart =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Caja en pesos"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Banco Nación"),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Deudores por ventas"),
        new(Held, "Proveedores"),
    ];

    IRenderedComponent<SearchLookupHost> Mount(bool lazy, Guid value = default, Func<string?, Task>? gate = null)
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Chart)
            .Add(x => x.Lazy, lazy)
            .Add(x => x.Value, value)
            .Add(x => x.Gate, gate));
    }

    // The real gesture, not a value written behind the vendor's back (testing.md — writing a
    // value without an event proves nothing about bindings). MudAutocomplete's SearchFunc runs
    // off the input event, behind its own 100ms DebounceInterval, so what is waited on is the
    // list having settled — never a timer.
    static Task Type(IRenderedComponent<SearchLookupHost> cut, string text)
    {
        return cut.Find("input").InputAsync(new ChangeEventArgs { Value = text });
    }

    static string[] Options(IRenderedComponent<SearchLookupHost> cut)
    {
        return [.. cut.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())];
    }

    static void Shows(IRenderedComponent<SearchLookupHost> cut, params string[] expected)
    {
        cut.WaitForAssertion(() => Assert.Equal(expected, Options(cut)), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ABrowsedLookupOpensOnTheWholeSet()
    {
        var cut = Mount(lazy: false);

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Shows(cut, "Caja en pesos", "Banco Nación", "Deudores por ventas", "Proveedores");
    }

    [Fact]
    public async Task ABrowsedLookupNarrowsToWhatTheTermMatched()
    {
        var cut = Mount(lazy: false);

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        await Type(cut, "Caja");

        Shows(cut, "Caja en pesos");

        // The term did reach the sender — which is what makes the rows on screen the only
        // half of the promise a defect could have been hiding in.
        Assert.Equal("Caja", cut.Instance.Asked[^1]);
    }

    /// <summary>The one state a Lazy lookup never reaches, and the reason the browsed path is
    /// the only suspect: its focus search is a round trip, so a keystroke landing before the
    /// answer leaves two searches in flight at once. The superseded one must not have the last
    /// word — the whole chart standing under the term the user typed is the reported defect's
    /// exact shape.</summary>
    [Fact]
    public async Task AKeystrokeDuringTheFocusSearchStillLeavesTheTermsOwnRowsOnScreen()
    {
        var gates = new System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource>();

        var cut = Mount(lazy: false, gate: term => gates
            .GetOrAdd(term ?? string.Empty, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .Task);

        // Not awaited: the focus search is deliberately left in flight, which is what the
        // browser does and what awaiting the dispatched event here would not.
        var focus = cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Contains(string.Empty, cut.Instance.Asked!), TimeSpan.FromSeconds(5));

        var typed = Type(cut, "Caja");

        cut.WaitForAssertion(() => Assert.Equal("Caja", cut.Instance.Asked[^1]), TimeSpan.FromSeconds(5));

        // Both answers come back, the focus one first — the order the wire put them in.
        gates[string.Empty].TrySetResult();
        gates["Caja"].TrySetResult();

        Shows(cut, "Caja en pesos");

        await focus;
        await typed;
    }

    /// <summary>nsail#1672: the sibling of the case above, with the answers landing in the
    /// OTHER order — the one real latency actually produces, since two in-flight searches race
    /// each other over the wire rather than finishing in the order they were asked. The newer
    /// term's own answer lands FIRST here; the older, superseded search then answers late, and
    /// its arrival must not undo what the box is already showing.</summary>
    [Fact]
    public async Task AnAnswerThatArrivesLateForATermTheBoxHasLeftDoesNotUndoTheNewerOne()
    {
        var gates = new System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource>();

        var cut = Mount(lazy: false, gate: term => gates
            .GetOrAdd(term ?? string.Empty, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .Task);

        // Not awaited: the focus search is deliberately left in flight, gated on its own term.
        var focus = cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Contains(string.Empty, cut.Instance.Asked!), TimeSpan.FromSeconds(5));

        var typed = Type(cut, "Caja");

        cut.WaitForAssertion(() => Assert.Equal("Caja", cut.Instance.Asked[^1]), TimeSpan.FromSeconds(5));

        // The newer term wins the network, answering before the older one — real latency's own
        // ordering, and the shape a keystroke typed while a slower answer is still out produces.
        gates["Caja"].TrySetResult();

        Shows(cut, "Caja en pesos");

        gates[string.Empty].TrySetResult();

        // The late, superseded answer must not repaint the chart over the term still in the box.
        Shows(cut, "Caja en pesos");

        await focus;
        await typed;
    }

    /// <summary>nsail#1672, Vigía's own reading of the fix: typing forward and then back onto
    /// the SAME term a cancelled search is still out for. A term compare alone cannot tell that
    /// call apart from the current one — both name "Caj" — so only checking the cancellation
    /// itself keeps its late, empty answer from overwriting the correct one the newer, un-
    /// cancelled search for the same term already drew.</summary>
    [Fact]
    public async Task ACancelledAnswerForATermTheBoxHasReturnedToDoesNotUndoTheNewerOne()
    {
        List<TaskCompletionSource> gates = [];

        var cut = Mount(lazy: false, gate: _ =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            gates.Add(gate);

            return gate.Task;
        });

        // Not awaited: the focus search (term "") is left in flight for the whole test, like its
        // sibling above — gates[0].
        var focus = cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Single(gates), TimeSpan.FromSeconds(5));

        // S1: typed forward to "Caj" — gates[1].
        var s1 = Type(cut, "Caj");

        cut.WaitForAssertion(() => Assert.Equal(2, gates.Count), TimeSpan.FromSeconds(5));

        // S2: typed further to "Caja" — cancels S1 — gates[2].
        var s2 = Type(cut, "Caja");

        cut.WaitForAssertion(() => Assert.Equal(3, gates.Count), TimeSpan.FromSeconds(5));

        // S3: backspaced back to "Caj" — cancels S2, and the box returns to S1's own term —
        // gates[3].
        var s3 = Type(cut, "Caj");

        cut.WaitForAssertion(() => Assert.Equal(4, gates.Count), TimeSpan.FromSeconds(5));

        // S3 — the term the box is actually on, and never cancelled — wins the network first.
        gates[3].TrySetResult();

        Shows(cut, "Caja en pesos");

        // S1's cancellation unwinds late, for the SAME term the box has returned to: its answer
        // must not win over S3's correct one just because the two terms read alike.
        gates[1].TrySetResult();

        Shows(cut, "Caja en pesos");

        gates[2].TrySetResult();
        gates[0].TrySetResult();

        await focus;
        await s1;
        await s2;
        await s3;
    }

    /// <summary>An edit form's own arrival: the field is born holding an account, so the
    /// vendor opens on the row it already names rather than on the chart.</summary>
    [Fact]
    public async Task ALookupBornHoldingAValueOpensOnThatRowAndStillNarrows()
    {
        var cut = Mount(lazy: false, value: Held);

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Shows(cut, "Proveedores");

        await Type(cut, "Caja");

        Shows(cut, "Caja en pesos");
    }

    [Fact]
    public async Task ALazyLookupAsksNothingOnAnEmptyBoxAndNarrowsOnATerm()
    {
        var cut = Mount(lazy: true);

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Assert.Empty(Options(cut));

        await Type(cut, "Caja");

        Shows(cut, "Caja en pesos");
    }
}

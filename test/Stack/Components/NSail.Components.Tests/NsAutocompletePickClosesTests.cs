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

/// <summary>What a pick owes the screen: the list it was made from is gone, and it stays gone
/// however the close and the box's own refocus are ordered. The vendor closes its own popover
/// inside the selection, and two opens still follow it. One is an OPEN already under way — the
/// keystroke before the pick, whose search is still out — running to completion afterwards and
/// raising the list again, on the very row just chosen, with nothing left to close it. The other
/// is the cursor the pick hands back: the vendor focuses the box it was picked from, and
/// OpenOnFocus answers that focus exactly like a click. The user is left reading an answer
/// through the list it came from, and the only way out is to click elsewhere.
///
/// The window is the round trip, so it is wide exactly where a lookup is slowest, and it is why
/// a search is held open here rather than hurried: the order is the subject, not the clock. The
/// field marks a pick made while a search was out and closes the list the vendor opens for it,
/// and it withholds OpenOnFocus for the one focus the pick hands back so that one opens nothing
/// at all. Both marks cover a single gesture and the cases below are what spends them: a search
/// begun AFTER the pick — a typed term, or a bare press on the box, which asks for the same term
/// the refused refocus would have — and, where no search is coming, the next key the box takes,
/// which is how a field left by the keyboard answers the focus it is come back with. The (x)
/// takes no mark at all: it hands no cursor back, and an empty box that could not open on focus
/// would have no list left to show.</summary>
public sealed class NsAutocompletePickClosesTests : BunitContext, IAsyncLifetime
{
    public NsAutocompletePickClosesTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsAutocompletePickClosesTests).Assembly, []));
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

    static readonly SelectRef Caja = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Caja en pesos");

    static readonly SelectRef[] Chart =
    [
        Caja,
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Banco Nación"),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Deudores por ventas"),
    ];

    readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new();

    TaskCompletionSource Gate(string? term)
    {
        return _gates.GetOrAdd(
            term ?? string.Empty,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    Guid _value;

    IRenderedComponent<SearchLookupHost> Mount(bool clearable)
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Chart)
            .Add(x => x.Clearable, clearable)
            .Add(x => x.Gate, term => Gate(term).Task)
            .Bind(x => x.Value, _value, value => _value = value, () => _value));
    }

    static Task Type(IRenderedComponent<SearchLookupHost> cut, string text)
    {
        return cut.Find("input").InputAsync(new ChangeEventArgs { Value = text });
    }

    static Task Pick(IRenderedComponent<SearchLookupHost> cut, string option)
    {
        return cut.InvokeAsync(() => cut.FindAll(".mud-list-item")
            .First(item => item.TextContent.Contains(option, StringComparison.Ordinal))
            .Click());
    }

    // The list standing on screen when the pick is made: the answer to the search BEFORE the
    // one left out, which is what a person clicks while the next one is still travelling.
    async Task<IRenderedComponent<SearchLookupHost>> OpenedOnTheWholeChart(bool clearable = false)
    {
        var cut = Mount(clearable);

        var focus = cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Contains(string.Empty, cut.Instance.Asked!), TimeSpan.FromSeconds(5));

        Gate(string.Empty).TrySetResult();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));

        await focus;

        return cut;
    }

    [Fact]
    public async Task APickMadeWhileASearchIsStillOutLeavesNoListOpen()
    {
        var cut = await OpenedOnTheWholeChart();

        // Not awaited: the keystroke's search is deliberately left in flight, which is the
        // whole window — the vendor's own open is waiting on it.
        var typed = Type(cut, "Caja");

        cut.WaitForAssertion(() => Assert.Equal("Caja", cut.Instance.Asked[^1]), TimeSpan.FromSeconds(5));

        await Pick(cut, Caja.DisplayName);

        // The answer lands after the selection is over, which is when the vendor opens.
        Gate("Caja").TrySetResult();

        await typed;

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        Assert.Equal(Caja.Id, _value);
    }

    /// <summary>The ordering the browser could not be made to produce and a loaded runner met on
    /// its own: the pick's close has already landed, and the cursor the pick hands back arrives
    /// after it, with no search of its own out. The vendor's one-shot mark for that focus is
    /// already spent by then — the box's blur, which the same gesture raised, clears it — so
    /// nothing but the field's own withheld OpenOnFocus stands between that focus and a list over
    /// the answer just chosen. Forced here rather than waited for: the order is the subject.
    /// </summary>
    [Fact]
    public async Task APicksOwnRefocusLandingAfterTheCloseLeavesNoListOpen()
    {
        var cut = await OpenedOnTheWholeChart();

        await Pick(cut, Caja.DisplayName);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        // The box's own focusout, raised when the option took the cursor at mousedown, landing
        // after the click it preceded.
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        // The cursor coming back to the box the pick was made from — the order the nightly's own
        // box met and no run on an idle one did, the close already landed and the refocus last.
        // Not awaited, and its gate opened straight after: a field that still answers this focus
        // with a search searches for the text the pick wrote in the box, and the list it raises
        // on that answer is what this case refuses — held, the act would prove nothing.
        var refocus = cut.Find("input").FocusAsync(new FocusEventArgs());

        Gate(Caja.DisplayName).TrySetResult();

        await refocus;

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The other half, and the reason the mark is dropped rather than held: a field
    /// asked for its list again after a pick has to open. Nothing here is left out — the pick
    /// is made on a settled list — and the term typed after it is a plain reopen.</summary>
    [Fact]
    public async Task AskingForTheListAgainAfterAPickStillOpensIt()
    {
        var cut = await OpenedOnTheWholeChart();

        await Pick(cut, Caja.DisplayName);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        var reopened = Type(cut, "Banco");

        cut.WaitForAssertion(() => Assert.Equal("Banco", cut.Instance.Asked[^1]), TimeSpan.FromSeconds(5));

        Gate("Banco").TrySetResult();

        await reopened;

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The same half for the gesture that asks without typing a thing: a press on the
    /// box after a pick. It opens on the text the pick left there, which is the term the refused
    /// refocus would have asked for too — so the mark cannot be read off the term, and what
    /// spends it is that this open asked for a search at all.</summary>
    [Fact]
    public async Task PressingTheBoxAfterAPickStillOpensItsList()
    {
        var cut = await OpenedOnTheWholeChart();

        await Pick(cut, Caja.DisplayName);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        var pressed = cut.Find("input").MouseDownAsync(new MouseEventArgs());

        Gate(Caja.DisplayName).TrySetResult();

        await pressed;

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The half no search can answer for, and the one a mark that waits for one would
    /// cost a person: the field is left by the keyboard and come back to. The pick's own
    /// refocus is acted out first, because that is the browser's own order — the vendor hands
    /// the cursor back before anybody can do anything else — and the Tab that follows is the
    /// gesture, with the focusout as what it causes. The focus that comes back is the person's,
    /// and it opens.</summary>
    [Fact]
    public async Task AKeyTakenAfterAPickGivesTheBoxBackTheFocusItOpensOn()
    {
        var cut = await OpenedOnTheWholeChart();

        await Pick(cut, Caja.DisplayName);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        Gate(Caja.DisplayName).TrySetResult();

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Tab", ShiftKey = true });
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The (x) is not a pick: it hands no cursor back — the vendor withdraws its own
    /// focus features for the whole clearing chain — and the box it leaves is the one state
    /// where the focus open is all the field has, since an empty box's list is the create entry
    /// alone (NsLookupCreateEntryTests). So the mark a pick takes is not taken here.</summary>
    [Fact]
    public async Task TheBoxClearedAfterAPickStillOpensOnItsNextFocus()
    {
        var cut = await OpenedOnTheWholeChart(clearable: true);

        await Pick(cut, Caja.DisplayName);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        // The browser's own order for the gesture this is about: the pick hands the cursor back,
        // and the hand that then reaches the (x) takes it out of the box again.
        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").BlurAsync(new FocusEventArgs());

        await cut.Find("button.mud-input-clear-button").ClickAsync(new MouseEventArgs());

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }
}

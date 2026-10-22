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
/// inside the selection, and four opens still get past that close. One is an OPEN already under
/// way — the keystroke before the pick, whose search is still out — running to completion
/// afterwards and raising the list again, on the very row just chosen, with nothing left to close
/// it. Another is the cursor the pick hands back: the vendor focuses the box it was picked from,
/// and OpenOnFocus answers that focus exactly like a click. The last two belong to the picks the
/// FIELD makes, which the vendor only ever learns as a Value change: the list its own keystroke's
/// answer raised on the way in, and the list the quick add's row was pressed inside. The user is
/// left reading an answer through the list it came from, and the only way out is to click
/// elsewhere.
///
/// The window is the round trip, so it is wide exactly where a lookup is slowest, and it is why
/// a search is held open here rather than hurried: the order is the subject, not the clock. The
/// field marks every pick and closes the open the vendor raises after it, and it withholds
/// OpenOnFocus for the one focus the pick hands back so that one opens nothing at all. The reopen
/// mark is read when an open LANDS, which is what keeps it off a list somebody asked for: every
/// other way of opening one asks for a search first and that search drops the mark. The refocus
/// mark has no landing of its own to be read at, so it stands until the field is used again, and
/// the cases below are what spends it: a search begun AFTER the pick — a typed term, or a bare
/// press on the box, which asks for the same term the refused refocus would have — and, where no
/// search is coming, the next key the box takes, which is how a field left by the keyboard answers
/// the focus it is come back with. The (x) takes no mark at all: it hands no cursor back, and a
/// mark taken there would cost the empty box it leaves the create entry, the only list a Lazy
/// field has to show. A pick the field makes closes its own list outright, since no mark can reach
/// a list that is not open yet.</summary>
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

    IRenderedComponent<SearchLookupHost> Mount(bool clearable, bool lazy = false)
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Chart)
            .Add(x => x.Clearable, clearable)
            .Add(x => x.Lazy, lazy)
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

    /// <summary>The pick the field makes INSIDE a keystroke (NsAutocompleteReaderEnterTests), and
    /// the one open neither mark can reach: the list the key's own answer raises is already up
    /// when the pick is made — it landed while the keystroke was waiting for that answer — and the
    /// vendor, never told a selection happened, closes nothing for it. Measured without the close:
    /// one popover holding the single row just taken, still standing a half-second later. The
    /// focus after it is the other half, the mark the pick took and its own key did not
    /// spend.</summary>
    [Fact]
    public async Task APickAKeystrokeResolvesLeavesNoListOpen()
    {
        var cut = Mount(clearable: false, lazy: true);

        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        // The reader's burst: the term goes in and the Enter is pressed over a search that is
        // still out, which is the keystroke the field answers itself.
        _ = Type(cut, "Caja");

        cut.WaitForAssertion(() => Assert.Contains("Caja", cut.Instance.Asked!), TimeSpan.FromSeconds(5));

        var entered = input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Gate("Caja").TrySetResult();

        await entered;

        cut.WaitForAssertion(() => Assert.Equal(Caja.Id, _value), TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        // The cursor coming back to the box the keystroke picked in, with its gate opened as the
        // mouse pick's is. What forces the mark's own ordering is the Tab case below — a box
        // whose text bUnit does not hand back searches for nothing, so this focus is the shape of
        // the gesture and not the measure of it (the case above).
        Gate(Caja.DisplayName).TrySetResult();

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The same pick at its earliest, where the field's own answer BEATS the vendor's
    /// interval: nothing is open when the key resolves, so the close it makes closes nothing —
    /// and what the vendor still holds is the debounced search for the text that key just took,
    /// whose own answer would open a list over the row it chose. That open never comes: the close
    /// takes the vendor's timer with it, the way the vendor's own selection path does. The
    /// interval is wound by hand here rather than waited out, so the one ask ever made is
    /// necessarily the field's own and the one that never arrives is measured instead of
    /// hoped for.</summary>
    [Fact]
    public async Task APickMadeBeforeTheVendorsIntervalLeavesNoSearchBehindToOpenAList()
    {
        var clock = new HandWoundClock();

        // Last registration wins the resolve, which is how the key interceptor stub is swapped in
        // too — and only for this case: every other one here needs the vendor's own interval to
        // run, since the search it asks for is what puts the list on screen.
        Services.AddSingleton<TimeProvider>(clock);

        var cut = Mount(clearable: false, lazy: true);

        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        // Open before the term is typed: what makes this burst the early one is that the field's
        // own ask answers while the vendor's interval is still unwound.
        Gate("Caja").TrySetResult();

        _ = Type(cut, "Caja");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.Equal(Caja.Id, _value), TimeSpan.FromSeconds(5));

        // The clock has not moved, so the vendor cannot have asked anything: the one term asked
        // is the one the keystroke resolved itself, and the row was taken with no list on screen.
        Assert.Equal(["Caja"], cut.Instance.Asked);
        Assert.Empty(cut.FindAll(".mud-popover-open"));

        clock.Advance(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => Assert.Equal(["Caja"], cut.Instance.Asked), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The other pick the FIELD makes, and the only one made from inside the open list:
    /// the quick add's row, which creates the item and selects it. The vendor saw no selection of
    /// its own — the entry is this field's own fragment and not one of its options — so the list
    /// the row was pressed in is left standing over the form, now holding nothing but the entry
    /// itself (measured without the close: 1 popover, 0 options, the made row in the box).
    /// </summary>
    [Fact]
    public async Task APickMadeFromTheQuickAddRowLeavesNoListOpen()
    {
        var made = new SelectRef(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Caja chica");

        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Chart)
            .Add(x => x.Lazy, true)
            .Add(x => x.Gate, term => Gate(term).Task)
            .Add(x => x.OnQuickAdd, EventCallback.Factory.Create<QuickAddEventArgs<SelectRef>>(
                this, args => args.Item = made))
            .Bind(x => x.Value, _value, value => _value = value, () => _value));

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        Gate(made.DisplayName).TrySetResult();

        await Type(cut, made.DisplayName);

        // A term the chart answers nothing for, which is the only state the row is offered in —
        // and the search for it has to have landed, or the entry would quote a term nobody looked
        // up yet (NsAutocompleteQuickAddTests).
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ns-lookup-create button")), TimeSpan.FromSeconds(5));

        Assert.NotEmpty(cut.FindAll(".mud-popover-open"));

        await cut.Find(".ns-lookup-create button").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Equal(made.Id, _value), TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
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

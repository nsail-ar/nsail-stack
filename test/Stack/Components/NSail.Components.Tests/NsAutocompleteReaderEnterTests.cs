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

/// <summary>nsail#2337: a reader is a keyboard — it types the whole code and ends it with Enter,
/// in one burst — and a search-first lookup's list is a round trip behind the vendor's own
/// debounce, so that key lands before any search was even asked. The vendor picks the option it
/// is HIGHLIGHTING, which at that instant is nothing, so the code resolved to nothing and the
/// counter was holding a reader that did not work (QA walked it on Stock's Producto filter).
///
/// <para>The vendor's own Enter, measured here before anything was written: once its list has
/// settled it takes the option it highlights — the FIRST one, with no arrow-down, and each arrow
/// moves one down from there. So what is missing is never "Enter picks nothing"; it is the Enter
/// that lands before the list exists.</para>
///
/// <para>So the field answers that one itself, and answers it the way the scan box over the grid
/// does (ProductScanField): one answer is taken, several are left in the list to choose from, and
/// none leaves the box exactly as it was — which is what the caller's own offer quotes. The
/// search is gated here rather than timed: what makes it a burst is that the Enter is pressed
/// while the answer is still out, never a sleep.</para></summary>
public sealed class NsAutocompleteReaderEnterTests : BunitContext, IAsyncLifetime
{
    public NsAutocompleteReaderEnterTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsAutocompleteReaderEnterTests).Assembly, []));
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

    static readonly Guid Scanned = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // A shelf in miniature: one article the code names alone, two that a word names together, and
    // a code that names two — the frame and the combo whose own reading quotes it, which is how an
    // exact code comes back as a choice (the search reads the name and the code alike).
    static readonly SelectRef[] Shelf =
    [
        new(Scanned, "7798123456789 Armazón Ray-Ban"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Armazón Vulk negro"),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Armazón Vulk tostado"),
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "7798999999999 Armazón Ray-Ban azul"),
        new(Guid.Parse("55555555-5555-5555-5555-555555555555"), "Combo 7798999999999 + estuche"),
    ];

    readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);

    Guid _picked;

    Task Gate(string? term)
    {
        return Held(term).Task;
    }

    // Let the search for a term answer, whether or not it has been asked yet: a test that only
    // needs the list ON SCREEN does not care which side of the vendor's interval it is reading.
    void Release(string term)
    {
        Held(term).TrySetResult();
    }

    TaskCompletionSource Held(string? term)
    {
        return _gates
            .GetOrAdd(term ?? string.Empty, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    IRenderedComponent<SearchLookupHost> Mount()
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Shelf)
            .Add(x => x.Lazy, true)
            .Add(x => x.Gate, Gate)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<Guid>(this, value => _picked = value)));
    }

    // The reader's whole gesture: the code arrives as one text and the Enter is pressed with the
    // search for it still out — nothing has answered, and no arrow has been touched.
    async Task<Task> Burst(IRenderedComponent<SearchLookupHost> cut, string code)
    {
        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        _ = input.InputAsync(new ChangeEventArgs { Value = code });

        cut.WaitForAssertion(() => Assert.Contains(code, cut.Instance.Asked!), TimeSpan.FromSeconds(5));

        return input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
    }

    static string[] Options(IRenderedComponent<SearchLookupHost> cut)
    {
        return [.. cut.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())];
    }

    [Fact]
    public async Task AReaderBurstTakesTheSingleAnswerWithNoArrowDown()
    {
        var cut = Mount();

        var entered = await Burst(cut, "7798123456789");

        // Nothing has answered yet, so nothing can have been taken — the state the vendor's own
        // Enter leaves for good.
        Assert.Equal(Guid.Empty, _picked);

        _gates["7798123456789"].TrySetResult();

        await entered;

        cut.WaitForAssertion(() => Assert.Equal(Scanned, _picked), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AReaderBurstThatNamesSeveralArticlesTakesNoneAndLeavesTheListOpen()
    {
        var cut = Mount();

        var entered = await Burst(cut, "Vulk");

        Release("Vulk");

        await entered;

        // The choice is the operator's: the key resolved the text and stopped there, and the
        // list the vendor's own search opens for it is what the choosing happens in.
        cut.WaitForAssertion(
            () => Assert.Equal(["Armazón Vulk negro", "Armazón Vulk tostado"], Options(cut)),
            TimeSpan.FromSeconds(5));

        Assert.Equal(Guid.Empty, _picked);
    }

    [Fact]
    public async Task AReaderBurstThatNamesNothingLeavesTheBoxAsItWas()
    {
        var cut = Mount();

        var entered = await Burst(cut, "8888888888882");

        _gates["8888888888882"].TrySetResult();

        await entered;

        Assert.Equal(Guid.Empty, _picked);

        // The text is still in the box: it is what the caller's offer and the create entry are
        // about, and a key that emptied it would be a door onto nothing.
        cut.WaitForAssertion(
            () => Assert.Equal("8888888888882", cut.Find("input").GetAttribute("value")),
            TimeSpan.FromSeconds(5));

        Assert.Empty(Options(cut));
    }

    /// <summary>The burst at its earliest, and the state the live reader is actually in: the code
    /// and the Enter arrive inside a fraction of the vendor's own debounce, so the key lands
    /// before any search was ASKED — the field's own is then the first one the box makes. The
    /// interval is wound by hand here, so what makes the key early is a step this case takes and
    /// not how fast the box it runs on happens to be: left to the real clock, a loaded runner let
    /// the interval elapse before the keystroke was dispatched and the burst under test was no
    /// longer a burst (preflight.md, 8).</summary>
    [Fact]
    public async Task AnEnterBeforeTheVendorsDebounceAsksTheSearchItself()
    {
        var clock = new HandWoundClock();

        Services.AddSingleton<TimeProvider>(clock);

        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Shelf)
            .Add(x => x.Lazy, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<Guid>(this, value => _picked = value)));

        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        await input.InputAsync(new ChangeEventArgs { Value = "7798123456789" });

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(Scanned, _picked);

        // The clock never moved, so the vendor's own search cannot have been asked: the one term
        // ever asked is the one this keystroke resolved itself.
        Assert.Equal(["7798123456789"], cut.Instance.Asked);
    }

    /// <summary>The other half of "one answer is taken": the search is the one the picker already
    /// asks, so a code that also spells other articles is several answers and opens the list
    /// rather than taking the one it reads as an exact match (nsail#2337, ruling 8).</summary>
    [Fact]
    public async Task ACodeThatAlsoSpellsAnotherArticleIsSeveralAnswers()
    {
        var cut = Mount();

        // A whole code, exactly as a reader sends it — and the shelf answers it with the article
        // it belongs to AND the combo whose own reading quotes it.
        var entered = await Burst(cut, "7798999999999");

        _gates["7798999999999"].TrySetResult();

        await entered;

        Assert.Equal(Guid.Empty, _picked);

        cut.WaitForAssertion(
            () => Assert.Equal(["7798999999999 Armazón Ray-Ban azul", "Combo 7798999999999 + estuche"], Options(cut)),
            TimeSpan.FromSeconds(5));
    }

    /// <summary>The burst's own search against the vendor's debounce, which is the race the live
    /// reader runs: the key lands first, so the field asks the search itself — and a tenth of a
    /// second later the vendor asks THE SAME text, which cancels that ask (one Runner, and every
    /// search opens by cancelling what it finds running). A cancelled ask filed no answer, so what
    /// it hands back is the list of an older term: read as this key's answer it takes nothing on
    /// the very code it was resolving, or — in a box used once already — the article of the term
    /// before it. The answer is the one the search that cancelled ours is bringing.</summary>
    [Fact]
    public async Task AnEnterWhoseOwnSearchTheVendorCancelsReadsTheAnswerThatCancelledIt()
    {
        var cut = Mount();

        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        _ = input.InputAsync(new ChangeEventArgs { Value = "7798123456789" });

        var entered = input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Two asks for the one text: the key's own, pressed before the interval, and the vendor's
        // debounced one over it — the ask that cancels the first.
        cut.WaitForAssertion(
            () => Assert.Equal(["7798123456789", "7798123456789"], cut.Instance.Asked),
            TimeSpan.FromSeconds(5));

        _gates["7798123456789"].TrySetResult();

        await entered;

        cut.WaitForAssertion(() => Assert.Equal(Scanned, _picked), TimeSpan.FromSeconds(5));
    }

    /// <summary>A SETTLED list is the vendor's keystroke and not this field's — the hand typed at
    /// its own speed, the list is open and the option it would take is the one it is highlighting,
    /// carried to a screen reader as the box's own active descendant. So the box stamps no commit
    /// mark there: what takes that Enter from the form is the open list's own read
    /// (NsComboboxExpandedMarkTests), and the two guards meet exactly here.</summary>
    [Fact]
    public async Task ASettledListsEnterBelongsToTheListAndTheBoxStampsNoMark()
    {
        var cut = Mount();

        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        _ = input.InputAsync(new ChangeEventArgs { Value = "Vulk" });

        Release("Vulk");

        cut.WaitForAssertion(
            () => Assert.Equal(["Armazón Vulk negro", "Armazón Vulk tostado"], Options(cut)),
            TimeSpan.FromSeconds(5));

        var box = cut.Find("input");

        Assert.Equal("true", box.GetAttribute("aria-expanded"));
        Assert.Null(box.GetAttribute("data-ns-commits"));

        var first = cut.FindAll(".mud-list-item")[0];

        Assert.Equal("true", first.GetAttribute("aria-selected"));
        Assert.Equal(first.GetAttribute("id"), box.GetAttribute("aria-activedescendant"));
    }

    /// <summary>And the box the operator already settled hands the key back to the policy around
    /// it: an article chosen, the list closed, and the Enter that sends the question a filter bar
    /// or a one-field dialog IS. A box that ate it there would answer nothing at all, while every
    /// other box of the same form still sent the question.</summary>
    [Fact]
    public async Task ABoxSettledOnWhatWasChosenHandsTheEnterBackToTheForm()
    {
        var cut = Mount();

        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        _ = input.InputAsync(new ChangeEventArgs { Value = "Vulk" });

        Release("Vulk");

        cut.WaitForAssertion(() => Assert.Equal(2, Options(cut).Length), TimeSpan.FromSeconds(5));

        await cut.FindAll(".mud-list-item")[1].ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(
            () => Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), _picked),
            TimeSpan.FromSeconds(5));

        // What a caller does with the value it is handed: the field is rendered holding it, and
        // the box reads the article — the state the operator's next Enter lands in.
        cut.Render(p => p.Add(x => x.Value, _picked));

        cut.WaitForAssertion(
            () => Assert.Equal("Armazón Vulk tostado", cut.Find("input").GetAttribute("value")),
            TimeSpan.FromSeconds(5));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-commits"));
    }

    /// <summary>A field nobody may write to resolves nothing: it asks no question, so there is no
    /// keystroke of its own for the key to be taken from the form for — and nothing was typed into
    /// it to resolve either way.</summary>
    [Fact]
    public async Task ABoxNobodyMayWriteToResolvesNothing()
    {
        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Shelf)
            .Add(x => x.Lazy, true)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.Gate, Gate)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<Guid>(this, value => _picked = value)));

        var input = cut.Find("input");

        _ = input.InputAsync(new ChangeEventArgs { Value = "7798123456789" });

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(Guid.Empty, _picked);
        Assert.Null(cut.Find("input").GetAttribute("data-ns-commits"));
    }
}

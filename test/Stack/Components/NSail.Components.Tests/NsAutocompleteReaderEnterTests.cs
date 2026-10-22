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

    // A shelf in miniature: one article the code names alone, and two that a word names together.
    static readonly SelectRef[] Shelf =
    [
        new(Scanned, "7798123456789 Armazón Ray-Ban"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Armazón Vulk negro"),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Armazón Vulk tostado"),
    ];

    readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);

    Guid _picked;

    Task Gate(string? term)
    {
        return _gates
            .GetOrAdd(term ?? string.Empty, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .Task;
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

        _gates["Vulk"].TrySetResult();

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
    /// before any search was ASKED — the field's own is then the first one the box makes.</summary>
    [Fact]
    public async Task AnEnterBeforeTheVendorsDebounceAsksTheSearchItself()
    {
        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Shelf)
            .Add(x => x.Lazy, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<Guid>(this, value => _picked = value)));

        var input = cut.Find("input");

        await input.FocusAsync(new FocusEventArgs());

        // Not awaited, and nothing waited for in between: the vendor's own search is still
        // behind its interval when the key is pressed.
        _ = input.InputAsync(new ChangeEventArgs { Value = "7798123456789" });

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(Scanned, _picked);

        // A Lazy box asks nothing for an empty one, so the first term it ever asked for is the
        // one this keystroke resolved.
        Assert.Equal("7798123456789", cut.Instance.Asked[0]);
    }

    /// <summary>The other half of "one answer is taken": the search is the one the picker already
    /// asks, so a code that also spells other articles is several answers and opens the list
    /// rather than taking the one it reads as an exact match (nsail#2337, ruling 8).</summary>
    [Fact]
    public async Task ACodeThatAlsoSpellsAnotherArticleIsSeveralAnswers()
    {
        var cut = Mount();

        // The whole code of the first row, as a term the search answers with that row AND the
        // one whose name carries the same word.
        var entered = await Burst(cut, "Armazón");

        _gates["Armazón"].TrySetResult();

        await entered;

        Assert.Equal(Guid.Empty, _picked);

        cut.WaitForAssertion(() => Assert.Equal(3, Options(cut).Length), TimeSpan.FromSeconds(5));
    }
}

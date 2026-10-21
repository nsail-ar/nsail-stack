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

/// <summary>What a pick owes the screen: the list it was made from is gone. The vendor closes
/// its own popover inside the selection, but an OPEN already under way — the keystroke before
/// the pick, whose search is still out — runs to completion afterwards and raises the list
/// again, on the very row just chosen, with nothing left to close it. The user is left reading
/// an answer through the list it came from, and the only way out is to click elsewhere.
///
/// The window is the round trip, so it is wide exactly where a lookup is slowest, and it is why
/// a search is held open here rather than hurried: the order is the subject, not the clock. The
/// field marks a pick made while a search was out and closes the list the vendor opens for it;
/// a search begun AFTER the pick drops the mark, because that one is the field being asked for
/// its list again and the list it opens is wanted.</summary>
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

    IRenderedComponent<SearchLookupHost> Mount()
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Chart)
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
    async Task<IRenderedComponent<SearchLookupHost>> OpenedOnTheWholeChart()
    {
        var cut = Mount();

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

    /// <summary>The other half, and the reason the mark is dropped rather than held: a field
    /// asked for its list again after a pick has to open. Nothing here is left out — the pick
    /// is made on a settled list — and the click that follows is a plain reopen.</summary>
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
}

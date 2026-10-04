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

/// <summary>nsail#1652: the list's third entry, the caller's own — an offer ABOUT the text in the
/// box, for an answer one send cannot ask for (a scanned code being taught which unit of which
/// article it names). WHEN there is something to offer is the facade's answer; these are the two
/// halves that are not, and they are the quick add's own two, because this entry is the one that
/// WRITES: an offer has to have text in the box, and the search for that text has to have answered.
/// A Lazy field emptied sends no search at all, so the facade never hears that the code it is
/// holding left the screen — and an offer nobody can read is still an offer a click acts on.
/// </summary>
public sealed class NsAutocompleteOfferTests : BunitContext, IAsyncLifetime
{
    public NsAutocompleteOfferTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsAutocompleteOfferTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only, so bUnit's
    // synchronous teardown cannot dispose it (NsLookupCreateEntryTests' own note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    const string Offered = "Teach this code";

    static readonly SelectRef[] Rows =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Gómez"),
    ];

    static readonly RenderFragment Offer = builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddContent(1, Offered);
        builder.CloseElement();
    };

    static IEnumerable<string> Entries(IRenderedComponent<SearchLookupHost> cut)
    {
        return cut.FindAll(".ns-lookup-create").Select(entry => entry.TextContent.Trim());
    }

    // The empty list is where a scanned code lands, and it is the case the offer was built for.
    [Fact]
    public async Task An_offer_is_drawn_for_a_term_the_search_answered_nothing_for()
    {
        var cut = await Typed("Zeta");

        cut.WaitForAssertion(() => Assert.Contains(Offered, Entries(cut)), TimeSpan.FromSeconds(5));
    }

    // And it is drawn with rows too: what the entry is about is the TEXT, not the emptiness — a
    // term that found an article and is still a code nobody taught is both at once.
    [Fact]
    public async Task An_offer_is_drawn_beside_the_rows_a_term_did_match()
    {
        var cut = await Typed("Gómez");

        cut.WaitForAssertion(
            () => Assert.Contains("Gómez", cut.FindAll(".mud-list-item").Select(item => item.TextContent.Trim())),
            TimeSpan.FromSeconds(5));

        Assert.Contains(Offered, Entries(cut));
    }

    // The box emptied takes the entry with it. A Lazy field answers an empty box itself and never
    // raises OnSearch, so nothing the facade holds can be what retires this — the entry would
    // otherwise stand over an empty box, quoting a term only it can still see, and a click on it
    // would write that term.
    [Fact]
    public async Task An_emptied_box_takes_the_offer_with_it()
    {
        var cut = await Typed("Zeta");

        cut.WaitForAssertion(() => Assert.Contains(Offered, Entries(cut)), TimeSpan.FromSeconds(5));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = string.Empty });

        cut.WaitForAssertion(() => Assert.DoesNotContain(Offered, Entries(cut)), TimeSpan.FromSeconds(5));
    }

    // And nothing is offered about a term nobody has looked up yet: until the search answers, no
    // facade can know whether its own escape hatch is the answer, and the entry that acts is the
    // last one that may be drawn on a guess (the quick add's own wait, NsAutocompleteQuickAddTests).
    [Fact]
    public async Task An_offer_waits_for_the_search_of_the_text_it_is_about()
    {
        var gates = new ConcurrentDictionary<string, TaskCompletionSource>();

        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Rows)
            .Add(x => x.Lazy, true)
            .Add(x => x.Offer, Offer)
            .Add(x => x.Gate, term => gates
                .GetOrAdd(term ?? string.Empty, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                .Task));

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        // Not awaited: the search is deliberately left out, the round trip a loaded box pays.
        var typed = cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Zeta" });

        cut.WaitForAssertion(() => Assert.Equal("Zeta", cut.Instance.Asked[^1]), TimeSpan.FromSeconds(5));

        Assert.DoesNotContain(Offered, Entries(cut));

        gates["Zeta"].TrySetResult();

        cut.WaitForAssertion(() => Assert.Contains(Offered, Entries(cut)), TimeSpan.FromSeconds(5));

        await typed;
    }

    async Task<IRenderedComponent<SearchLookupHost>> Typed(string text)
    {
        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Rows)
            .Add(x => x.Lazy, true)
            .Add(x => x.Offer, Offer));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = text });

        return cut;
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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

    // The keyboard's own way to the offer: a row of the list past the matches, whose Enter presses
    // the caller's own control (ns.js, pressOffer), as a click on it does (nsail#3684).

    static readonly SelectRef[] Shelf =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Gómez"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Gómora"),
    ];

    readonly List<Guid> _changes = [];

    async Task<IRenderedComponent<SearchLookupHost>> Walkable(string text, Func<SelectRef, bool>? disabled = null, RenderFragment? offer = null)
    {
        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Shelf)
            .Add(x => x.Lazy, true)
            .Add(x => x.Offer, offer ?? Offer)
            .Add(x => x.ItemDisabledFunc, disabled)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<Guid>(this, value => _changes.Add(value))));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = text });

        cut.WaitForAssertion(() => Assert.Contains(Offered, Entries(cut)), TimeSpan.FromSeconds(5));

        return cut;
    }

    static Task Press(IRenderedComponent<SearchLookupHost> cut, string key)
    {
        return cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });
    }

    static bool OnOffer(IRenderedComponent<SearchLookupHost> cut)
    {
        return cut.FindAll(".ns-lookup-offer.ns-lookup-create-active").Count == 1;
    }

    int Pressed
    {
        get { return JSInterop.Invocations.Count(call => call.Identifier == "nsapp.pressOffer"); }
    }

    [Fact]
    public async Task The_offer_is_the_only_row_of_an_empty_answer_and_Enter_presses_it()
    {
        var cut = await Walkable("Zeta");

        await Press(cut, "ArrowDown");

        Assert.True(OnOffer(cut));
        Assert.Equal(0, Pressed);

        await Press(cut, "Enter");

        Assert.Equal(1, Pressed);
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task ArrowUp_from_the_offer_of_an_empty_answer_stays_on_it()
    {
        var cut = await Walkable("Zeta");

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowUp");

        Assert.True(OnOffer(cut));
    }

    // K4: a row the caller disabled is skipped on the way down, and the offer below it is reached.
    [Fact]
    public async Task A_disabled_row_above_the_offer_is_skipped_and_the_offer_is_still_reached()
    {
        var cut = await Walkable("Góm", row => row.DisplayName == "Gómora");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));

        await Press(cut, "ArrowDown");

        Assert.False(OnOffer(cut));

        await Press(cut, "ArrowDown");

        Assert.True(OnOffer(cut));

        await Press(cut, "ArrowUp");

        Assert.False(OnOffer(cut));
        Assert.Equal("true", cut.FindAll(".mud-list-item")[0].GetAttribute("aria-selected"));

        await Press(cut, "ArrowDown");
        await Press(cut, "Enter");

        Assert.Equal(1, Pressed);
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task Reaching_the_offer_raises_no_ValueChanged()
    {
        var cut = await Walkable("Zeta");

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");

        Assert.True(OnOffer(cut));
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task Escape_on_the_offer_closes_the_list_with_nothing_pressed()
    {
        var cut = await Walkable("Zeta");

        await Press(cut, "ArrowDown");
        await Press(cut, "Escape");
        await cut.Find("input").KeyUpAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        Assert.Equal(0, Pressed);
        Assert.Empty(_changes);
    }

    // A menu with a face of its own sits inside the vendor's wrapper, which carries role="button"
    // and comes first in the document but wires no click for that face (NsMenu.GetActivator):
    // the control pressOffer picks out of the lit offer is the face, and pressing it opens the menu.
    [Fact]
    public async Task Enter_on_an_offer_that_is_a_menu_presses_the_menus_own_face()
    {
        RenderFragment menu = builder =>
        {
            builder.OpenComponent<OfferMenuFace>(0);
            builder.CloseComponent();
        };

        var cut = await Walkable("Zeta", offer: menu);

        await Press(cut, "ArrowDown");

        Assert.True(OnOffer(cut));

        var control = cut.Find(".ns-lookup-offer.ns-lookup-create-active").QuerySelector(OfferControlSelector());

        Assert.NotNull(control);
        Assert.Contains("ns-menu-activator", control.ClassList);

        await cut.InvokeAsync(() => control.ClickAsync(new MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-menu-item").Count), TimeSpan.FromSeconds(5));
        Assert.Empty(_changes);
    }

    // A fragment that draws nothing is the facade saying it has nothing to offer: no row is drawn,
    // and the arrows walk as if no offer were there — here the quick add stays the only entry.
    [Fact]
    public async Task An_offer_that_draws_nothing_is_no_row_to_the_mouse_or_the_arrows()
    {
        var cut = Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, Shelf)
            .Add(x => x.Lazy, true)
            .Add(x => x.Offer, _ => { })
            .Add(x => x.OnQuickAdd, EventCallback.Factory.Create<QuickAddEventArgs<SelectRef>>(this, _ => { })));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Zeta" });

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ns-lookup-create")), TimeSpan.FromSeconds(5));

        Assert.Empty(cut.FindAll(".ns-lookup-offer"));

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");

        Assert.Empty(cut.FindAll(".ns-lookup-offer"));
        Assert.Single(cut.FindAll(".ns-lookup-create-active"));
    }

    // The selector the page presses the offer's control by, read from ns.js: the press itself
    // runs in a browser, so this layer proves which element it picks.
    static string OfferControlSelector()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var script = File.ReadAllText(Path.Combine(directory.FullName, "src/Stack/BaseServices/NSail.BaseServices.WebApp/wwwroot/ns.js"));

        return Regex.Match(script, "const OFFER_CONTROL_SELECTOR = \"(.*)\";").Groups[1].Value;
    }
}

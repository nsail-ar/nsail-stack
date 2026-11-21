// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The innermost open thing wins Escape (menus.md): inside the wrapper a routed surface
/// and the companion region take Escape from, an open dropdown, help, hint, expander or lookup
/// list answers the key and stops it, and only the next Escape closes the surface. The browser reads each
/// <c>:stopPropagation</c> flag after that element's handler returned; bUnit reads them all
/// before any handler runs, so the order is the browser suites' and the flag at that instant is
/// what is pinned here.</summary>
public sealed class EscapeInnermostTests : BunitContext, IAsyncLifetime
{
    static readonly KeyboardEventArgs Escape = new() { Key = "Escape" };

    int _closes;

    public EscapeInnermostTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IStringSource>(new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Help"] = "Ayuda"
        }));
        Services.AddSingleton<StringCatalog>();
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(EscapeInnermostTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider resolves a MudBlazor service that is IAsyncDisposable-only and internal,
    // so bUnit's synchronous teardown cannot dispose it.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    IRenderedComponent<EscapeNestHost> Render(string inner, bool dropdownOpen = false)
    {
        return Render<EscapeNestHost>(p => p
            .Add(x => x.Inner, inner)
            .Add(x => x.DropdownOpen, dropdownOpen)
            .Add(x => x.Closes, EventCallback.Factory.Create(this, () => _closes++)));
    }

    [Fact]
    public async Task AnOpenDropdown_TakesEscapeFromItsField_AndTheRegionStays()
    {
        var cut = Render("dropdown", dropdownOpen: true);

        await cut.Find(".field").KeyDownAsync(Escape);

        Assert.False(cut.Instance.DropdownOpen);
        Assert.Equal(0, _closes);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("hint")]
    public async Task AnOpenBubble_TakesEscape_AndTheRegionStays(string inner)
    {
        var cut = Render(inner);

        await cut.Find(".ns-help button").ClickAsync(new MouseEventArgs());
        Assert.Equal("true", cut.Find(".ns-help button").GetAttribute("aria-expanded"));

        await cut.Find(".ns-help button").KeyDownAsync(Escape);

        Assert.Equal("false", cut.Find(".ns-help button").GetAttribute("aria-expanded"));
        Assert.Equal(0, _closes);
    }

    /// <summary>Anywhere inside it while open, the detail as much as the chevron; and Escape
    /// closes it, which NsExpanderTests does not otherwise pin.</summary>
    [Theory]
    [InlineData(".ns-expander-toggle")]
    [InlineData(".working")]
    public async Task AnOpenExpander_TakesEscapeFromInsideIt_AndTheRegionStays(string from)
    {
        var cut = Render("expander");

        await cut.Find(".ns-expander-toggle").ClickAsync(new MouseEventArgs());
        Assert.Contains("ns-expander-open", cut.Find(".ns-expander").ClassName, StringComparison.Ordinal);

        await cut.Find(from).KeyDownAsync(Escape);

        Assert.DoesNotContain("ns-expander-open", cut.Find(".ns-expander").ClassName, StringComparison.Ordinal);
        Assert.Equal(0, _closes);
    }

    /// <summary>The browser reads the stop after the handler returned and before what it deferred
    /// ran, so the wrapper must still stop the key then; a close in the same dispatch drops it
    /// and the surface behind closes too.</summary>
    [Theory]
    [InlineData("help", ".ns-help button", ".ns-help")]
    [InlineData("hint", ".ns-help button", ".ns-help")]
    [InlineData("expander", ".ns-expander-toggle", ".ns-expander")]
    [InlineData("dropdown", ".field", ".ns-dropdown")]
    public async Task TheStopStillHolds_WhenReadAfterTheHandlerRan(string inner, string from, string wrapper)
    {
        var cut = Render(inner, dropdownOpen: true);
        if (inner != "dropdown")
        {
            await cut.Find(from).ClickAsync(new MouseEventArgs());
        }

        // Dispatched and read on the renderer's dispatcher: the deferred close is queued behind
        // the read there, where on the test's thread it may already have run.
        Task dispatch = Task.CompletedTask;
        string? stops = null;
        await cut.InvokeAsync(() =>
        {
            dispatch = cut.Find(from).KeyDownAsync(Escape);
            stops = cut.Find(wrapper).GetAttribute("blazor:onkeydown:stoppropagation");
        });
        await dispatch;

        Assert.Equal("", stops);
        Assert.Equal(0, _closes);
    }

    // nsail#4043: a lookup's open list is an open thing too, whether it holds rows or only its
    // entries, and whether the arrows reached one or not. The vendor closes the list on the
    // Escape's key UP, so the keydown the surface listens to finds the stop still standing.

    async Task<IRenderedComponent<EscapeNestHost>> OpenLookup(bool entries, bool reach, SurfaceContext? surface = null)
    {
        var cut = Render<EscapeNestHost>(p => p
            .Add(x => x.Inner, "lookup")
            .Add(x => x.LookupEntries, entries)
            .Add(x => x.Surface, surface)
            .Add(x => x.Closes, surface is null ? EventCallback.Factory.Create(this, () => _closes++) : default));

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        if (entries)
        {
            await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz9" });

            cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ns-lookup-create button")), TimeSpan.FromSeconds(5));
        }
        else
        {
            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));
        }

        if (reach)
        {
            await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        }

        return cut;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AnOpenLookupList_TakesEscape_AndOnlyTheNextOneClosesTheRegion(bool entries, bool reach)
    {
        var cut = await OpenLookup(entries, reach);

        await cut.Find("input").KeyDownAsync(Escape);

        Assert.Equal(0, _closes);

        await cut.Find("input").KeyUpAsync(Escape);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
        Assert.Equal(Guid.Empty, cut.Instance.Picked);

        await cut.Find("input").KeyDownAsync(Escape);

        Assert.Equal(1, _closes);
    }

    /// <summary>A lookup's open list inside a menu's block is the innermost thing too: its Escape
    /// leaves the menu standing, and only the next one, from the same box, closes the menu.</summary>
    [Fact]
    public async Task AnOpenLookupListInsideAMenu_TakesEscape_AndOnlyTheNextOneClosesTheMenu()
    {
        var cut = Render("menu-lookup");

        await cut.Find(".probe-face").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ns-menu-popover.mud-popover-open input")), TimeSpan.FromSeconds(5));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));

        await cut.Find("input").KeyDownAsync(Escape);
        await cut.Find("input").KeyUpAsync(Escape);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-list-item")), TimeSpan.FromSeconds(5));
        Assert.Single(cut.FindAll(".ns-menu-popover.mud-popover-open"));

        await cut.Find("input").KeyDownAsync(Escape);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ns-menu-popover.mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    /// <summary>A Tab is not the open list's to keep: it closes the list and the menu the cursor
    /// leaves, with the one key.</summary>
    [Fact]
    public async Task AnOpenLookupListInsideAMenu_LetsTabThrough_AndTheMenuCloses()
    {
        var cut = Render("menu-lookup");

        await cut.Find(".probe-face").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ns-menu-popover.mud-popover-open input")), TimeSpan.FromSeconds(5));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ns-menu-popover.mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AnOpenLookupList_OnARoutedSurface_KeepsItsClose()
    {
        var surface = new SurfaceContext(
            Surfaces.Dialog,
            Services.GetRequiredService<NavigationManager>(),
            Services.GetRequiredService<RouteTable>(),
            JSInterop.JSRuntime,
            new SurfaceHistory(),
            close: () => _closes++);
        var cut = await OpenLookup(entries: true, reach: false, surface);

        await cut.Find("input").KeyDownAsync(Escape);

        Assert.Equal(0, _closes);

        await cut.Find("input").KeyUpAsync(Escape);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        await cut.Find("input").KeyDownAsync(Escape);

        Assert.Equal(1, _closes);
    }

    /// <summary>Inside a hosted dialog the dialog's Escape is the vendor's native listener, which
    /// no stop reaches: the open list holds it, as an open dropdown does.</summary>
    [Fact]
    public async Task AnOpenLookupList_HoldsTheHostedDialogsEscape_WhileOpen()
    {
        var holds = new EscapeHolds(() => Task.CompletedTask);
        var cut = Render<CascadingValue<EscapeHolds>>(ps => ps
            .Add(p => p.Value, holds)
            .Add(p => p.IsFixed, true)
            .AddChildContent<EscapeNestHost>(host => host
                .Add(x => x.Inner, "lookup")
                .Add(x => x.LookupEntries, true)));

        Assert.False(holds.Any);

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz9" });

        cut.WaitForAssertion(() => Assert.True(holds.Any), TimeSpan.FromSeconds(5));

        await cut.Find("input").KeyDownAsync(Escape);
        await cut.Find("input").KeyUpAsync(Escape);

        cut.WaitForAssertion(() => Assert.False(holds.Any), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OnceTheInnerThingClosed_TheNextEscapeClosesTheRegion_Once()
    {
        var cut = Render("help");

        await cut.Find(".ns-help button").ClickAsync(new MouseEventArgs());
        await cut.Find(".ns-help button").KeyDownAsync(Escape);
        await cut.Find(".ns-help button").KeyDownAsync(Escape);

        Assert.Equal(1, _closes);
    }

    /// <summary>The same two Escapes on a routed non-main surface, whose way out is its own
    /// Close() — the unsaved-changes guard's door — rather than a named delegate.</summary>
    [Fact]
    public async Task OnARoutedSurface_OnlyTheSecondEscapeReachesItsClose()
    {
        var surface = new SurfaceContext(
            Surfaces.Dialog,
            Services.GetRequiredService<NavigationManager>(),
            Services.GetRequiredService<RouteTable>(),
            JSInterop.JSRuntime,
            new SurfaceHistory(),
            close: () => _closes++);
        var cut = Render<EscapeNestHost>(p => p
            .Add(x => x.Inner, "dropdown")
            .Add(x => x.DropdownOpen, true)
            .Add(x => x.Surface, surface));

        await cut.Find(".field").KeyDownAsync(Escape);

        Assert.False(cut.Instance.DropdownOpen);
        Assert.Equal(0, _closes);

        await cut.Find(".field").KeyDownAsync(Escape);

        Assert.Equal(1, _closes);
    }

    [Theory]
    [InlineData("dropdown", ".field")]
    [InlineData("help", ".ns-help button")]
    [InlineData("expander", ".working")]
    [InlineData("other", ".plain")]
    public async Task WithNothingOpenInside_EscapeClosesTheRegion(string inner, string from)
    {
        var cut = Render(inner);

        await cut.Find(from).KeyDownAsync(Escape);

        Assert.Equal(1, _closes);
    }

    /// <summary>The one exception: the vendor publishes no open state for a menu, so the Escape
    /// that closes a list the pointer opened still reaches the surface behind.</summary>
    [Fact]
    public async Task AMenuOpenedByThePointer_LetsItsEscapeThrough()
    {
        var cut = Render("menu");

        await cut.Find(".mud-menu button").ClickAsync(new MouseEventArgs());
        Assert.Contains("Alpha", cut.Markup, StringComparison.Ordinal);

        await cut.Find(".mud-menu button").KeyDownAsync(Escape);

        Assert.DoesNotContain("Alpha", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, _closes);
    }
}

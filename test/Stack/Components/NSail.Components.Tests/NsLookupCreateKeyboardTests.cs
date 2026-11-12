// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A lookup's create entry is the list's last row to the keyboard: ArrowDown from the
/// last option reaches it, Enter opens the create route as the click does, Escape closes the list
/// without choosing, and arriving there is not a pick — the bound value never moves.</summary>
public sealed class NsLookupCreateKeyboardTests : BunitContext, IAsyncLifetime
{
    public NsLookupCreateKeyboardTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IStringSource>(new FixedStrings(new Dictionary<string, string>
        {
            [Concept] = "Personas",
            ["Common.CreateNew"] = "Crear {0}…",
            ["Common.QuickAdd"] = "Agregar «{0}»",
        }));
        Services.AddSingleton<StringCatalog>();
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsLookupCreateKeyboardTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    static readonly string Concept = new MetadataProvider().KeyFor(typeof(SelectRef));

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    const string CreateRoute = "/directory/parties/create";

    static readonly SelectRef[] Two =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alpha"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beta"),
    ];

    readonly List<Guid> _changes = [];

    readonly List<string> _added = [];

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    async Task<IRenderedComponent<LookupCreateHost>> Open(string? route = CreateRoute, Surface? target = null, bool quickAdd = false)
    {
        Navigation.NavigateTo("/directory/parties/new");

        var cut = Render<LookupCreateHost>(p => p
            .Add(x => x.Items, Two)
            .Add(x => x.CreateRoute, route)
            .Add(x => x.CreateTarget, target ?? Surfaces.Aside)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<Guid>(this, value => _changes.Add(value)))
            .Add(x => x.OnQuickAdd, quickAdd ? EventCallback.Factory.Create<QuickAddEventArgs<SelectRef>>(this, args => _added.Add(args.Text)) : default));

        await cut.Find("input").FocusAsync(new FocusEventArgs());
        cut.Render();

        return cut;
    }

    static Task Press(IRenderedComponent<LookupCreateHost> cut, string key)
    {
        return cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });
    }

    static bool OnEntry(IRenderedComponent<LookupCreateHost> cut)
    {
        return cut.FindAll(".ns-lookup-create-active").Count > 0;
    }

    string[] Selected(IRenderedComponent<LookupCreateHost> cut)
    {
        return [.. cut.FindAll(".mud-list-item").Select(item => item.GetAttribute("aria-selected")!)];
    }

    [Fact]
    public async Task ArrowDownFromTheLastRowReachesTheEntryAndEnterOpensIt()
    {
        var cut = await Open();

        await Press(cut, "ArrowDown");

        Assert.False(OnEntry(cut));
        Assert.Equal(["false", "true"], Selected(cut));

        await Press(cut, "ArrowDown");

        Assert.True(OnEntry(cut));
        Assert.Equal(["false", "false"], Selected(cut));

        await Press(cut, "Enter");

        Assert.Contains("aside=directory%2Fparties%2Fcreate", Navigation.History.Last().Uri, StringComparison.Ordinal);
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task TheEntryIsTheOnlyRowAnEmptyAnswerHas()
    {
        var cut = await Open();

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz9" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-list-item")), TimeSpan.FromSeconds(5));

        await Press(cut, "ArrowDown");

        Assert.True(OnEntry(cut));

        await Press(cut, "Enter");

        Assert.Contains("aside=directory%2Fparties%2Fcreate", Navigation.History.Last().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnterOnTheEntryOpensABrowserTargetAsATabAndLeavesTheForm()
    {
        var cut = await Open(target: Surfaces.Blank);
        var before = Navigation.History.Count;

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");
        await Press(cut, "Enter");

        var open = JSInterop.Invocations["open"].Single();

        Assert.Equal("_blank", open.Arguments[1]);
        Assert.Equal(before, Navigation.History.Count);
    }

    [Fact]
    public async Task ArrowUpFromTheEntryOfAnEmptyAnswerStaysOnIt()
    {
        var cut = await Open();

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz9" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-list-item")), TimeSpan.FromSeconds(5));

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowUp");

        Assert.True(OnEntry(cut));
    }

    [Fact]
    public async Task ArrowUpFromTheEntryReturnsToTheLastRow()
    {
        var cut = await Open();

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowUp");

        Assert.False(OnEntry(cut));
        Assert.Equal(["false", "true"], Selected(cut));
    }

    [Fact]
    public async Task EscapeOnTheEntryClosesTheListWithoutChoosing()
    {
        var cut = await Open();
        var before = Navigation.History.Count;

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");
        await Press(cut, "Escape");
        await cut.Find("input").KeyUpAsync(new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));

        Assert.Empty(_changes);
        Assert.Equal(before, Navigation.History.Count);
    }

    [Fact]
    public async Task ArrivingAtTheEntryRaisesNoValueChanged()
    {
        var cut = await Open();

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");

        Assert.True(OnEntry(cut));
        Assert.Empty(_changes);
    }

    [Fact]
    public async Task ALookupWithNoCreateEntryGrowsNone()
    {
        var cut = await Open(route: null);
        var before = Navigation.History.Count;

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowDown");

        Assert.Empty(cut.FindAll(".ns-lookup-create"));
        Assert.False(OnEntry(cut));
        Assert.Equal(["false", "true"], Selected(cut));

        await Press(cut, "Enter");

        Assert.Equal(before, Navigation.History.Count);
    }

    // The entries are walked in the order they are drawn: the create entry, then the quick add.
    [Fact]
    public async Task TheQuickAddIsReachedAfterTheCreateEntryAndEnterAddsTheText()
    {
        var cut = await Open(quickAdd: true);
        var before = Navigation.History.Count;

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Nueva" });
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ns-lookup-create").Count), TimeSpan.FromSeconds(5));

        await Press(cut, "ArrowDown");

        Assert.Contains("Crear", cut.Find(".ns-lookup-create-active").TextContent, StringComparison.Ordinal);

        await Press(cut, "ArrowDown");

        Assert.Contains("Agregar «Nueva»", cut.Find(".ns-lookup-create-active").TextContent, StringComparison.Ordinal);

        await Press(cut, "ArrowUp");

        Assert.Contains("Crear", cut.Find(".ns-lookup-create-active").TextContent, StringComparison.Ordinal);

        await Press(cut, "ArrowDown");
        await Press(cut, "Enter");

        Assert.Equal(["Nueva"], _added);
        Assert.Equal(before, Navigation.History.Count);
        Assert.Empty(_changes);
    }
}

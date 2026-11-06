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

/// <summary>The keyboard contract of a lookup (keyboard.md): focus, type, Arrow Down, Enter picks
/// the highlighted row, Esc closes the list and keeps the cursor in the box.</summary>
public sealed class KeyboardContractLookupTests : BunitContext, IAsyncLifetime
{
    public KeyboardContractLookupTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(KeyboardContractLookupTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static readonly SelectRef Alpha = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alpha");
    static readonly SelectRef Beta = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beta");

    Guid _picked;

    IRenderedComponent<SearchLookupHost> Mount()
    {
        return Render<SearchLookupHost>(p => p
            .Add(x => x.Rows, [Alpha, Beta])
            .Bind(x => x.Value, _picked, value => _picked = value, () => _picked));
    }

    async Task<IRenderedComponent<SearchLookupHost>> Opened()
    {
        var cut = Mount();

        await cut.Find("input").FocusAsync(new FocusEventArgs());

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".mud-list-item").Count), TimeSpan.FromSeconds(5));

        return cut;
    }

    // The browser's own order: the vendor navigates on the key down and picks or closes on the
    // key up.
    static async Task Press(IRenderedComponent<SearchLookupHost> cut, string key)
    {
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = key });
        await cut.Find("input").KeyUpAsync(new KeyboardEventArgs { Key = key });
    }

    [Fact]
    public async Task ArrowDownMovesTheHighlightAndEnterPicksIt()
    {
        var cut = await Opened();

        await Press(cut, "ArrowDown");
        await Press(cut, "Enter");

        cut.WaitForAssertion(() => Assert.Equal(Beta.Id, _picked), TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ArrowUpWalksBackToThePreviousRow()
    {
        var cut = await Opened();

        await Press(cut, "ArrowDown");
        await Press(cut, "ArrowUp");
        await Press(cut, "Enter");

        cut.WaitForAssertion(() => Assert.Equal(Alpha.Id, _picked), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EscapeClosesTheListAndPicksNothing()
    {
        var cut = await Opened();

        await Press(cut, "Escape");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-popover-open")), TimeSpan.FromSeconds(5));
        Assert.Equal(Guid.Empty, _picked);
        Assert.NotEmpty(cut.FindAll("input"));
    }

    [Fact]
    public async Task TheBoxIsATabStop()
    {
        var cut = Mount();

        var tabindex = cut.Find("input").GetAttribute("tabindex");

        Assert.True(tabindex is null or "0", $"tabindex was {tabindex}");
        Assert.Null(cut.Find("input").GetAttribute("disabled"));

        await Task.CompletedTask;
    }
}

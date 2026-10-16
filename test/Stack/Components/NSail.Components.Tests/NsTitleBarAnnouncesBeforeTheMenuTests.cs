// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

[Route("/registers")]
public sealed class RegisterTestPage : ComponentBase;

/// <summary>The page's name reaches its surface before the GLYPH is derived, because deriving it
/// reads the nav tree and a contributor is free to go to the server for its own entries — an
/// arrangement this install stored, a count a kit computes. Announced after that read, the
/// surface answers null for as long as it takes, and a reader that names a file off it
/// (NsTable's export) wrote one named after the act instead of after the list.</summary>
public sealed class NsTitleBarAnnouncesBeforeTheMenuTests : BunitContext, IAsyncLifetime
{
    static readonly Glyph RegisterGlyph = NsIcons.People;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // The overlay's X pulls in a MudBlazor service that is IAsyncDisposable-only, which bUnit's
    // synchronous teardown throws on — every popover-hosting fixture here carries this note.
    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    sealed class FakeJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }
    }

    // A contributor that has not answered yet: the round trip a client's menu read really is,
    // held open so the window the bug lived in can be stood in.
    sealed class HeldNavContributor(params NavMenuItem[] items) : INavMenuContributor
    {
        readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<NavMenuItem>> GetItems()
        {
            await _held.Task;

            return items;
        }

        public void Answer()
        {
            _held.TrySetResult();
        }
    }

    SurfaceContext Setup(HeldNavContributor menu)
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsTitleBarAnnouncesBeforeTheMenuTests).Assembly, []));
        Services.AddSingleton<INavMenuContributor>(menu);
        Services.AddScoped<NavMenu>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/registers");

        return new SurfaceContext(null, navigation, routes, new FakeJs(), new SurfaceHistory());
    }

    [Fact]
    public void TheNameIsAnnouncedWhileTheMenuReadIsStillInFlight()
    {
        var menu = new HeldNavContributor(
            new NavMenuItem { Name = "Registers", Icon = RegisterGlyph, PageType = typeof(RegisterTestPage) });

        var surface = Setup(menu);

        Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Monedas"));

        Assert.Equal("Monedas", surface.Title);
    }

    // And the glyph still lands, on the second announcement: the first one carries whatever is
    // known, which on a page with no override is nothing.
    [Fact]
    public void TheGlyphArrivesWhenTheMenuAnswers()
    {
        var menu = new HeldNavContributor(
            new NavMenuItem { Name = "Registers", Icon = RegisterGlyph, PageType = typeof(RegisterTestPage) });

        var surface = Setup(menu);

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Monedas"));

        Assert.Null(surface.Icon);

        menu.Answer();

        cut.WaitForAssertion(() => Assert.Equal(RegisterGlyph, surface.Icon));
    }
}

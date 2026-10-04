// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#484: the app bar asked the vendor for <c>Color.Primary</c>, so the header wore
/// <c>mud-theme-primary</c> — the brand's primary at full strength, its text from
/// <c>primary-text</c> — and the appbar palette the brand actually fills was painted over and
/// never seen. Whatever a brand puts in its chrome slot reaches the eye through these classes,
/// which is why the bar takes no colour of its own.</summary>
public sealed class NsAppBarPaletteTests : BunitContext, IAsyncLifetime
{
    // The bar pulls MudBlazor's popover service into the container and that one only implements
    // IAsyncDisposable — a synchronous teardown throws on it by design.
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    [Fact]
    public void TheBarWearsTheAppbarPaletteAndNeverForcesPrimary()
    {
        var surface = Setup();

        var cut = Render<MainSurfaceChromeHost>(p => p
            .Add(x => x.Surface, surface)
            .Add(x => x.Title, "Personas"));

        var header = cut.Find("header");

        Assert.DoesNotContain("mud-theme-primary", header.ClassName, StringComparison.Ordinal);
        Assert.Contains("mud-appbar", header.ClassName, StringComparison.Ordinal);
    }

    SurfaceContext Setup()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Loading"] = "Cargando",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(NsAppBarPaletteTests).Assembly, []));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var routes = Services.GetRequiredService<RouteTable>();
        var navigation = Services.GetRequiredService<NavigationManager>();

        return new SurfaceContext(null, navigation, routes, new FakeJs(), new SurfaceHistory());
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
}

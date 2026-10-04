// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

public sealed class AsyncLoadArrivalTests : BunitContext, IAsyncLifetime
{
    public AsyncLoadArrivalTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static RouteTable BuildRouteTable()
    {
        return new(typeof(AsyncLoadArrivalTests).Assembly, Array.Empty<System.Reflection.Assembly>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFormWhoseModelArrivesAsync_IsNotDirty(bool swapInstance)
    {
        var host = Render<AsyncLoadFormHost>(p => p
            .Add(x => x.RouteTable, BuildRouteTable())
            .Add(x => x.SwapInstance, swapInstance));

        host.WaitForAssertion(() => Assert.Contains("already stored", host.Markup));

        Assert.False(host.Instance.Surface!.HasChanges, $"swapInstance={swapInstance} went dirty");
    }
}

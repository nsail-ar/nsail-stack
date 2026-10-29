// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Metadata;
using NSail.Storage.Settings;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Tests;

// The screen lists one row per declared store name, in the order the server answers, the
// default under its own words and every other name as its kit declared it.
public sealed class StorageSettingsPageTests : BunitContext, IAsyncLifetime
{
    readonly StoresMediator _mediator = new()
    {
        Stores =
        [
            new() { Name = StorageNames.Default, Provider = "file", Container = "/data/storage" },
            new() { Name = "reports", Provider = "s3", Container = "reports" },
            new() { Name = "backups", Provider = "file", Container = "/data/storage", UsesDefault = true },
        ],
    };

    public StorageSettingsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddStringsFromAssembly(typeof(StorageSettingsPage).Assembly);
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<Mediator>(_mediator);
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton(new RouteTable(typeof(StorageSettingsPage).Assembly, []));

        AddAuthorization().SetAuthorized("admin");
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void DrawsOneRowPerDeclaredStoreName()
    {
        Services.GetRequiredService<LanguageProvider>().Current = "es";

        var host = Host();

        host.WaitForAssertion(() => Assert.Equal(3, host.FindAll("tbody tr").Count));

        var rows = host.FindAll("tbody tr").Select(row => row.TextContent).ToList();

        Assert.Contains("Por defecto", rows[0]);
        Assert.Contains("Local folder", rows[0]);
        Assert.Contains("reports", rows[1]);
        Assert.Contains("S3-compatible", rows[1]);
        Assert.Contains("backups", rows[2]);
        Assert.Contains("✓", rows[2]);
    }

    // A store's form saves beside the list, so the list reads again and draws the new target.
    [Fact]
    public async Task ReadsAgainWhenAStoreIsSaved()
    {
        var host = Host();

        host.WaitForAssertion(() => Assert.Equal(3, host.FindAll("tbody tr").Count));

        _mediator.Stores.RemoveAt(2);

        await host.InvokeAsync(() => _mediator.Publish(new StorageStoreSaved { Name = "backups" }));

        host.WaitForAssertion(() => Assert.Equal(2, host.FindAll("tbody tr").Count));
        Assert.Equal(2, _mediator.Lists);
    }

    IRenderedComponent<StoresHost> Host()
    {
        return Render<StoresHost>(p => p.Add(x => x.RouteTable, Services.GetRequiredService<RouteTable>()));
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A search typed while the grid's previous page is still arriving: the new query
/// supersedes the one in flight rather than being refused by a Runner already running it — the
/// refusal the vendor swallowed, which left the grid showing rows the search excludes (found on
/// the price list sheet, where adding a price reloads the grid and the search follows at once).</summary>
public sealed class NsTableReloadWhileLoadingTests : BunitContext, IAsyncLifetime
{
    public NsTableReloadWhileLoadingTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>())]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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

    [Fact]
    public async Task A_query_asked_while_a_page_is_loading_is_run_rather_than_refused()
    {
        var gate = new TaskCompletionSource();

        var cut = Render<ReloadWhileLoadingHost>(p => p.Add(x => x.Gate, gate));

        cut.WaitForAssertion(() => Assert.Equal(1, cut.Instance.Queries));

        await cut.InvokeAsync(() => cut.Instance.Table.Reload(first: true));

        Assert.Equal(2, cut.Instance.Queries);

        gate.SetResult();
    }
}

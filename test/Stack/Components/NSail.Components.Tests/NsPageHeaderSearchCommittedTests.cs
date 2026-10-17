// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2339: a list that only acts on a deliberate Enter (a scanner's own key) needs
/// to tell it from the debounced typing `SearchTextChanged` already answers, and from the box
/// merely losing the cursor — `SearchCommitted` is that one gesture, wired through the header's
/// own search box onto `NsSearchField.OnEnterCommit`.</summary>
public sealed class NsPageHeaderSearchCommittedTests : BunitContext, IAsyncLifetime
{
    public NsPageHeaderSearchCommittedTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddScoped<NavMenu>();
        Services.AddSingleton(new RouteTable(typeof(ProbeGuidePage).Assembly, []));
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

    // The race the story names: a reader's burst lands the Enter inside the header's own
    // debounce window, before SearchTextChanged would ever have carried the code.
    [Fact]
    public async Task EnterInTheSearchBox_FiresSearchCommitted()
    {
        string? committed = null;

        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Catálogo")
            .Add(header => header.SearchText, (string?)null)
            .Add(header => header.SearchTextChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .Add(header => header.SearchCommitted, EventCallback.Factory.Create<string?>(this, text => committed = text)));

        var input = cut.Find(".ns-toolbar-search input");

        await cut.InvokeAsync(() => input.Input("7791234567890"));
        await cut.InvokeAsync(() => input.KeyDown("Enter"));

        Assert.Equal("7791234567890", committed);
    }

    [Fact]
    public async Task LeavingTheSearchBox_DoesNotFireSearchCommitted()
    {
        var fired = false;

        var cut = Render<NsPageHeader>(parameters => parameters
            .Add(header => header.Title, "Catálogo")
            .Add(header => header.SearchText, (string?)null)
            .Add(header => header.SearchTextChanged, EventCallback.Factory.Create<string?>(this, _ => { }))
            .Add(header => header.SearchCommitted, EventCallback.Factory.Create<string?>(this, _ => fired = true)));

        var input = cut.Find(".ns-toolbar-search input");

        await cut.InvokeAsync(() => input.Input("buscado"));
        await cut.InvokeAsync(() => input.Blur());

        Assert.False(fired);
    }
}

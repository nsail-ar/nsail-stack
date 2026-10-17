// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#2338: a list's own search box is where a navigation's arrival hands the
/// cursor — NsPageHeader's built-in NsSearchField carries the marker by default, and
/// ArrivalFocus turns it off for a screen whose first act is not searching.</summary>
public sealed class NsPageHeaderArrivalFocusTests : BunitContext, IAsyncLifetime
{
    public NsPageHeaderArrivalFocusTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>())]));
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

    static string? InputArrivalMark(IRenderedComponent<NsPageHeader> cut)
    {
        return cut.Find(".ns-toolbar-search input").GetAttribute("data-ns-arrival-focus");
    }

    [Fact]
    public void TheBuiltInSearchBox_CarriesTheArrivalMarkByDefault()
    {
        var cut = Render<NsPageHeader>(p => p
            .Add(h => h.Title, "Catálogo")
            .Add(h => h.SearchText, string.Empty)
            .Add(h => h.SearchTextChanged, _ => { }));

        Assert.Equal("true", InputArrivalMark(cut));
    }

    [Fact]
    public void ArrivalFocusOff_LeavesTheSearchBoxUnmarked()
    {
        var cut = Render<NsPageHeader>(p => p
            .Add(h => h.Title, "Catálogo")
            .Add(h => h.ArrivalFocus, false)
            .Add(h => h.SearchText, string.Empty)
            .Add(h => h.SearchTextChanged, _ => { }));

        Assert.Null(InputArrivalMark(cut));
    }
}

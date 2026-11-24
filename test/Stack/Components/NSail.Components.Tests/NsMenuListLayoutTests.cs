// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A menu nested under a block stands one level deeper than the list's direct child, and
/// the stylesheet's rule for a nested menu has to reach it there.</summary>
public sealed class NsMenuListLayoutTests : BunitContext, IAsyncLifetime
{
    public NsMenuListLayoutTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton<IStringSource>(new FixedStrings(new Dictionary<string, string>()));
        Services.AddSingleton<StringCatalog>();
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsMenuListLayoutTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider's service is IAsyncDisposable-only, so bUnit's synchronous teardown
    // cannot dispose it.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public async Task ANestedMenuUnderABlockIsNotAChildOfTheListButTheRuleReachesIt()
    {
        var cut = Render<MenuNestedInBlockHost>();

        await cut.Find(".mud-menu button").ClickAsync(new MouseEventArgs());

        var list = cut.Find(".ns-menu-list");

        Assert.Empty(list.QuerySelectorAll(":scope > .ns-menu-keys > .mud-menu"));
        Assert.NotEmpty(list.QuerySelectorAll(".ns-menu-keys > .mud-menu"));
        Assert.Matches(
            new Regex(@"\.ns-menu-list\s+\.ns-menu-keys\s*>\s*\.mud-menu\s*\{[^}]*width:\s*100%", RegexOptions.None),
            HouseStylesheet.Read());
    }

    [Fact]
    public void TheListStacksItsRowsAsAColumn()
    {
        Assert.Matches(
            new Regex(@"\.ns-menu-list\s*\{[^}]*flex-direction:\s*column", RegexOptions.None),
            HouseStylesheet.Read());
    }
}

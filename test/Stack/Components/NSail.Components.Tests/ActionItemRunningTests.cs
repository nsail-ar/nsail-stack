// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>An act whose work outlives its click — a backup still on the box — is held running by
/// the page's own state (ActionItem.Running), and wears the face NsAction draws while a click is
/// in flight, in every presenter that draws the item: the turning glyph, the loading word, and a
/// press that reaches nothing.</summary>
public sealed class ActionItemRunningTests : BunitContext, IAsyncLifetime
{
    const string Loading = "Cargando";

    public ActionItemRunningTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Loading"] = Loading,
            ["Common.MoreActions"] = "More actions",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(ActionItemRunningTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider pulls in a MudBlazor service that is IAsyncDisposable-only and
    // internal, so bUnit's synchronous teardown cannot dispose it (NsActionColumnTests' note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    int _calls;

    ActionItem Backup(bool running = true)
    {
        return new ActionItem
        {
            Name = "Backup",
            Icon = NsIcons.Sync,
            Running = running,
            OnClick = () =>
            {
                _calls++;

                return Task.CompletedTask;
            },
        };
    }

    [Fact]
    public async Task ARunningActWearsTheInFlightFaceAndAPressNeverReachesOnClick()
    {
        var cut = Render<ActionRunHost>(p => p.Add(x => x.Action, Backup()));

        var button = cut.Find("button");

        Assert.Contains(NsIcons.Progress.Markup, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(NsIcons.Sync.Markup, cut.Markup, StringComparison.Ordinal);
        Assert.Equal(Loading, button.GetAttribute("aria-label"));
        Assert.True(button.HasAttribute("disabled"));

        await cut.InvokeAsync(() => cut.Find("button").Click());

        Assert.Equal(0, _calls);
    }

    [Fact]
    public async Task TheSameActHandedBackNotRunningReturnsToItsOwnFace()
    {
        var cut = Render<ActionRunHost>(p => p.Add(x => x.Action, Backup()));

        cut.Render(p => p.Add(x => x.Action, Backup(running: false)));

        Assert.Contains(NsIcons.Sync.Markup, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(NsIcons.Progress.Markup, cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.Find("button").HasAttribute("disabled"));

        await cut.InvokeAsync(() => cut.Find("button").Click());

        Assert.Equal(1, _calls);
    }

    [Fact]
    public void ARunningLeadingItemKeepsTheFaceInTheToolbar()
    {
        var cut = Render<ActionColumnHost>(p => p.Add(x => x.Leading, [Backup()]));

        var button = cut.Find(".ns-action-toolbar button");

        Assert.NotNull(button.QuerySelector(".ns-spin"));
        Assert.True(button.HasAttribute("disabled"));
    }

    // The constant passes through WithDoorGlyph, which copies an item field by field: a field
    // it does not copy is lost there, so a running door handed no glyph is the case to hold.
    [Fact]
    public void ARunningConstantKeepsTheFaceThroughTheDoorsChevron()
    {
        var door = new ActionItem { Name = "Ficha", PageType = typeof(ProbeOtherPage), Running = true };

        var cut = Render<ActionColumnHost>(p => p.Add(x => x.Constant, door));

        var toolbar = cut.Find(".ns-action-toolbar");

        Assert.Empty(toolbar.QuerySelectorAll("a"));
        Assert.NotNull(toolbar.QuerySelector(".ns-spin"));
        Assert.True(toolbar.QuerySelector("button")!.HasAttribute("disabled"));
    }

    [Fact]
    public async Task ARunningActInTheKebabIsADisabledRow()
    {
        var leading = Enumerable
            .Range(1, 3)
            .Select(i => new ActionItem { Name = $"Verb{i}", OnClick = () => Task.CompletedTask })
            .Append(Backup())
            .ToList();

        var cut = Render<ActionColumnHost>(p => p.Add(x => x.Leading, leading));

        await cut.Find(".ns-action-toolbar .mud-menu button").ClickAsync(new MouseEventArgs());

        var row = cut.Find(".ns-menu-list .ns-menu-item");

        Assert.NotNull(row.QuerySelector(".ns-spin"));
        Assert.Equal("true", row.GetAttribute("aria-disabled"));

        await cut.Find(".ns-menu-list .ns-menu-item").ClickAsync(new MouseEventArgs());

        Assert.Equal(0, _calls);
    }
}

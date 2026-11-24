// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A toolbar asked for buttons draws a contributed page act and a contributed command
/// as one filled, worded button — still an anchor and a button — spaced like NsButtons in a
/// horizontal NsStack at Gap Sm, each at its own item's As. A toolbar not asked keeps the icon
/// row, where an item's As changes nothing.</summary>
public sealed class NsActionToolbarButtonsTests : BunitContext, IAsyncLifetime
{
    public NsActionToolbarButtonsTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsActionToolbarButtonsTests).Assembly, []));
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

    static ActionItem Page(NsAs intention = NsAs.Default)
    {
        return new ActionItem { Name = "Collect", PageType = typeof(ProbeOtherPage), Writes = false, As = intention };
    }

    static ActionItem Command(string name, NsAs intention = NsAs.Default)
    {
        return new ActionItem { Name = name, OnClick = () => Task.CompletedTask, As = intention };
    }

    IRenderedComponent<NsActionToolbar> RenderToolbar(bool buttons, params ActionItem[] items)
    {
        return Render<NsActionToolbar>(p => p
            .Add(x => x.Actions, items)
            .Add(x => x.Buttons, buttons));
    }

    [Fact]
    public void APageActAndACommandWearOneFilledWordedButton()
    {
        var cut = RenderToolbar(true, Page(), Command("Note"));

        var link = cut.Find(".ns-action-toolbar a");
        var button = cut.Find(".ns-action-toolbar button");

        // One vendor root is one casing: the class an NsButton beside them carries too.
        Assert.Contains("mud-button-root", link.ClassList);
        Assert.Contains("mud-button-root", button.ClassList);
        Assert.Contains("mud-button-filled", link.ClassList);
        Assert.Contains("mud-button-filled", button.ClassList);
        Assert.Contains("Collect", link.TextContent, StringComparison.Ordinal);
        Assert.Contains("Note", button.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("ns-square", link.ClassList);
        Assert.DoesNotContain("ns-square", button.ClassList);
    }

    // Read off NsStack rather than written here: the row's own buttons stand in one, so the
    // contributed ones must sit at whatever distance it puts between them.
    [Fact]
    public void TwoActsAreSpacedLikeNsButtonsInAStackAtGapSm()
    {
        var stack = Render<NsStack>(p => p
            .Add(x => x.Horizontal, true)
            .Add(x => x.Gap, NsSize.Sm));
        var gap = Assert.Single(stack.Find("div").ClassList, name => name.StartsWith("gap-", StringComparison.Ordinal));

        var cut = RenderToolbar(true, Page(), Command("Note"));
        var toolbar = cut.Find(".ns-action-toolbar");

        Assert.Contains(gap, toolbar.ClassList);
        Assert.DoesNotContain("gap-1", toolbar.ClassList);
    }

    [Fact]
    public void AnActNamingMainAloneWearsTheAccentFill()
    {
        var cut = RenderToolbar(true, Command("Note"), Command("Collect", NsAs.Main), Command("Send"));

        var buttons = cut.FindAll(".ns-action-toolbar button");

        Assert.Equal(3, buttons.Count);
        Assert.DoesNotContain("mud-button-filled-primary", buttons[0].ClassList);
        Assert.Contains("mud-button-filled-primary", buttons[1].ClassList);
        Assert.DoesNotContain("mud-button-filled-primary", buttons[2].ClassList);
        Assert.Contains("mud-button-filled-default", buttons[0].ClassList);
        Assert.Contains("mud-button-filled-default", buttons[2].ClassList);
    }

    [Fact]
    public void APageActNamingMainWearsTheAccentFill()
    {
        var cut = RenderToolbar(true, Page(NsAs.Main));

        Assert.Contains("mud-button-filled-primary", cut.Find(".ns-action-toolbar a").ClassList);
    }

    [Fact]
    public void WithoutButtonsTheRowKeepsItsGapAndItsFlatActs()
    {
        var cut = RenderToolbar(false, Page(), Command("Note"));

        Assert.Contains("gap-1", cut.Find(".ns-action-toolbar").ClassList);
        Assert.Empty(cut.FindAll(".ns-action-toolbar .mud-button-filled"));
    }

    // A host that only words its acts asked for the word, not for a button: it keeps the flat
    // rung, an inline anchor and a text button.
    [Fact]
    public void AWordedRowWithoutButtonsIgnoresTheItemsIntention()
    {
        var cut = Render<NsActionToolbar>(p => p
            .Add(x => x.Actions, [Page(NsAs.Main), Command("Note", NsAs.Main)])
            .Add(x => x.Breakpoint, NsBreakpoint.Always));

        Assert.Empty(cut.FindAll(".ns-action-toolbar .mud-button-filled"));
        Assert.Contains("gap-1", cut.Find(".ns-action-toolbar").ClassList);
    }

    [Fact]
    public void AnIntentionNeverChangesAnIconOnlyGlyph()
    {
        var plain = RenderToolbar(false, new ActionItem { Name = "Note", Icon = NsIcons.Edit, OnClick = () => Task.CompletedTask });
        var main = RenderToolbar(false, new ActionItem { Name = "Note", Icon = NsIcons.Edit, OnClick = () => Task.CompletedTask, As = NsAs.Main });

        Assert.Equal(plain.Find(".ns-action-toolbar button").ClassName, main.Find(".ns-action-toolbar button").ClassName);
        Assert.Equal(plain.Find(".ns-action-toolbar svg").OuterHtml, main.Find(".ns-action-toolbar svg").OuterHtml);
    }
}

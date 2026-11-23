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

/// <summary>An ActionItem.Url is honoured wherever the toolbar draws the item: on the row as an
/// anchor into a new browser tab, and in the kebab as the menu's own anchor row.</summary>
public sealed class NsActionToolbarAddressTests : BunitContext, IAsyncLifetime
{
    public NsActionToolbarAddressTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsActionToolbarAddressTests).Assembly, []));
        Services.AddScoped<RootSurface>();
        Services.AddScoped<SurfaceHistory>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // The kebab's list is a popover, and MudPopoverProvider resolves a service bUnit's
    // synchronous teardown cannot dispose (NsMenuTests carries the same note).
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static ActionItem Paper(string name)
    {
        return new ActionItem { Name = name, Icon = NsIcons.Print, Url = $"/files/{name}.pdf", Writes = false };
    }

    [Fact]
    public void AnAddressOnTheRowIsAnAnchorIntoANewTab()
    {
        var cut = Render<NsActionToolbar>(parameters => parameters.Add(toolbar => toolbar.Actions, [Paper("Receipt")]));

        var anchor = cut.Find("a[href='/files/Receipt.pdf']");

        Assert.Equal("_blank", anchor.GetAttribute("target"));
        Assert.Contains("noopener", anchor.GetAttribute("rel") ?? string.Empty, StringComparison.Ordinal);
    }

    // The same item answers the same on the row and in the kebab: an address WebAddress refuses
    // is drawn in neither, including one NsLink alone would repair or leave inert.
    [Theory]
    [InlineData("files/report.pdf")]
    [InlineData("tel:+5491100000000")]
    [InlineData("#report")]
    [InlineData("javascript:alert(1)")]
    public void AnAddressNoDoorMayOpenIsWithheldOnTheRowAndInTheKebab(string address)
    {
        var refused = new ActionItem { Name = "Refused", Icon = NsIcons.Print, Url = address, Writes = false };

        var row = Render<NsActionToolbar>(parameters => parameters.Add(toolbar => toolbar.Actions, [refused]));

        Assert.Empty(row.FindAll("a, button"));

        var cut = Render<ToolbarHost>(parameters => parameters.Add(host => host.Actions, [Paper("Receipt"), Paper("Invoice"), Paper("Ticket"), refused]));

        Assert.Empty(cut.FindAll(".mud-menu"));
        Assert.Equal(3, cut.FindAll("a[target='_blank']").Count);
    }

    [Fact]
    public void AnAddressDoorWearsTheChevron()
    {
        var door = new ActionItem { Name = "Paper", Url = "/files/Paper.pdf", Writes = false };

        var cut = Render<NsActionToolbar>(parameters => parameters.Add(toolbar => toolbar.Door, door));

        var anchor = cut.Find("a[href='/files/Paper.pdf']");

        Assert.Equal("_blank", anchor.GetAttribute("target"));
        Assert.NotEmpty(anchor.QuerySelectorAll("svg"));
    }

    [Fact]
    public async Task AnAddressInTheKebabIsAnAnchorRow()
    {
        var cut = Render<ToolbarHost>(parameters => parameters.Add(host => host.Actions, [Paper("Receipt"), Paper("Invoice"), Paper("Ticket"), Paper("Label")]));

        await cut.Find(".mud-menu button").ClickAsync(new MouseEventArgs());

        var row = cut.Find("a.mud-menu-item[href='/files/Label.pdf']");

        Assert.Equal("_blank", row.GetAttribute("target"));
        Assert.Contains("noopener", row.GetAttribute("rel") ?? string.Empty, StringComparison.Ordinal);
    }
}

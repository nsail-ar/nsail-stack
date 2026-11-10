// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

public sealed class NsDropdownTests : BunitContext, IAsyncLifetime
{
    public NsDropdownTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
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

    // The panel is the vendor's popover, drawn by its provider and not inside the wrapper.
    IRenderedComponent<MudBlazor.MudPopoverProvider> _provider = default!;

    IRenderedComponent<NsDropdown> Render(bool open, Action<bool>? changed = null)
    {
        _provider = Render<MudBlazor.MudPopoverProvider>();

        return Render<NsDropdown>(p => p
            .Add(x => x.Open, open)
            .Add(x => x.OpenChanged, EventCallback.Factory.Create<bool>(this, changed ?? (_ => { })))
            .Add(x => x.Anchor, b => b.AddMarkupContent(0, "<input class=\"field\" />"))
            .Add(x => x.ChildContent, b => b.AddMarkupContent(0, "<div class=\"row\">one</div>")));
    }

    [Fact]
    public void Closed_TheAnchorIsThereAndThePanelIsNot()
    {
        var cut = Render(open: false);

        Assert.NotEmpty(cut.FindAll(".ns-dropdown > .field"));
        Assert.Empty(_provider.FindAll(".ns-dropdown-panel"));
    }

    [Fact]
    public void Open_RendersThePanelAfterTheAnchor()
    {
        var cut = Render(open: true);

        Assert.Equal("one", _provider.Find(".ns-dropdown-panel .row").TextContent);
        Assert.Null(_provider.Find(".ns-dropdown-panel").GetAttribute("role"));
        Assert.Equal("field", cut.Find(".ns-dropdown").Children[0].ClassName);
        Assert.Contains("max-height:300px", _provider.Find(".ns-dropdown-panel").GetAttribute("style"));
    }

    [Fact]
    public void Open_FollowsTheParameter()
    {
        var cut = Render(open: true);

        cut.Render(p => p.Add(x => x.Open, false));

        Assert.Empty(_provider.FindAll(".ns-dropdown-panel"));
    }

    [Fact]
    public async Task Escape_AsksTheHostToClose()
    {
        bool? seen = null;
        var cut = Render(open: true, v => seen = v);

        await cut.Find(".ns-dropdown").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(seen);
        Assert.Empty(_provider.FindAll(".ns-dropdown-panel"));
    }

    [Fact]
    public void Open_HangsUnderTheAnchorAtLeastAsWideAsIt()
    {
        var cut = Render(open: true);

        var popover = cut.FindComponent<MudBlazor.MudPopover>().Instance;

        Assert.Equal(MudBlazor.Origin.BottomLeft, popover.AnchorOrigin);
        Assert.Equal(MudBlazor.Origin.TopLeft, popover.TransformOrigin);
        Assert.Equal(MudBlazor.DropdownWidth.Adaptive, popover.RelativeWidth);
    }

    [Fact]
    public void Open_MarksTheWrapperForTheKeyboardPolicy()
    {
        var cut = Render(open: true);

        Assert.Equal("true", cut.Find(".ns-dropdown").GetAttribute("data-ns-dropdown-open"));

        cut.Render(p => p.Add(x => x.Open, false));

        Assert.Null(cut.Find(".ns-dropdown").GetAttribute("data-ns-dropdown-open"));
    }

    [Fact]
    public async Task Escape_InsideThePanelAsksTheHostToClose()
    {
        bool? seen = null;
        Render(open: true, v => seen = v);

        await _provider.Find(".ns-dropdown-body").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(seen);
    }

    [Fact]
    public void AClickOutside_AsksTheHostToClose()
    {
        bool? seen = null;
        Render(open: true, v => seen = v);

        _provider.Find(".mud-overlay").Click();

        Assert.False(seen);
    }

    // The catcher sits one tier under the popover ceiling; the panel names the ceiling itself, or
    // the vendor seats it at its default tier, under the catcher, and a click on a row closes it.
    [Fact]
    public void ThePanel_SitsAboveItsCatcher()
    {
        Render(open: true);

        var panel = _provider.Find(".ns-dropdown-panel").GetAttribute("style") ?? string.Empty;
        var catcher = _provider.Find(".mud-overlay").GetAttribute("style") ?? string.Empty;

        Assert.Contains($"z-index:{SurfaceContext.VendorZIndex}", panel.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Contains($"z-index:{SurfaceContext.VendorZIndex - 1}", catcher.Replace(" ", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void TheWrapperIsTheFlexItemTheFieldWas()
    {
        var css = HouseStylesheet.Read();
        var rule = css[css.IndexOf(".ns-dropdown {", StringComparison.Ordinal)..];
        rule = rule[..rule.IndexOf('}', StringComparison.Ordinal)];

        Assert.Contains("flex: 1 1 auto", rule, StringComparison.Ordinal);
        Assert.Contains("min-width: 0", rule, StringComparison.Ordinal);
    }
}

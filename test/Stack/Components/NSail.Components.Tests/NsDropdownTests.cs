// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NSail.Components.Tests;

public sealed class NsDropdownTests : BunitContext
{
    IRenderedComponent<NsDropdown> Render(bool open, Action<bool>? changed = null)
    {
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
        Assert.Empty(cut.FindAll(".ns-dropdown-panel"));
    }

    [Fact]
    public void Open_RendersThePanelAfterTheAnchor()
    {
        var cut = Render(open: true);

        Assert.Equal("one", cut.Find(".ns-dropdown-panel .row").TextContent);
        Assert.Equal("listbox", cut.Find(".ns-dropdown-panel").GetAttribute("role"));
        Assert.Equal("field", cut.Find(".ns-dropdown").Children[0].ClassName);
    }

    [Fact]
    public void Open_FollowsTheParameter()
    {
        var cut = Render(open: true);

        cut.Render(p => p.Add(x => x.Open, false));

        Assert.Empty(cut.FindAll(".ns-dropdown-panel"));
    }

    [Fact]
    public void Escape_AsksTheHostToClose()
    {
        bool? seen = null;
        var cut = Render(open: true, v => seen = v);

        cut.Find(".ns-dropdown").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(seen);
        Assert.Empty(cut.FindAll(".ns-dropdown-panel"));
    }

    [Fact]
    public void ThePanelIsBoundedAndScrolls()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (!File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
        {
            directory = directory.Parent!;
        }

        var css = File.ReadAllText(Path.Combine(
            directory.FullName,
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));
        var rule = css[css.IndexOf(".ns-dropdown-panel {", StringComparison.Ordinal)..];
        rule = rule[..rule.IndexOf('}', StringComparison.Ordinal)];

        Assert.Contains("max-height", rule, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto", rule, StringComparison.Ordinal);
        Assert.Contains("top: 100%", rule, StringComparison.Ordinal);
        Assert.Contains("left: 0", rule, StringComparison.Ordinal);
    }
}

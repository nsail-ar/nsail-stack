// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#787: a block's height is its time, so the only box that can be clipped on a
/// line boundary is one INSIDE it — .ns-time-block-lines, capped in ns-mud.css at a whole number
/// of the block's own lines. A browser is what proves no line is half drawn
/// (TodayAgendaBlockLinesTests); what is pinned here is the markup that rule keys off, which is
/// the half a stylesheet cannot defend on its own: the box exists, it is the block's own child
/// so the cap resolves against the block's content box, and everything the caller handed over is
/// inside it — on the clickable branch as much as the plain one.</summary>
public sealed class NsTimeBlockLinesTests : BunitContext
{
    static readonly DateOnly Day = new(2026, 9, 4);

    public NsTimeBlockLinesTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    IRenderedComponent<NsTimeGrid> Grid(Action<ComponentParameterCollectionBuilder<NsTimeBlock>> block)
    {
        return Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0))
            .AddChildContent<NsTimeBlock>(b =>
            {
                b.Add(x => x.Starts, Day.ToDateTime(new TimeOnly(15, 30)));
                b.Add(x => x.Duration, TimeSpan.FromMinutes(30));
                b.Add<RenderFragment>(x => x.ChildContent, builder =>
                {
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "class", "first");
                    builder.AddContent(2, "15:30 Sesión");
                    builder.CloseElement();

                    builder.OpenElement(3, "span");
                    builder.AddAttribute(4, "class", "second");
                    builder.AddContent(5, "Confirmada · Lagos 1034");
                    builder.CloseElement();
                });

                block(b);
            }));
    }

    /// <summary>The percentage the cap is measured in resolves against the block's own content
    /// box, so a box between the two would measure something else entirely.</summary>
    [Fact]
    public void TheLinesBoxIsTheBlocksOwnChild()
    {
        var cut = Grid(_ => { });

        var lines = cut.Find(".ns-time-block > .ns-time-block-lines");

        Assert.NotNull(lines);
    }

    [Fact]
    public void EverythingTheCallerHandedOverIsInsideIt()
    {
        var cut = Grid(_ => { });

        var lines = cut.Find(".ns-time-block-lines");

        Assert.NotNull(lines.QuerySelector(".first"));
        Assert.NotNull(lines.QuerySelector(".second"));
    }

    /// <summary>The clickable branch is a &lt;button&gt; and is where the rule matters most —
    /// the Hoy card's turnos and the Calendario's are all clickable.</summary>
    [Fact]
    public void AClickableBlockWrapsItsLinesToo()
    {
        var cut = Grid(b => b.Add(x => x.OnClick, () => { }));

        var lines = cut.Find("button.ns-time-block > .ns-time-block-lines");

        Assert.NotNull(lines.QuerySelector(".first"));
    }

    /// <summary>A free interval and somebody else's calendar are blocks like any other: the rule
    /// belongs to the block, so neither kind is an exception to it.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ABackgroundOrReadOnlyBlockWrapsItsLinesToo(bool background, bool readOnly)
    {
        var cut = Grid(b => b
            .Add(x => x.Background, background)
            .Add(x => x.ReadOnly, readOnly));

        var lines = cut.Find(".ns-time-block > .ns-time-block-lines");

        Assert.NotNull(lines.QuerySelector(".first"));
    }
}

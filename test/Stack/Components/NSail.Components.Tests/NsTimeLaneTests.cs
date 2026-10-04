// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The width of a block is decided by what it COLLIDES with, never by what it is
/// (Emmanuel, 2026-08-12: an external event alone in its column rendered at half width — "al
/// estar solo, debería ocupar toda la columna"). The first cut of the overlay pinned read-only
/// blocks to a fixed lane so an appointment could not hide them; NsTimeLanes answers that same
/// question by measuring the actual overlaps, so the cost is paid only where there is something
/// to hide behind.</summary>
public sealed class NsTimeLaneTests : BunitContext
{
    static readonly DateOnly Day = new(2026, 8, 12);

    public NsTimeLaneTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>The reported case: nothing overlaps it, so nothing narrows it.</summary>
    [Fact]
    public void ABlockThatOverlapsNothing_TakesTheWholeColumn()
    {
        var lanes = NsTimeLanes.Assign([Item(9, 60), Item(15, 60)]);

        Assert.All(lanes, lane => Assert.Equal(1, lane.Count));
        Assert.All(lanes, lane => Assert.Equal(0, lane.Index));
    }

    /// <summary>Half-open, so noon-to-one and one-to-two do not touch: they are drawn one below
    /// the other and neither hides a pixel of the other.</summary>
    [Fact]
    public void BlocksThatMeetAtAnEdge_DoNotShareAnything()
    {
        var lanes = NsTimeLanes.Assign([Item(12, 60), Item(13, 60)]);

        Assert.All(lanes, lane => Assert.Equal(1, lane.Count));
    }

    [Fact]
    public void TwoBlocksOverTheSameHour_SplitTheColumn()
    {
        var lanes = NsTimeLanes.Assign([Item(10, 60), Item(10, 60)]);

        Assert.Equal(new NsTimeLane(0, 2), lanes[0]);
        Assert.Equal(new NsTimeLane(1, 2), lanes[1]);
    }

    /// <summary>Priority is the consumer's own precedence and the only meaning this layout
    /// carries: the practice's turno keeps the leading lane whatever order the blocks arrived
    /// in.</summary>
    [Fact]
    public void APriorityBlock_TakesTheLeadingLane_WhateverOrderItArrivedIn()
    {
        var lanes = NsTimeLanes.Assign([Item(10, 60), Item(10, 60, priority: true)]);

        Assert.Equal(1, lanes[0].Index);
        Assert.Equal(0, lanes[1].Index);
    }

    /// <summary>A cluster is a local fact: the pair that collides splits its own share and says
    /// nothing about a block hours away, which keeps the column whole.</summary>
    [Fact]
    public void ACollidingPair_CostsNothingToABlockElsewhereInTheDay()
    {
        var lanes = NsTimeLanes.Assign([Item(9, 120), Item(10, 60), Item(18, 60)]);

        Assert.Equal(2, lanes[0].Count);
        Assert.Equal(2, lanes[1].Count);
        Assert.Equal(1, lanes[2].Count);
    }

    /// <summary>Overlap is transitive, so A touching B and B touching C makes ONE cluster of
    /// three even though A and C never meet — and the whole cluster is measured against the same
    /// width. That is what the transitivity buys: C ending up in a cluster of its own would be
    /// drawn full width, straight over B. It does NOT buy a third lane — A and C do not collide,
    /// so they share one, which is the layout every mature calendar draws.</summary>
    [Fact]
    public void AChainOfOverlaps_IsOneClusterAndOneSharedWidth()
    {
        var lanes = NsTimeLanes.Assign([Item(9, 120), Item(10, 120), Item(11, 120)]);

        Assert.All(lanes, lane => Assert.Equal(2, lane.Count));
        Assert.Equal(lanes[0].Index, lanes[2].Index);
        Assert.NotEqual(lanes[0].Index, lanes[1].Index);
    }

    /// <summary>A lane freed by a block that already ended is taken again rather than opening a
    /// third: the cluster is one long block with two short ones stacked beside it.</summary>
    [Fact]
    public void ALaneIsReusedOnceItsBlockHasEnded()
    {
        var lanes = NsTimeLanes.Assign([Item(9, 240), Item(9, 60), Item(11, 60)]);

        Assert.All(lanes, lane => Assert.Equal(2, lane.Count));
        Assert.Equal(lanes[1].Index, lanes[2].Index);
    }

    /// <summary>The lane travels as two custom properties; the division itself stays in
    /// ns-mud.css, the trade the block's top and height already make.</summary>
    [Fact]
    public void TheBlockWritesItsLaneAsCustomProperties()
    {
        var cut = RenderBlock(new NsTimeLane(1, 3));

        var style = cut.Find(".ns-time-block").GetAttribute("style") ?? string.Empty;

        Assert.Contains("--ns-time-lane: 1", style, StringComparison.Ordinal);
        Assert.Contains("--ns-time-lanes: 3", style, StringComparison.Ordinal);
    }

    /// <summary>And writes neither when it has the column to itself, so the stylesheet's own
    /// fallback is what draws it: the default costs no markup.</summary>
    [Fact]
    public void ABlockAloneWritesNoLaneAtAll()
    {
        var cut = RenderBlock(default);

        var style = cut.Find(".ns-time-block").GetAttribute("style") ?? string.Empty;

        Assert.DoesNotContain("--ns-time-lane", style, StringComparison.Ordinal);
    }

    /// <summary>The stylesheet divides by the lane count and falls back to one lane — which is
    /// the whole column minus its gutters — so a block that declares nothing is full width.</summary>
    [Fact]
    public void TheStylesheetDividesTheColumnByTheLaneCount()
    {
        var rule = ReadRule(".ns-time-block {");

        Assert.Contains("var(--ns-time-lanes, 1)", rule, StringComparison.Ordinal);
        Assert.Contains("var(--ns-time-lane, 0)", rule, StringComparison.Ordinal);
    }

    /// <summary>And the read-only rule owns no geometry any more: being somebody else's time is
    /// a look, not a width. Nor pointer-events:none — the click opens the detail dialog, and a
    /// block that cannot be clicked leaves that dialog reachable by keyboard alone.</summary>
    [Fact]
    public void ReadOnlyOwnsNoWidthAndNoPointerBlock()
    {
        var rule = ReadRule(".ns-time-block-readonly {");

        Assert.DoesNotContain("inset-inline", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("inline-size", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("width", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("pointer-events", rule, StringComparison.Ordinal);
    }

    IRenderedComponent<NsTimeGrid> RenderBlock(NsTimeLane lane)
    {
        return Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add<NsTimeBlock>(x => x.ChildContent, block => block
                .Add(x => x.Starts, Day.ToDateTime(new TimeOnly(10, 0)))
                .Add(x => x.Duration, TimeSpan.FromHours(1))
                .Add(x => x.Lane, lane)));
    }

    static NsTimeLaneItem Item(int hour, int minutes, bool priority = false)
    {
        return new NsTimeLaneItem(Day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromMinutes(minutes), priority);
    }

    static string ReadRule(string selector)
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{selector}' was not found in the stylesheet.");

        var end = css.IndexOf('}', start);
        Assert.True(end >= 0, $"'{selector}' has no closing brace.");

        return css[start..end];
    }

    static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}

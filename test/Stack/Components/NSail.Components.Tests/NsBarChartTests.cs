// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Localization;

namespace NSail.Components.Tests;

public sealed class NsBarChartTests : BunitContext
{
    static readonly NsBarSeries[] Series =
    [
        new("a", "Alpha", NsChartColor.Primary),
        new("b", "Beta", NsChartColor.Info)
    ];

    public NsBarChartTests()
    {
        Services.AddStringsFromAssembly(typeof(NsBarChart).Assembly);
    }

    static NsBarBucket Bucket(string label, double a, double b)
    {
        return new NsBarBucket(label, new Dictionary<string, double> { ["a"] = a, ["b"] = b });
    }

    static double Height(AngleSharp.Dom.IElement rect)
    {
        // Each segment is drawn a pixel short at both ends so neighbours do not touch.
        return double.Parse(rect.GetAttribute("height")!, CultureInfo.InvariantCulture) + 2;
    }

    [Fact]
    public void SegmentsOfABucketStackToItsTotal()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, Series)
            .Add(c => c.Buckets, new[] { Bucket("10h", 4, 6), Bucket("11h", 2, 0) }));

        var rects = cut.FindAll("rect");
        Assert.Equal(3, rects.Count);

        var first = rects.Where(r => r.GetAttribute("x") == rects[0].GetAttribute("x")).ToList();
        Assert.Equal(2, first.Count);
        var scale = Height(rects[0]) / 4;
        Assert.Equal(10 * scale, first.Sum(Height), 0);
        Assert.Equal(2 * scale, Height(rects[2]), 0);
        Assert.Contains("10h · Alpha: 4", rects[0].QuerySelector("title")!.TextContent);
    }

    [Fact]
    public void LegendListsTheSeries()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, Series)
            .Add(c => c.Buckets, new[] { Bucket("10h", 1, 1) }));

        var keys = cut.FindAll(".ns-bar-chart-key").Select(k => k.TextContent).ToList();
        Assert.Equal(["Alpha", "Beta"], keys);
        Assert.Contains("var(--mud-palette-info)", cut.FindAll(".ns-bar-chart-swatch")[1].GetAttribute("style"));
    }

    [Fact]
    public void MarkerRendersWithItsText()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, Series)
            .Add(c => c.Buckets, new[] { Bucket("10h", 1, 1), Bucket("11h", 1, 1) })
            .Add(c => c.Markers, new[] { new NsBarMarker(1, "started on 1.2.3") }));

        Assert.Equal("started on 1.2.3", cut.Find(".ns-bar-chart-marker title").TextContent);
    }

    [Fact]
    public void EmptyDataDrawsTheAxisAndTheEmptyLine()
    {
        var cut = Render<NsBarChart>(p => p.Add(c => c.Series, Series));

        Assert.Empty(cut.FindAll("rect"));
        Assert.NotEmpty(cut.FindAll(".ns-bar-chart-grid"));
        Assert.Equal("No data", cut.Find(".ns-bar-chart-empty").TextContent);
    }

    [Fact]
    public void BucketsWithNothingInThemStillSayNoData()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, Series)
            .Add(c => c.Buckets, new[] { Bucket("10h", 0, 0), Bucket("11h", 0, 0) }));

        Assert.Empty(cut.FindAll("rect"));
        Assert.Equal("No data", cut.Find(".ns-bar-chart-empty").TextContent);
    }

    [Fact]
    public void AxisLabelsOfWholeCountsAreWhole()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, Series)
            .Add(c => c.Buckets, new[] { Bucket("10h", 1, 0) }));

        Assert.Equal(["0", "1", "2"], cut.FindAll(".ns-bar-chart-axis").Take(3).Select(t => t.TextContent).ToList());
    }

    [Fact]
    public void AllNanBucketsDrawNothingInsteadOfThrowing()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, Series)
            .Add(c => c.Buckets, new[] { Bucket("10h", double.NaN, double.NaN) }));

        Assert.Empty(cut.FindAll("rect"));
    }

    [Fact]
    public void FractionalDataKeepsAFinerAxis()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, Series)
            .Add(c => c.Buckets, new[] { Bucket("10h", 0.5, 0) }));

        Assert.Equal(["0", "0.25", "0.5"], cut.FindAll(".ns-bar-chart-axis").Take(3).Select(t => t.TextContent).ToList());
    }

    [Fact]
    public void WarningIsAPaletteSlot()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(c => c.Series, new[] { new NsBarSeries("a", "Alpha", NsChartColor.Warning) })
            .Add(c => c.Buckets, new[] { Bucket("10h", 1, 0) }));

        Assert.Contains("var(--mud-palette-warning)", cut.Find("rect").GetAttribute("fill"));
    }
}

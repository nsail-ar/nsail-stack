// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A background block and a content block painted the same color are told apart by
/// weight alone, so the background's has to stay the weaker one in both places it shows —
/// the fill and the edge.</summary>
public sealed class NsTimeBlockColorTests : BunitContext
{
    static readonly DateOnly Day = new(2026, 8, 12);
    const string Color = "#7986cb";

    public NsTimeBlockColorTests()
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
                b.Add(x => x.Starts, Day.ToDateTime(new TimeOnly(13, 0)));
                b.Add(x => x.Duration, TimeSpan.FromHours(1));
                b.Add(x => x.Color, Color);

                block(b);
            }));
    }

    static double WashPercent(string style)
    {
        var marker = $"background-color: color-mix(in srgb, {Color} ";
        var start = style.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = style.IndexOf('%', start);

        return double.Parse(style[start..end], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The whole reason the story exists: a free band's fill reads unmistakably
    /// weaker than the same place's appointment, not just numerically different.</summary>
    [Fact]
    public void ABackgroundBlocksFillIsStrictlyWeakerThanAContentBlocks()
    {
        var background = Grid(b => b.Add(x => x.Background, true));
        var content = Grid(_ => { });

        var backgroundStyle = background.Find(".ns-time-block").GetAttribute("style")!;
        var contentStyle = content.Find(".ns-time-block").GetAttribute("style")!;

        Assert.True(WashPercent(backgroundStyle) < WashPercent(contentStyle));
    }

    /// <summary>A saturated, full-strength border was half of why the band still read as
    /// solid — its dashed edge softens along with the fill rather than staying at full color.</summary>
    [Fact]
    public void ABackgroundBlocksBorderIsNotFullStrength()
    {
        var cut = Grid(b => b.Add(x => x.Background, true));

        var style = cut.Find(".ns-time-block").GetAttribute("style")!;

        Assert.Contains($"border-color: color-mix(in srgb, {Color}", style);
        Assert.DoesNotContain($"border-color: {Color};", style);
    }

    /// <summary>The content block keeps its full-strength leading edge — occupied stays solid,
    /// exactly as the story demanded nothing here move.</summary>
    [Fact]
    public void AContentBlocksLeadingEdgeStaysFullStrength()
    {
        var cut = Grid(_ => { });

        var style = cut.Find(".ns-time-block").GetAttribute("style")!;

        Assert.Contains($"border-inline-start-color: {Color}", style);
    }
}

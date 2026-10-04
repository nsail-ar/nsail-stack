// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A day can be qualified AS A WHOLE — closed, borrowed, out of season — and the grid
/// draws that as the column rather than as a block spanning it. The distinction is the point: a
/// block would be handed a lane, would join whatever the consumer frames its hours from, and
/// would sit level with the background blocks it has to stay behind. A column wash has no start
/// and no duration, so none of those three is a rule anybody has to remember.</summary>
public sealed class NsTimeGridTintTests : BunitContext
{
    static readonly DateOnly Day = new(2026, 9, 9);

    const string Grey = "#9e9e9e";

    public NsTimeGridTintTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>The wash is the column it names and no other — a week showing one tinted day
    /// shows exactly one.</summary>
    [Fact]
    public void ATintedDay_WashesItsOwnColumnAndNoOther()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint> { [Day.AddDays(2)] = new(Grey) });

        var columns = cut.FindAll(".ns-time-grid-column");

        Assert.Equal(7, columns.Count);
        Assert.Single(cut.FindAll(".ns-time-grid-tint"));
        Assert.NotNull(columns[2].QuerySelector(".ns-time-grid-tint"));
        Assert.Null(columns[0].QuerySelector(".ns-time-grid-tint"));
        Assert.Null(columns[6].QuerySelector(".ns-time-grid-tint"));
    }

    /// <summary>Two tinted days, two washes, each with the color IT was given — the map is read
    /// per date, never once for the grid.</summary>
    [Fact]
    public void EachTintedDay_WearsTheColorItWasGiven()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint>
        {
            [Day] = new(Grey),
            [Day.AddDays(1)] = new("#7986cb"),
        });

        var tints = cut.FindAll(".ns-time-grid-tint");

        Assert.Equal(2, tints.Count);
        Assert.Contains(Grey, tints[0].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("#7986cb", tints[1].GetAttribute("style"), StringComparison.Ordinal);
    }

    /// <summary>A wash, not a fill: the caller's own color mixed with transparent, at a
    /// percentage that is neither nothing (which would draw no tint at all) nor the whole color
    /// (which would hide the hours it is qualifying).</summary>
    [Fact]
    public void TheWash_IsTheCallersColorAndItIsTranslucent()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint> { [Day] = new(Grey) });

        var style = cut.Find(".ns-time-grid-tint").GetAttribute("style") ?? string.Empty;

        Assert.StartsWith($"background-color: color-mix(in srgb, {Grey} ", style, StringComparison.Ordinal);
        Assert.Contains("%, transparent)", style, StringComparison.Ordinal);

        var percent = WashPercent(style);

        Assert.InRange(percent, 1, 99);
    }

    /// <summary>The day's own word (nsail#987): drawn on the column, sticky under the day names
    /// so it stays in view, and on the wash's title so hovering says what the day is. A tint
    /// with no label draws neither — the wash alone is still a tint.</summary>
    [Fact]
    public void ATintWithALabel_SaysItOnTheColumnAndOnHover()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint> { [Day] = new(Grey, "Día de la Independencia") });

        var tint = cut.Find(".ns-time-grid-tint");

        Assert.Equal("Día de la Independencia", tint.GetAttribute("title"));
        Assert.Equal("Día de la Independencia", cut.Find(".ns-time-grid-tint-label").TextContent.Trim());

        var unlabeled = Week(new Dictionary<DateOnly, NsTimeTint> { [Day] = new(Grey) });

        Assert.Empty(unlabeled.FindAll(".ns-time-grid-tint-label"));
        Assert.True(string.IsNullOrEmpty(unlabeled.Find(".ns-time-grid-tint").GetAttribute("title")));
    }

    /// <summary>It reads as a stretch of time and not as a painted ground (nsail#987): the edge
    /// is the background block's dashed one, in the day's own color and softened the same way,
    /// and the label is the one part that takes the pointer while the wash still takes none.</summary>
    [Fact]
    public void TheTint_WearsABackgroundBlocksEdgeInItsOwnColor()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint> { [Day] = new(Grey) });

        var style = cut.Find(".ns-time-grid-tint").GetAttribute("style") ?? string.Empty;

        Assert.Contains($"border-color: color-mix(in srgb, {Grey} ", style, StringComparison.Ordinal);

        var rule = Rule(".ns-time-grid-tint");
        var label = Rule(".ns-time-grid-tint-label");

        Assert.Contains("dashed", rule, StringComparison.Ordinal);
        Assert.Contains("position: absolute", label, StringComparison.Ordinal);
    }

    /// <summary>The label is on screen at rest: the column clips its own overflow, so nothing in
    /// it can stick to the scrollport, and the label sits at the hour the frame opens instead.</summary>
    [Fact]
    public void TheLabel_SitsWhereTheFrameOpens()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint> { [Day] = new(Grey, "Feriado") });

        var style = cut.Find(".ns-time-grid-tint-label").GetAttribute("style") ?? string.Empty;

        Assert.Equal("top: calc(var(--ns-time-hour) * 8)", style);
    }

    /// <summary>Nothing named, nothing drawn — the parameter costs an untinted grid no markup
    /// at all.</summary>
    [Fact]
    public void AGridWithNoTints_DrawsNone()
    {
        Assert.Empty(Week(null).FindAll(".ns-time-grid-tint"));
        Assert.Empty(Week(new Dictionary<DateOnly, NsTimeTint>()).FindAll(".ns-time-grid-tint"));
    }

    /// <summary>The AC this shape exists for: a tint changes NOTHING about the hours the grid
    /// frames. The window is the consumer's own DayStart/DayEnd, the track is still the whole
    /// day, and a tinted day is neither an early start nor a late finish.</summary>
    [Fact]
    public void TheTint_DoesNotStretchTheHoursTheGridFrames()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint> { [Day] = new(Grey) });

        Assert.Contains("--ns-time-window: 12", cut.Find(".ns-time-grid").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(24, cut.FindAll(".ns-time-grid-gutter .ns-time-grid-hour").Count);
        Assert.Equal(24 * 7, cut.FindAll(".ns-time-grid-column .ns-time-grid-hour").Count);
    }

    /// <summary>It is background, and document order is what makes it so: the wash is the
    /// column's FIRST child, ahead of the hour rows, the now line and everything the caller
    /// draws — so nothing it must stay under needs a z-index to win.</summary>
    [Fact]
    public void TheTint_IsTheFirstThingInItsColumn()
    {
        var cut = Week(new Dictionary<DateOnly, NsTimeTint> { [Day] = new(Grey) });

        var column = cut.FindAll(".ns-time-grid-column")[0];

        Assert.Contains("ns-time-grid-tint", column.FirstElementChild!.ClassList);
    }

    /// <summary>The now line still wins over it — the clock's mark is the one thing above the
    /// blocks, and a tinted day must not be the day it goes missing on.</summary>
    [Fact]
    public void TheTint_LeavesTheNowLineStanding()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.Now, Day.ToDateTime(new TimeOnly(11, 30)))
            .Add(x => x.Tints, Map(Day, Grey)));

        Assert.Single(cut.FindAll(".ns-time-grid-tint"));
        Assert.Single(cut.FindAll(".ns-time-grid-now"));
    }

    /// <summary>bUnit runs no stylesheet, so the DOM above proves which element exists and
    /// nothing about the painting order. That is read off the shipped rule instead: the ABSENCE
    /// of a z-index is what leaves every positioned block above the wash — a z-index here would
    /// lift the tint over the very blocks it is drawn behind. The wash takes the pointer on
    /// purpose (nsail#987, hover names the day), and the blocks above it still take theirs.</summary>
    [Fact]
    public void TheShippedRule_ClaimsNoLayerSoEveryBlockStaysAboveIt()
    {
        var rule = Rule(".ns-time-grid-tint");

        Assert.Contains("position: absolute", rule, StringComparison.Ordinal);
        Assert.Contains("inset: 0", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("pointer-events: none", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("z-index", rule, StringComparison.Ordinal);
    }

    IRenderedComponent<NsTimeGrid> Week(IReadOnlyDictionary<DateOnly, NsTimeTint>? tints)
    {
        return Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day.AddDays(6))
            .Add(x => x.Day, Day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0))
            .Add(x => x.Tints, tints));
    }

    static IReadOnlyDictionary<DateOnly, NsTimeTint> Map(DateOnly date, string color)
    {
        return new Dictionary<DateOnly, NsTimeTint> { [date] = new(color) };
    }

    static double WashPercent(string style)
    {
        var end = style.IndexOf('%', StringComparison.Ordinal);
        var start = style.LastIndexOf(' ', end) + 1;

        return double.Parse(style[start..end], CultureInfo.InvariantCulture);
    }

    static string Rule(string selector)
    {
        var css = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css"));

        var start = css.IndexOf($"{selector} {{", StringComparison.Ordinal);

        Assert.True(start >= 0, $"{selector} is not declared in ns-mud.css.");

        var end = css.IndexOf('}', start);

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

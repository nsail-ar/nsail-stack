// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Proves the grid owns the whole day. The window (DayStart/DayEnd) used to be the
/// content: a wall clock past DayEnd fell off the edge and the now line simply stopped being
/// drawn (Emmanuel's Today card at 20:01 against an 08–20 window, 2026-08-11), and an
/// appointment outside it was unreachable. The window is now the visible height and the
/// opening anchor; the track is always 00:00–24:00 inside the component's own scroll.</summary>
public sealed class NsTimeGridDayTests : BunitContext
{
    static readonly DateOnly Day = new(2026, 8, 11);

    public NsTimeGridDayTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>The track is the day, not the window: twenty-four rows in the gutter and
    /// twenty-four in every column, whatever hours the consumer framed.</summary>
    [Fact]
    public void TheTrack_CoversTheWholeDayWhateverTheWindow()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0)));

        Assert.Equal(24, cut.FindAll(".ns-time-grid-gutter .ns-time-grid-hour").Count);
        Assert.Equal(24, cut.FindAll(".ns-time-grid-column .ns-time-grid-hour").Count);

        // The marks are clock hours from midnight, so the first names 00 and the last 23.
        var marks = cut.FindAll(".ns-time-grid-gutter .ns-time-grid-mark");
        Assert.Equal(24, marks.Count);
        Assert.Equal(new TimeOnly(0, 0).ToString("t"), marks[0].TextContent.Trim());
        Assert.Equal(new TimeOnly(23, 0).ToString("t"), marks[23].TextContent.Trim());
    }

    /// <summary>The reported defect, pinned: 20:01 against an 08–20 window. The line is drawn,
    /// and it is drawn at its offset from midnight — not clamped to an edge.</summary>
    [Fact]
    public void TheNowLine_IsDrawnPastTheOldWindowsEdge()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0))
            .Add(x => x.Now, Day.ToDateTime(new TimeOnly(20, 1))));

        var line = cut.Find(".ns-time-grid-now");

        // 20h01 = 20.01667 hours past midnight, rounded to the five decimals the style writes.
        Assert.Contains("var(--ns-time-hour) * 20.01667", line.GetAttribute("style"));
    }

    /// <summary>A clock on another day still draws nothing — the line belongs to the column
    /// whose date it is, and to no other.</summary>
    [Fact]
    public void TheNowLine_StaysOffTheColumnsThatAreNotItsDay()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day.AddDays(2))
            .Add(x => x.Now, Day.AddDays(1).ToDateTime(new TimeOnly(3, 30))));

        Assert.Single(cut.FindAll(".ns-time-grid-now"));
    }

    /// <summary>The window survives as the visible height: the component owns a vertical scroll
    /// of its own, sized by the framed hours and wearing the house's thin scrollbar.</summary>
    [Fact]
    public void TheWindow_BecomesTheVisibleHeightOfTheComponentsOwnScroll()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0)));

        var scroll = cut.Find(".ns-time-grid-scroll");

        Assert.Contains("ns-scroll-thin", scroll.GetAttribute("class"));
        Assert.Contains("--ns-time-window: 12", cut.Find(".ns-time-grid").GetAttribute("style"));
    }

    /// <summary>Compact stays honest: the dense hour is still declared, and it is the same
    /// hour unit the visible height is counted in — the card's window shrinks with it rather
    /// than opening a taller box than the cell it sits in.</summary>
    [Fact]
    public void CompactMode_KeepsItsDenseHourAndItsOwnWindow()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.Dense, true)
            .Add(x => x.DayStart, new TimeOnly(9, 0))
            .Add(x => x.DayEnd, new TimeOnly(18, 0)));

        var grid = cut.Find(".ns-time-grid");

        Assert.Contains("ns-time-dense", grid.GetAttribute("class"));
        Assert.Contains("--ns-time-window: 9", grid.GetAttribute("style"));
    }

    /// <summary>The minute timer that keeps the line moving dies with the component, and dies
    /// once: a second Dispose is a no-op rather than a throw.</summary>
    [Fact]
    public void TheMinuteTimer_DiesCleanlyAndIdempotently()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.Now, Day.ToDateTime(new TimeOnly(20, 1))));

        var grid = Assert.IsAssignableFrom<IDisposable>(cut.Instance);

        grid.Dispose();
        grid.Dispose();
    }

    /// <summary>The columns' collapse is unchanged: the grid is still the container the width
    /// query measures, and the days that are not focused still carry the class it hides.</summary>
    [Fact]
    public void TheColumnQuery_SurvivesTheNewScrollBox()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day.AddDays(2))
            .Add(x => x.Day, Day));

        Assert.Contains("ns-time-collapse-md", cut.Find(".ns-time-grid").GetAttribute("class"));

        // Two of the three columns are aside, and so are their two day-name cells.
        Assert.Equal(4, cut.FindAll(".ns-time-grid-aside").Count);
    }

    /// <summary>The defect this pins (nsail#76): the collapse used to leave nothing on the
    /// page to say six of seven days were gone. The note exists exactly when there is more
    /// than one day to lose — CSS decides whether the width on screen actually hides one,
    /// but a single-day grid has nothing to hide and renders no note at all.</summary>
    [Fact]
    public void TheHiddenDaysNote_RendersOnlyWhenThereIsMoreThanOneDay()
    {
        var single = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day));

        Assert.Empty(single.FindAll(".ns-time-grid-note"));

        var week = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day.AddDays(6))
            .Add(x => x.Day, Day));

        Assert.Single(week.FindAll(".ns-time-grid-note"));
    }

    /// <summary>The note's own count-agreement: StringManager's ".Plural" convention picks
    /// the singular key for exactly one hidden day and the plural key for six — the same
    /// count the aside columns above actually carry.</summary>
    [Fact]
    public void TheHiddenDaysNote_PicksTheKeyThatAgreesWithTheCount()
    {
        var week = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day.AddDays(6))
            .Add(x => x.Day, Day));

        Assert.Equal("Common.TimeGridHiddenDay.Plural", week.Find(".ns-time-grid-note").TextContent.Trim());

        var pair = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day.AddDays(1))
            .Add(x => x.Day, Day));

        Assert.Equal("Common.TimeGridHiddenDay", pair.Find(".ns-time-grid-note").TextContent.Trim());
    }
}

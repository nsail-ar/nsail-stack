// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Where the grid opens: the framed start, and the clock instead only when the clock
/// stands inside the framed hours. A clock outside the frame — 02:00 over a practice's
/// 08:00–20:00 — centred its anchor onto midnight and left every framed hour below the window.
/// The measurement is the arguments recorded for nsapp.scrollToFraction, with Now handed in:
/// how tall an hour is on screen is the browser's answer, and a geometry assertion that
/// depends on the runner's wall clock is a red nobody can trust.</summary>
public sealed class NsTimeGridAnchorTests : BunitContext
{
    const string Scroll = "nsapp.scrollToFraction";

    const int HoursInDay = 24;

    static readonly DateOnly Day = new(2026, 9, 3);

    public NsTimeGridAnchorTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AClockBeforeTheFrameOpensTheGridAtTheFramedStart()
    {
        Render(Day.ToDateTime(new TimeOnly(2, 0)));

        Assert.Equal(8d / HoursInDay, Anchor());
        Assert.Equal(0d, Bias());
    }

    [Fact]
    public void AClockAfterTheFrameOpensTheGridAtTheFramedStart()
    {
        Render(Day.ToDateTime(new TimeOnly(22, 30)));

        Assert.Equal(8d / HoursInDay, Anchor());
        Assert.Equal(0d, Bias());
    }

    [Fact]
    public void AClockInsideTheFrameIsWhatTheGridCentresOn()
    {
        Render(Day.ToDateTime(new TimeOnly(10, 0)));

        Assert.Equal(10d / HoursInDay, Anchor());
        Assert.Equal(0.5d, Bias());
    }

    /// <summary>A range holding another day than the clock's opens at the framed start however
    /// the hour reads — the rule that was already true, and the one the frame condition must
    /// not have moved.</summary>
    [Fact]
    public void ARangeHoldingNoTodayOpensAtTheFramedStart()
    {
        Render(new DateTime(2026, 9, 10, 10, 0, 0));

        Assert.Equal(8d / HoursInDay, Anchor());
        Assert.Equal(0d, Bias());
    }

    void Render(DateTime now)
    {
        Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0))
            .Add(x => x.Now, now));
    }

    double Anchor()
    {
        return Argument(1);
    }

    double Bias()
    {
        return Argument(2);
    }

    double Argument(int index)
    {
        var invocation = Assert.Single(JSInterop.Invocations[Scroll]);

        return Assert.IsType<double>(invocation.Arguments[index]);
    }
}

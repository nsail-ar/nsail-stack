// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The grid is the only scroll on the screen it fills. Framed hours are a fixed
/// height by default — the frame IS the box — so a window opened to the whole day stands
/// taller than the screen and the surface around the grid scrolls it, day names and all
/// (nsail#765). Fits turns the frame into a ceiling: the room left over is what the scrollport
/// takes. The layout itself is CSS (.ns-time-fits in ns-mud.css) and bUnit lays nothing out,
/// so what is pinned here is the hook that rule keys off — present exactly when it is asked
/// for, and absent from every grid that did not, which is what keeps a grid mounted inside a
/// card or a form out of the blast radius.</summary>
public sealed class NsTimeGridFitsTests : BunitContext
{
    static readonly DateOnly Day = new(2026, 9, 3);

    public NsTimeGridFitsTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AFittedGridCarriesTheHookTheLayoutRuleKeysOff()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.Fits, true));

        Assert.Contains("ns-time-fits", cut.Find(".ns-time-grid").GetAttribute("class"));
    }

    [Fact]
    public void AGridThatDidNotAskToFitCarriesNothing()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day));

        Assert.DoesNotContain("ns-time-fits", cut.Find(".ns-time-grid").GetAttribute("class"));
    }

    /// <summary>Fitting is about the box around the grid, never about the grid's own contract:
    /// the frame is still declared for the hours it always was, the track is still the whole
    /// day, and the scrollport is still the grid's own.</summary>
    [Fact]
    public void FittingChangesNothingAboutTheFrameOrTheTrack()
    {
        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, Day)
            .Add(x => x.To, Day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0))
            .Add(x => x.Fits, true));

        Assert.Contains("--ns-time-window: 12", cut.Find(".ns-time-grid").GetAttribute("style"));
        Assert.Equal(24, cut.FindAll(".ns-time-grid-column .ns-time-grid-hour").Count);
        Assert.Contains("ns-scroll-thin", cut.Find(".ns-time-grid-scroll").GetAttribute("class"));
    }
}

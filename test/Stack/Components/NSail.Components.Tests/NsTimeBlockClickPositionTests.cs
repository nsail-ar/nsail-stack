// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A block can now tell its caller WHERE down itself a click landed, which is the one
/// thing C# cannot work out: how tall an hour is on this screen belongs to the browser, the same
/// trade the grid's own calc() offsets make. OnClick keeps its exact behaviour — the position is
/// an additional callback, never a change to the old one.</summary>
public sealed class NsTimeBlockClickPositionTests : BunitContext
{
    static readonly DateOnly Day = new(2026, 8, 12);

    public NsTimeBlockClickPositionTests()
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
                b.Add(x => x.Duration, TimeSpan.FromHours(4));
                b.Add(x => x.Background, true);

                block(b);
            }));
    }

    /// <summary>The browser measures, the block reports: whatever fraction the interop answers
    /// is what the caller is handed.</summary>
    [Fact]
    public async Task OnClickAt_CarriesTheFractionTheBrowserMeasured()
    {
        JSInterop.Setup<double>("nsapp.fractionAt", _ => true).SetResult(0.5);

        double? landed = null;

        var cut = Grid(b => b.Add(x => x.OnClickAt, (double fraction) => landed = fraction));

        var block = cut.Find(".ns-time-block");

        await cut.InvokeAsync(() => block.Click(new MouseEventArgs { Detail = 1, ClientY = 300 }));

        Assert.Equal(0.5, landed);
    }

    /// <summary>Enter and Space on the focused button raise a click with no pointer behind it
    /// (Detail 0, ClientY 0). Measuring that would report the very top of the block as if
    /// somebody had aimed there, so the block answers 0 without asking the browser at all —
    /// which is what the caller's own no-position behaviour already means.</summary>
    [Fact]
    public async Task OnClickAt_AnswersZeroForAClickWithNoPointer()
    {
        JSInterop.Setup<double>("nsapp.fractionAt", _ => true).SetResult(0.5);

        double? landed = null;

        var cut = Grid(b => b.Add(x => x.OnClickAt, (double fraction) => landed = fraction));

        var block = cut.Find(".ns-time-block");

        await cut.InvokeAsync(() => block.Click(new MouseEventArgs { Detail = 0 }));

        Assert.Equal(0, landed);
    }

    /// <summary>Every caller that was here before this parameter existed: still a button, still
    /// the same callback, still nothing measured.</summary>
    [Fact]
    public async Task OnClick_IsUntouched()
    {
        var clicked = 0;

        var cut = Grid(b => b.Add(x => x.OnClick, () => clicked++));

        var block = cut.Find("button.ns-time-block");

        await cut.InvokeAsync(() => block.Click(new MouseEventArgs { Detail = 1, ClientY = 300 }));

        Assert.Equal(1, clicked);
        Assert.Empty(JSInterop.Invocations["nsapp.fractionAt"]);
    }

    /// <summary>A block nobody wired stays a div — no button, no pointer cursor.</summary>
    [Fact]
    public void ABlockWithNeitherCallbackIsNotAButton()
    {
        var cut = Grid(_ => { });

        Assert.Empty(cut.FindAll("button.ns-time-block"));
        Assert.Empty(cut.FindAll(".ns-time-block.cursor-pointer"));
    }

    /// <summary>A block wired for position is as clickable as one wired for the plain click.</summary>
    [Fact]
    public void ABlockWiredForPositionIsAButton()
    {
        var cut = Grid(b => b.Add(x => x.OnClickAt, (double _) => { }));

        Assert.Single(cut.FindAll("button.ns-time-block.cursor-pointer"));
    }
}

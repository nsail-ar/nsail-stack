// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;

namespace NSail.Components.Tests;

/// <summary>nsail#2589: how long ago, in the one unit a person would say it in — a backup reads
/// "hace 5 min" and the exact moment is one gesture away, never the other way round. The
/// boundaries are the whole of the rule, and the words are the feature: the catalog here is the
/// assembly's own shipped strings, not a fixture dictionary, so a reading that resolved a key
/// instead of a sentence fails.</summary>
public sealed class NsSinceTests : BunitContext, IAsyncLifetime
{
    static readonly DateTime Noon = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    public NsSinceTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddStringsFromAssembly(typeof(NsSince).Assembly);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    /// <summary>Each unit hands the reading on at its own boundary, read off the computation
    /// rather than off a render: the control asks the clock itself, so a rendered second either
    /// side of a boundary is a race and this is not.</summary>
    [Theory]
    [InlineData(0, "Moments", 0)]
    [InlineData(59, "Moments", 0)]
    [InlineData(60, "Minutes", 1)]
    [InlineData(90, "Minutes", 1)]
    [InlineData(3599, "Minutes", 59)]
    [InlineData(3600, "Hours", 1)]
    [InlineData(86_399, "Hours", 23)]
    [InlineData(86_400, "Days", 1)]
    [InlineData(172_799, "Days", 1)]
    [InlineData(172_800, "Days", 2)]
    [InlineData(259_200, "Days", 3)]
    public void EachUnitHandsTheReadingOnAtItsOwnBoundary(int secondsAgo, string unit, int value)
    {
        Assert.Equal(($"Components.NsSince.{unit}", value), NsSince.Elapsed(Noon.AddSeconds(-secondsAgo), Noon));
    }

    /// <summary>A box whose own clock runs ahead of this one reads as the nearest unit, never as
    /// a negative number.</summary>
    [Fact]
    public void AMomentAheadOfTheClockReadsAsMomentsAgo()
    {
        Assert.Equal(("Components.NsSince.Moments", 0), NsSince.Elapsed(Noon.AddMinutes(30), Noon));
    }

    [Theory]
    [InlineData("en", 5, "5 min ago")]
    [InlineData("es", 5, "hace 5 min")]
    [InlineData("en", 180, "3 h ago")]
    [InlineData("es", 180, "hace 3 h")]
    public void TheReadingIsSaidInTheUnitAPersonWouldSayItIn(string language, int minutesAgo, string words)
    {
        var cut = Render(language, DateTime.UtcNow.AddMinutes(-minutesAgo));

        Assert.Equal(words, cut.Find(".ns-whisper-host").TextContent);
    }

    [Theory]
    [InlineData("en", 0, "moments ago")]
    [InlineData("es", 0, "hace instantes")]
    public void AMomentJustPastReadsAsNoNumberAtAll(string language, int minutesAgo, string words)
    {
        var cut = Render(language, DateTime.UtcNow.AddMinutes(-minutesAgo));

        Assert.Equal(words, cut.Find(".ns-whisper-host").TextContent);
    }

    /// <summary>One day is the value the warning tone lights up on, so it is the most read number
    /// of the column, and Spanish cannot spell it with the plural's word: the day carries a
    /// reading of its own through the catalog's reserved ".Plural" pair.</summary>
    [Theory]
    [InlineData("en", 25, "1 d ago")]
    [InlineData("es", 25, "hace 1 día")]
    [InlineData("en", 74, "3 d ago")]
    [InlineData("es", 74, "hace 3 días")]
    public void ASingleDayIsSaidInTheSingularAndTheRestInThePlural(string language, int hoursAgo, string words)
    {
        var cut = Render(language, DateTime.UtcNow.AddHours(-hoursAgo));

        Assert.Equal(words, cut.Find(".ns-whisper-host").TextContent);
    }

    /// <summary>What the elapsed time is OF, where the column alone does not say it — the
    /// caller's own sentence, with the reading set inside it.</summary>
    [Theory]
    [InlineData("en", "measured {0}", "measured 5 min ago")]
    [InlineData("es", "medido {0}", "medido hace 5 min")]
    public void ACallersSentenceSetsTheReadingInside(string language, string format, string words)
    {
        var cut = Render(language, DateTime.UtcNow.AddMinutes(-5), format: format);

        Assert.Equal(words, cut.Find(".ns-whisper-host").TextContent);
    }

    /// <summary>Past the caller's threshold the same text wears the warning tone: the number
    /// alone is what nobody reads on a list of forty rows.</summary>
    [Fact]
    public void PastTheCallersThresholdTheReadingWearsTheWarningTone()
    {
        var cut = Render("es", DateTime.UtcNow.AddHours(-25), staleAfter: TimeSpan.FromDays(1));

        Assert.Contains("ns-status-warning", cut.Find(".ns-status-text").ClassList);
    }

    [Fact]
    public void UnderTheThresholdItIsOrdinaryTextAndWearsNoTone()
    {
        var cut = Render("es", DateTime.UtcNow.AddHours(-23), staleAfter: TimeSpan.FromDays(1));

        Assert.Empty(cut.FindAll(".ns-status-text"));
    }

    /// <summary>A threshold nobody named is a reading that is never late, however old it is.</summary>
    [Fact]
    public void WithNoThresholdNoReadingIsEverLate()
    {
        var cut = Render("es", DateTime.UtcNow.AddDays(-400));

        Assert.Empty(cut.FindAll(".ns-status-text"));
    }

    /// <summary>A moment that never happened says so in the caller's own words, at the warning
    /// tone — and draws nothing at all where the caller names none, because an absent date is
    /// not always an alarm.</summary>
    [Fact]
    public void AMomentThatNeverHappenedSaysSoInTheCallersWords()
    {
        var cut = Render("es", at: null, missing: "nunca respaldada");

        var text = cut.Find(".ns-status-text");

        Assert.Equal("nunca respaldada", text.TextContent);
        Assert.Contains("ns-status-warning", text.ClassList);
    }

    [Fact]
    public void NoMomentAndNoWordForItDrawsNothingAtAll()
    {
        var cut = Render("es", at: null);

        Assert.Empty(cut.Markup.Trim());
    }

    /// <summary>The exact moment rides the whisper attribute, in the OPERATOR's region pattern —
    /// day first for es-AR, month first for en-US — and never in the host machine's ambient
    /// culture: the app's language is a neutral two-letter code carrying no date pattern at all.
    /// The day is read off the runner's own clock rather than written out: a moment that says it
    /// is UTC is drawn in the reader's zone, so a runner east of Greenwich is already on the next
    /// day and what this pins is the pattern.</summary>
    [Theory]
    [InlineData("es", true)]
    [InlineData("en", false)]
    public void TheExactMomentReadsInTheOperatorsRegion(string language, bool dayFirst)
    {
        var local = Noon.ToLocalTime();
        var cut = Render(language, Noon);

        var whisper = cut.Find(".ns-whisper-host").GetAttribute("data-ns-whisper")!;

        Assert.Equal(
            dayFirst ? local.Day : local.Month,
            int.Parse(whisper.Split('/')[0], CultureInfo.InvariantCulture));
        Assert.Equal(local, DateTime.Parse(whisper, Region(language)));
    }

    /// <summary>A moment with no zone to it is the civil hour it says it is — the house's reading
    /// of any kind that is not UTC (exporting.md) — in BOTH halves: the count reads it as a local
    /// hour, and the whisper names that same hour unmoved, so the bubble is the long form of the
    /// number beside it and not a second instant an offset away from it. A runner whose own clock
    /// is UTC cannot tell the two apart, which is the whole reason the rule is pinned here.
    /// </summary>
    [Fact]
    public void AMomentWithNoZoneToItIsTheSameInstantToBothHalves()
    {
        var civil = DateTime.SpecifyKind(Noon.ToLocalTime(), DateTimeKind.Unspecified);

        Assert.Equal(("Components.NsSince.Minutes", 5), NsSince.Elapsed(civil, Noon.AddMinutes(5)));

        var cut = Render("es", civil);

        Assert.Equal(civil, DateTime.Parse(cut.Find(".ns-whisper-host").GetAttribute("data-ns-whisper")!, Region("es")));
    }

    // The region the control itself reads the moment in, reached through its own mapping rather
    // than a second copy of it here (NsDateFieldTests does the same).
    static CultureInfo Region(string language)
    {
        return RegionCulture.For(new LanguageProvider { Current = language });
    }

    IRenderedComponent<NsSince> Render(
        string language,
        DateTime? at,
        string? missing = null,
        TimeSpan? staleAfter = null,
        string? format = null)
    {
        Services.GetRequiredService<LanguageProvider>().Current = language;

        return Render<NsSince>(ps => ps
            .Add(p => p.At, at)
            .Add(p => p.Missing, missing)
            .Add(p => p.StaleAfter, staleAfter)
            .Add(p => p.Format, format));
    }
}

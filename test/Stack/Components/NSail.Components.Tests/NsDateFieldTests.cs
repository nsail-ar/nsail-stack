// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The typing contract of a date box: what is typed belongs to the BROWSER until the
/// field is left, and the whole of it is read at once when it is (nsail#1196 — a per-keystroke
/// mask made every key a round trip, and a key typed while one was in flight was overwritten by
/// the reply). What the box accepts is the active culture's own field order, punctuated or not;
/// what it cannot read it says so about instead of clearing.</summary>
public sealed class NsDateFieldTests : BunitContext, IAsyncLifetime
{
    CultureInfo? _originalCulture;
    CultureInfo? _originalUICulture;

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // The vendor picker lays its calendar out against the AMBIENT thread culture as well as
        // the Culture parameter it is handed, so a test that pins that ambient culture must hand
        // the pool thread back unchanged, or the next test to land on it inherits the wrong one.
        if (_originalCulture is not null)
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUICulture!;
        }

        await DisposeAsync();
    }

    void Compose(string language)
    {
        // No MudPopoverProvider in this host: the vendor otherwise refuses to build the popover
        // a tooltip or a picker owns, and none of these assertions is about a popover.
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog(
        [
            new TwoLanguageStrings("Common.DateLetters", "dmy", "dma"),
            new TwoLanguageStrings("Problems.UnreadableDate", "This is not a date — write it as {shape}", "Esto no es una fecha — escribila como {shape}")
        ]));
        Services.AddSingleton(new LanguageProvider { Current = language });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        PinCulture(language);
    }

    // Same region mapping NsDateField's own RegionCulture uses (en -> en-US, es -> es-AR),
    // reached through it (InternalsVisibleTo already lets this assembly in) rather than
    // duplicated here — a second mapping is a second place to drift out of step.
    void PinCulture(string language)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;

        var culture = RegionCulture.For(new LanguageProvider { Current = language });
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    // Leaving the box is what the browser reports as a change, and it carries the whole text —
    // whether it was typed key by key, pasted in one motion or filled by a harness. Generic over
    // TValue because the nullable shape and the one the pages really bind answer differently
    // under a refusal, and both are asserted here.
    static Task Leave<TValue>(IRenderedComponent<NsDateField<TValue>> cut, string text)
    {
        return cut.InvokeAsync(() => cut.Find("input").Change(text));
    }

    // What the field says under the box, and nothing else: the placeholder spells the same
    // shape the refusal names, so a search of the whole markup would find it either way.
    static string? Refusal<TValue>(IRenderedComponent<NsDateField<TValue>> cut)
    {
        return cut.FindAll(".mud-input-helper-text").FirstOrDefault()?.TextContent;
    }

    /// <summary>The defect itself, at the seam it was made on: with no mask the vendor restores
    /// its own change wiring, so NOTHING is bound to the input event and there is no per-key
    /// round trip left to drop a key from. PickerNotifyTests records the opposite reading of
    /// the same input from the days it was masked.</summary>
    [Fact]
    public void NoKeystrokeReachesTheServer_TheBoxIsTheBrowsersUntilItIsLeft()
    {
        Compose("es");

        var cut = Render<NsDateField<DateOnly?>>();

        var input = cut.Find("input");

        Assert.False(input.HasAttribute("blazor:oninput"));
        Assert.True(input.HasAttribute("blazor:onchange"));
    }

    [Fact]
    public async Task ADateLeftInTheBox_InSpanish_ReadsDayBeforeMonth()
    {
        Compose("es");

        DateOnly? captured = null;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        await Leave(cut, "07/08/2026");

        Assert.Equal(new DateOnly(2026, 8, 7), captured);
        Assert.Equal("07/08/2026", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>The same characters, read in the other order: es-AR is day-first, en-US is
    /// month-first. Same text, different date — the proof the field order follows the culture
    /// rather than a fixed shape.</summary>
    [Fact]
    public async Task ADateLeftInTheBox_InEnglish_ReadsMonthBeforeDay()
    {
        Compose("en");

        DateOnly? captured = null;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        await Leave(cut, "07/08/2026");

        Assert.Equal(new DateOnly(2026, 7, 8), captured);
    }

    /// <summary>Leonardo's 2026-08-07 convenience — not having to type the separators — kept,
    /// moved off the keystroke and onto the parse: the shape already says where the slashes
    /// fall, so a bare run of digits is the same date.</summary>
    [Fact]
    public async Task TheSeparatorsNeedNotBeTyped()
    {
        Compose("es");

        DateOnly? captured = null;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        await Leave(cut, "07082026");

        Assert.Equal(new DateOnly(2026, 8, 7), captured);

        // And what the person typed is left exactly as they typed it: the box belongs to them
        // until something other than their own keyboard puts a date in it.
        Assert.Equal("07082026", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>A person who types the day and the month without their leading zero means the
    /// same day, and the old mask could not have produced this text at all.</summary>
    [Fact]
    public async Task ADateTypedWithoutItsLeadingZeros_Lands()
    {
        Compose("es");

        DateOnly? captured = null;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        await Leave(cut, "7/8/2026");

        Assert.Equal(new DateOnly(2026, 8, 7), captured);
    }

    /// <summary>A year typed short is this century's, by the culture's own two-digit rule.</summary>
    [Fact]
    public async Task AYearTypedShort_IsThisCentury()
    {
        Compose("es");

        DateOnly? captured = null;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        await Leave(cut, "07/08/26");

        Assert.Equal(new DateOnly(2026, 8, 7), captured);
    }

    /// <summary>A paste — and every harness fill, which is the same event — arrives whole, over
    /// a date the field already held, and replaces it.</summary>
    [Fact]
    public async Task APastedDate_ReplacesTheOneAlreadyHeld()
    {
        Compose("es");

        DateOnly? captured = null;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.Value, new DateOnly(2026, 1, 3))
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        Assert.Equal("03/01/2026", cut.Find("input").GetAttribute("value"));

        await Leave(cut, "07/08/2026");

        Assert.Equal(new DateOnly(2026, 8, 7), captured);
    }

    /// <summary>A value the page already holds — never typed by this user — still displays in
    /// the culture's own shape rather than the ambient thread's.</summary>
    [Fact]
    public void AValueEchoedFromTheModel_DisplaysInThePromisedShape()
    {
        Compose("es");

        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.Value, new DateOnly(2026, 8, 7)));

        Assert.Equal("07/08/2026", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>The other half of AC 1: text the field cannot read is refused in words, in the
    /// language of the screen, naming the shape it does read — and it is LEFT IN THE BOX, so
    /// the person can see what they are being asked to fix. The value the page handed in is
    /// left alone: a refusal is the field declining to answer, not an answer of its own.</summary>
    [Theory]
    [InlineData("es", "dd/mm/aaaa")]
    [InlineData("en", "mm/dd/yyyy")]
    public async Task TextThatIsNotADate_IsRefusedInWordsAndLeftStanding(string language, string shape)
    {
        Compose(language);

        var writes = 0;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.Value, new DateOnly(2026, 1, 3))
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, _ => writes++)));

        await Leave(cut, "mañana");

        Assert.Contains(shape, Refusal(cut));
        Assert.Equal("mañana", cut.Find("input").GetAttribute("value"));
        Assert.Equal(0, writes);
    }

    /// <summary>The shape every page in the tree actually binds — a date that is NOT nullable
    /// (the OT's promise date, the sale's, the prescription's). The vendor picker raises the
    /// same DateChanged(null) for "cleared" and "unreadable", and writing that null through gave
    /// this binding <c>default(DateOnly)</c>: the model left the field holding 0001-01-01, a real
    /// date as far as the submit, the wire and the row are concerned, under a visible refusal
    /// saying the field could not read anything. Nothing about the model may move here.</summary>
    [Fact]
    public async Task TextThatIsNotADate_LeavesANonNullableDateExactlyAsItWas()
    {
        Compose("es");

        var captured = new DateOnly(2026, 1, 3);
        var writes = 0;
        var cut = Render<NsDateField<DateOnly>>(ps => ps
            .Add(p => p.Value, new DateOnly(2026, 1, 3))
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly>(this, v =>
            {
                captured = v;
                writes++;
            })));

        await Leave(cut, "mañana");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));
        Assert.Equal(0, writes);
        Assert.Equal(new DateOnly(2026, 1, 3), captured);
        Assert.NotEqual(default, captured);
    }

    /// <summary>And the refusal lifts on the non-nullable shape too, with the date the person
    /// finally typed landing — the guard above declines to write, it does not stop writing.</summary>
    [Fact]
    public async Task ARefusalOnANonNullableDate_LiftsAndTheNextDateLands()
    {
        Compose("es");

        var captured = new DateOnly(2026, 1, 3);
        var cut = Render<NsDateField<DateOnly>>(ps => ps
            .Add(p => p.Value, new DateOnly(2026, 1, 3))
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly>(this, v => captured = v)));

        await Leave(cut, "mañana");
        await Leave(cut, "07/08/2026");

        Assert.Equal(new DateOnly(2026, 8, 7), captured);
        Assert.Null(Refusal(cut));
    }

    /// <summary>A year half typed is not a short year: reading "202" as the year 202 is the
    /// silently wrong date this field exists to refuse.</summary>
    [Fact]
    public async Task AHalfTypedYear_IsRefusedRatherThanRead()
    {
        Compose("es");

        var cut = Render<NsDateField<DateOnly?>>();

        await Leave(cut, "07/08/202");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));
    }

    /// <summary>A day the month does not have is refused, not rolled forward.</summary>
    [Fact]
    public async Task ADayTheMonthDoesNotHave_IsRefused()
    {
        Compose("es");

        var cut = Render<NsDateField<DateOnly?>>();

        await Leave(cut, "31/02/2026");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));
    }

    /// <summary>Emptying the box empties the value, and says nothing about it — a blank date
    /// box is not a refusal, it is how an optional date is cleared.</summary>
    [Fact]
    public async Task ClearingTheBox_ClearsTheValueWithoutARefusal()
    {
        Compose("es");

        DateOnly? captured = new DateOnly(2026, 1, 3);
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.Value, new DateOnly(2026, 1, 3))
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        await Leave(cut, "");

        Assert.Null(captured);
        Assert.Null(Refusal(cut));
    }

    /// <summary>A refusal is not a state the field gets stuck in: the next readable date clears
    /// it along with the text that earned it.</summary>
    [Fact]
    public async Task ARefusalLiftsWhenAReadableDateArrives()
    {
        Compose("es");

        DateOnly? captured = null;
        var cut = Render<NsDateField<DateOnly?>>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v)));

        await Leave(cut, "mañana");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));

        await Leave(cut, "07/08/2026");

        Assert.Equal(new DateOnly(2026, 8, 7), captured);
        Assert.Null(Refusal(cut));
    }
}

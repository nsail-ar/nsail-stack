// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The composite date+time field, pinned after it lost a person's booking in the
/// wild (2026-08-04): picking Friday kept Tuesday and reset the hour to midnight, because
/// the date half read the time off the DATE PICKER — which always hands back midnight — and
/// combined it with the date it already had. Exactly inverted, and invisible to anyone
/// reading the value the screen showed, which is why both halves are asserted here.</summary>
public sealed class NsDateTimeFieldTests : BunitContext, IAsyncLifetime
{
    CultureInfo? _originalCulture;
    CultureInfo? _originalUICulture;

    public NsDateTimeFieldTests()
    {
        Services.AddMudServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider pulls in a MudBlazor service that is IAsyncDisposable-only, so
    // bUnit's synchronous teardown cannot dispose it — the NsActionColumnTests rule.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Same guard as NsDateFieldTests: the vendor picker reads the AMBIENT thread culture
        // for some of its own formatting/parsing (NsDateField.razor.cs's note), so a test that
        // pins it via ComposeCulture must hand the pool thread back unchanged for whichever
        // test lands on it next.
        if (_originalCulture is not null)
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUICulture!;
        }

        await DisposeAsync();
    }

    [Fact]
    public async Task PickingADate_KeepsTheHourAlreadyChosen()
    {
        DateTime? captured = null;

        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0))
            .Add(p => p.ValueChanged, v => captured = v));

        await cut.InvokeAsync(() => cut.FindComponent<MudDatePicker>().Instance.DateChanged
            .InvokeAsync(new DateTime(2026, 8, 7, 0, 0, 0)));

        // Friday, and still half past three — the day moves, the hour does not.
        Assert.Equal(new DateTime(2026, 8, 7, 15, 30, 0), captured);
    }

    [Fact]
    public async Task PickingAnHour_KeepsTheDateAlreadyChosen()
    {
        DateTime? captured = null;

        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0))
            .Add(p => p.ValueChanged, v => captured = v));

        await cut.InvokeAsync(() => cut.FindComponent<MudTimePicker>().Instance.TimeChanged
            .InvokeAsync(new TimeSpan(9, 0, 0)));

        Assert.Equal(new DateTime(2026, 8, 4, 9, 0, 0), captured);
    }

    /// <summary>The sibling NsDateField carries this guard with the reason written down: the
    /// vendor picker re-renders off the validation event before the new value round-trips
    /// back as a parameter, and a bare read would show the stale date and wipe the pick. The
    /// composite embeds the same picker, so it needs the same guard — a form that validates
    /// on change (every Required field does) is where the loss would happen.</summary>
    [Fact]
    public async Task ARenderBeforeTheValueRoundTrips_DoesNotWipeThePickedDate()
    {
        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0)));

        await cut.InvokeAsync(() => cut.FindComponent<MudDatePicker>().Instance.DateChanged
            .InvokeAsync(new DateTime(2026, 8, 7, 0, 0, 0)));

        // The page never pushed the new value back down, and a re-render happens anyway:
        // the picker must still show Friday rather than snapping back to Tuesday.
        cut.Render();

        Assert.Equal(new DateTime(2026, 8, 7), cut.FindComponent<MudDatePicker>().Instance.Date);
    }

    // Calqued from NsDateFieldTests: the date half reads through the same seam, so it earns
    // the same proof. Overrides the constructor's neutral LanguageProvider/StringCatalog with a
    // culture-specific pair — the last registration of a singleton type wins on resolve, the
    // same override shape NsDateFieldTests.Compose uses.
    void ComposeCulture(string language)
    {
        Services.AddSingleton(new StringCatalog(
        [
            new TwoLanguageStrings("Common.DateLetters", "dmy", "dma"),
            new TwoLanguageStrings("Problems.UnreadableDate", "This is not a date — write it as {shape}", "Esto no es una fecha — escribila como {shape}")
        ]));
        Services.AddSingleton(new LanguageProvider { Current = language });

        PinCulture(language);
    }

    // Same region mapping NsDateField's own RegionCulture uses (en -> en-US, es -> es-AR),
    // reached through it rather than duplicated here.
    void PinCulture(string language)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;

        var culture = RegionCulture.For(new LanguageProvider { Current = language });
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    // Same shape as NsDateFieldTests.Leave, scoped to the DATE picker's own input — the
    // composite renders a second input for the time half. Leaving the box is what the browser
    // reports as a change, and it carries the whole text however it got there.
    static Task LeaveDate(IRenderedComponent<DateTimeValueHost> cut, string text)
    {
        return cut.InvokeAsync(() => cut.FindComponent<MudDatePicker>().Find("input").Change(text));
    }

    // Scoped to the DATE picker rather than taken off the whole tree: the composite renders a
    // second picker below it, and only the date half has a refusal to say anything about.
    static string? Refusal(IRenderedComponent<DateTimeValueHost> cut)
    {
        return cut.FindComponent<MudDatePicker>().FindAll(".mud-input-helper-text").FirstOrDefault()?.TextContent;
    }

    /// <summary>The composite's date half carries the same typing contract its sibling does
    /// (nsail#1196): no mask, so nothing is bound to the input event and no keystroke is a
    /// round trip that a later one can be overwritten by.</summary>
    [Fact]
    public void TheDateHalfTakesNoKeystrokeUntilItIsLeft()
    {
        ComposeCulture("es");

        var input = Render<DateTimeValueHost>().FindComponent<MudDatePicker>().Find("input");

        Assert.False(input.HasAttribute("blazor:oninput"));
        Assert.True(input.HasAttribute("blazor:onchange"));
    }

    /// <summary>Same text, different date, depending on which culture is active — the proof the
    /// composite reads the culture's own field order rather than a fixed one, exactly as
    /// NsDateField proves for itself. Two Facts rather than one: a BunitContext locks its
    /// Services the moment a component renders, so switching culture mid-test is not an
    /// option — each culture gets its own instance, xunit's usual one-instance-per-Fact.</summary>
    [Fact]
    public async Task ADateLeftInTheDateHalf_InSpanish_ReadsDayBeforeMonth()
    {
        ComposeCulture("es");

        DateTime? captured = null;
        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime>(this, v => captured = v)));

        await LeaveDate(cut, "07/08/2026");

        Assert.Equal(new DateTime(2026, 8, 7), captured);
    }

    [Fact]
    public async Task ADateLeftInTheDateHalf_InEnglish_ReadsMonthBeforeDay()
    {
        ComposeCulture("en");

        DateTime? captured = null;
        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime>(this, v => captured = v)));

        await LeaveDate(cut, "07/08/2026");

        Assert.Equal(new DateTime(2026, 7, 8), captured);
    }

    /// <summary>The separators need not be typed here either — the convenience moved off the
    /// keystroke and onto the parse, and the composite reads the same seam.</summary>
    [Fact]
    public async Task TheDateHalfTakesABareRunOfDigits()
    {
        ComposeCulture("es");

        DateTime? captured = null;
        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime>(this, v => captured = v)));

        await LeaveDate(cut, "07082026");

        Assert.Equal(new DateTime(2026, 8, 7), captured);
    }

    /// <summary>A value the page already holds — never typed by this user — still displays
    /// day-first under es. The field's own converter is what keeps that true: left to the
    /// vendor's, the picker formats against the AMBIENT culture and reads month-first, and the
    /// converter has to reach the picker BEFORE the value does (NsDateField.razor's note on
    /// parameter order) or the first date a page hands in is drawn by the wrong one.</summary>
    [Fact]
    public void AValueEchoedFromTheModel_DisplaysDayFirstUnderEs()
    {
        ComposeCulture("es");

        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 7, 15, 30, 0)));

        Assert.Equal("07/08/2026", cut.FindComponent<MudDatePicker>().Find("input").GetAttribute("value"));
    }

    /// <summary>The refusal half of the same contract, which this suite owed and did not carry:
    /// this control reads through the identical converter and the identical GetErrorText
    /// override, and the value it binds — DateTime, never nullable, the shape CreateAppointmentPage
    /// and RescheduleForm hand it — is the worse one to write a refusal through. Combine fills a
    /// missing date with DateTime.Today, so the turno would silently move to TODAY at the hour
    /// already chosen: a real date, in range, that nobody entered, under a visible refusal.</summary>
    [Fact]
    public async Task TextThatIsNotADate_IsRefusedAndTheStartDoesNotMoveToToday()
    {
        ComposeCulture("es");

        var captured = new DateTime(2026, 8, 4, 15, 30, 0);
        var writes = 0;
        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0))
            .Add(p => p.ValueChanged, v =>
            {
                captured = v;
                writes++;
            }));

        await LeaveDate(cut, "mañana");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));
        Assert.Equal("mañana", cut.FindComponent<MudDatePicker>().Find("input").GetAttribute("value"));
        Assert.Equal(0, writes);
        Assert.Equal(new DateTime(2026, 8, 4, 15, 30, 0), captured);
        Assert.NotEqual(DateTime.Today, captured.Date);
    }

    /// <summary>A year half typed is not a short year here either — the same reading NsDateField
    /// proves for itself, through the same parse.</summary>
    [Fact]
    public async Task AHalfTypedYear_IsRefusedRatherThanRead()
    {
        ComposeCulture("es");

        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0)));

        await LeaveDate(cut, "07/08/202");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));
    }

    /// <summary>A day the month does not have is refused, not rolled forward.</summary>
    [Fact]
    public async Task ADayTheMonthDoesNotHave_IsRefused()
    {
        ComposeCulture("es");

        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0)));

        await LeaveDate(cut, "31/02/2026");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));
    }

    /// <summary>A refusal is not a state this field gets stuck in either: the next readable date
    /// lands, keeping the hour, and the refusal goes with the text that earned it.</summary>
    [Fact]
    public async Task ARefusalLifts_AndTheNextDateLandsKeepingTheHour()
    {
        ComposeCulture("es");

        var captured = new DateTime(2026, 8, 4, 15, 30, 0);
        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0))
            .Add(p => p.ValueChanged, v => captured = v));

        await LeaveDate(cut, "mañana");

        Assert.Contains("dd/mm/aaaa", Refusal(cut));

        await LeaveDate(cut, "07/08/2026");

        Assert.Equal(new DateTime(2026, 8, 7, 15, 30, 0), captured);
        Assert.Null(Refusal(cut));
    }

    /// <summary>Emptying the date half is not a refusal — it is a gesture the field says nothing
    /// about, the same answer NsDateField gives for a box somebody cleared.</summary>
    [Fact]
    public async Task ClearingTheDateHalf_SaysNothingAboutIt()
    {
        ComposeCulture("es");

        var cut = Render<DateTimeValueHost>(ps => ps
            .Add(p => p.Value, new DateTime(2026, 8, 4, 15, 30, 0)));

        await LeaveDate(cut, "");

        Assert.Null(Refusal(cut));
    }
}

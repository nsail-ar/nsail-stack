// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>What a date box that cannot read its text owes the FORM around it (nsail#1196). The
/// field refuses out loud and keeps the value it was handed — but a refusal only the eye can see
/// is how a value nobody entered gets saved anyway: the person types a date, the box says it
/// could not read it, they press Guardar, and the row takes whatever the model still held. So the
/// refusal is posted to the EditContext through NsFieldBase's own message store — the seam
/// Required already travels — and Validate() answers false for exactly as long as the box holds
/// text nobody can read.</summary>
public sealed class NsDateRefusalSubmitTests : BunitContext, IAsyncLifetime
{
    CultureInfo? _originalCulture;
    CultureInfo? _originalUICulture;

    public NsDateRefusalSubmitTests()
    {
        // No MudPopoverProvider in this host, the NsFormRequiredTests recipe: no assertion here
        // is about a popover, and the vendor otherwise refuses to build the pickers' own.
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog(
        [
            new TwoLanguageStrings("Common.DateLetters", "dmy", "dma"),
            new TwoLanguageStrings("Problems.UnreadableDate", "This is not a date — write it as {shape}", "Esto no es una fecha — escribila como {shape}")
        ]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // The vendor pickers lay out against the ambient thread culture as well as the Culture
        // they are handed, so the pool thread is pinned here and handed back in DisposeAsync —
        // NsDateFieldTests carries the same pair for the same reason.
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = RegionCulture.For(new LanguageProvider { Current = "es" });
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        CultureInfo.CurrentCulture = _originalCulture!;
        CultureInfo.CurrentUICulture = _originalUICulture!;

        await DisposeAsync();
    }

    static Task LeaveDeliveryDate(IRenderedComponent<RefusedDateHost> cut, string text)
    {
        return cut.InvokeAsync(() => cut.FindComponent<NsDateField<DateOnly>>().Find("input").Change(text));
    }

    static Task LeaveStartDate(IRenderedComponent<RefusedDateHost> cut, string text)
    {
        return cut.InvokeAsync(() => cut.FindComponent<NsDateTimeField<DateTime>>().FindComponent<MudDatePicker>().Find("input").Change(text));
    }

    static Task Save(IRenderedComponent<RefusedDateHost> cut)
    {
        return cut.InvokeAsync(() => cut.Find("form").Submit());
    }

    [Fact]
    public async Task ARefusedDate_BlocksTheSubmitAndLeavesTheModelAlone()
    {
        var model = new RefusedDateModel
        {
            Delivery = new DateOnly(2026, 1, 3),
            Start = new DateTime(2026, 8, 4, 15, 30, 0)
        };
        var submitted = false;

        var cut = Render<RefusedDateHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await LeaveDeliveryDate(cut, "mañana");
        await Save(cut);

        Assert.False(submitted);
        Assert.Equal(new DateOnly(2026, 1, 3), model.Delivery);
    }

    /// <summary>The composite's date half owes the same answer, and the value it would otherwise
    /// have written is the more believable one: today at the hour already chosen.</summary>
    [Fact]
    public async Task ARefusedStartDate_BlocksTheSubmitAndLeavesTheStartAlone()
    {
        var model = new RefusedDateModel
        {
            Delivery = new DateOnly(2026, 1, 3),
            Start = new DateTime(2026, 8, 4, 15, 30, 0)
        };
        var submitted = false;

        var cut = Render<RefusedDateHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await LeaveStartDate(cut, "mañana");
        await Save(cut);

        Assert.False(submitted);
        Assert.Equal(new DateTime(2026, 8, 4, 15, 30, 0), model.Start);
    }

    /// <summary>nsail#1867: the composite's two boxes are ONE field, so a refusal marks both of
    /// them — a red date box beside a grey time box reads as the hour being fine when what was
    /// refused is the instant the pair spells together. The mark is read off the picker the
    /// vendor's error class and the invalid announcement land on, never counted in the markup:
    /// the vendor stamps .mud-input-error on several nested nodes of one errored field
    /// (NsFormProblemDisplayTests). And the sentence is still said ONCE — the date box draws the
    /// words, the time box draws the mark alone, with no empty line under it.</summary>
    [Fact]
    public async Task ARefusedStart_MarksBothOfItsBoxes_AndSaysItsSentenceOnce()
    {
        var model = new RefusedDateModel
        {
            Delivery = new DateOnly(2026, 1, 3),
            Start = new DateTime(2026, 8, 4, 15, 30, 0)
        };

        var cut = Render<RefusedDateHost>(p => p.Add(x => x.Model, model));

        await LeaveStartDate(cut, "mañana");

        var start = cut.FindComponent<NsDateTimeField<DateTime>>();

        Assert.True(start.FindComponent<MudDatePicker>().Instance.Error);

        // The vendor paints the outline from an ancestor's error class
        // (.mud-input-error .mud-input-outlined-border), so the mark is the class standing above
        // this box's own border — and the box says it is invalid for whatever cannot see red.
        var time = TimeBox(cut);

        Assert.Contains("mud-input-error", time.ClassList);
        Assert.NotNull(time.QuerySelector(".mud-input-outlined-border"));
        Assert.Equal("true", time.QuerySelector("input")!.GetAttribute("aria-invalid"));

        var refusal = Assert.Single(start.FindAll(".mud-input-helper-text"));

        Assert.Contains("dd/mm/aaaa", refusal.TextContent, StringComparison.Ordinal);
    }

    /// <summary>And the mark is not one the time box is born wearing: a composite nobody refused
    /// leaves both halves clean and spends no line on either.</summary>
    [Fact]
    public void AnUnrefusedStart_MarksNeitherOfItsBoxes()
    {
        var model = new RefusedDateModel
        {
            Delivery = new DateOnly(2026, 1, 3),
            Start = new DateTime(2026, 8, 4, 15, 30, 0)
        };

        var cut = Render<RefusedDateHost>(p => p.Add(x => x.Model, model));
        var start = cut.FindComponent<NsDateTimeField<DateTime>>();
        var time = TimeBox(cut);

        Assert.False(start.FindComponent<MudDatePicker>().Instance.Error);
        Assert.DoesNotContain("mud-input-error", time.ClassList);
        Assert.Equal("false", time.QuerySelector("input")!.GetAttribute("aria-invalid"));
        Assert.Empty(start.FindAll(".mud-input-helper-text"));
    }

    // The time half's own picker root, which is where a class handed to the component lands.
    static IElement TimeBox(IRenderedComponent<RefusedDateHost> cut)
    {
        return cut.FindComponent<NsDateTimeField<DateTime>>()
            .FindComponent<MudTimePicker>()
            .Find(".mud-picker");
    }

    /// <summary>The refusal is not a lock the form never gets out of: fixing the date lifts it and
    /// the same Guardar goes through, carrying the date that was finally readable.</summary>
    [Fact]
    public async Task FixingTheRefusedDate_LetsTheSameSubmitThrough()
    {
        var model = new RefusedDateModel
        {
            Delivery = new DateOnly(2026, 1, 3),
            Start = new DateTime(2026, 8, 4, 15, 30, 0)
        };
        var submitted = false;

        var cut = Render<RefusedDateHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await LeaveDeliveryDate(cut, "mañana");
        await Save(cut);

        Assert.False(submitted);

        await LeaveDeliveryDate(cut, "07/08/2026");
        await Save(cut);

        Assert.True(submitted);
        Assert.Equal(new DateOnly(2026, 8, 7), model.Delivery);
    }

    /// <summary>And a form nobody refused anything in still saves — the guard above must not be
    /// a refusal every date field is born holding.</summary>
    [Fact]
    public async Task AFormWithReadableDates_Submits()
    {
        var model = new RefusedDateModel
        {
            Delivery = new DateOnly(2026, 1, 3),
            Start = new DateTime(2026, 8, 4, 15, 30, 0)
        };
        var submitted = false;

        var cut = Render<RefusedDateHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Submitted, () => submitted = true));

        await Save(cut);

        Assert.True(submitted);
    }
}

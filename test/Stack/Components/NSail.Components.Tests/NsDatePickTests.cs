// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The other way a date reaches the box: picked off the calendar rather than typed. A
/// day chosen there is the value — the calendar closing over a box that still reads the date it
/// held before is the picker saying yes and the field saying no (Leonardo, v0.58.0, Encargar).</summary>
public sealed class NsDatePickTests : BunitContext, IAsyncLifetime
{
    CultureInfo? _originalCulture;
    CultureInfo? _originalUICulture;

    public NsDatePickTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog(
        [
            new TwoLanguageStrings("Common.DateLetters", "dmy", "dma"),
            new TwoLanguageStrings("Problems.UnreadableDate", "This is not a date — write it as {shape}", "Esto no es una fecha — escribila como {shape}")
        ]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _originalCulture = CultureInfo.CurrentCulture;
        _originalUICulture = CultureInfo.CurrentUICulture;

        var culture = RegionCulture.For(new LanguageProvider { Current = "es" });
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
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

    static async Task Pick(IRenderedComponent<DatePickerHost> cut, string day)
    {
        await cut.InvokeAsync(() => cut.Find(".mud-input-adornment button").Click());

        var button = cut.FindAll(".mud-picker-calendar-day")
            .First(candidate => candidate.TextContent.Trim() == day && !candidate.HasAttribute("disabled"));

        await cut.InvokeAsync(() => button.Click());
    }

    [Fact]
    public async Task A_day_picked_off_the_calendar_is_the_value()
    {
        var cut = Render<DatePickerHost>(p => p.Add(x => x.Value, new DateOnly(2026, 9, 15)));

        await Pick(cut, "25");

        cut.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 9, 25), cut.Instance.Value), TimeSpan.FromSeconds(2));
        Assert.Equal("25/09/2026", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task An_empty_box_takes_the_day_picked()
    {
        var cut = Render<DatePickerHost>();

        await Pick(cut, "25");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Instance.Value), TimeSpan.FromSeconds(2));
        Assert.Equal(25, cut.Instance.Value!.Value.Day);
    }
}

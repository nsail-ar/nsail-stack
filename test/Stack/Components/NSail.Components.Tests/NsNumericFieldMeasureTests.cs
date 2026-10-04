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

/// <summary>A measure reads in the reader's own numbers (nsail#1621). The vendor's Culture
/// defaults to InvariantCulture and its format to none, so a minimum stock of ten arrived from a
/// decimal(18,3) column as "10.000" — ten thousand, to anyone reading es-AR — and a comma typed
/// into the box was taken for a thousands separator. A document number, which rides the same
/// field, is not a measure and is never grouped.</summary>
public sealed class NsNumericFieldMeasureTests : BunitContext, IAsyncLifetime
{
    public NsNumericFieldMeasureTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton(new LanguageProvider { Current = "es" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
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

    static string Value(IRenderedComponent<IComponent> cut)
    {
        return cut.Find("input").GetAttribute("value")!;
    }

    [Fact]
    public void A_whole_measure_kept_to_three_decimals_reads_as_the_whole_it_is()
    {
        var cut = Render<NsNumericField<decimal?>>(ps => ps.Add(p => p.Value, 10.000m));

        Assert.Equal("10", Value(cut));
    }

    [Fact]
    public void A_measure_with_decimals_reads_them_in_the_reader_separator()
    {
        var cut = Render<NsNumericField<decimal?>>(ps => ps.Add(p => p.Value, 2.5m));

        Assert.Equal("2,5", Value(cut));
    }

    // The format is what the box hands back when the field is left, so a decimal it cannot
    // print is a decimal the binding loses.
    [Fact]
    public void No_decimal_the_figure_really_has_is_dropped()
    {
        var cut = Render<NsNumericField<decimal?>>(ps => ps.Add(p => p.Value, 1.234567m));

        Assert.Equal("1,234567", Value(cut));
    }

    // A factura's number rides this same field: grouping it would print the voucher 12345 as
    // "12.345" and make it a different document.
    [Fact]
    public void A_document_number_is_not_grouped()
    {
        var cut = Render<NsNumericField<long?>>(ps => ps.Add(p => p.Value, 12345L));

        Assert.Equal("12345", Value(cut));
    }

    [Fact]
    public async Task A_comma_typed_into_the_box_is_the_decimal_point_it_looks_like()
    {
        decimal? held = null;

        var cut = Render<NsNumericField<decimal?>>(ps => ps
            .Add(p => p.Value, 0m)
            .Add(p => p.ValueChanged, v => held = v));

        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = "2,5" });

        Assert.Equal(2.5m, held);
    }

    // A domain whose measure has a written convention hands the field its own reading; the plain
    // measure above is what every other box keeps.
    [Fact]
    public void A_domains_own_reading_is_what_the_box_writes()
    {
        var cut = Render<NsNumericField<decimal?>>(ps => ps
            .Add(p => p.Value, -2m)
            .Add(p => p.Format, "+0.00;-0.00;0.00"));

        Assert.Equal("-2,00", Value(cut));
    }

    // The format is also the notation the box hands back, so a reading the field cannot read
    // again is a value the binding loses on the way out.
    [Fact]
    public async Task What_that_reading_writes_is_what_the_box_reads_back()
    {
        decimal? held = null;

        var cut = Render<NsNumericField<decimal?>>(ps => ps
            .Add(p => p.Value, 0m)
            .Add(p => p.Format, "+0.00;-0.00;0.00")
            .Add(p => p.ValueChanged, v => held = v));

        await cut.Find("input").ChangeAsync(new ChangeEventArgs { Value = "+2,25" });

        Assert.Equal(2.25m, held);
    }

    // The text twin a list cell writes with reads the field character for character, so the
    // same figure is never two figures across one screen.
    [Fact]
    public void The_text_twin_reads_like_the_field()
    {
        var cut = Render<NsNumericField<decimal?>>(ps => ps.Add(p => p.Value, 10.000m));

        Assert.Equal(Value(cut), NsQuantityText.Of(10.000m));
        Assert.Equal("10", 10.000m.ToString(NsQuantityText.Format, CultureInfo.GetCultureInfo("es")));
        Assert.Equal("2,5", 2.5m.ToString(NsQuantityText.Format, CultureInfo.GetCultureInfo("es")));
    }
}

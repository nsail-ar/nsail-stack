// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A figure is typed in the reader's own notation (nsail#1620, the cristal's window):
/// the vendor's Culture defaults to invariant, which reads the decimal comma an es-AR keyboard
/// types as a THOUSANDS separator — "0,5" arrived as 5 and "-0,25" as -25, and the window then
/// bred its variants off the multiplied figure. The input is plain text with inputmode decimal,
/// so no browser locale stands between what is typed and what the converter parses.</summary>
public sealed class NsNumericFieldCultureTests : BunitContext, IAsyncLifetime
{
    readonly LanguageProvider _language = new() { Current = "es" };

    public NsNumericFieldCultureTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton(_language);
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

    [Theory]
    [InlineData("0,5", 0.5)]
    [InlineData("-0,25", -0.25)]
    [InlineData("-1,75", -1.75)]
    [InlineData("1,67", 1.67)]
    public async Task A_figure_typed_with_the_decimal_comma_arrives_as_the_fraction(string typed, double expected)
    {
        decimal? read = null;

        var cut = Render<NsNumericField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => read = v));

        await cut.InvokeAsync(() => cut.Find("input").Input(typed));

        Assert.Equal((decimal)expected, read);
    }

    // The other half of the round trip: what the field puts back on screen is the same
    // notation, so the figure a person typed is the figure they read.
    [Fact]
    public void A_fraction_reads_back_with_the_decimal_comma()
    {
        var cut = Render<NsNumericField<decimal?>>(ps => ps.Add(p => p.Value, -0.25m));

        Assert.Equal("-0,25", cut.Find("input").GetAttribute("value"));
    }

    // The percent field is the same vendor input under the same default, so it reads the
    // comma the same way.
    [Fact]
    public async Task The_percent_field_reads_the_decimal_comma_too()
    {
        decimal? read = null;

        var cut = Render<NsPercentField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => read = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("12,5"));

        Assert.Equal(12.5m, read);
    }

    // And the duration box, which is the same vendor input asked for a count of hours: half an
    // hour is "0,5", not five hours and a half day.
    [Fact]
    public async Task The_duration_field_reads_the_decimal_comma_too()
    {
        TimeSpan? read = null;

        var cut = Render<NsDurationField<TimeSpan?>>(ps => ps
            .Add(p => p.Value, (TimeSpan?)null)
            .Add(p => p.ValueChanged, v => read = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("1,5"));

        Assert.Equal(TimeSpan.FromHours(1.5), read);
    }

    // An English reader keeps the dot: the culture comes from the app's language, not from
    // whatever the server's thread happens to be set to.
    [Fact]
    public async Task An_english_reader_keeps_the_decimal_point()
    {
        _language.Current = "en";

        decimal? read = null;

        var cut = Render<NsNumericField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => read = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("0.5"));

        Assert.Equal(0.5m, read);
        Assert.Equal("0.5", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>The keypad's own key, under the Spanish notation (nsail#2165): a graduación typed
    /// "-2.25" on test.optical reached the parser as -225, because the dot read as a grouping —
    /// and -225 dioptres rode to the taller. The dot is the separator key there is, so a figure
    /// box reads it as one, and the box still writes the reader's comma back.</summary>
    [Theory]
    [InlineData("-2.25", -2.25)]
    [InlineData("0.75", 0.75)]
    [InlineData("-0.5", -0.5)]
    [InlineData("1.6667", 1.6667)]
    public async Task A_figure_typed_with_the_keypads_dot_arrives_as_the_fraction(string typed, double expected)
    {
        decimal? read = null;

        var cut = Render<NsNumericField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => read = v));

        await cut.InvokeAsync(() => cut.Find("input").Input(typed));

        Assert.Equal((decimal)expected, read);
    }

    // Both keys reach the same figure, which is the whole point: whichever the receta is typed
    // with, the lens is ground to the same graduation. The box writes the comma back either
    // way — that half is A_fraction_reads_back_with_the_decimal_comma above.
    [Fact]
    public async Task Both_separators_reach_the_same_figure()
    {
        foreach (var typed in new[] { "-2.25", "-2,25" })
        {
            decimal? read = null;

            var cut = Render<NsNumericField<decimal?>>(ps => ps
                .Add(p => p.Value, (decimal?)null)
                .Add(p => p.ValueChanged, v => read = v));

            await cut.InvokeAsync(() => cut.Find("input").Input(typed));

            Assert.Equal(-2.25m, read);
        }
    }

    // The whole figure-box family has the one keyboard, so it reads the one key: the percent
    // box, the duration box, and the money box at the mostrador, where "1.5" was fifteen pesos.
    [Fact]
    public async Task The_sibling_figure_boxes_read_the_keypads_dot_too()
    {
        decimal? percent = null;
        TimeSpan? hours = null;
        decimal? amount = null;

        var percentField = Render<NsPercentField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => percent = v));

        await percentField.InvokeAsync(() => percentField.Find("input").Input("12.5"));

        var durationField = Render<NsDurationField<TimeSpan?>>(ps => ps
            .Add(p => p.Value, (TimeSpan?)null)
            .Add(p => p.ValueChanged, v => hours = v));

        await durationField.InvokeAsync(() => durationField.Find("input").Input("1.5"));

        var moneyField = Render<NsMoneyField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => amount = v));

        await moneyField.InvokeAsync(() => moneyField.Find("input").Input("1.5"));

        Assert.Equal(12.5m, percent);
        Assert.Equal(TimeSpan.FromHours(1.5), hours);
        Assert.Equal(1.5m, amount);
    }

    // And the money box keeps the grouping it writes: an importe typed the way the field itself
    // prints it is the same importe.
    [Fact]
    public async Task The_money_box_keeps_its_own_grouping()
    {
        decimal? amount = null;

        var cut = Render<NsMoneyField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => amount = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("48.600,25"));

        Assert.Equal(48600.25m, amount);

        await cut.InvokeAsync(() => cut.Find("input").Input("48.600"));

        Assert.Equal(48600m, amount);
    }

    // The reading a dot still has under es-AR is not taken from it: a group is three digits and
    // the figure ends there, which is what an operator typing a factura number or a price means.
    [Theory]
    [InlineData("48.600", 48600)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("48.600,25", 48600.25)]
    public async Task A_grouped_figure_still_reads_as_the_whole_number(string typed, double expected)
    {
        decimal? read = null;

        var cut = Render<NsNumericField<decimal?>>(ps => ps
            .Add(p => p.Value, (decimal?)null)
            .Add(p => p.ValueChanged, v => read = v));

        await cut.InvokeAsync(() => cut.Find("input").Input(typed));

        Assert.Equal((decimal)expected, read);
    }
}

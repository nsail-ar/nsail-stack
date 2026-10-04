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

        await cut.InvokeAsync(() => cut.Find("input").Change(typed));

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

        await cut.InvokeAsync(() => cut.Find("input").Change("12,5"));

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

        await cut.InvokeAsync(() => cut.Find("input").Change("1,5"));

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

        await cut.InvokeAsync(() => cut.Find("input").Change("0.5"));

        Assert.Equal(0.5m, read);
        Assert.Equal("0.5", cut.Find("input").GetAttribute("value"));
    }
}

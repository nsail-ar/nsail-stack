// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Paging;
using static NSail.Components.Tests.Fixtures.SelectionTableHost;

namespace NSail.Components.Tests;

/// <summary>Chart ticks, grid labels and table counts follow the app language's region, not the
/// thread's: the server and the client threads differ, the language is the one thing they share.</summary>
public sealed class AppLanguageCultureTests : BunitContext, IAsyncLifetime
{
    readonly CultureInfo _thread = CultureInfo.CurrentCulture;

    public AppLanguageCultureTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Selected"] = "{0:N0} selected",
            ["Common.Selected.Plural"] = "{0:N0} selected",
            ["Common.SelectAll.Plural"] = "Select all {0:N0}",
            ["Common.ClearSelection"] = "Clear selection",
        })]));
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
        CultureInfo.CurrentCulture = _thread;
        await DisposeAsync();
    }

    [Fact]
    public void The_chart_ticks_and_tooltips_read_in_the_app_language()
    {
        var cut = Render<NsBarChart>(p => p
            .Add(x => x.Series, [new NsBarSeries("a", "A", NsChartColor.Neutral)])
            .Add(x => x.Buckets, [new NsBarBucket("b", new Dictionary<string, double> { ["a"] = 1.5 })]));

        Assert.Contains(cut.FindAll(".ns-bar-chart-axis"), e => e.TextContent.Contains(','));
        Assert.Contains(cut.FindAll("title"), e => e.TextContent.EndsWith("A: 1,5"));
    }

    [Fact]
    public void The_grid_labels_read_in_the_app_language()
    {
        var day = new DateOnly(2026, 8, 12);

        var cut = Render<NsTimeGrid>(p => p
            .Add(x => x.From, day)
            .Add(x => x.To, day)
            .Add(x => x.DayStart, new TimeOnly(8, 0))
            .Add(x => x.DayEnd, new TimeOnly(20, 0)));

        var es = CultureInfo.GetCultureInfo("es-AR");
        var marks = cut.FindAll(".ns-time-grid-mark").Select(e => e.TextContent).ToList();

        Assert.Contains(day.ToString("ddd d", es), marks);
        Assert.Contains(new TimeOnly(15, 0).ToString("t", es), marks);
        Assert.DoesNotContain(day.ToString("ddd d", CultureInfo.GetCultureInfo("en-US")), marks);
    }

    [Fact]
    public void The_table_s_counts_read_in_the_app_language()
    {
        var rows = new List<SelectableRow> { new(Guid.NewGuid(), "uno") };

        var cut = Render<SelectionTableHost>(p => p
            .Add(x => x.Rows, rows)
            .Add(x => x.Total, 1234)
            .Add(x => x.Selection, new Selection { All = true }));

        Assert.Contains("1.234 selected", cut.Find(".ns-selection-bar").TextContent);
    }
}

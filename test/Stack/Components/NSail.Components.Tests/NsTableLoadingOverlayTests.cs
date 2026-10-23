// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The vendor's own loading row (.mud-table-loading-row) is IN FLOW and drops every
/// row under it by the bar's own height — drawn from the ServerData await itself, whether or
/// not NsTable hands it a Loading parameter at all, so removing that parameter (NsTable.razor)
/// is not what stops it; a CSS rule neutralising the row is. What this file pins: the rule
/// exists, and the overlay that answers for loading instead stands AS A SIBLING of the table,
/// never inside it. bUnit paints nothing, so "centred" and "neutralised" are read off the
/// shipped stylesheet (CardIconRankTests' idiom) rather than measured — NSail.Optical.E2E is
/// where a real browser's layout is on trial.</summary>
public sealed class NsTableLoadingOverlayTests : BunitContext, IAsyncLifetime
{
    public NsTableLoadingOverlayTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Loading"] = "Cargando",
        })]));
        Services.AddSingleton<LanguageProvider>();
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

    /// <summary>The vendor draws its own row from its OWN await on the data source — a
    /// ServerData table draws .mud-table-loading-row whether or not NsTable hands it a Loading
    /// parameter at all, which bUnit's render below still shows (AngleSharp parses no
    /// stylesheet, so a node the house neutralises by CSS is still a node it can find). What
    /// takes its height away is read off the shipped stylesheet instead (CardIconRankTests'
    /// idiom) — the one thing a render cannot prove here.</summary>
    [Fact]
    public void TheVendorsOwnRowIsNeutralisedByCssRatherThanKeptOutOfTheMarkup()
    {
        var gate = new TaskCompletionSource();

        var cut = Render<EmptyTableHost>(p => p.Add(x => x.Gate, gate));

        Assert.NotEmpty(cut.FindAll(".mud-table-loading-row"));

        Assert.Contains(
            """
            .ns-table .mud-table-loading-row {
                display: none;
            }
            """,
            HouseStylesheet.Read());

        var host = cut.Find(".ns-table-loading-host");

        // The table is still the first thing in the host: the overlay a sibling of it, drawn
        // on top by its own absolute positioning rather than by standing in the table's way.
        Assert.Contains("ns-table", host.Children[0].ClassList);

        gate.SetResult();
    }

    [Fact]
    public void TheOverlayStandsWhileLoadingAndLeavesOnceTheFirstReadHasAnswered()
    {
        var gate = new TaskCompletionSource();

        var cut = Render<EmptyTableHost>(p => p.Add(x => x.Gate, gate));

        var overlay = cut.Find(".ns-table-loading-host .ns-table-loading-overlay");

        Assert.Equal("status", overlay.GetAttribute("role"));
        Assert.Equal("Cargando", overlay.GetAttribute("aria-label"));

        // Bare, not through cut.InvokeAsync: the continuation this unblocks is what fires
        // Runner's own IsRunningChanged and schedules the render WaitForAssertion below waits
        // on, and the established precedent for releasing a held read is to let that run on
        // its own rather than nest it inside another dispatcher call (NsTableEmptyTests).
        gate.SetResult();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ns-table-loading-overlay")));
    }

    [Fact]
    public void TheOverlayCarriesTheHouseSpinnerOverALightScrim()
    {
        var gate = new TaskCompletionSource();

        var cut = Render<EmptyTableHost>(p => p.Add(x => x.Gate, gate));

        var overlay = cut.Find(".ns-table-loading-host .ns-table-loading-overlay");

        // The scrim stands first in document order — painted before the spinner, which is
        // what puts the spinner on top with no z-index for either to carry.
        Assert.Contains("ns-table-loading-scrim", overlay.Children[0].ClassList);
        Assert.NotEmpty(overlay.QuerySelectorAll(".ns-spin"));

        gate.SetResult();
    }

    [Fact]
    public void TheOverlayIsCentredOverTheTableAndTakesThePointerWhileItStands()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            """
            .ns-table-loading-overlay {
                position: absolute;
                inset: 0;
                display: flex;
                align-items: center;
                justify-content: center;
                pointer-events: auto;
            }
            """,
            css);

        Assert.Contains(
            """
            .ns-table-loading-host {
                position: relative;
            }
            """,
            css);
    }

    [Fact]
    public void TheSpinnerInTheOverlayReadsThePrimaryInk()
    {
        Assert.Contains(
            """
            .ns-table-loading-overlay .mud-icon-root {
                position: relative;
                color: var(--mud-palette-primary);
            }
            """,
            HouseStylesheet.Read());
    }
}

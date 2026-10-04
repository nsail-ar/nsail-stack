// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Stories.md, "el modo stackeado respeta los Breakpoint de columna" — Leonardo
/// 2026-08-08, on WorkOrdersPage's saldos ("ocultar saldo paciente y a cobrar obra social...
/// así en mobile no se hace tan largo"). NsTh/NsTd already compute a column's importance into
/// d-c-none d-c-{bp}-table-cell (NsResponsive.CellClasses); the CSS half (ns-mud.css, the
/// stacked-mode block) is what actually hides the cell once MudTable turns the row into a
/// card — bUnit lays nothing out and runs no container query, so what is pinned here is the
/// DOM shape that rule depends on: the compound class the stacked-mode selector needs
/// (.mud-table-cell.d-c-none, re-enabled per breakpoint inside @container) actually reaching
/// the cell, and a column that declared nothing never carrying the hook at all.</summary>
public sealed class StackedColumnBreakpointTests : BunitContext, IAsyncLifetime
{
    public StackedColumnBreakpointTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
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

    [Fact]
    public void TheTableCarriesTheVendorsStackedBreakpointClass()
    {
        var cut = Render<StackedBreakpointHost>();

        // The stacked-mode rules key off this compound class (.ns-table.mud-sm-table) —
        // without it on the same element the CSS below never engages, same as
        // NsActionColumnTests pins for the action column's own stacked rules.
        var table = cut.Find(".ns-table");

        Assert.Contains("mud-sm-table", table.ClassName);
    }

    [Fact]
    public void ADeclaredColumnsCellCarriesTheHidingAndReenablingHooks()
    {
        var cut = Render<StackedBreakpointHost>();

        // Age declared Breakpoint="Lg" — CellClasses(Lg) emits "d-c-none d-c-lg-table-cell"
        // (NsResponsive.Suffix maps Lg to "lg"), which is exactly what the new stacked-mode
        // rule family in ns-mud.css selects: OFF by .mud-table-cell.d-c-none, back ON only
        // inside @container (min-width: 1280px) .mud-table-cell.d-c-lg-table-cell.
        var cell = cut.FindAll("tbody td")[1];

        Assert.Contains("mud-table-cell", cell.ClassList);
        Assert.Contains("d-c-none", cell.ClassList);
        Assert.Contains("d-c-lg-table-cell", cell.ClassList);
    }

    [Fact]
    public void AnUndeclaredColumnsCellCarriesNeitherHook()
    {
        var cut = Render<StackedBreakpointHost>();

        // Name declares no Breakpoint — CellClasses(None) returns null (NsResponsive), so the
        // cell never matches .d-c-none and the new stacked-mode rule leaves it alone: it
        // shows in the card exactly as it always has.
        var cell = cut.FindAll("tbody td")[0];

        Assert.DoesNotContain("d-c-none", cell.ClassList);
        Assert.DoesNotContain("d-c-lg-table-cell", cell.ClassList);
    }

    [Fact]
    public void TheActionCellDeclaresNothingAndCarriesNeitherHookEither()
    {
        var cut = Render<StackedBreakpointHost>();

        // A cell with no For and no DataLabel is the action column (ns-actions) — it never
        // registers with TableColumns, so it stays visible in the stacked card the same way
        // an undeclared field does.
        var cell = cut.FindAll("tbody td")[2];

        Assert.Contains("ns-actions", cell.ClassList);
        Assert.DoesNotContain("d-c-none", cell.ClassList);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1564: NsTable drew its own Confirmar/Cancelar and Editar/Eliminar cells
/// through a bare MudTd/MudTh, so they never picked up the "ns-actions" class every other
/// action column in the app reads (ns-mud.css's compact chrome) and rendered at the vendor's
/// larger default instead — the one column the grid never hides was also the one spot in the
/// tree not marked the way ui/hosts.md says the action column always is.</summary>
public sealed class NsTableActionsChromeTests : BunitContext, IAsyncLifetime
{
    public NsTableActionsChromeTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Add"] = "Agregar",
            ["Actions.Confirm"] = "Confirmar",
            ["Actions.Cancel"] = "Cancelar",
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

    [Fact]
    public void The_headers_own_action_column_carries_the_compact_chrome()
    {
        var cut = Render<TableAddHost>();

        var actionHeader = cut.FindAll("th").Single(th => th.TextContent.Trim().Length == 0);

        Assert.Contains("ns-actions", actionHeader.ClassList);
    }

    [Fact]
    public async Task An_open_rows_action_cell_carries_the_compact_chrome()
    {
        var cut = Render<TableAddHost>(p => p.Add(x => x.AddControl, false));

        await cut.InvokeAsync(() => cut.Find("#outside").Click());

        var actionCell = cut.FindAll("td").Single(td => td.QuerySelector("button") is not null);

        Assert.Contains("ns-actions", actionCell.ClassList);
    }
}

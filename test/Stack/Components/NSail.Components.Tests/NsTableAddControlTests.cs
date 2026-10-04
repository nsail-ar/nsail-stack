// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A page that adds from chrome of its own — Nueva Venta's tab strip over the counter's
/// lines — turns the table's add control off and calls Add(): the row opens exactly as the
/// control would have opened it, and there is no second control beside the first.</summary>
public sealed class NsTableAddControlTests : BunitContext, IAsyncLifetime
{
    public NsTableAddControlTests()
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

    static bool HasAdd(IRenderedComponent<TableAddHost> cut)
    {
        return cut.FindAll("button").Any(button => button.TextContent.Contains("Agregar", StringComparison.Ordinal)
            || button.GetAttribute("aria-label") == "Agregar");
    }

    /// <summary>The add control has one seat and the bar is it: a named collection draws Agregar
    /// at the bar's right edge, above the rows, and nowhere else (nsail#1821).</summary>
    [Fact]
    public void A_named_table_draws_its_add_control_on_its_bar()
    {
        var cut = Render<TableAddHost>(p => p.Add(x => x.Title, "Renglones"));

        Assert.True(HasAdd(cut));
        Assert.NotNull(cut.Find(".ns-collection-bar").QuerySelector("button[aria-label='Agregar']"));
    }

    /// <summary>And the other half of one seat: unnamed there is no bar, so there is nowhere for
    /// the control to live — which is why the Architecture suite refuses an unnamed grid that
    /// wires NewRow rather than letting it ship with no way in.</summary>
    [Fact]
    public void An_unnamed_table_draws_no_bar_and_no_add_control()
    {
        var cut = Render<TableAddHost>();

        Assert.Empty(cut.FindAll(".ns-collection-bar"));
        Assert.False(HasAdd(cut));
    }

    [Fact]
    public void Turned_off_it_draws_none_on_its_bar()
    {
        var cut = Render<TableAddHost>(p => p.Add(x => x.AddControl, false).Add(x => x.Title, "Renglones"));

        Assert.False(HasAdd(cut));
        Assert.NotEmpty(cut.FindAll(".ns-collection-bar"));
    }

    [Fact]
    public async Task Add_from_outside_opens_a_new_row_for_editing()
    {
        var cut = Render<TableAddHost>(p => p.Add(x => x.AddControl, false));

        await cut.InvokeAsync(() => cut.Find("#outside").Click());

        Assert.Equal(1, cut.Instance.Count);
        Assert.NotEmpty(cut.FindAll("tbody input"));
    }

    /// <summary>One row at a time, whoever asks: a second Add while a row is open would drop the
    /// half-typed one the control keeps safe by being disabled.</summary>
    [Fact]
    public async Task Add_while_a_row_is_open_opens_no_second_one()
    {
        var cut = Render<TableAddHost>(p => p.Add(x => x.AddControl, false));

        await cut.InvokeAsync(() => cut.Find("#outside").Click());
        await cut.InvokeAsync(() => cut.Find("#outside").Click());

        Assert.Equal(1, cut.Instance.Count);
    }
}

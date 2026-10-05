// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The under-field strip has two tenants and they take turns. A hint travels through
/// NsFieldHelper — a house block in normal flow, right after the field, never the vendor's own
/// HelperText slot, which is one string per control and is where the refusal goes. A refusal
/// takes the strip whole: the hint yields its box with its words, because both stand in flow
/// (nsail#796) and a hint kept invisible would spend an empty line beside the message it
/// stepped aside for. The two are the same .75rem/1.66 box, so a one-line refusal lands where
/// the hint stood.</summary>
public sealed class NsFieldHelperTests : BunitContext, IAsyncLifetime
{
    public NsFieldHelperTests()
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

    IRenderedComponent<FieldHelperHost> RenderHost(string? helper)
    {
        return Render<FieldHelperHost>(p => p
            .Add(x => x.Model, new FieldHelperModel { Name = "Ok" })
            .Add(x => x.Helper, helper)
            .Add(x => x.Submitted, () => { }));
    }

    /// <summary>The mechanism itself: a clean field's hint renders in the house block, in
    /// normal flow — never through the vendor's own HelperText slot, which is the slot the
    /// bug came from.</summary>
    [Fact]
    public void AHintRendersInTheHouseBlockUnderTheControl_NeverTheVendorSlot()
    {
        var cut = RenderHost("Elegí un paciente primero para continuar con la receta");

        var hint = Assert.Single(cut.FindAll(".ns-field-helper"));

        Assert.Contains("Elegí un paciente primero", hint.TextContent, StringComparison.Ordinal);

        // The vendor's own reserved-band slot never exists for a hint: nothing is clean AND
        // has a message there, because the mechanism no longer feeds it one.
        Assert.Empty(cut.FindAll(".mud-input-control-helper-container"));
    }

    /// <summary>No Helper set: the house block renders nothing at all, not an empty node —
    /// there is nothing to reserve for a hint that was never asked for.</summary>
    [Fact]
    public void NoHelperSet_TheHouseBlockRendersNothing()
    {
        var cut = RenderHost(helper: null);

        Assert.Empty(cut.FindAll(".ns-field-helper"));
    }

    /// <summary>While a refusal is present it OWNS the under-field strip and the hint steps
    /// aside whole — box included, not only its words. Both tenants stand in normal flow, so a
    /// hint that only went invisible would hold an empty line open directly beside the message
    /// it yielded to, and a two-line message would read with a blank line hanging off it.</summary>
    [Fact]
    public async Task AnErrorTakesTheStrip_AndTheHintYieldsItsBoxWithItsWords()
    {
        var cut = Render<FieldHelperHost>(p => p
            .Add(x => x.Model, new FieldHelperModel { Name = null })
            .Add(x => x.Helper, "Elegí un paciente primero para continuar con la receta")
            .Add(x => x.Submitted, () => { }));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        var slot = Assert.Single(cut.FindAll(".mud-input-control-helper-container"));

        Assert.Contains("Problems.Required", slot.TextContent, StringComparison.Ordinal);

        Assert.Empty(cut.FindAll(".ns-field-helper"));
    }

    /// <summary>Fixed again: the field returns clean, the message leaves the strip and the hint
    /// takes it back.</summary>
    [Fact]
    public async Task ClearingTheErrorBringsTheHintBack()
    {
        var model = new FieldHelperModel { Name = null };
        var cut = Render<FieldHelperHost>(p => p
            .Add(x => x.Model, model)
            .Add(x => x.Helper, "Elegí un paciente primero")
            .Add(x => x.Submitted, () => { }));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEmpty(cut.FindAll(".mud-input-control-helper-container"));
        Assert.Empty(cut.FindAll(".ns-field-helper"));

        model.Name = "Ok";

        await cut.InvokeAsync(() => cut.Find("input").Input("Ok"));

        Assert.Empty(cut.FindAll(".mud-input-control-helper-container"));

        var hint = Assert.Single(cut.FindAll(".ns-field-helper"));

        Assert.Contains("Elegí un paciente primero", hint.TextContent, StringComparison.Ordinal);
    }
}

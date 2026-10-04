// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>Story: "los campos se seleccionan al recibir foco" (Leonardo, 2026-08-07) — the
/// mostrador feature: tab into Importe showing 0,00, type 500, it replaces, no manual clear.
/// Wired once in NsFieldBase.SetInputAttributes (SelectOnFocus), as a plain "onfocus" HTML
/// attribute (no @ prefix, so Blazor never mediates it — the browser runs it as inline JS the
/// instant the input gets focus). Text, numeric and money opt in; a field usually edited in
/// place (email, phone, a lookup's search box, a picker) does not.
///
/// bUnit proves the ATTRIBUTE reaches the markup on every opted-in unmasked shape, and — just
/// as load-bearing — that it does NOT reach a masked one (measured below: the vendor's own
/// mask mode claims the input's onfocus for its own caret bookkeeping). What bUnit cannot
/// answer is how a touch tap differs from a mouse click on an UNMASKED field; that needs a
/// real device and is reasoned about in the commit, not measured here.</summary>
public sealed class NsFieldFocusSelectTests : BunitContext, IAsyncLifetime
{
    public NsFieldFocusSelectTests()
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
    public void APlainTextField_SelectsItsContentOnFocus()
    {
        var cut = Render<NsTextField>(ps => ps.Add(p => p.Value, "hello"));

        Assert.Equal("this.select()", cut.Find("input").GetAttribute("onfocus"));
    }

    /// <summary>MEASURED, not assumed: a masked field (a CUIT, a DNI, and by the same
    /// mechanism NsDateField's own vendor mask) does NOT carry the attribute. The vendor's
    /// masked mode renders its own Blazor-bound "blazor:onfocus" handler on the input — the
    /// mask engine's own caret/selection bookkeeping — and does not merge UserAttributes'
    /// plain "onfocus" in alongside it (dumped markup: no "onfocus" attribute at all once
    /// Mask is set, confirmed against an unmasked render of the same field carrying it).
    /// This is the answer the story asked to verify ("¿select-all + tipeo de dígito convive
    /// con el Mask de Mud?"): it does not reach the input in the first place, so there is no
    /// keystroke-level conflict to have — a masked field's focus behaviour is entirely the
    /// vendor's own, unchanged by this story.</summary>
    [Fact]
    public void AMaskedTextField_DoesNotCarryTheAttribute_TheVendorsOwnMaskOwnsFocus()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, "00-00000000-0")
            .Add(p => p.Value, "20123456789"));

        Assert.Null(cut.Find("input").GetAttribute("onfocus"));
    }

    [Fact]
    public void AMoneyField_SelectsItsContentOnFocus_TheMostradorCase()
    {
        var cut = Render<NsMoneyField<decimal?>>(ps => ps.Add(p => p.Value, 0m));

        Assert.Equal("this.select()", cut.Find("input").GetAttribute("onfocus"));
    }

    [Fact]
    public void ANumericField_SelectsItsContentOnFocus()
    {
        var cut = Render<NsNumericField<int?>>(ps => ps.Add(p => p.Value, 1));

        Assert.Equal("this.select()", cut.Find("input").GetAttribute("onfocus"));
    }

    [Fact]
    public void APercentField_SelectsItsContentOnFocus()
    {
        var cut = Render<NsPercentField<decimal?>>(ps => ps.Add(p => p.Value, 0.1m));

        Assert.Equal("this.select()", cut.Find("input").GetAttribute("onfocus"));
    }

    [Fact]
    public void ADurationField_SelectsItsContentOnFocus()
    {
        var cut = Render<NsDurationField<TimeSpan?>>(ps => ps.Add(p => p.Value, TimeSpan.FromHours(2)));

        Assert.Equal("this.select()", cut.Find("input").GetAttribute("onfocus"));
    }

    /// <summary>Scope discipline: an email field shares NsTextField's own MudTextField shell
    /// but is usually corrected in place rather than replaced wholesale, so it stays out —
    /// opting in is a per-family decision, never the base's default.</summary>
    [Fact]
    public void AnEmailField_DoesNotSelectOnFocus()
    {
        var cut = Render<NsEmailField>(ps => ps.Add(p => p.Value, "a@b.com"));

        Assert.Null(cut.Find("input").GetAttribute("onfocus"));
    }
}

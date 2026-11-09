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
/// Wired once in NsFieldBase.SetInputAttributes (SelectOnFocus) as a "data-ns-select-on-focus"
/// marker that ns.js acts on, so the markup carries no inline handler and a host whose CSP omits
/// 'unsafe-inline' keeps the feature. Text, numeric and money opt in; a field usually edited in
/// place (email, phone, a lookup's search box, a picker) does not.
///
/// bUnit proves the marker reaches the markup on every opted-in unmasked shape, and that it does
/// NOT reach a masked one. The browser behaviour needs a real device.</summary>
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

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }

    /// <summary>A masked field (a CUIT, a DNI, and by the same mechanism NsDateField's own vendor
/// mask) carries no marker: NsTextField.SelectOnFocus is false while Mask is set, because the mask
/// engine owns the caret and selection. Do not simplify that condition to true — the marker
/// would survive the vendor's attribute splat and the masked field would start selecting.</summary>
    [Fact]
    public void AMaskedTextField_DoesNotCarryTheAttribute_TheVendorsOwnMaskOwnsFocus()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, "00-00000000-0")
            .Add(p => p.Value, "20123456789"));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }

    [Fact]
    public void AMoneyField_SelectsItsContentOnFocus_TheMostradorCase()
    {
        var cut = Render<NsMoneyField<decimal?>>(ps => ps.Add(p => p.Value, 0m));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }

    [Fact]
    public void ANumericField_SelectsItsContentOnFocus()
    {
        var cut = Render<NsNumericField<int?>>(ps => ps.Add(p => p.Value, 1));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }

    [Fact]
    public void APercentField_SelectsItsContentOnFocus()
    {
        var cut = Render<NsPercentField<decimal?>>(ps => ps.Add(p => p.Value, 0.1m));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }

    [Fact]
    public void ADurationField_SelectsItsContentOnFocus()
    {
        var cut = Render<NsDurationField<TimeSpan?>>(ps => ps.Add(p => p.Value, TimeSpan.FromHours(2)));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }

    /// <summary>Scope discipline: an email field shares NsTextField's own MudTextField shell
    /// but is usually corrected in place rather than replaced wholesale, so it stays out —
    /// opting in is a per-family decision, never the base's default.</summary>
    [Fact]
    public void AnEmailField_DoesNotSelectOnFocus()
    {
        var cut = Render<NsEmailField>(ps => ps.Add(p => p.Value, "a@b.com"));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }
}

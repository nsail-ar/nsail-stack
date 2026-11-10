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
/// NOT reach a masked one; the listener's half is read from ns.js. The browser behaviour needs a
/// real device.</summary>
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

    /// <summary>A masked field (a CUIT, a DNI) carries no marker: NsTextField.SelectOnFocus is
    /// false while a Mask is set, because the vendor's mask engine owns the caret and selection.
    /// Do not simplify that condition to true — the marker would survive the vendor's attribute
    /// splat and the masked field would start selecting.</summary>
    [Fact]
    public void AMaskedTextField_CarriesNoMarker_TheMaskEngineOwnsTheCaret()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, "00-00000000-0")
            .Add(p => p.Value, "20123456789"));

        Assert.Null(cut.Find("input").GetAttribute("data-ns-select-on-focus"));
    }

    /// <summary>An empty Mask installs no mask (GetMask reads it as none), so the box is a plain
    /// one and selects like one.</summary>
    [Fact]
    public void AnEmptyMask_IsNoMask_TheFieldSelectsOnFocus()
    {
        var cut = Render<NsTextField>(ps => ps
            .Add(p => p.Mask, "")
            .Add(p => p.Value, "hello"));

        Assert.Equal("true", cut.Find("input").GetAttribute("data-ns-select-on-focus"));
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

    // The marker is only half the mechanism: read here, so a rename on either side goes red
    // instead of silently losing the feature in a browser suite nobody runs.
    [Fact]
    public void TheKeyboardLayer_SelectsTheFieldThatCarriesTheMarker()
    {
        var layer = ReadSource("src/Stack/BaseServices/NSail.BaseServices.WebApp/wwwroot/ns.js");

        Assert.Contains("t.getAttribute(\"data-ns-select-on-focus\") === \"true\")", layer, StringComparison.Ordinal);
        Assert.Contains("t.select();", layer, StringComparison.Ordinal);
    }

    static string ReadSource(string path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, path));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"{path} not found above the test assembly.");
    }
}

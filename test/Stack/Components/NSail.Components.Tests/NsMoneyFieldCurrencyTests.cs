// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>A figure in the install's own currency wears no code (nsail#1252, "nunca, si es una
/// sola"): the field reads exactly like the totals and saldo written beside it with "N2", and
/// only an amount its caller says is in ANOTHER currency carries that currency's code.</summary>
public sealed class NsMoneyFieldCurrencyTests : BunitContext, IAsyncLifetime
{
    public NsMoneyFieldCurrencyTests()
    {
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
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
        await DisposeAsync();
    }

    // The pages write their totals with ToString("N2") under the app's neutral language, so
    // that string is the one the field has to match character for character.
    static string Total(decimal amount)
    {
        return amount.ToString("N2", CultureInfo.GetCultureInfo("es"));
    }

    [Fact]
    public void A_field_in_the_install_currency_reads_like_the_total_beside_it()
    {
        var cut = Render<NsMoneyField<decimal>>(ps => ps.Add(p => p.Value, 48600m));

        Assert.Equal(Total(48600m), cut.Find("input").GetAttribute("value"));
        Assert.Equal("48.600,00", cut.Find("input").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".mud-input-adornment"));
        Assert.DoesNotContain("ARS", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("$", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_read_only_field_reads_the_same()
    {
        var cut = Render<NsMoneyField<decimal?>>(ps => ps
            .Add(p => p.Value, 3000m)
            .Add(p => p.ReadOnly, true));

        Assert.Equal(Total(3000m), cut.Find("input").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".mud-input-adornment"));
    }

    [Fact]
    public void A_field_told_another_currency_wears_its_code_beside_the_figure()
    {
        var cut = Render<NsMoneyField<decimal>>(ps => ps
            .Add(p => p.Value, 48600m)
            .Add(p => p.Currency, "USD"));

        Assert.Equal(Total(48600m), cut.Find("input").GetAttribute("value"));
        Assert.Contains("USD", cut.Find(".mud-input-adornment").TextContent, StringComparison.Ordinal);
    }

    // The text twin a total or a list cell writes with: the bare "N2" unless a code is named.
    [Fact]
    public void The_text_twin_wears_a_code_only_when_named()
    {
        Assert.Equal(48600m.ToString("N2"), NsMoneyText.Of(48600m));
        Assert.Equal(48600m.ToString("N2"), NsMoneyText.Of(48600m, null));
        Assert.Equal(48600m.ToString("N2"), NsMoneyText.Of(48600m, ""));
        Assert.Equal($"{48600m.ToString("N2")} USD", NsMoneyText.Of(48600m, "USD"));
    }
}

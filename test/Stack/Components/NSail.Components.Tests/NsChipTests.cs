// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>A chip names a value; given a severity it names a state, tinted and not filled,
/// and given an Href the whole chip is its link.</summary>
public sealed class NsChipTests : BunitContext
{
    public NsChipTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Fact]
    public void A_chip_with_no_severity_is_the_neutral_filled_chip()
    {
        var cut = Render<NsChip>(p => p.Add(c => c.Text, "Rol"));

        Assert.Contains("mud-chip-filled", cut.Markup);
        Assert.DoesNotContain("mud-chip-color-error", cut.Markup);
    }

    [Fact]
    public void A_chip_with_a_severity_wears_its_tint_and_not_a_fill()
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Modo de prueba")
            .Add(c => c.Severity, NsSeverity.Error));

        Assert.Contains("mud-chip-text", cut.Markup);
        Assert.Contains("mud-chip-color-error", cut.Markup);
        Assert.DoesNotContain("mud-chip-filled", cut.Markup);
    }

    [Fact]
    public void A_chip_with_an_href_is_an_anchor_to_it()
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Modo de prueba")
            .Add(c => c.Href, "/settings/test-mode"));

        Assert.Equal("/settings/test-mode", cut.Find("a").GetAttribute("href"));
    }
}

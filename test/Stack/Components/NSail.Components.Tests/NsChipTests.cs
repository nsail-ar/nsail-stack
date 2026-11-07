// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>A chip names a value; given a severity it names a state, tinted — or filled when
/// Solid — and given an Href the whole chip is its link; given a Tooltip its name says the
/// sentence after the text.</summary>
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
    public void A_solid_chip_wears_its_severity_as_the_filled_face()
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Modo de prueba")
            .Add(c => c.Severity, NsSeverity.Warning)
            .Add(c => c.Solid, true));

        Assert.Contains("mud-chip-filled", cut.Markup);
        Assert.Contains("mud-chip-color-warning", cut.Markup);
        Assert.DoesNotContain("mud-chip-text", cut.Markup);
    }

    [Fact]
    public void A_chip_with_an_href_is_an_anchor_to_it()
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Modo de prueba")
            .Add(c => c.Href, "/settings/test-mode"));

        Assert.Equal("/settings/test-mode", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void A_chip_with_a_tooltip_is_named_by_its_text_and_then_the_sentence()
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Rol")
            .Add(c => c.Tooltip, "Asignado por el administrador")
            .Add(c => c.Href, "/roles"));

        Assert.Equal("Rol: Asignado por el administrador", cut.Find("a").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_sentence_that_opens_with_the_text_is_the_whole_name()
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Modo prueba")
            .Add(c => c.Tooltip, "Modo prueba: los cobros, las facturas y los mensajes no son reales")
            .Add(c => c.OnClose, () => { }));

        Assert.Equal("Modo prueba: los cobros, las facturas y los mensajes no son reales",
            cut.Find(".mud-chip").GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Rol")]
    public void A_chip_with_no_sentence_beyond_its_text_keeps_its_bare_text_as_its_name(string? tooltip)
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Rol")
            .Add(c => c.Tooltip, tooltip));

        Assert.False(cut.Find(".mud-chip").HasAttribute("aria-label"));
        Assert.Equal("Rol", cut.Find(".mud-chip").TextContent.Trim());
    }

    [Fact]
    public void A_chip_declares_that_a_tap_opens_its_whisper()
    {
        var cut = Render<NsChip>(p => p
            .Add(c => c.Text, "Modo prueba")
            .Add(c => c.Tooltip, "Modo prueba: los cobros no son reales"));

        Assert.True(cut.Find(".mud-chip").HasAttribute("data-ns-whisper-tap"));
    }
}

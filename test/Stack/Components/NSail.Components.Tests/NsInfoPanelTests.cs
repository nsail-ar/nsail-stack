// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Tones;

namespace NSail.Components.Tests;

/// <summary>An info panel is one surface for loose facts: its layout sets them, its severity
/// tints it and swaps its mark, and the mark always says what the panel is.</summary>
public sealed class NsInfoPanelTests : BunitContext
{
    public NsInfoPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
    }

    [Fact]
    public void A_panel_is_facts_and_neutral_by_default()
    {
        var cut = Render<NsInfoPanel>(p => p.Add(c => c.Label, "Datos del servidor"));

        var panel = cut.Find("section.ns-info-panel");
        Assert.Contains("ns-info-panel-facts", panel.ClassName);
        Assert.DoesNotContain("ns-status-", panel.ClassName);
    }

    [Fact]
    public void The_mark_carries_what_the_panel_is_as_its_name_and_its_whisper()
    {
        var cut = Render<NsInfoPanel>(p => p.Add(c => c.Label, "Datos del servidor"));

        var mark = cut.Find(".ns-info-panel-mark");
        Assert.Equal("Datos del servidor", mark.QuerySelector("[data-ns-whisper]")!.GetAttribute("data-ns-whisper"));
        Assert.Contains("Datos del servidor", mark.InnerHtml);
        Assert.Contains(NsIcons.Info.Markup, cut.Markup);
    }

    [Theory]
    [InlineData(NsSeverity.Warning, "ns-status-warning")]
    [InlineData(NsSeverity.Error, "ns-status-error")]
    [InlineData(NsSeverity.Success, "ns-status-success")]
    public void A_severity_tints_the_panel_and_swaps_the_mark(NsSeverity severity, string tone)
    {
        var cut = Render<NsInfoPanel>(p => p
            .Add(c => c.Label, "Aviso")
            .Add(c => c.Severity, severity));

        Assert.Contains(tone, cut.Find("section.ns-info-panel").ClassName);
        Assert.DoesNotContain(NsIcons.Info.Markup, cut.Markup);
        var glyph = severity switch
        {
            NsSeverity.Warning => NsIcons.Warning,
            NsSeverity.Error => NsIcons.Error,
            _ => NsIcons.Success
        };
        Assert.Contains(glyph.Markup, cut.Markup);
    }

    [Fact]
    public void A_list_panel_sets_its_facts_as_rows_under_its_title()
    {
        var cut = Render<NsInfoPanel>(p => p
            .Add(c => c.Label, "Estado del tenant")
            .Add(c => c.Layout, NsInfoLayout.List)
            .Add(c => c.Title, "Estado")
            .Add(c => c.ChildContent, Facts(("Activo", "Sí"), ("Plan", "Pro"))));

        Assert.Contains("ns-info-panel-list", cut.Find("section").ClassName);
        Assert.Equal("Estado", cut.Find(".ns-info-panel-title").TextContent.Trim());
        Assert.Equal(["Activo", "Plan"], cut.FindAll("dt").Select(d => d.TextContent));
        Assert.Equal(["Sí", "Pro"], cut.FindAll("dd").Select(d => d.TextContent));
    }

    [Fact]
    public void A_panel_with_no_title_or_actions_draws_neither()
    {
        var cut = Render<NsInfoPanel>(p => p.Add(c => c.Label, "Datos"));

        Assert.Empty(cut.FindAll(".ns-info-panel-title"));
        Assert.Empty(cut.FindAll(".ns-info-panel-actions"));
    }

    [Fact]
    public void The_actions_ride_their_own_slot()
    {
        var cut = Render<NsInfoPanel>(p => p
            .Add(c => c.Label, "Datos")
            .Add(c => c.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<button>Reanudar</button>"))));

        Assert.Equal("Reanudar", cut.Find(".ns-info-panel-actions button").TextContent);
    }

    [Fact]
    public void Info_is_the_neutral_and_not_a_second_reading_of_it()
    {
        var cut = Render<NsInfoPanel>(p => p
            .Add(c => c.Label, "Datos")
            .Add(c => c.Severity, NsSeverity.Info));

        Assert.DoesNotContain("ns-status-", cut.Find("section.ns-info-panel").ClassName);
        Assert.Contains(NsIcons.Info.Markup, cut.Markup);
    }

    [Fact]
    public void The_label_is_said_once_by_the_mark_and_the_section_takes_only_the_title()
    {
        var untitled = Render<NsInfoPanel>(p => p.Add(c => c.Label, "Datos"));
        var titled = Render<NsInfoPanel>(p => p
            .Add(c => c.Label, "Datos")
            .Add(c => c.Title, "Estado"));

        Assert.False(untitled.Find("section").HasAttribute("aria-label"));
        Assert.Contains("Datos", untitled.Find(".ns-info-panel-mark").InnerHtml);
        Assert.Equal("Estado", titled.Find("section").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_list_row_that_is_not_a_fact_takes_a_facts_air_and_its_rule()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains("padding-block: 0.375rem;", Rule(css, ".ns-info-panel-list .ns-info-panel-body > * {"));
        Assert.Contains("border-top: 1px solid var(--mud-palette-lines-default);", Rule(css, ".ns-info-panel-list .ns-info-panel-body > * + * {"));
    }

    [Fact]
    public void A_class_composes_over_the_panels_own()
    {
        var cut = Render<NsInfoPanel>(p => p
            .Add(c => c.Label, "Datos")
            .Add(c => c.Class, "mb-4"));

        var panel = cut.Find("section");
        Assert.Contains("ns-info-panel", panel.ClassList);
        Assert.Contains("mb-4", panel.ClassList);
    }

    // bUnit paints nothing, so the ruled promises that live in CSS are read off the stylesheet
    // the app ships.
    [Fact]
    public void The_mark_spans_the_title_and_the_body_so_it_is_top_right_with_or_without_a_title()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            """
                grid-template-columns: minmax(0, 1fr) auto;
                grid-template-areas: "title mark" "body mark" "actions actions";
            """,
            css);
        Assert.Contains(
            """
            .ns-info-panel-mark {
                grid-area: mark;
                align-self: start;
            """,
            css);
    }

    [Fact]
    public void The_surface_is_the_raised_rung_with_the_hairline_and_never_the_accent()
    {
        var css = HouseStylesheet.Read();
        var panel = Rule(css, ".ns-info-panel {");

        Assert.Contains("border: 1px solid var(--mud-palette-lines-default);", panel);
        Assert.Contains("background-color: var(--mud-palette-background-gray);", panel);
        Assert.DoesNotContain("primary", panel);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("warning")]
    [InlineData("success")]
    public void A_tone_washes_the_raised_rung_with_the_raw_severity_at_the_row_wash(string severity)
    {
        var css = HouseStylesheet.Read();

        Assert.Contains(
            $$"""
            .ns-info-panel.ns-status-{{severity}} {
                background-color: color-mix(in srgb, var(--mud-palette-{{severity}}) {{Percent(BrandTone.RowWash)}}%, var(--mud-palette-background-gray));
            }
            """,
            css);
        Assert.Contains("border-color: var(--ns-status-color);", Rule(css, """.ns-info-panel[class*="ns-status-"] {"""));
    }

    [Fact]
    public void Facts_wrap_and_a_long_label_or_value_breaks_inside_its_box_so_a_phone_never_scrolls_sideways()
    {
        var css = HouseStylesheet.Read();

        Assert.Contains("flex-wrap: wrap;", Rule(css, ".ns-info-panel-facts .ns-info-panel-body {"));
        Assert.Contains("flex-wrap: wrap;", Rule(css, ".ns-info-panel-list .ns-fact {"));
        Assert.Contains("overflow-wrap: anywhere;", Rule(css, ".ns-fact {"));
        Assert.Contains("min-width: 0;", Rule(css, ".ns-info-panel-body {"));
    }

    static string Rule(string css, string selector)
    {
        var start = css.IndexOf("\n" + selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no rule {selector}");

        return css[start..css.IndexOf('}', start)];
    }

    static string Percent(double weight)
    {
        return Math.Round(weight * 100, 4).ToString(CultureInfo.InvariantCulture);
    }

    static RenderFragment Facts(params (string Label, string Value)[] facts)
    {
        return builder =>
        {
            foreach (var (label, value) in facts)
            {
                builder.OpenComponent<NsFact>(0);
                builder.AddAttribute(1, nameof(NsFact.Label), label);
                builder.AddAttribute(2, nameof(NsFact.ChildContent), (RenderFragment)(b => b.AddContent(0, value)));
                builder.CloseComponent();
            }
        };
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;

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

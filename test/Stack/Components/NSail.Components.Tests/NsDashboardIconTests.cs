// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Icons;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

public sealed class IconStampedTestCard : ComponentBase
{
    [Parameter]
    public Glyph? Icon { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Icon is null)
        {
            return;
        }

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "test-card-icon");
        builder.CloseElement();
    }
}

/// <summary>DashboardItem.Icon flows from the contributor, through NsDashboard's
/// DynamicComponent parameters, into the card — this pins that wiring without a
/// MudBlazor-backed card: an item with an Icon renders it, one without renders none.</summary>
public sealed class NsDashboardIconTests : BunitContext
{
    sealed class TestContributor(DashboardItem item) : IDashboardContributor
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>([item]);
        }
    }

    public NsDashboardIconTests()
    {
        Services.AddAuthorizationCore();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsDashboardIconTests).Assembly, []));
    }

    [Fact]
    public void AnItemWithAnIcon_RendersIt()
    {
        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "WithIcon", CardType = typeof(IconStampedTestCard), Icon = new Glyph("<path />") }));

        var cut = Render<NsDashboard>();

        Assert.Single(cut.FindAll(".test-card-icon"));
    }

    [Fact]
    public void AnItemWithoutAnIcon_RendersNone()
    {
        Services.AddSingleton<IDashboardContributor>(new TestContributor(
            new DashboardItem { Name = "NoIcon", CardType = typeof(IconStampedTestCard) }));

        var cut = Render<NsDashboard>();

        Assert.Empty(cut.FindAll(".test-card-icon"));
    }
}

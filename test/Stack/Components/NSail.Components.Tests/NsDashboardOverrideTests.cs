// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

public sealed class HiddenTestCard : ComponentBase
{
    [Parameter]
    public Glyph? Icon { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "id", "hidden-card");
        builder.CloseElement();
    }
}

public sealed class KeptTestCard : ComponentBase
{
    [Parameter]
    public Glyph? Icon { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "id", "kept-card");
        builder.CloseElement();
    }
}

/// <summary>The override rule at render height: a card the app hid is out of the home's markup,
/// not merely styled away, and one it only reordered or resized keeps everything the kit gave
/// it. The unit rule is DashboardItemMergeTests; this is the wiring between it and the
/// grid.</summary>
public sealed class NsDashboardOverrideTests : BunitContext
{
    sealed class TestContributor(params DashboardItem[] items) : IDashboardContributor
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(items);
        }
    }

    public NsDashboardOverrideTests()
    {
        Services.AddAuthorizationCore();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsDashboardOverrideTests).Assembly, []));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // The kit's contributor first and the app's second, which is the order both products'
    // composition roots register them in.
    IRenderedComponent<NsDashboard> Show(DashboardItem[] planted, params DashboardItem[] arranged)
    {
        Services.AddSingleton<IDashboardContributor>(new TestContributor(planted));
        Services.AddSingleton<IDashboardContributor>(new TestContributor(arranged));

        return Render<NsDashboard>();
    }

    [Fact]
    public void AHiddenCardIsOutOfTheHome()
    {
        var cut = Show(
            [
                new DashboardItem { Name = "Hidden", CardType = typeof(HiddenTestCard), Weight = 5 },
                new DashboardItem { Name = "Kept", CardType = typeof(KeptTestCard), Weight = 10 }
            ],
            new DashboardItem { Name = "Hidden", Visible = false });

        Assert.Empty(cut.FindAll("#hidden-card"));
        Assert.NotNull(cut.Find("#kept-card"));
        Assert.Single(cut.FindAll(".ns-dashboard-item"));
    }

    // The grid cell goes with the card: a hidden card leaving its wrapper behind would hold an
    // empty column open, which is the same defect a mute card pays for.
    [Fact]
    public void AHiddenCardTakesItsCellWithIt()
    {
        var cut = Show(
            [new DashboardItem { Name = "Hidden", CardType = typeof(HiddenTestCard) }],
            new DashboardItem { Name = "Hidden", Visible = false });

        Assert.Empty(cut.FindAll(".ns-dashboard-item"));
    }

    [Fact]
    public void AnOverrideReordersTheCardsItNames()
    {
        var cut = Show(
            [
                new DashboardItem { Name = "Hidden", CardType = typeof(HiddenTestCard), Weight = 5 },
                new DashboardItem { Name = "Kept", CardType = typeof(KeptTestCard), Weight = 10 }
            ],
            new DashboardItem { Name = "Kept", Weight = 1 });

        Assert.Equal(
            ["kept-card", "hidden-card"],
            cut.FindAll(".ns-dashboard-item span").Select(element => element.Id));
    }

    [Fact]
    public void AnOverrideResizesTheCardItNames()
    {
        var cut = Show(
            [new DashboardItem { Name = "Kept", CardType = typeof(KeptTestCard) }],
            new DashboardItem { Name = "Kept", Tall = true });

        Assert.NotNull(cut.Find(".ns-dashboard-tall"));
    }

    // A card two contributors name renders once: the concatenation this merge replaced rendered
    // it twice, and the duplicate is invisible to every test that only counts names.
    [Fact]
    public void ACardTwoContributorsNameRendersOnce()
    {
        var cut = Show(
            [new DashboardItem { Name = "Kept", CardType = typeof(KeptTestCard) }],
            new DashboardItem { Name = "Kept", CardType = typeof(KeptTestCard) });

        Assert.Single(cut.FindAll("#kept-card"));
    }
}

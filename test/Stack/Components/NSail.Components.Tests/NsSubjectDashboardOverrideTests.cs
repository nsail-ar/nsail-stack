// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Icons;

namespace NSail.Components.Tests;

public sealed class HiddenSubjectTestCard : ComponentBase
{
    [Parameter]
    public Glyph? Icon { get; set; }

    [Parameter]
    public TestSubject? Subject { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "id", "hidden-subject-card");
        builder.CloseElement();
    }
}

public sealed class KeptSubjectTestCard : ComponentBase
{
    [Parameter]
    public Glyph? Icon { get; set; }

    [Parameter]
    public TestSubject? Subject { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "id", "kept-subject-card");
        builder.CloseElement();
    }
}

/// <summary>A subject dashboard merges the way the home does — a Ficha is the same host about
/// one row, so an app that mounted a kit's cards there arranges them by naming one, and a card
/// two contributors name renders once.</summary>
public sealed class NsSubjectDashboardOverrideTests : BunitContext
{
    sealed class TestContributor(params DashboardItem[] items) : IDashboardContributor<TestSubject>
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems(TestSubject subject)
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(items);
        }
    }

    public NsSubjectDashboardOverrideTests()
    {
        Services.AddAuthorizationCore();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    IRenderedComponent<NsSubjectDashboard<TestSubject>> Show(
        DashboardItem[] planted, params DashboardItem[] arranged)
    {
        Services.AddSingleton<IDashboardContributor<TestSubject>>(new TestContributor(planted));
        Services.AddSingleton<IDashboardContributor<TestSubject>>(new TestContributor(arranged));

        return Render<NsSubjectDashboard<TestSubject>>(parameters => parameters.Add(
            dashboard => dashboard.Subject,
            new TestSubject { Name = "Ana" }));
    }

    [Fact]
    public void AHiddenCardIsOutOfTheFicha()
    {
        var cut = Show(
            [
                new DashboardItem { Name = "Hidden", CardType = typeof(HiddenSubjectTestCard), Weight = 5 },
                new DashboardItem { Name = "Kept", CardType = typeof(KeptSubjectTestCard), Weight = 10 }
            ],
            new DashboardItem { Name = "Hidden", Visible = false });

        Assert.Empty(cut.FindAll("#hidden-subject-card"));
        Assert.NotNull(cut.Find("#kept-subject-card"));
    }

    [Fact]
    public void AnOverrideReordersTheCardsItNames()
    {
        var cut = Show(
            [
                new DashboardItem { Name = "Hidden", CardType = typeof(HiddenSubjectTestCard), Weight = 5 },
                new DashboardItem { Name = "Kept", CardType = typeof(KeptSubjectTestCard), Weight = 10 }
            ],
            new DashboardItem { Name = "Kept", Weight = 1 });

        Assert.Equal(
            ["kept-subject-card", "hidden-subject-card"],
            cut.FindAll(".ns-dashboard span").Select(element => element.Id));
    }

    [Fact]
    public void ACardTwoContributorsNameRendersOnce()
    {
        var cut = Show(
            [new DashboardItem { Name = "Kept", CardType = typeof(KeptSubjectTestCard) }],
            new DashboardItem { Name = "Kept", CardType = typeof(KeptSubjectTestCard) });

        Assert.Single(cut.FindAll("#kept-subject-card"));
    }
}

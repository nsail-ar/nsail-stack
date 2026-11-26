// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Components;

namespace NSail.Components.Tests;

/// <summary>The subject dashboard's Empty slot: what a portal tells a session whose role reaches
/// none of the cards about its subject — appearing exactly when the gate left nothing, never in
/// front of cards about to arrive, and never where no Empty was given.</summary>
public sealed class NsSubjectDashboardEmptyTests : BunitContext
{
    const string EmptyMarkup = "<p id=\"nothing-here\">portal</p>";

    sealed class TestContributor(Func<TestSubject, Task<IReadOnlyList<DashboardItem>>> items)
        : IDashboardContributor<TestSubject>
    {
        public TestContributor(Task<IReadOnlyList<DashboardItem>> items)
            : this(_ => items)
        {
        }

        public Task<IReadOnlyList<DashboardItem>> GetItems(TestSubject subject)
        {
            return items(subject);
        }
    }

    static readonly RenderFragment EmptySlot = builder => builder.AddMarkupContent(0, EmptyMarkup);

    public NsSubjectDashboardEmptyTests()
    {
        AddAuthorization().SetNotAuthorized();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    IRenderedComponent<NsSubjectDashboard<TestSubject>> Show(Task<IReadOnlyList<DashboardItem>> items)
    {
        Services.AddSingleton<IDashboardContributor<TestSubject>>(new TestContributor(items));

        return Render<NsSubjectDashboard<TestSubject>>(parameters => parameters
            .Add(dashboard => dashboard.Subject, new TestSubject { Name = "Ana" })
            .Add(dashboard => dashboard.Empty, EmptySlot));
    }

    IRenderedComponent<NsSubjectDashboard<TestSubject>> Show(params DashboardItem[] items)
    {
        return Show(Task.FromResult<IReadOnlyList<DashboardItem>>(items));
    }

    [Fact]
    public void TheEmptySlotRendersWhenTheSessionMayOpenNoCard()
    {
        var cut = Show(new DashboardItem { Name = "Denied", CardType = typeof(AdminTestCard) });

        Assert.NotNull(cut.Find("#nothing-here"));
    }

    [Fact]
    public void TheEmptySlotRendersWhenNobodyContributedAnything()
    {
        var cut = Show();

        Assert.NotNull(cut.Find("#nothing-here"));
    }

    [Fact]
    public void TheEmptySlotWaitsForTheGateToAnswer()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<DashboardItem>>();

        var cut = Show(pending.Task);

        Assert.Empty(cut.FindAll("#nothing-here"));

        pending.SetResult([]);

        // The release resumes the host's OnParametersSetAsync, which this act does not await.
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#nothing-here")));
    }

    [Fact]
    public void TheEmptySlotStaysAwayWhenACardIsAllowed()
    {
        var cut = Show(new DashboardItem { Name = "Kept", CardType = typeof(KeptSubjectTestCard) });

        Assert.Empty(cut.FindAll("#nothing-here"));
        Assert.NotNull(cut.Find("#kept-subject-card"));
    }

    [Fact]
    public void WithNoEmptySlotADeniedDashboardRendersNothing()
    {
        Services.AddSingleton<IDashboardContributor<TestSubject>>(new TestContributor(
            Task.FromResult<IReadOnlyList<DashboardItem>>(
                [new DashboardItem { Name = "Denied", CardType = typeof(AdminTestCard) }])));

        var cut = Render<NsSubjectDashboard<TestSubject>>(parameters => parameters.Add(
            dashboard => dashboard.Subject, new TestSubject { Name = "Ana" }));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void ANewSubjectsPendingGateTakesThePreviousSubjectsEmptyAway()
    {
        var ana = new TestSubject { Name = "Ana" };
        var bruno = new TestSubject { Name = "Bruno" };
        var pending = new TaskCompletionSource<IReadOnlyList<DashboardItem>>();

        Services.AddSingleton<IDashboardContributor<TestSubject>>(new TestContributor(subject =>
            subject == ana ? Task.FromResult<IReadOnlyList<DashboardItem>>([]) : pending.Task));

        var cut = Render<NsSubjectDashboard<TestSubject>>(parameters => parameters
            .Add(dashboard => dashboard.Subject, ana)
            .Add(dashboard => dashboard.Empty, EmptySlot));

        Assert.NotNull(cut.Find("#nothing-here"));

        cut.Render(parameters => parameters.Add(dashboard => dashboard.Subject, bruno));

        Assert.Empty(cut.FindAll("#nothing-here"));

        pending.SetResult([new DashboardItem { Name = "Kept", CardType = typeof(KeptSubjectTestCard) }]);

        // The release resumes the host's OnParametersSetAsync, which this act does not await.
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#kept-subject-card")));
        Assert.Empty(cut.FindAll("#nothing-here"));
    }

    [Fact]
    public void LosingTheSubjectTakesItsEmptyAway()
    {
        var cut = Show();

        Assert.NotNull(cut.Find("#nothing-here"));

        cut.Render(parameters => parameters.Add(dashboard => dashboard.Subject, (TestSubject?)null));

        Assert.Empty(cut.FindAll("#nothing-here"));
    }
}

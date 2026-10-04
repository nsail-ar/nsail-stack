// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Components;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Metadata;
using NSail.Security;

namespace NSail.Components.Tests;

/// <summary>The dashboard's Empty slot: what an app shows a session none of the contributed
/// cards are for. Optical's patient portal is that answer — the home page renders the
/// person's own Ficha there — so what this pins is that the slot appears exactly when the
/// gate left nothing, and never in front of cards that were about to arrive.</summary>
public sealed class NsDashboardEmptyTests : BunitContext
{
    const string EmptyMarkup = "<p id=\"nothing-here\">portal</p>";

    sealed class TestContributor(params DashboardItem[] items) : IDashboardContributor
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(items);
        }
    }

    public NsDashboardEmptyTests()
    {
        AddAuthorization().SetNotAuthorized();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddScoped<PageGate>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton(new RouteTable(typeof(NsDashboardEmptyTests).Assembly, []));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    IRenderedComponent<NsDashboard> Show(params DashboardItem[] items)
    {
        Services.AddScoped<IDashboardContributor>(_ => new TestContributor(items));

        return Render<NsDashboard>(parameters => parameters.Add(
            dashboard => dashboard.Empty,
            (RenderFragment)(builder => builder.AddMarkupContent(0, EmptyMarkup))));
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

    // The other half, and the one that matters for the flash: a card the session may open
    // takes the surface, and the empty answer is nowhere in the markup — not in the render
    // before the gate answered either.
    [Fact]
    public void TheEmptySlotStaysAwayWhenACardIsAllowed()
    {
        var cut = Show(new DashboardItem { Name = "Allowed", CardType = typeof(IconStampedTestCard) });

        Assert.Empty(cut.FindAll("#nothing-here"));
        Assert.NotNull(cut.Find(".ns-dashboard"));
    }

    // nsail#1607: NsDashboard resolves SecurityManager and SessionProvider itself (Services,
    // never @inject) rather than requiring every host to register them — the one path this
    // suite otherwise never exercises. Eligible here answers false only when it is handed back
    // the very Session this test registered, so a wiring cut between the component and the
    // container (Services.GetService(typeof(SessionProvider)) removed or reverted to null
    // defaults, the way NsSubjectDashboard.razor already calls Allowed) has the card pass
    // Eligible's permissive default and the Empty slot never appears — the assertion below
    // would fail rather than pass for the wrong reason.
    [Fact]
    public void TheEmptySlotRendersWhenTheOnlyAllowedCardsEligibleCheckDeniesIt()
    {
        var session = new Session { PartyId = Guid.NewGuid(), IsAuthenticated = true };

        Services.AddSingleton(new SessionProvider { Session = session });
        Services.AddSingleton(new SecurityManager(
            new MessageRegistry(new MetadataProvider(), []), new RelationProvider(), [], []));

        var cut = Show(new DashboardItem
        {
            Name = "Ineligible",
            CardType = typeof(IconStampedTestCard),
            Eligible = (security, resolvedSession) =>
                security is null || resolvedSession is null || !ReferenceEquals(resolvedSession, session),
        });

        Assert.NotNull(cut.Find("#nothing-here"));
    }
}

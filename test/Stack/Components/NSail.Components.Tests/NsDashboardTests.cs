// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Components;

namespace NSail.Components.Tests;

public sealed class OpenTestCard : ComponentBase;

public sealed class SecondOpenTestCard : ComponentBase;

public sealed class ThirdOpenTestCard : ComponentBase;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
public sealed class AdminTestCard : ComponentBase;

public sealed class NsDashboardTests
{
    sealed class TestContributor(params DashboardItem[] items) : IDashboardContributor
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(items);
        }
    }

    static PageGate Build()
    {
        var services = new ServiceCollection();

        services.AddAuthorizationCore();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var provider = services.BuildServiceProvider();

        return new PageGate(
            provider.GetRequiredService<IAuthorizationService>(),
            provider.GetRequiredService<IAuthorizationPolicyProvider>());
    }

    static ClaimsPrincipal User(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    static Task<IReadOnlyList<DashboardItem>> Collect(params IDashboardContributor[] contributors)
    {
        return NsDashboard.Collect(contributors, Build(), User("clerk"));
    }

    [Fact]
    public async Task OrdersCardsByWeightAscending()
    {
        var cards = await Collect(new TestContributor(
            new DashboardItem { Name = "Late", CardType = typeof(ThirdOpenTestCard), Weight = 60 },
            new DashboardItem { Name = "Early", CardType = typeof(OpenTestCard), Weight = 5 },
            new DashboardItem { Name = "Middle", CardType = typeof(SecondOpenTestCard), Weight = 30 }));

        Assert.Equal(["Early", "Middle", "Late"], cards.Select(card => card.Name));
    }

    [Fact]
    public async Task OrdersCardsByWeightAcrossContributors()
    {
        var cards = await Collect(
            new TestContributor(new DashboardItem { Name = "Late", CardType = typeof(OpenTestCard), Weight = 90 }),
            new TestContributor(new DashboardItem { Name = "Early", CardType = typeof(SecondOpenTestCard), Weight = 10 }));

        Assert.Equal(["Early", "Late"], cards.Select(card => card.Name));
    }

    [Fact]
    public async Task KeepsContributionOrderBetweenEqualWeights()
    {
        var cards = await Collect(new TestContributor(
            new DashboardItem { Name = "Second", CardType = typeof(SecondOpenTestCard), Weight = 10 },
            new DashboardItem { Name = "Third", CardType = typeof(ThirdOpenTestCard), Weight = 10 },
            new DashboardItem { Name = "First", CardType = typeof(OpenTestCard), Weight = 5 }));

        Assert.Equal(["First", "Second", "Third"], cards.Select(card => card.Name));
    }

    [Fact]
    public async Task ACardTheSessionMayNotOpenIsAbsent()
    {
        var cards = await Collect(new TestContributor(
            new DashboardItem { Name = "Allowed", CardType = typeof(OpenTestCard) },
            new DashboardItem { Name = "Denied", CardType = typeof(AdminTestCard) }));

        var card = Assert.Single(cards);
        Assert.Equal("Allowed", card.Name);
        Assert.DoesNotContain(typeof(AdminTestCard), cards.Select(item => item.CardType));
    }

    [Fact]
    public async Task TheSameCardIsPresentForASessionThatMayOpenIt()
    {
        var contributor = new TestContributor(
            new DashboardItem { Name = "Denied", CardType = typeof(AdminTestCard) });

        var cards = await NsDashboard.Collect([contributor], Build(), User("admin"));

        Assert.Equal("Denied", Assert.Single(cards).Name);
    }

    [Fact]
    public async Task NoContributorsMeansNoCards()
    {
        Assert.Empty(await Collect());
    }

    // nsail#1607: the coarse gate above answers "may this session ever send this message" —
    // Eligible is a card's own, narrower answer for a grant the coarse gate cannot see through
    // (a portal session's constrained party, an org-wide alert its own use never fills).
    [Fact]
    public async Task ACardWhoseEligibleAnswersNoIsAbsentEvenWhenTheCoarseGateAllowsIt()
    {
        var items = new[]
        {
            new DashboardItem { Name = "Allowed", CardType = typeof(OpenTestCard) },
            new DashboardItem
            {
                Name = "Ineligible",
                CardType = typeof(SecondOpenTestCard),
                Eligible = (_, _) => false,
            },
        };

        var cards = await NsDashboard.Allowed(items, Build(), User("clerk"));

        Assert.Equal(["Allowed"], cards.Select(card => card.Name));
    }

    [Fact]
    public async Task ACardWithNoEligibleCheckIsExactlyAsAllowedAsBefore()
    {
        var items = new[] { new DashboardItem { Name = "Allowed", CardType = typeof(OpenTestCard) } };

        var cards = await NsDashboard.Allowed(items, Build(), User("clerk"));

        Assert.Equal(["Allowed"], cards.Select(card => card.Name));
    }
}

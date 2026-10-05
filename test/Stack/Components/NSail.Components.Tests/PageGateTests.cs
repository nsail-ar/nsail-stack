// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Components;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

public sealed class OpenTestPage : ComponentBase;

[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class AuthenticatedTestPage : ComponentBase;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
public sealed class AdminTestPage : ComponentBase;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
[AllowAnonymous]
public sealed class PublicTestPage : ComponentBase;

public sealed class PageGateTests
{
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

    static ClaimsPrincipal Anonymous()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    static ClaimsPrincipal User(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task AllowsPageWithoutAttributes()
    {
        Assert.True(await Build().Allows(typeof(OpenTestPage), Anonymous()));
    }

    [Fact]
    public async Task DeniesAuthenticatedPageToAnonymous()
    {
        Assert.False(await Build().Allows(typeof(AuthenticatedTestPage), Anonymous()));
    }

    [Fact]
    public async Task AllowsAuthenticatedPageToSignedInUser()
    {
        Assert.True(await Build().Allows(typeof(AuthenticatedTestPage), User()));
    }

    [Fact]
    public async Task DeniesPageWhoseRoleTheUserLacks()
    {
        Assert.False(await Build().Allows(typeof(AdminTestPage), User("clerk")));
    }

    [Fact]
    public async Task AllowsPageWhoseRoleTheUserHas()
    {
        Assert.True(await Build().Allows(typeof(AdminTestPage), User("admin")));
    }

    [Fact]
    public async Task AllowAnonymousWinsOverAuthorize()
    {
        Assert.True(await Build().Allows(typeof(PublicTestPage), Anonymous()));
    }

    [Fact]
    public async Task TreatsAMissingAuthenticationStateAsAnonymous()
    {
        Assert.False(await Build().Allows(typeof(AuthenticatedTestPage), state: null));
    }

    // The nav entry's half of the gate, and it goes through the host's own policy provider: the
    // name a page's [Authorize<TMessage>] carries and the one an entry's Permission asks for are
    // fabricated by one piece of code, so a door and the page it opens cannot disagree.
    static PageGate BuildWithMessagePolicies(params Type[] granted)
    {
        var services = new ServiceCollection();

        services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationPolicyProvider, MessagePolicyProvider>();
        services.AddSingleton<IAuthorizationHandler>(new GrantedMessages(granted));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var provider = services.BuildServiceProvider();

        return new PageGate(
            provider.GetRequiredService<IAuthorizationService>(),
            provider.GetRequiredService<IAuthorizationPolicyProvider>());
    }

    [Fact]
    public async Task PermitsAMessageTheSessionCouldSend()
    {
        Assert.True(await BuildWithMessagePolicies(typeof(OpenTestPage)).Permits(typeof(OpenTestPage), User()));
    }

    [Fact]
    public async Task RefusesAMessageTheSessionCouldNotSend()
    {
        Assert.False(await BuildWithMessagePolicies().Permits(typeof(OpenTestPage), User()));
    }

    /// <summary>A host that registered no message policies answers null for the fabricated name,
    /// which is the same "no gate" a page with no attributes gets rather than a drawer that
    /// hides every door it cannot ask about.</summary>
    [Fact]
    public async Task PermitsEverythingWhereNoProviderKnowsMessagePolicies()
    {
        Assert.True(await Build().Permits(typeof(OpenTestPage), User()));
    }

    // nsail#1934. Every caller of this gate asks from inside a render and none of them can
    // remember on its own: NsPageLink re-asks on every parameter set, and a parameter set is
    // every render pass of whatever hosts it, so one pass over a full list of rows asks for
    // the same handful of pages dozens of times. What that costs is not assertable on the CI
    // box; how often the evaluator is ASKED is, and it is the same fact.
    static (PageGate Gate, CountingAuthorization Asked) Counted()
    {
        var services = new ServiceCollection();

        services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationPolicyProvider, MessagePolicyProvider>();
        services.AddSingleton<IAuthorizationHandler>(new GrantedMessages(typeof(OpenTestPage)));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var provider = services.BuildServiceProvider();
        var asked = new CountingAuthorization(provider.GetRequiredService<IAuthorizationService>());

        return (new PageGate(asked, provider.GetRequiredService<IAuthorizationPolicyProvider>()), asked);
    }

    [Fact]
    public async Task AsksTheEvaluatorOncePerPageForOneSession()
    {
        var (gate, asked) = Counted();
        var user = User("admin");

        for (var call = 0; call < 20; call++)
        {
            Assert.True(await gate.Allows(typeof(AdminTestPage), user));
        }

        Assert.Equal(1, asked.Calls);
    }

    /// <summary>A verdict is a function of the page and the session, so neither of them collapses
    /// into the other: a second page is its own question.</summary>
    [Fact]
    public async Task AsksAgainForAnotherPage()
    {
        var (gate, asked) = Counted();
        var user = User("admin");

        Assert.True(await gate.Allows(typeof(AdminTestPage), user));
        Assert.True(await gate.Allows(typeof(AuthenticatedTestPage), user));

        Assert.Equal(2, asked.Calls);
    }

    /// <summary>And the session is the one thing that earns a fresh ask — a sign-in, an
    /// organization switch — which is the same bargain NavMenu makes with the tree it builds
    /// out of these answers.</summary>
    [Fact]
    public async Task AsksAgainForAnotherSession()
    {
        var (gate, asked) = Counted();

        Assert.True(await gate.Allows(typeof(AdminTestPage), User("admin")));
        Assert.True(await gate.Allows(typeof(AdminTestPage), User("admin")));

        Assert.Equal(2, asked.Calls);
    }

    /// <summary>And the session with nobody in it is one session: a link cascaded no state
    /// stands in for the same anonymous principal every time rather than minting one per call,
    /// which would be a remembered answer nothing ever reads again on every render pass.</summary>
    [Fact]
    public async Task AsksOnceForALinkCascadedNoSession()
    {
        var (gate, asked) = Counted();

        for (var call = 0; call < 20; call++)
        {
            Assert.False(await gate.Allows(typeof(AuthenticatedTestPage), state: null));
        }

        Assert.Equal(1, asked.Calls);
    }

    [Fact]
    public async Task AsksTheEvaluatorOncePerMessageForOneSession()
    {
        var (gate, asked) = Counted();
        var user = User("admin");

        for (var call = 0; call < 20; call++)
        {
            Assert.True(await gate.Permits(typeof(OpenTestPage), user));
        }

        Assert.Equal(1, asked.Calls);
    }
}

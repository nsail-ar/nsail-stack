// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
}

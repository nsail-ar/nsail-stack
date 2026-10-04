// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Metadata;
using NSail.Problems;
using NSail.Security;

namespace NSail.Security.Tests;

public sealed class GatedMessage : IMessage
{
}

public sealed class GatedQuery : IMessage<int>
{
}

// The generator's output for a message with no constrainable field: the key and the audience
// are the whole question, so a policy that applies to it applies.
internal sealed class GatedPolicyHandler : PolicyHandler
{
    public GatedPolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(message is GatedMessage);
    }
}

// Assigning SessionProvider.Session writes an AsyncLocal that outlives the assignment, so two
// providers built in one test method would answer with whichever was assigned last. A derived
// provider is what a host does anyway (HttpSessionProvider), and it keeps each case's caller
// its own.
internal sealed class FixedSessionProvider : SessionProvider
{
    readonly Session _session;

    public FixedSessionProvider(Session session)
    {
        _session = session;
    }

    public override Session Session
    {
        get { return _session; }
    }
}

/// <summary>Issue #47's root cause. Deny-by-default answered every refused send the same
/// Forbidden, so a client whose credential had quietly expired heard "you may not do this" —
/// indistinguishable from a real permission refusal — and went on believing it was signed in:
/// the name in the corner, the menu, the effective policies, all from hours ago, and every
/// save coming back refused. Nothing in that answer could tell it to sign in again.
///
/// The two refusals are now two answers. Who the caller is decides which: 403 for somebody the
/// policies do not cover, 401 for nobody at all. Only the second is recoverable by
/// re-authenticating, which is why only the second may say so.</summary>
public sealed class SecurityInterceptorDenialTests
{
    static SecurityManager Manager(params Policy[] builtIns)
    {
        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(GatedMessage), policy => new GatedPolicyHandler(policy)),
        };

        return new SecurityManager(Registry.For(factories), new RelationProvider(), factories, builtIns);
    }

    static SessionProvider Anonymous()
    {
        return new FixedSessionProvider(new Session());
    }

    static SessionProvider SignedIn()
    {
        return new FixedSessionProvider(new Session
        {
            IsAuthenticated = true,
            UserId = Guid.NewGuid(),
            Roles = ["recepcion"],
        });
    }

    static Task Unreached(GatedMessage message, CancellationToken cancellationToken)
    {
        Assert.Fail("The gate let a refused send through to the sender.");

        return Task.CompletedTask;
    }

    static Task<int> UnreachedQuery(GatedQuery message, CancellationToken cancellationToken)
    {
        Assert.Fail("The gate let a refused send through to the sender.");

        return Task.FromResult(0);
    }

    [Fact]
    public async Task A_caller_with_no_session_is_answered_Unauthorized()
    {
        var interceptor = new SecurityInterceptor<GatedMessage>(Manager(), Anonymous());

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new GatedMessage(), Unreached, CancellationToken.None));

        Assert.Equal("Unauthorized", refusal.Code);
        Assert.Equal(401, refusal.Status);
    }

    [Fact]
    public async Task A_signed_in_caller_no_policy_covers_is_answered_Forbidden()
    {
        var interceptor = new SecurityInterceptor<GatedMessage>(Manager(), SignedIn());

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new GatedMessage(), Unreached, CancellationToken.None));

        Assert.Equal("Forbidden", refusal.Code);
        Assert.Equal(403, refusal.Status);
    }

    /// <summary>The refusal a query carries is the one a screen's load meets, and it travels
    /// the other arity of the same gate — which is exactly where the two could drift.</summary>
    [Fact]
    public async Task The_result_arity_answers_Unauthorized_to_a_caller_with_no_session()
    {
        var interceptor = new SecurityInterceptor<GatedQuery, int>(Manager(), Anonymous());

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new GatedQuery(), UnreachedQuery, CancellationToken.None));

        Assert.Equal(401, refusal.Status);
    }

    [Fact]
    public async Task The_result_arity_answers_Forbidden_to_a_signed_in_caller()
    {
        var interceptor = new SecurityInterceptor<GatedQuery, int>(Manager(), SignedIn());

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new GatedQuery(), UnreachedQuery, CancellationToken.None));

        Assert.Equal(403, refusal.Status);
    }

    /// <summary>The pre-auth path is untouched: a message an anonymous built-in covers still
    /// passes, so nothing about the sign-in boot learns to ask for a session it is on its way
    /// to create. Only a DENIED anonymous send answers 401.</summary>
    [Fact]
    public async Task An_anonymous_built_in_still_lets_the_send_through()
    {
        var manager = Manager(new Policy
        {
            Name = "Anyone may send this",
            Messages = ["Security.Tests.GatedMessage"],
        });

        var reached = false;

        var interceptor = new SecurityInterceptor<GatedMessage>(manager, Anonymous());

        await interceptor.Invoke(new GatedMessage(), (message, cancellationToken) =>
        {
            reached = true;

            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.True(reached);
    }
}

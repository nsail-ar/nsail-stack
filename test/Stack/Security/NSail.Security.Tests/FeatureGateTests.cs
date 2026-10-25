// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Problems;
using NSail.Security.Annotations;

namespace NSail.Security.Tests;

[RequiresFeature("whatsapp")]
public sealed class SoldMessage : IMessage
{
}

[RequiresFeature("whatsapp")]
public sealed class SoldQuery : IMessage<int>
{
}

internal sealed class FixedFeatureProvider : FeatureProvider
{
    readonly Entitlements _entitlements;

    public FixedFeatureProvider(string? bag)
    {
        _entitlements = Entitlements.Read(bag);
    }

    public override Task<Entitlements> Current(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_entitlements);
    }

    public override Entitlements Assumed
    {
        get { return _entitlements; }
    }
}

/// <summary>A feature the install was not sold is refused in the same gate as a permission and
/// not in a rule each handler keeps — but it is a different refusal, and that is the whole point
/// of it being a verdict of its own: nothing a caller is granted widens a feature nobody bought,
/// so telling them "you do not have permission" would send them to an administrator who cannot
/// help. Which is also why it is read permissively: a key nobody has written is on, and the
/// fleet's bags are empty the day this ships.</summary>
public sealed class FeatureGateTests
{
    static SecurityManager Manager(string? bag)
    {
        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(SoldMessage), policy => new SoldPolicyHandler(policy)),
            new PolicyHandlerFactory(typeof(SoldQuery), policy => new SoldPolicyHandler(policy)),
        };

        // A grant wide enough that nothing but the feature can be what refuses the send.
        var everything = new Policy
        {
            Name = "Anyone may send these",
            Messages = ["Security.Tests.SoldMessage", "Security.Tests.SoldQuery"],
        };

        return new SecurityManager(
            Registry.For(factories),
            new RelationProvider(),
            factories,
            [everything],
            logger: null,
            features: new FixedFeatureProvider(bag));
    }

    static Session Caller()
    {
        return new Session
        {
            IsAuthenticated = true,
            UserId = Guid.NewGuid(),
            Roles = ["recepcion"],
        };
    }

    static Task Unreached(SoldMessage message, CancellationToken cancellationToken)
    {
        Assert.Fail("The gate let a send of a feature the install does not have through to the sender.");

        return Task.CompletedTask;
    }

    static Task<int> UnreachedQuery(SoldQuery message, CancellationToken cancellationToken)
    {
        Assert.Fail("The gate let a send of a feature the install does not have through to the sender.");

        return Task.FromResult(0);
    }

    [Fact]
    public async Task A_send_whose_feature_is_off_is_refused_and_the_verdict_names_it()
    {
        var result = await Manager("""{"whatsapp": false}""").Authorize(new SoldMessage(), Caller());

        Assert.False(result.Allowed);
        Assert.Equal("whatsapp", result.FeatureOff);
    }

    [Theory]
    [InlineData("""{"whatsapp": true}""")]
    [InlineData("""{"arca": false}""")]
    [InlineData("{}")]
    [InlineData(null)]
    public async Task A_send_the_bag_does_not_close_is_allowed_and_names_no_feature(string? bag)
    {
        var result = await Manager(bag).Authorize(new SoldMessage(), Caller());

        Assert.True(result.Allowed);
        Assert.Null(result.FeatureOff);
    }

    /// <summary>A composition with no provider at all — a client, a fixture, a host with no
    /// store to read the bag from — is the state of every install the day the column ships.</summary>
    [Fact]
    public async Task A_manager_built_with_no_provider_allows_a_marked_send()
    {
        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(SoldMessage), policy => new SoldPolicyHandler(policy)),
        };

        var manager = new SecurityManager(
            Registry.For(factories),
            new RelationProvider(),
            factories,
            [new Policy { Name = "Anyone may send this", Messages = ["Security.Tests.SoldMessage"] }]);

        Assert.True((await manager.Authorize(new SoldMessage(), Caller())).Allowed);
    }

    [Fact]
    public async Task The_refusal_a_caller_meets_is_the_feature_and_not_their_permission()
    {
        var interceptor = new SecurityInterceptor<SoldMessage>(
            Manager("""{"whatsapp": false}"""),
            new FixedSessionProvider(Caller()));

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new SoldMessage(), Unreached, CancellationToken.None));

        Assert.Equal("FeatureUnavailable", refusal.Code);
        Assert.Equal(403, refusal.Status);
        Assert.Equal("whatsapp", refusal.Issues[0].Source);
    }

    /// <summary>The two arities are two copies of the same decision, which is exactly where they
    /// could drift: a screen's load meets the refusal through this one.</summary>
    [Fact]
    public async Task The_result_arity_refuses_the_same_way()
    {
        var interceptor = new SecurityInterceptor<SoldQuery, int>(
            Manager("""{"whatsapp": false}"""),
            new FixedSessionProvider(Caller()));

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new SoldQuery(), UnreachedQuery, CancellationToken.None));

        Assert.Equal("FeatureUnavailable", refusal.Code);
        Assert.Equal("whatsapp", refusal.Issues[0].Source);
    }

    /// <summary>What hides the entry points: the page, the menu entry and the row action all
    /// come off PreAuthorize, and a plan that excludes something must not go on offering it only
    /// to refuse the press.</summary>
    [Fact]
    public void A_feature_that_is_off_is_not_offered_at_all()
    {
        Assert.False(Manager("""{"whatsapp": false}""").PreAuthorize(typeof(SoldMessage), Caller()));
        Assert.True(Manager("""{"whatsapp": true}""").PreAuthorize(typeof(SoldMessage), Caller()));
        Assert.True(Manager(null).PreAuthorize(typeof(SoldMessage), Caller()));
    }

    /// <summary>A bag not yet in hand is read as unrestricted and not as closed: hiding
    /// something the install does have is the one failure here that nothing recovers from.</summary>
    [Fact]
    public void A_provider_with_nothing_read_yet_offers_the_feature()
    {
        Assert.True(new FeatureProvider().Assumed.IsEnabled("whatsapp"));
    }

    [Fact]
    public void The_mark_is_read_off_the_message_and_only_where_it_is_worn()
    {
        Assert.Equal("whatsapp", SecurityManager.FeatureOf(typeof(SoldMessage)));
        Assert.Null(SecurityManager.FeatureOf(typeof(GatedMessage)));
    }

    /// <summary>The gate ahead of model binding has no message to read fields off and asks this
    /// instead (EndpointGateMiddleware): same bag, same verdict, so a caller cannot be told one
    /// thing at the door and another by the interceptor behind it. Awaited and not assumed —
    /// what the door may refuse on is the read, not the optimistic answer visibility rests on.</summary>
    [Fact]
    public async Task The_gate_ahead_of_the_message_refuses_the_feature_as_itself()
    {
        var refusal = await Manager("""{"whatsapp": false}""").PreRefusal(typeof(SoldMessage), Caller());

        Assert.Equal("FeatureUnavailable", refusal?.Code);
        Assert.Equal(403, refusal?.Status);
        Assert.Equal("whatsapp", refusal?.Issues[0].Source);
    }

    [Theory]
    [InlineData("""{"whatsapp": true}""")]
    [InlineData("{}")]
    [InlineData(null)]
    public async Task The_gate_ahead_of_the_message_lets_through_what_the_bag_does_not_close(string? bag)
    {
        Assert.Null(await Manager(bag).PreRefusal(typeof(SoldMessage), Caller()));
    }

    /// <summary>And the permission half of that same gate still answers as the permission it is:
    /// the feature verdict is a second answer, not a replacement.</summary>
    [Fact]
    public async Task The_gate_ahead_of_the_message_refuses_a_permission_as_a_permission()
    {
        var refusal = await Manager("""{"whatsapp": false}""").PreRefusal(typeof(GatedMessage), Caller());

        Assert.Equal("Forbidden", refusal?.Code);
    }
}

// The generator's output for a message with no constrainable field, as the denial tests' own:
// the key and the audience are the whole question.
internal sealed class SoldPolicyHandler : PolicyHandler
{
    public SoldPolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}

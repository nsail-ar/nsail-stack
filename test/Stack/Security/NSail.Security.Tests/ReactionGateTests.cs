// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Sending;
using NSail.Problems;

namespace NSail.Security.Tests;

public sealed class CloseHours : IMessage
{
}

public sealed class HoursClosed : IMessage
{
}

public sealed class WithdrawMirror : IMessage
{
}

public sealed class CountMirrors : IMessage<int>
{
}

// The generator's output for a message with no constrainable field: the key and the audience
// are the whole question, so a policy that applies to it applies.
internal sealed class FlatPolicyHandler : PolicyHandler
{
    public FlatPolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}

// The Claim's chain, at message altitude: the psychologist closes hours she is granted, the
// closure publishes what it did, and the app's bridge reacts by sending into a kit her policy
// never heard of.
internal sealed class ClosingHandler : IHandler<CloseHours>
{
    readonly Mediator _mediator;

    public ClosingHandler(Mediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Handle(CloseHours message, CancellationToken cancellationToken = default)
    {
        await _mediator.Publish(new HoursClosed(), cancellationToken);
    }
}

internal sealed class MirrorHandler : IHandler<HoursClosed>
{
    readonly Mediator _mediator;

    public MirrorHandler(Mediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Handle(HoursClosed message, CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new CountMirrors(), cancellationToken);
        await _mediator.Send(new WithdrawMirror(), cancellationToken);
    }
}

internal sealed class Withdrawals : IHandler<WithdrawMirror>, IHandler<CountMirrors, int>
{
    public int Count;

    public Task Handle(WithdrawMirror message, CancellationToken cancellationToken = default)
    {
        Count++;

        return Task.CompletedTask;
    }

    public Task<int> Handle(CountMirrors message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Count);
    }
}

/// <summary>Issue #366's root cause. SecurityInterceptor was registered as an open-generic
/// Mediator interceptor and PublishPipeline consumed the same registration a SendPipeline
/// does, so a domain event was authorized as if a caller had sent it — and so was everything
/// its subscribers sent one frame down, under that same session. Under deny-by-default a
/// psychologist granted exactly the message she sent got Access denied from the event behind
/// it, and granting the event key bought one frame: the next Send in the chain, in a kit her
/// policy's author never heard of, refused in its turn.
///
/// A reaction is the system carrying out a decision that was already authorized. The gate
/// answers where a caller asks — the send — and nowhere below it.</summary>
public sealed class ReactionGateTests
{
    static ServiceProvider Build(Withdrawals withdrawals, params string[] granted)
    {
        var services = new ServiceCollection();

        services.AddMessaging();

        // Registered before AddSecurityEnforcement, which only TryAdds the default: the caller
        // is a signed-in psychologist and every case here turns on what her policy does NOT say.
        services.AddSingleton<SessionProvider>(new FixedSessionProvider(new Session
        {
            IsAuthenticated = true,
            UserId = Guid.NewGuid(),
            PartyId = Guid.NewGuid(),
            Roles = ["psicologa"],
        }));

        services.AddSecurityEnforcement();

        // Both registrations stand in for the Policies.Handlers generator, which emits the
        // factory and the registry entry from the same line: the registry is what resolves the
        // policy's message keys, the factory is what hydrates them.
        foreach (var type in new[] { typeof(CloseHours), typeof(HoursClosed), typeof(WithdrawMirror), typeof(CountMirrors) })
        {
            services.AddSingleton(new PolicyHandlerFactory(type, policy => new FlatPolicyHandler(policy)));
            services.AddSingleton(new MessageRegistration(type));
        }

        services.AddBuiltInPolicy(new Policy
        {
            Name = "Psychologists close their own hours",
            Messages = granted,
            Audience = new PolicyAudience { HasRole = "psicologa" },
        });

        services.AddScoped<ISender<CloseHours>, InProcessSender<CloseHours>>();
        services.AddScoped<ISender<WithdrawMirror>, InProcessSender<WithdrawMirror>>();
        services.AddScoped<ISender<CountMirrors, int>, InProcessSender<CountMirrors, int>>();

        services.AddScoped<IHandler<CloseHours>, ClosingHandler>();
        services.AddScoped<IHandler<HoursClosed>, MirrorHandler>();
        services.AddSingleton<IHandler<WithdrawMirror>>(withdrawals);
        services.AddSingleton<IHandler<CountMirrors, int>>(withdrawals);

        return services.BuildServiceProvider();
    }

    /// <summary>The Claim: granted the message she sent and nothing else, the whole act
    /// completes — the event she never heard of, and the send its subscriber makes.</summary>
    [Fact]
    public async Task A_granted_send_carries_its_event_and_the_reaction_behind_it()
    {
        var withdrawals = new Withdrawals();

        var services = Build(withdrawals, "Security.Tests.CloseHours");

        await services.GetRequiredService<Mediator>().Send(new CloseHours());

        Assert.Equal(1, withdrawals.Count);
    }

    /// <summary>The other half of the Done, and the one a permissive fix would lose: the gate
    /// is untouched where a caller asks. WithdrawMirror is exactly as ungranted as it was in
    /// the case above — sent by the caller instead of by a subscriber, it is refused.</summary>
    [Fact]
    public async Task An_ungranted_send_from_the_caller_is_still_refused()
    {
        var services = Build(new Withdrawals(), "Security.Tests.CloseHours");

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => services.GetRequiredService<Mediator>().Send(new WithdrawMirror()));

        Assert.Equal("Forbidden", refusal.Code);
        Assert.Equal(403, refusal.Status);
    }

    /// <summary>And the send that opens the act is gated like any other: the exemption starts
    /// at the publish, so a caller who was granted nothing never reaches the event at all.</summary>
    [Fact]
    public async Task A_caller_granted_nothing_never_reaches_the_publish()
    {
        var withdrawals = new Withdrawals();

        var services = Build(withdrawals, "Security.Tests.WithdrawMirror");

        await Assert.ThrowsAsync<BusinessException>(
            () => services.GetRequiredService<Mediator>().Send(new CloseHours()));

        Assert.Equal(0, withdrawals.Count);
    }
}

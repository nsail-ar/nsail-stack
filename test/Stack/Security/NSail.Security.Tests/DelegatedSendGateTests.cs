// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Sending;
using NSail.Problems;

namespace NSail.Security.Tests;

// What each case turns on: whether CloseHours' handler marks its derived send.
internal sealed class Delegation
{
    public bool Delegated;
}

// What the sends behind the door saw: the session, and the verdict the org filter would read.
internal sealed class Seen
{
    public Session? Session;
    public int Withdrawn;
    public int Counted;
    public (bool FieldsVetted, bool EveryOrganization)? InsideDelegation;
    public (bool FieldsVetted, bool EveryOrganization)? InsideReaction;
}

internal sealed class DelegatingHandler : IHandler<CloseHours>
{
    readonly Mediator _mediator;
    readonly Delegation _delegation;

    public DelegatingHandler(Mediator mediator, Delegation delegation)
    {
        _mediator = mediator;
        _delegation = delegation;
    }

    public async Task Handle(CloseHours message, CancellationToken cancellationToken = default)
    {
        await _mediator.Publish(new HoursClosed(), cancellationToken);

        if (_delegation.Delegated)
        {
            await DelegatedSend.Run(() => _mediator.Send(new WithdrawMirror(), cancellationToken));
        }
        else
        {
            await _mediator.Send(new WithdrawMirror(), cancellationToken);
        }
    }
}

internal sealed class ObservingReaction : IHandler<HoursClosed>
{
    readonly Seen _seen;

    public ObservingReaction(Seen seen)
    {
        _seen = seen;
    }

    public Task Handle(HoursClosed message, CancellationToken cancellationToken = default)
    {
        _seen.InsideReaction = (AmbientAuthorization.FieldsVetted, AmbientAuthorization.EveryOrganization);

        return Task.CompletedTask;
    }
}

internal sealed class ObservingWithdrawal : IHandler<WithdrawMirror>
{
    readonly Mediator _mediator;
    readonly SessionProvider _sessions;
    readonly Seen _seen;

    public ObservingWithdrawal(Mediator mediator, SessionProvider sessions, Seen seen)
    {
        _mediator = mediator;
        _sessions = sessions;
        _seen = seen;
    }

    public async Task Handle(WithdrawMirror message, CancellationToken cancellationToken = default)
    {
        _seen.Session = _sessions.Session;
        _seen.InsideDelegation = (AmbientAuthorization.FieldsVetted, AmbientAuthorization.EveryOrganization);

        await _mediator.Send(new CountMirrors(), cancellationToken);

        _seen.Withdrawn++;
    }
}

internal sealed class ObservingCount : IHandler<CountMirrors, int>
{
    readonly Seen _seen;

    public ObservingCount(Seen seen)
    {
        _seen = seen;
    }

    public Task<int> Handle(CountMirrors message, CancellationToken cancellationToken = default)
    {
        _seen.Counted++;

        return Task.FromResult(_seen.Counted);
    }
}

/// <summary>A delegated send: the handler of a send the gate passed marks what it derives, the
/// gate does not ask the caller's grants again for it or for anything behind it, and the
/// session stays the caller's. The caller here is granted CloseHours alone; WithdrawMirror and
/// CountMirrors are ungranted throughout.</summary>
public sealed class DelegatedSendGateTests
{
    static readonly Session Caller = new()
    {
        IsAuthenticated = true,
        UserId = Guid.NewGuid(),
        PartyId = Guid.NewGuid(),
        Roles = ["psicologa"],
    };

    static ServiceProvider Build(Seen seen, bool delegated, bool everyOrganization = false)
    {
        var services = new ServiceCollection();

        services.AddMessaging();
        services.AddSingleton<SessionProvider>(new FixedSessionProvider(Caller));
        services.AddSecurityEnforcement();
        services.AcceptPermissiveProviders();

        foreach (var type in new[] { typeof(CloseHours), typeof(HoursClosed), typeof(WithdrawMirror), typeof(CountMirrors) })
        {
            services.AddSingleton(new PolicyHandlerFactory(type, policy => new FlatPolicyHandler(policy)));
            services.AddSingleton(new MessageRegistration(type));
        }

        services.AddBuiltInPolicy(new Policy
        {
            Name = "Psychologists close their own hours",
            Messages = ["Security.Tests.CloseHours"],
            Audience = new PolicyAudience { HasRole = "psicologa" },
            EveryOrganization = everyOrganization,
        });

        services.AddSingleton(seen);
        services.AddSingleton(new Delegation { Delegated = delegated });

        services.AddScoped<ISender<CloseHours>, InProcessSender<CloseHours>>();
        services.AddScoped<ISender<WithdrawMirror>, InProcessSender<WithdrawMirror>>();
        services.AddScoped<ISender<CountMirrors, int>, InProcessSender<CountMirrors, int>>();

        services.AddScoped<IHandler<CloseHours>, DelegatingHandler>();
        services.AddScoped<IHandler<HoursClosed>, ObservingReaction>();
        services.AddScoped<IHandler<WithdrawMirror>, ObservingWithdrawal>();
        services.AddScoped<IHandler<CountMirrors, int>, ObservingCount>();

        return services.BuildServiceProvider();
    }

    /// <summary>AC1: the delegated send runs, the send its own handler makes runs behind it,
    /// and the session inside is the caller's — not a system session.</summary>
    [Fact]
    public async Task A_delegated_send_and_the_sends_behind_it_run_as_the_caller()
    {
        var seen = new Seen();

        var services = Build(seen, delegated: true);

        await services.GetRequiredService<Mediator>().Send(new CloseHours());

        Assert.Equal(1, seen.Withdrawn);
        Assert.Equal(1, seen.Counted);
        Assert.NotNull(seen.Session);
        Assert.False(seen.Session.IsSystem);
        Assert.Equal(Caller.PartyId, seen.Session.PartyId);
        Assert.Equal(Caller.UserId, seen.Session.UserId);
    }

    /// <summary>AC2: the same ungranted message, sent by the caller herself.</summary>
    [Fact]
    public async Task The_ungranted_message_sent_by_the_caller_is_refused()
    {
        var seen = new Seen();

        var services = Build(seen, delegated: true);

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => services.GetRequiredService<Mediator>().Send(new WithdrawMirror()));

        Assert.Equal("Forbidden", refusal.Code);
        Assert.Equal(403, refusal.Status);
        Assert.Equal(0, seen.Withdrawn);
    }

    /// <summary>AC2: the same ungranted message, sent by the handler without marking it.</summary>
    [Fact]
    public async Task The_ungranted_message_sent_by_the_handler_undelegated_is_refused()
    {
        var seen = new Seen();

        var services = Build(seen, delegated: false);

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => services.GetRequiredService<Mediator>().Send(new CloseHours()));

        Assert.Equal("Forbidden", refusal.Code);
        Assert.Equal(403, refusal.Status);
        Assert.Equal(0, seen.Withdrawn);
    }

    /// <summary>AC3: a delegation opened where no gated send runs — here the test body, as a
    /// page or a job would — opens nothing, before a gated send in the same flow and after
    /// one has completed.</summary>
    [Fact]
    public async Task A_delegation_opened_outside_a_gated_send_opens_nothing()
    {
        var seen = new Seen();

        var services = Build(seen, delegated: true);
        var mediator = services.GetRequiredService<Mediator>();

        var before = await Assert.ThrowsAsync<BusinessException>(
            () => DelegatedSend.Run(() => mediator.Send(new WithdrawMirror())));

        Assert.Equal(403, before.Status);
        Assert.False(DelegatedSend.Active);

        await mediator.Send(new CloseHours());

        var after = await Assert.ThrowsAsync<BusinessException>(
            () => DelegatedSend.Run(() => mediator.Send(new CountMirrors())));

        Assert.Equal(403, after.Status);
        Assert.Equal(1, seen.Counted);
    }

    /// <summary>AC4: behind a delegated send the org filter reads what it reads behind a
    /// reaction in the same flow — the verdict of the door, never a wider one of its own.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_delegated_send_reads_the_org_filter_as_a_reaction_does(bool everyOrganization)
    {
        var seen = new Seen();

        var services = Build(seen, delegated: true, everyOrganization);

        await services.GetRequiredService<Mediator>().Send(new CloseHours());

        Assert.NotNull(seen.InsideReaction);
        Assert.Equal(seen.InsideReaction, seen.InsideDelegation);
        Assert.Equal(everyOrganization, seen.InsideDelegation!.Value.EveryOrganization);
    }
}

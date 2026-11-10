// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Problems;

namespace NSail.Security.Tests;

/// <summary>The ticket-epoch cut reaching a send, which is all an open circuit ever does: its
/// session was built from the request that opened it, so the middleware never sees it again.
/// A session minted under the old generation is the one the raised number must refuse.</summary>
public sealed class SecurityInterceptorEpochTests
{
    const int Raised = 2;

    static SecurityManager Manager()
    {
        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(GatedMessage), policy => new GatedPolicyHandler(policy)),
        };

        var open = new Policy { Name = "Anyone", Messages = ["Security.Tests.GatedMessage"] };

        return new SecurityManager(Registry.For(factories), new RelationProvider(), factories, [open]);
    }

    static SessionProvider Holding(int epoch, bool system = false)
    {
        return new FixedSessionProvider(new Session
        {
            IsAuthenticated = true,
            IsSystem = system,
            UserId = Guid.NewGuid(),
            TicketEpoch = epoch,
        });
    }

    [Fact]
    public async Task A_session_from_the_generation_before_is_refused_as_a_stale_ticket()
    {
        var options = new SecurityOptions { TicketEpoch = Raised };
        var interceptor = new SecurityInterceptor<GatedMessage>(Manager(), Holding(Raised - 1), options);

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new GatedMessage(), Unreached, CancellationToken.None));

        Assert.Equal(401, refusal.Status);
    }

    [Fact]
    public async Task The_cut_raised_under_a_live_session_refuses_its_next_send()
    {
        var options = new SecurityOptions();
        var interceptor = new SecurityInterceptor<GatedMessage>(Manager(), Holding(0), options);
        var reached = 0;

        await interceptor.Invoke(new GatedMessage(), (m, t) => { reached++; return Task.CompletedTask; }, CancellationToken.None);

        options.TicketEpoch = Raised;

        await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new GatedMessage(), Unreached, CancellationToken.None));

        Assert.Equal(1, reached);
    }

    [Fact]
    public async Task The_result_arity_refuses_a_stale_session_too()
    {
        var options = new SecurityOptions { TicketEpoch = Raised };
        var interceptor = new SecurityInterceptor<GatedQuery, int>(Manager(), Holding(0), options);

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new GatedQuery(), (m, t) => Task.FromResult(1), CancellationToken.None));

        Assert.Equal(401, refusal.Status);
    }

    [Fact]
    public async Task A_current_session_and_the_system_are_never_interrupted()
    {
        var options = new SecurityOptions { TicketEpoch = Raised };
        var reached = 0;

        foreach (var sessions in new[] { Holding(Raised), Holding(0, system: true) })
        {
            var interceptor = new SecurityInterceptor<GatedMessage>(Manager(), sessions, options);

            await interceptor.Invoke(new GatedMessage(), (m, t) => { reached++; return Task.CompletedTask; }, CancellationToken.None);
        }

        Assert.Equal(2, reached);
    }

    static Task Unreached(GatedMessage message, CancellationToken cancellationToken)
    {
        Assert.Fail("The gate let a stale ticket through to the sender.");

        return Task.CompletedTask;
    }
}

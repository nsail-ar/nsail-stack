// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Problems;
using NSail.Messaging.WebApi.Push;
using NSail.Security;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>A suspension reaching what the request wall cannot: an open circuit's sends, and a
/// push feed held since before it was written. Both ask the same memoed state the wall does, so
/// they stop when its window expires and not before.</summary>
public sealed class TenantStateOpenWorkTests
{
    const string Slug = "lumina";

    [Fact]
    public async Task An_open_circuits_send_is_refused_once_the_memo_expires_on_a_suspension()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);
        await using var scope = Enter(host);

        var interceptor = new TenantStateInterceptor<Rang>(scope.ServiceProvider);
        var reached = 0;

        await interceptor.Invoke(new Rang(), (message, token) => { reached++; return Task.CompletedTask; }, CancellationToken.None);

        await Suspend(host);

        // Inside the window the memoed row still says active, and a circuit is never
        // interrupted by a state it has not read yet.
        await interceptor.Invoke(new Rang(), (message, token) => { reached++; return Task.CompletedTask; }, CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(2));

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => interceptor.Invoke(new Rang(), (message, token) => { reached++; return Task.CompletedTask; }, CancellationToken.None));

        Assert.Equal(503, refusal.Status);
        Assert.Equal(2, reached);
    }

    // The shape a circuit's event has: a flow carrying the circuit's tenant (TenancyCircuit
    // enters it on every inbound activity) and a caller's scope that never entered one, whose
    // outermost send builds the wall from a scope of the send's own.
    [Fact]
    public async Task A_circuits_send_through_the_pipeline_is_refused_once_the_memo_expires_on_a_suspension()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);

        await Circuit(host, async mediator =>
        {
            await mediator.Send(new Knocked());

            await Suspend(host);

            await mediator.Send(new Knocked());

            clock.Advance(TimeSpan.FromMinutes(2));

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => mediator.Send(new Knocked()));

            Assert.Equal(503, refusal.Status);
        });
    }

    [Fact]
    public async Task A_background_session_is_not_stopped_by_the_wall()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);
        await using var scope = Enter(host);

        await Suspend(host);

        // The host's own provider reads the request's ticket, so the system's is composed beside
        // the scope's tenant state.
        var services = new ServiceCollection()
            .AddSingleton<SessionProvider>(new SystemSessions())
            .AddSingleton(scope.ServiceProvider.GetRequiredService<TenantStates>())
            .BuildServiceProvider();

        var interceptor = new TenantStateInterceptor<Rang>(services);
        var reached = false;

        await interceptor.Invoke(new Rang(), (message, token) => { reached = true; return Task.CompletedTask; }, CancellationToken.None);

        Assert.True(reached);
    }

    [Fact]
    public async Task A_held_feed_loses_its_right_to_listen_when_the_suspension_arrives()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);
        await using var scope = Enter(host);

        var holds = scope.ServiceProvider.GetRequiredService<PushAudience>().Watch();

        Assert.NotNull(holds);
        Assert.True(await holds(CancellationToken.None));

        await Suspend(host);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(await holds(CancellationToken.None));

        // Asked again inside the new window, the memo answers and the right stays withdrawn.
        Assert.False(await holds(CancellationToken.None));
    }

    static AsyncServiceScope Enter(TenancyHost host)
    {
        var scope = host.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(Tenant.For(Slug));

        return scope;
    }

    static async Task Circuit(TenancyHost host, Func<Mediator, Task> events)
    {
        AmbientTenant.Enter(Tenant.For(Slug));

        await using var scope = host.Services.CreateAsyncScope();

        await events(scope.ServiceProvider.GetRequiredService<Mediator>());
    }

    static async Task Suspend(TenancyHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        db.Add(new TenantState { TenantId = TenantKey.For(Slug), Status = TenantState.Suspended });

        await db.SaveChangesAsync();
    }

    sealed class SystemSessions : SessionProvider
    {
        public override Session Session
        {
            get { return Session.System(); }
            set { }
        }
    }
}

public sealed class Knocked : IMessage
{
}

sealed class KnockedHandler : IHandler<Knocked>
{
    public Task Handle(Knocked message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

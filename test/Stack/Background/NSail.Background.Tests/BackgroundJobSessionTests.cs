// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Sending;
using NSail.Problems;
using NSail.Security;

namespace NSail.Background.Tests;

sealed class Ping : IMessage
{
}

sealed class PingHandler : IHandler<Ping>
{
    readonly Signal _handled;

    public PingHandler(Signal handled)
    {
        _handled = handled;
    }

    public Task Handle(Ping message, CancellationToken cancellationToken = default)
    {
        _handled.Record();

        return Task.CompletedTask;
    }
}

sealed class Outcome
{
    readonly TaskCompletionSource _recorded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Recorded => _recorded.Task;

    public Exception? Failure { get; private set; }

    public bool WasSystem { get; private set; }

    public void Record(bool wasSystem, Exception? failure)
    {
        WasSystem = wasSystem;
        Failure = failure;

        _recorded.TrySetResult();
    }
}

/// <summary>Sends through the real pipeline and reports what came back, rather than letting
/// a refusal escape into the runner's Error path: a failed gate then reads as a named
/// assertion instead of a test that waited out its bound.</summary>
sealed class SendingJob : IBackgroundJob
{
    readonly Mediator _mediator;
    readonly SessionProvider _sessions;
    readonly Outcome _outcome;

    public SendingJob(Mediator mediator, SessionProvider sessions, Outcome outcome)
    {
        _mediator = mediator;
        _sessions = sessions;
        _outcome = outcome;
    }

    public TimeSpan Interval => TimeSpan.FromMilliseconds(20);

    public TenancyScope Tenancy => TenancyScope.Install;

    public async Task Run(CancellationToken cancellationToken)
    {
        var isSystem = _sessions.Session.IsSystem;

        try
        {
            await _mediator.Send(new Ping(), cancellationToken);

            _outcome.Record(isSystem, null);
        }
        catch (Exception exception)
        {
            _outcome.Record(isSystem, exception);
        }
    }
}

/// <summary>The pair the whole area exists to support: the same message, through the same
/// deny-by-default gate, refused from an empty session and allowed from a job's.</summary>
public sealed class BackgroundJobSessionTests
{
    static void Pipeline(IServiceCollection services, Signal handled)
    {
        services.AddSingleton(handled);
        services.AddMessaging();

        // No policies of any kind are registered: the manager hydrates nothing, so the only
        // thing that can let a send through is the system short-circuit.
        services.AddSecurityEnforcement();

        services.AddScoped<IHandler<Ping>, PingHandler>();
        services.AddScoped<ISender<Ping>, InProcessSender<Ping>>();
    }

    [Fact]
    public async Task An_empty_session_is_refused_by_the_gate()
    {
        var handled = new Signal();

        await using var provider = Harness.Build(services => Pipeline(services, handled));
        await using var scope = provider.CreateAsyncScope();

        Assert.False(scope.ServiceProvider.GetRequiredService<SessionProvider>().Session.IsSystem);

        var mediator = scope.ServiceProvider.GetRequiredService<Mediator>();
        var refused = await Assert.ThrowsAsync<BusinessException>(() => mediator.Send(new Ping()));

        // Unauthorized, not Forbidden: the empty session is nobody, and the gate now says which
        // of its two refusals it is answering (issue #47). What this test pins is unchanged —
        // the send never reached the handler.
        Assert.Equal("Unauthorized", refused.Code);
        Assert.Equal(401, refused.Status);
        Assert.Equal(0, handled.Count);
    }

    [Fact]
    public async Task The_same_send_passes_the_gate_from_inside_a_job()
    {
        var handled = new Signal();
        var outcome = new Outcome();

        await using var provider = Harness.Build(services =>
        {
            Pipeline(services, handled);

            services.AddSingleton(outcome);
            services.AddBackgroundJob<SendingJob>();
        });

        await Harness.Run(provider, outcome.Recorded);

        Assert.True(outcome.WasSystem, "the runner's scope did not carry a system session.");
        Assert.Null(outcome.Failure);
        Assert.True(handled.Count >= 1, "the send was allowed but never reached the handler.");
    }
}

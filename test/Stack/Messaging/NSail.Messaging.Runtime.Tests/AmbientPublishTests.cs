// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Publishing;
using NSail.Messaging.Runtime.Sending;

namespace NSail.Messaging.Runtime.Tests;

// The ambient a published event runs under, at the altitude where its rules are rules: it
// covers the broadcast and everything a subscriber does inside it, it does not cover a send,
// and it is gone the moment the publish returns. What the flag MEANS to the gate is pinned in
// NSail.Security.Tests — here it is only the flow that carries it.
public sealed class AmbientPublishTests
{
    static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();

        services.AddMessaging();
        config(services);

        return services.BuildServiceProvider();
    }

    sealed record Happened : IMessage;

    sealed record Command : IMessage;

    sealed class Reader : IHandler<Happened>, IHandler<Command>
    {
        public bool? SeenOnEvent;

        public bool? SeenOnCommand;

        public Task Handle(Happened message, CancellationToken cancellationToken)
        {
            SeenOnEvent = AmbientPublish.Active;

            return Task.CompletedTask;
        }

        public Task Handle(Command message, CancellationToken cancellationToken)
        {
            SeenOnCommand = AmbientPublish.Active;

            return Task.CompletedTask;
        }
    }

    // A subscriber that sends: the shape the whole exemption exists for. Enumerating event
    // keys would have granted the broadcast and then lost to this send one frame down.
    sealed class Reacting : IHandler<Happened>
    {
        readonly Mediator _mediator;

        public Reacting(Mediator mediator)
        {
            _mediator = mediator;
        }

        public async Task Handle(Happened message, CancellationToken cancellationToken)
        {
            await _mediator.Send(new Command(), cancellationToken);
        }
    }

    // A subscriber that actually suspends: without a real await, Publish completes
    // synchronously and the returned task is already done by the time a caller could
    // inspect it, so a leak between the broadcast and the return has nothing to be caught by.
    sealed class Suspending : IHandler<Happened>
    {
        public async Task Handle(Happened message, CancellationToken cancellationToken)
        {
            await Task.Yield();
        }
    }

    [Fact]
    public async Task A_subscriber_runs_inside_the_ambient()
    {
        var reader = new Reader();

        var services = Build(s => s.AddSingleton<IHandler<Happened>>(reader));

        await services.GetRequiredService<Mediator>().Publish(new Happened());

        Assert.True(reader.SeenOnEvent);
    }

    [Fact]
    public async Task What_a_subscriber_sends_in_reaction_runs_inside_it_too()
    {
        var reader = new Reader();

        var services = Build(s =>
        {
            s.AddScoped<ISender<Command>, InProcessSender<Command>>();
            s.AddSingleton<IHandler<Command>>(reader);
            s.AddScoped<IHandler<Happened>, Reacting>();
        });

        await services.GetRequiredService<Mediator>().Publish(new Happened());

        Assert.True(reader.SeenOnCommand);
    }

    [Fact]
    public async Task A_send_of_its_own_is_not_inside_it()
    {
        var reader = new Reader();

        var services = Build(s =>
        {
            s.AddScoped<ISender<Command>, InProcessSender<Command>>();
            s.AddSingleton<IHandler<Command>>(reader);
        });

        await services.GetRequiredService<Mediator>().Send(new Command());

        Assert.False(reader.SeenOnCommand);
    }

    /// <summary>The flag is written before the broadcast starts and undone before the publish
    /// ever awaits, so a caller that has not awaited yet does not inherit it — the leak that
    /// would leave the gate off for the rest of the request.</summary>
    [Fact]
    public async Task It_is_gone_when_the_publish_returns()
    {
        var services = Build(s => s.AddSingleton<IHandler<Happened>>(new Suspending()));

        var publishing = services.GetRequiredService<Mediator>().Publish(new Happened());

        Assert.False(AmbientPublish.Active);

        await publishing;

        Assert.False(AmbientPublish.Active);
    }
}

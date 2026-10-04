// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Sending;

namespace NSail.Messaging.Runtime.Tests;

public sealed class MediatorRoutingTests
{
    private static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        config(services);
        return services.BuildServiceProvider();
    }

    private sealed record Query : IMessage<int>;

    private sealed record Command : IMessage;

    private sealed record Event : IMessage;

    private sealed class QueryHandler : IHandler<Query, int>
    {
        public Task<int> Handle(Query q, CancellationToken ct)
        {
            return Task.FromResult(42);
        }
    }

    private sealed class CommandHandler : IHandler<Command>
    {
        public int Count;
        public Task Handle(Command c, CancellationToken ct)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class EventHandler : IHandler<Event>
    {
        public int Count;
        public Task Handle(Event e, CancellationToken ct)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Mediator_routes_Send_TResult()
    {
        var sp = Build(s =>
        {
            s.AddScoped<ISender<Query, int>, InProcessSender<Query, int>>();
            s.AddSingleton<IHandler<Query, int>, QueryHandler>();
        });

        var mediator = sp.GetRequiredService<Mediator>();

        var result = await mediator.Send(new Query());

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Mediator_routes_Send_void()
    {
        var handler = new CommandHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<Command>, InProcessSender<Command>>();
            s.AddSingleton<IHandler<Command>>(handler);
        });

        var mediator = sp.GetRequiredService<Mediator>();

        await mediator.Send(new Command());

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Mediator_routes_Publish()
    {
        var handler = new EventHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IHandler<Event>>(handler);
        });

        var mediator = sp.GetRequiredService<Mediator>();

        await mediator.Publish(new Event());

        Assert.Equal(1, handler.Count);
    }
}

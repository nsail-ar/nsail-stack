// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Pipelines;

namespace NSail.Messaging.Runtime.Tests;

public sealed class PublishPipelineTests
{
    private static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        config(services);
        return services.BuildServiceProvider();
    }

    private sealed record EventMessage : IMessage;

    private sealed class CounterHandler : IHandler<EventMessage>
    {
        public int Count;

        public Task Handle(EventMessage message, CancellationToken ct)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class CounterInterceptor : IInterceptor<EventMessage>
    {
        public int Count;

        public async Task Invoke(
            EventMessage message,
            PipelineDelegate<EventMessage> next,
            CancellationToken ct)
        {
            Count++;
            await next(message, ct);
        }
    }

    [Fact]
    public async Task Publish_calls_all_DI_handlers()
    {
        var h1 = new CounterHandler();
        var h2 = new CounterHandler();

        var sp = Build(s =>
        {
            s.AddSingleton<IHandler<EventMessage>>(h1);
            s.AddSingleton<IHandler<EventMessage>>(h2);
        });

        var mediator = sp.GetRequiredService<Mediator>();

        await mediator.Publish(new EventMessage());

        Assert.Equal(1, h1.Count);
        Assert.Equal(1, h2.Count);
    }

    [Fact]
    public async Task Publish_with_zero_listeners_is_valid()
    {
        var sp = Build(_ => { });

        var mediator = sp.GetRequiredService<Mediator>();

        await mediator.Publish(new EventMessage());
    }

    [Fact]
    public async Task Publish_calls_interceptors()
    {
        var handler = new CounterHandler();
        var interceptor = new CounterInterceptor();

        var sp = Build(s =>
        {
            s.AddSingleton<IHandler<EventMessage>>(handler);
            s.AddSingleton<IInterceptor<EventMessage>>(interceptor);
        });

        var mediator = sp.GetRequiredService<Mediator>();

        await mediator.Publish(new EventMessage());

        Assert.Equal(1, interceptor.Count);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Publish_calls_dynamic_subscribers()
    {
        int count = 0;

        var sp = Build(_ => { });

        var mediator = sp.GetRequiredService<Mediator>();

        mediator.Subscribe<EventMessage>((msg, ct) =>
        {
            count++;
            return Task.CompletedTask;
        });

        await mediator.Publish(new EventMessage());

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Publish_does_not_call_unsubscribed_handlers()
    {
        int count = 0;

        var sp = Build(_ => { });

        var mediator = sp.GetRequiredService<Mediator>();

        var subscription = mediator.Subscribe<EventMessage>((msg, ct) =>
        {
            count++;
            return Task.CompletedTask;
        });

        subscription.Dispose();

        await mediator.Publish(new EventMessage());

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Publish_throws_if_subscription_throws()
    {
        var sp = Build(_ => { });

        var mediator = sp.GetRequiredService<Mediator>();

        mediator.Subscribe<EventMessage>((msg, ct) =>
        {
            throw new InvalidOperationException("boom");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mediator.Publish(new EventMessage()));
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Problems;

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

        var published = mediator.Publish(new EventMessage());

        await published;

        Assert.True(published.IsCompletedSuccessfully);
        Assert.Empty(sp.GetServices<IHandler<EventMessage>>());
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

    private sealed class ThrowingHandler(bool sync, string text) : IHandler<EventMessage>
    {
        public Task Handle(EventMessage message, CancellationToken ct)
        {
            if (sync)
            {
                throw new InvalidOperationException(text);
            }

            return Task.FromException(new InvalidOperationException(text));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Publish_runs_every_listener_and_reports_every_failure(bool sync)
    {
        var after = new CounterHandler();
        var subscribed = 0;

        var sp = Build(s =>
        {
            s.AddSingleton<IHandler<EventMessage>>(new ThrowingHandler(sync, "a"));
            s.AddSingleton<IHandler<EventMessage>>(after);
            s.AddSingleton<IHandler<EventMessage>>(new ThrowingHandler(sync, "b"));
        });
        var mediator = sp.GetRequiredService<Mediator>();
        mediator.Subscribe<EventMessage>((m, ct) =>
        {
            subscribed++;
            return Task.CompletedTask;
        });

        var ex = await Assert.ThrowsAsync<AggregateException>(() => mediator.Publish(new EventMessage()));

        Assert.Equal(new[] { "a", "b" }, ex.InnerExceptions.Select(e => e.Message));
        Assert.Equal(1, after.Count);
        Assert.Equal(1, subscribed);
    }

    private sealed class SlowHandler(Probe probe) : IHandler<EventMessage>
    {
        public async Task Handle(EventMessage message, CancellationToken ct)
        {
            if (Interlocked.Increment(ref probe.Running) > 1)
            {
                probe.Overlapped = true;
            }

            Interlocked.Increment(ref probe.Entered);
            await Task.Delay(30, ct);
            Interlocked.Decrement(ref probe.Running);
        }
    }

    private sealed class Probe
    {
        public int Running;
        public bool Overlapped;
        public int Entered;
    }

    [Fact]
    public async Task Publish_runs_handlers_one_after_another()
    {
        var probe = new Probe();
        var sp = Build(s =>
        {
            s.AddSingleton(probe);
            s.AddSingleton<IHandler<EventMessage>, SlowHandler>();
            s.AddSingleton<IHandler<EventMessage>, SlowHandler>();
        });

        await sp.GetRequiredService<Mediator>().Publish(new EventMessage());

        Assert.False(probe.Overlapped);
        Assert.Equal(2, probe.Entered);
    }

    private sealed class FailingHandler(Exception failure) : IHandler<EventMessage>
    {
        public Task Handle(EventMessage message, CancellationToken ct)
        {
            return Task.FromException(failure);
        }
    }

    [Fact]
    public async Task Publish_of_only_refusals_ends_as_the_first_refusal()
    {
        var first = new BusinessException("A", "first", [], 409);
        var sp = Build(s =>
        {
            s.AddSingleton<IHandler<EventMessage>>(new FailingHandler(first));
            s.AddSingleton<IHandler<EventMessage>>(new FailingHandler(new BusinessException("B", "second", [], 400)));
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sp.GetRequiredService<Mediator>().Publish(new EventMessage()));

        Assert.Same(first, ex);
    }

    [Fact]
    public async Task Publish_of_only_cancellations_stays_a_cancellation()
    {
        var sp = Build(s =>
        {
            s.AddSingleton<IHandler<EventMessage>>(new FailingHandler(new OperationCanceledException()));
            s.AddSingleton<IHandler<EventMessage>>(new FailingHandler(new OperationCanceledException()));
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() => sp.GetRequiredService<Mediator>().Publish(new EventMessage()));
    }
}

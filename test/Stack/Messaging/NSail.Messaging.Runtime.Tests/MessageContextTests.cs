// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Sending;

namespace NSail.Messaging.Runtime.Tests;

// The headers a call was made with, at the altitude where the promise is a promise: they reach
// the delivery they were passed to and no other. There is no write door anywhere in this file
// on purpose — the only way a header exists is the argument on the Send or Publish it belongs
// to, which is what an ambient "set these next" API would have given up.
public sealed class MessageContextTests
{
    static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();

        services.AddMessaging();
        config(services);

        return services.BuildServiceProvider();
    }

    static Dictionary<string, string> Headers(string source)
    {
        return new Dictionary<string, string> { [MessageHeaders.Source] = source };
    }

    sealed record Happened : IMessage;

    sealed record Command : IMessage;

    sealed class Reader : IHandler<Command>
    {
        readonly MessageContextAccessor _delivery;

        public Reader(MessageContextAccessor delivery)
        {
            _delivery = delivery;
        }

        public string? Seen;

        public bool Read;

        public Task Handle(Command message, CancellationToken cancellationToken)
        {
            Read = true;
            Seen = _delivery.Context?.GetHeader(MessageHeaders.Source);

            return Task.CompletedTask;
        }
    }

    // The IHttpContextAccessor pattern's other half: a handler asks its accessor and a test
    // hands it one, with no pipeline, no publish and no async flow to arrange.
    sealed class FixedContext : MessageContextAccessor
    {
        readonly MessageContext _context;

        public FixedContext(MessageContext context)
        {
            _context = context;
        }

        public override MessageContext? Context
        {
            get { return _context; }
        }
    }

    [Fact]
    public async Task ASubscriberSeesTheHeadersItsOwnPublishCarried()
    {
        var services = Build(_ => { });
        var mediator = services.GetRequiredService<Mediator>();
        var delivery = services.GetRequiredService<MessageContextAccessor>();

        string? seen = null;

        mediator.Subscribe<Happened>((_, _) =>
        {
            seen = delivery.Context?.GetHeader(MessageHeaders.Source);

            return Task.CompletedTask;
        });

        await mediator.Publish(new Happened(), Headers("asker"));

        Assert.Equal("asker", seen);
    }

    [Fact]
    public async Task APublishWithNoHeadersReachesItsSubscriberWithNoContext()
    {
        var services = Build(_ => { });
        var mediator = services.GetRequiredService<Mediator>();
        var delivery = services.GetRequiredService<MessageContextAccessor>();

        var read = false;
        MessageContext? seen = null;

        mediator.Subscribe<Happened>((_, _) =>
        {
            read = true;
            seen = delivery.Context;

            return Task.CompletedTask;
        });

        await mediator.Publish(new Happened());

        Assert.True(read);
        Assert.Null(seen);
    }

    /// <summary>The reason the context is assigned rather than merged: an event published from
    /// inside another delivery is its own operation. A subscriber that publishes would otherwise
    /// hand its own listeners a token nobody put on their event — precisely the adoption this
    /// mechanism exists to refuse.</summary>
    [Fact]
    public async Task AnEventPublishedFromInsideADeliveryStartsClean()
    {
        var services = Build(_ => { });
        var mediator = services.GetRequiredService<Mediator>();
        var delivery = services.GetRequiredService<MessageContextAccessor>();

        MessageContext? inner = null;

        mediator.Subscribe<Command>((_, token) => mediator.Publish(new Happened(), token));

        mediator.Subscribe<Happened>((_, _) =>
        {
            inner = delivery.Context;

            return Task.CompletedTask;
        });

        await mediator.Publish(new Command(), Headers("outer"));

        Assert.Null(inner);
    }

    /// <summary>Two operations in flight at once, each awaited by the other's turn: the headers
    /// travel with the flow rather than with the process, so neither ever reads the other's.
    /// The gates make the interleaving deterministic — without them the test would pass on a
    /// broken implementation whenever the first publish happened to finish first.</summary>
    [Fact]
    public async Task TwoInterleavedPublishesNeverSeeEachOthersHeaders()
    {
        var services = Build(_ => { });
        var mediator = services.GetRequiredService<Mediator>();
        var delivery = services.GetRequiredService<MessageContextAccessor>();

        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var seen = new List<string?>();

        mediator.Subscribe<Happened>(async (_, _) =>
        {
            var before = delivery.Context?.GetHeader(MessageHeaders.Source);

            if (before == "first")
            {
                entered.SetResult();
                await release.Task;
            }

            lock (seen)
            {
                seen.Add($"{before}/{delivery.Context?.GetHeader(MessageHeaders.Source)}");
            }
        });

        var first = mediator.Publish(new Happened(), Headers("first"));

        await entered.Task;

        await mediator.Publish(new Happened(), Headers("second"));

        release.SetResult();

        await first;

        Assert.Equal(["second/second", "first/first"], seen);
    }

    static ServiceProvider BuildWithReader(out Reader reader)
    {
        var services = Build(s =>
        {
            s.AddScoped<ISender<Command>, InProcessSender<Command>>();
            s.AddSingleton<IHandler<Command>>(p => new Reader(p.GetRequiredService<MessageContextAccessor>()));
        });

        reader = (Reader)services.GetServices<IHandler<Command>>().Single();

        return services;
    }

    [Fact]
    public async Task AHandlerSeesTheHeadersItsOwnSendCarried()
    {
        var services = BuildWithReader(out var reader);
        var mediator = services.GetRequiredService<Mediator>();

        await mediator.Send(new Command(), Headers("caller"));

        Assert.True(reader.Read);
        Assert.Equal("caller", reader.Seen);
    }

    [Fact]
    public async Task AHandlerOfASendWithNoHeadersSeesNoContext()
    {
        var services = BuildWithReader(out var reader);
        var mediator = services.GetRequiredService<Mediator>();

        await mediator.Send(new Command());

        Assert.True(reader.Read);
        Assert.Null(reader.Seen);
    }

    [Fact]
    public async Task AFakeAccessorGivesAHandlerItsHeadersWithNoPipelineAtAll()
    {
        var reader = new Reader(new FixedContext(new MessageContext(Headers("injected"))));

        await reader.Handle(new Command(), CancellationToken.None);

        Assert.Equal("injected", reader.Seen);
    }

    [Fact]
    public void AContextAnswersItsHeadersTheWayHttpNamesThem()
    {
        var context = new MessageContext(new Dictionary<string, string> { ["Source"] = "asker" });

        Assert.Equal("asker", context.GetHeader(MessageHeaders.Source));
        Assert.Null(context.GetHeader("other"));
    }
}

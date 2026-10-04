// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Sending;

namespace NSail.Messaging.Runtime.Tests;

public sealed class SendPipelineVoidTests
{
    private static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        config(services);
        return services.BuildServiceProvider();
    }

    private sealed record VoidCommand : IMessage;

    private sealed class VoidHandler : IHandler<VoidCommand>
    {
        public int Count;
        public Task Handle(VoidCommand message, CancellationToken ct)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSender : ISender<VoidCommand>
    {
        public Task Send(VoidCommand message, CancellationToken ct)
        {
            throw new InvalidOperationException("boom");
        }
    }

    [Fact]
    public async Task Send_void_invokes_handler()
    {
        var handler = new VoidHandler();

        var sp = Build(s =>
        {
            s.AddScoped<ISender<VoidCommand>, InProcessSender<VoidCommand>>();
            s.AddSingleton<IHandler<VoidCommand>>(handler);
        });

        var mediator = sp.GetRequiredService<Mediator>();

        await mediator.Send(new VoidCommand());

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Send_throws_if_no_sender_registered()
    {
        var sp = Build(_ => { });

        var mediator = sp.GetRequiredService<Mediator>();

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            mediator.Send(new VoidCommand()));

        Assert.Equal("SenderNotFound", exception.Code);
    }

    [Fact]
    public async Task Send_throws_if_multiple_senders_registered()
    {
        var sp = Build(s =>
        {
            s.AddScoped<ISender<VoidCommand>, InProcessSender<VoidCommand>>();
            s.AddScoped<ISender<VoidCommand>, ThrowingSender>();
        });

        var mediator = sp.GetRequiredService<Mediator>();

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            mediator.Send(new VoidCommand()));

        Assert.Equal("MultipleSenders", exception.Code);
    }

    [Fact]
    public async Task Send_propagates_sender_exceptions()
    {
        var sp = Build(s =>
        {
            s.AddScoped<ISender<VoidCommand>, ThrowingSender>();
        });

        var mediator = sp.GetRequiredService<Mediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mediator.Send(new VoidCommand()));
    }
}

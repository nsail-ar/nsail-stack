// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Sending;

namespace NSail.Messaging.Runtime.Tests;

public sealed class SendPipelineResultTests
{
    private static ServiceProvider Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        config(services);
        return services.BuildServiceProvider();
    }

    private sealed record QueryMessage : IMessage<int>;

    private sealed class QueryHandler : IHandler<QueryMessage, int>
    {
        public int Count;
        public Task<int> Handle(QueryMessage message, CancellationToken ct)
        {
            Count++;
            return Task.FromResult(7);
        }
    }

    private static void AddInProcessSender(IServiceCollection services)
    {
        services.AddScoped<ISender<QueryMessage, int>, InProcessSender<QueryMessage, int>>();
    }

    [Fact]
    public async Task Send_invokes_handler_and_returns_result()
    {
        var handler = new QueryHandler();

        var sp = Build(s =>
        {
            AddInProcessSender(s);
            s.AddSingleton<IHandler<QueryMessage, int>>(handler);
        });

        var mediator = sp.GetRequiredService<Mediator>();

        var result = await mediator.Send(new QueryMessage());

        Assert.Equal(7, result);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Send_throws_if_multiple_handlers_registered()
    {
        var sp = Build(s =>
        {
            AddInProcessSender(s);
            s.AddSingleton<IHandler<QueryMessage, int>, QueryHandler>();
            s.AddSingleton<IHandler<QueryMessage, int>, QueryHandler>();
        });

        var mediator = sp.GetRequiredService<Mediator>();

        await Assert.ThrowsAsync<BusinessException>(() =>
            mediator.Send(new QueryMessage()));
    }

    [Fact]
    public async Task Send_throws_if_no_handler_found()
    {
        var sp = Build(AddInProcessSender);

        var mediator = sp.GetRequiredService<Mediator>();

        await Assert.ThrowsAsync<BusinessException>(() =>
            mediator.Send(new QueryMessage()));
    }

    [Fact]
    public async Task Send_throws_if_no_sender_registered()
    {
        var sp = Build(_ => { });

        var mediator = sp.GetRequiredService<Mediator>();

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            mediator.Send(new QueryMessage()));

        Assert.Equal("SenderNotFound", exception.Code);
    }
}

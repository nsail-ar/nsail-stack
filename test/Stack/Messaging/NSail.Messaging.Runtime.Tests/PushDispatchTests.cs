// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.Runtime.Tests;

public sealed class PushDispatchTests
{
    public sealed record Changed : IMessage
    {
        public int Id { get; init; }
    }

    public sealed record Unregistered : IMessage;

    sealed class Listener<TMessage> : IHandler<TMessage>
        where TMessage : IMessage
    {
        readonly Func<TMessage, Task> _handle;

        public Listener(Func<TMessage, Task> handle)
        {
            _handle = handle;
        }

        public Task Handle(TMessage message, CancellationToken ct)
        {
            return _handle(message);
        }
    }

    sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, Exception? Failure)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception));
        }
    }

    static (PushDispatch Dispatch, RecordingLogger Logger) Build(Action<IServiceCollection> config)
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        config(services);

        var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger();

        return (new PushDispatch(provider.GetRequiredService<Mediator>(), [new PushedMessage<Changed>()], logger), logger);
    }

    [Fact]
    public async Task A_registered_name_is_published_on_this_side_with_its_body()
    {
        var heard = new List<int>();
        var (dispatch, logger) = Build(s => s.AddSingleton<IHandler<Changed>>(new Listener<Changed>(message =>
        {
            heard.Add(message.Id);

            return Task.CompletedTask;
        })));

        await dispatch.Receive(PushedMessage<Changed>.Key, """{"id":7}""");

        Assert.Equal([7], heard);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task A_name_the_composition_did_not_register_is_dropped_without_a_word()
    {
        var heard = 0;
        var (dispatch, logger) = Build(s => s.AddSingleton<IHandler<Unregistered>>(new Listener<Unregistered>(_ =>
        {
            heard++;

            return Task.CompletedTask;
        })));

        await dispatch.Receive(typeof(Unregistered).FullName!, "{}");

        Assert.Equal(0, heard);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task A_listener_that_fails_is_logged_as_an_error_and_never_thrown()
    {
        var failure = new InvalidOperationException("read failed");
        var (dispatch, logger) = Build(s => s.AddSingleton<IHandler<Changed>>(new Listener<Changed>(_ => throw failure)));

        await dispatch.Receive(PushedMessage<Changed>.Key, """{"id":1}""");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Failure);
    }

    [Fact]
    public async Task Connecting_publishes_PushConnected_to_its_listeners()
    {
        var connected = 0;
        var (dispatch, _) = Build(s => s.AddSingleton<IHandler<PushConnected>>(new Listener<PushConnected>(_ =>
        {
            connected++;

            return Task.CompletedTask;
        })));

        await dispatch.Connected();
        await dispatch.Connected();

        Assert.Equal(2, connected);
    }

    [Fact]
    public async Task A_listener_that_fails_on_connecting_is_logged_as_an_error_and_never_thrown()
    {
        var (dispatch, logger) = Build(s => s.AddSingleton<IHandler<PushConnected>>(new Listener<PushConnected>(_ => throw new InvalidOperationException("boom"))));

        await dispatch.Connected();

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }
}

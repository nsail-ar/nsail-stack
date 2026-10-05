// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.SignalR;

/// <summary>The push over SignalR. Scoped, because what it publishes into has to be the scope
/// the screens subscribed in — on WebAssembly that is the renderer's, which is why the chrome
/// opens it rather than the host at boot.</summary>
public sealed class HubFeed : PushFeed, IAsyncDisposable
{
    // After the first try, how long to wait before the next one; the last figure repeats for as
    // long as the tab stays open. A server restarting is the common drop and comes back inside
    // the first few, and a laptop that slept for an hour still reconnects on its own.
    static readonly TimeSpan[] Delays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    readonly Mediator _mediator;
    readonly PushConnection _connect;
    readonly ILogger<HubFeed> _logger;
    readonly Dictionary<string, PushedMessage> _known;

    HubConnection? _connection;
    CancellationTokenSource? _opened;

    // What the composition's SignalR.Clients targets registered, one per [Pushed] message of
    // every kit this client mounts.
    public HubFeed(Mediator mediator, PushConnection connect, IEnumerable<PushedMessage> known, ILogger<HubFeed> logger)
    {
        _mediator = mediator;
        _connect = connect;
        _logger = logger;
        _known = known.ToDictionary(pushed => pushed.Name, StringComparer.Ordinal);
    }

    public override void Open()
    {
        if (_opened is not null)
        {
            return;
        }

        _opened = new CancellationTokenSource();

        var connection = _connect.Build(new Retry());

        connection.On<string, string>(PushFeed.Method, Receive);

        // The catch-up is the listeners' own read: whatever was published while the line was
        // down is not replayed, so they are told to ask.
        connection.Reconnected += _ => Connected();

        // The automatic reconnect answers a line that dropped with an error, and only that: a
        // server that closes it cleanly — a deploy stopping, a connection it aborted — ends in
        // Closed and nothing comes back. While this feed is open, a closed line is started
        // again the same way the first one was, catch-up included; only a refused session
        // stays closed.
        var token = _opened.Token;

        connection.Closed += failure =>
        {
            if (!token.IsCancellationRequested && (failure is null || !Refused(failure)))
            {
                _ = Start(connection, token);
            }

            return Task.CompletedTask;
        };

        _connection = connection;

        _ = Start(connection, token);
    }

    public override async Task Close()
    {
        if (_opened is null)
        {
            return;
        }

        await _opened.CancelAsync();

        _opened.Dispose();
        _opened = null;

        if (_connection is { } connection)
        {
            _connection = null;

            await connection.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Close();
    }

    // The automatic reconnect covers a line that was up and dropped; it does nothing for one
    // that never came up — a deploy in progress when the tab opened — so the first start keeps
    // trying on the same delays.
    async Task Start(HubConnection connection, CancellationToken cancellationToken)
    {
        for (var attempt = 0; !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await connection.StartAsync(cancellationToken);
            }
            catch (Exception failure) when (Refused(failure))
            {
                // The session this feed was opened for is over — expired, or a ticket minted
                // for another tenant. The sign-in that follows reloads the app and opens a
                // feed of its own; this one knocking every 30 s until then would never be let
                // in, so it stops.
                return;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // Nothing to say out loud: the screens still answer every read, and the next
                // attempt is already scheduled.
                await Wait(attempt, cancellationToken);

                continue;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // The first connect is a catch-up too: a screen that read before the line came up
            // has missed whatever was pushed in between.
            await Connected();

            return;
        }
    }

    static async Task Wait(int attempt, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Delay(attempt), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Closed while waiting: the loop reads the token and stops.
        }
    }

    // What arrived, published here as if it had been raised on this side. A name this
    // composition did not register is dropped: the server's word picks an entry from the closed
    // list, never which type a string deserializes into.
    async Task Receive(string name, string body)
    {
        if (!_known.TryGetValue(name, out var pushed))
        {
            return;
        }

        try
        {
            await pushed.Publish(_mediator, body, CancellationToken.None);
        }
        catch (Exception failure)
        {
            // The hub's handler would swallow it, and a listener that failed to re-read is
            // exactly the stale screen the push exists to prevent: said, never silent.
            _logger.LogError(failure, "A listener of the pushed event {Name} failed.", name);
        }
    }

    async Task Connected()
    {
        try
        {
            await _mediator.Publish(new PushConnected());
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "A listener failed on the push connecting; its catch-up read did not happen.");
        }
    }

    // The hub answered that this session may not listen: a cookie that expired, a ticket for
    // another tenant. Not a line to keep knocking on.
    static bool Refused(Exception failure)
    {
        return failure is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden };
    }

    static TimeSpan Delay(int attempt)
    {
        return Delays[Math.Min(attempt, Delays.Length - 1)];
    }

    sealed class Retry : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.RetryReason is { } reason && Refused(reason))
            {
                return null;
            }

            return Delay((int)retryContext.PreviousRetryCount);
        }
    }
}

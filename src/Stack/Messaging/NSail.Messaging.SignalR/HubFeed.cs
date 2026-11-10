// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
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
    readonly PushConnection _connect;
    readonly PushDispatch _dispatch;

    HubConnection? _connection;
    long _since;
    int _attempt;
    CancellationTokenSource? _opened;

    public HubFeed(Mediator mediator, PushConnection connect, IEnumerable<PushedMessage> known, ILogger<HubFeed> logger)
    {
        _connect = connect;
        _dispatch = new PushDispatch(mediator, known, logger);
    }

    public override void Open()
    {
        if (_opened is not null)
        {
            return;
        }

        _opened = new CancellationTokenSource();

        var connection = _connect.Build(new Retry(this));

        connection.On<string, string>(PushFeed.Method, _dispatch.Receive);

        // The catch-up is the listeners' own read: whatever was published while the line was
        // down is not replayed, so they are told to ask.
        connection.Reconnected += _ =>
        {
            _since = Stopwatch.GetTimestamp();
            _attempt++;

            return _dispatch.Connected();
        };

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
                // A line that ended before PushRetry.Steady keeps climbing the ladder: a server
                // that accepts and hangs up must not be answered with a connect per cycle.
                if (Stopwatch.GetElapsedTime(_since) >= PushRetry.Steady)
                {
                    _attempt = 0;
                }

                _ = Start(connection, token, _attempt);
            }

            return Task.CompletedTask;
        };

        _connection = connection;

        _ = Start(connection, token, 0);
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
    async Task Start(HubConnection connection, CancellationToken cancellationToken, int attempt)
    {
        for (; !cancellationToken.IsCancellationRequested; attempt++)
        {
            // The wait comes before the connect, so a line that came up and was closed at once
            // is answered on the ladder it carries; the first connect and a held line's reset
            // are step zero, which is no wait.
            await PushRetry.Wait(attempt, cancellationToken);

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
                continue;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // The first connect is a catch-up too: a screen that read before the line came up
            // has missed whatever was pushed in between.
            _since = Stopwatch.GetTimestamp();
            _attempt = attempt + 1;

            await _dispatch.Connected();

            return;
        }
    }

    // The hub answered that this session may not listen: a cookie that expired, a ticket for
    // another tenant. Not a line to keep knocking on.
    static bool Refused(Exception failure)
    {
        return failure is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden };
    }

    // The step a drop retries on: SignalR counts from zero after every reconnect it wins, so a
    // line that came back and was lost again at once would never climb; the feed's own step
    // carries over, and starts over only for a line that stayed up PushRetry.Steady.
    int Step(int previousRetryCount)
    {
        if (previousRetryCount == 0 && Stopwatch.GetElapsedTime(_since) >= PushRetry.Steady)
        {
            _attempt = 0;
        }

        return _attempt + previousRetryCount;
    }

    sealed class Retry(HubFeed feed) : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.RetryReason is { } reason && Refused(reason))
            {
                return null;
            }

            return PushRetry.Delay(feed.Step((int)retryContext.PreviousRetryCount));
        }
    }
}

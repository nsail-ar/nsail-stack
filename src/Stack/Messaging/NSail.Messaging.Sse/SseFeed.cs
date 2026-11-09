// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.Sse;

/// <summary>The push over Server-Sent Events: one endless response the browser reads, with no
/// vendor client in the download — the push only ever flows server → browser. Scoped, because
/// what it publishes into has to be the scope the screens subscribed in, which on WebAssembly
/// is the renderer's: the chrome opens it, never the host at boot.</summary>
public sealed class SseFeed : PushFeed, IAsyncDisposable
{
    readonly SseConnection _connect;
    readonly PushDispatch _dispatch;

    CancellationTokenSource? _opened;
    long _up;

    public SseFeed(Mediator mediator, SseConnection connect, IEnumerable<PushedMessage> known, ILogger<SseFeed> logger)
    {
        _connect = connect;
        _dispatch = new PushDispatch(mediator, known, logger);
    }

    enum Line
    {
        /// <summary>The line did not come up, or came up and has ended — a deploy in progress, a
        /// laptop that slept, a restart, a proxy's idle cut.</summary>
        Ended,

        /// <summary>The session may not listen. Not a line to keep knocking on.</summary>
        Refused,
    }

    public override void Open()
    {
        if (_opened is not null)
        {
            return;
        }

        _opened = new CancellationTokenSource();

        _ = Listen(_opened.Token);
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
    }

    public async ValueTask DisposeAsync()
    {
        await Close();
    }

    async Task Listen(CancellationToken cancellationToken)
    {
        using var client = _connect.Build();

        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            _up = 0;

            if (await Hold(client, cancellationToken) is Line.Refused)
            {
                // The session this feed was opened for is over — expired, or a ticket minted
                // for another tenant. The sign-in that follows reloads the app and opens a
                // feed of its own; this one knocking every 30 s until then would never be
                // let in, so it stops.
                return;
            }

            // A line that stayed up PushRetry.Steady and ended, cleanly or cut, starts the
            // ladder over; one that ended sooner — a 200 and a hang-up — keeps climbing, or it
            // would be a request storm. The screens still answer every read meanwhile.
            if (_up != 0 && Stopwatch.GetElapsedTime(_up) >= PushRetry.Steady)
            {
                attempt = 0;
            }

            await PushRetry.Wait(attempt, cancellationToken);

            attempt++;
        }
    }

    async Task<Line> Hold(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var request = _connect.Open();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Line.Refused;
            }

            if (!response.IsSuccessStatusCode)
            {
                return Line.Ended;
            }

            _up = Stopwatch.GetTimestamp();

            // The head of the response is the line coming up, and the first connect is a
            // catch-up too: a screen that read before it came up has missed whatever was
            // pushed in between.
            await _dispatch.Connected();

            await Read(response, cancellationToken);

            return Line.Ended;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Line.Ended;
        }
        catch (Exception)
        {
            return Line.Ended;
        }
    }

    // The event-stream format, read as it arrives: a frame per blank line, its event field the
    // name the server published under and its data fields that message's body. A line opening
    // with a colon is a comment — the server's heartbeat, which says the line is alive and
    // nothing else.
    async Task Read(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var reader = new StreamReader(body, Encoding.UTF8);

        var name = (string?)null;
        var data = new StringBuilder();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (name is not null)
                {
                    await _dispatch.Receive(name, data.ToString());
                }

                name = null;
                data.Clear();

                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            var separator = line.IndexOf(':');
            var field = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? string.Empty : line[(separator + 1)..];

            // One space after the colon belongs to the format, not to the value.
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            switch (field)
            {
                case "event":
                    name = value;

                    break;

                case "data":
                    if (data.Length > 0)
                    {
                        data.Append('\n');
                    }

                    data.Append(value);

                    break;
            }
        }
    }
}

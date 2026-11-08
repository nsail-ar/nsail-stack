// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.WebApi.Push;

/// <summary>The server's end of <see cref="PushFeed"/> over Server-Sent Events: one endless
/// response per listening client, mapped at <see cref="PushFeed.SsePath"/> beside the hub's
/// path. The push only ever flows server → browser, so a client says nothing on it.</summary>
static class PushSse
{
    /// <summary>Holds the line open until the client goes away, or until the audience the edge
    /// resolved for it — the tenant, after the ticket was checked — loses the right to listen
    /// (<see cref="PushAudience.Watch"/>).</summary>
    public static async Task Listen(HttpContext context, PushStreams streams, PushAudience audience)
    {
        var response = context.Response;

        response.ContentType = "text/event-stream";

        // An event-stream that is buffered, compressed or cached anywhere in front of this
        // answers nothing until it ends, which it never does.
        response.Headers.CacheControl = "no-cache,no-transform";
        response.Headers["X-Accel-Buffering"] = "no";

        // The client counts itself connected on the response head, so it goes out before the
        // first event rather than with it.
        await response.Body.FlushAsync(context.RequestAborted);

        using var line = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

        var watch = audience.Watch() is { } holds
            ? PushWatch.Over(holds, line.Cancel, Logger(context), line.Token)
            : Task.CompletedTask;

        try
        {
            await streams.Hold(audience.Current, (frame, cancellationToken) => Write(response, frame, cancellationToken), line.Token);
        }
        catch (OperationCanceledException)
        {
            // The tab closed, or the server cut the line: nothing to say and nothing to answer
            // with — the response is long since on its way.
        }
        catch (IOException)
        {
        }
        finally
        {
            await line.CancelAsync();
            await watch;
        }
    }

    static ILogger Logger(HttpContext context)
    {
        return context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(PushSse)) ?? NullLogger.Instance;
    }

    static async Task Write(HttpResponse response, string frame, CancellationToken cancellationToken)
    {
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(frame), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}

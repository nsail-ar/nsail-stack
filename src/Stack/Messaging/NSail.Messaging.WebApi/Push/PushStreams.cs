// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using System.Threading.Channels;

namespace NSail.Messaging.WebApi.Push;

/// <summary>Every Server-Sent Events line this process holds, by audience — what
/// <c>IHubContext</c> is to the hub's groups: it outlives the scope each publish happens in,
/// and a publish can only reach the lines opened in its own audience.</summary>
public sealed class PushStreams
{
    // A line is dropped rather than held once this many frames are waiting on it: a reader that
    // far behind is a tab that cannot keep up, and nothing is replayed anyway — its reconnect
    // publishes PushConnected and its screens read again.
    const int Queued = 64;

    // While nothing is pushed, a comment every so often is what tells a dead line from an idle
    // one, on both ends and in anything between them that times an idle connection out.
    static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(20);

    const string Comment = ":\n\n";

    readonly object _gate = new();
    readonly Dictionary<string, List<Channel<string>>> _open = new(StringComparer.Ordinal);

    /// <summary>Holds one line open for <paramref name="audience"/>, handing every frame it owes
    /// to <paramref name="write"/>, until the request it belongs to ends.</summary>
    public async Task Hold(string audience, Func<string, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        var line = Channel.CreateBounded<string>(new BoundedChannelOptions(Queued) { SingleReader = true });

        Open(audience, line);

        try
        {
            while (true)
            {
                string frame;

                using (var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    idle.CancelAfter(Heartbeat);

                    try
                    {
                        frame = await line.Reader.ReadAsync(idle.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        frame = Comment;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (ChannelClosedException)
                    {
                        return;
                    }
                }

                await write(frame, cancellationToken);
            }
        }
        finally
        {
            Close(audience, line);
        }
    }

    /// <summary>Queues one pushed event on every line of <paramref name="audience"/>. Returns at
    /// once: a publish waits on no client's reading.</summary>
    public void Send(string audience, string name, string body)
    {
        var frame = Frame(name, body);

        lock (_gate)
        {
            if (!_open.TryGetValue(audience, out var lines))
            {
                return;
            }

            foreach (var line in lines)
            {
                if (!line.Writer.TryWrite(frame))
                {
                    line.Writer.TryComplete();
                }
            }
        }
    }

    // The event's name is the frame's own event field and the body its data, so nothing wraps
    // either: the name picks an entry from the client's closed list the way the hub's method
    // argument does. A data field cannot carry a newline, so a body that has one is written as
    // the several fields the event-stream format joins back.
    static string Frame(string name, string body)
    {
        var frame = new StringBuilder();

        frame.Append("event: ").Append(name).Append('\n');

        foreach (var line in body.Split('\n'))
        {
            frame.Append("data: ").Append(line.TrimEnd('\r')).Append('\n');
        }

        return frame.Append('\n').ToString();
    }

    void Open(string audience, Channel<string> line)
    {
        lock (_gate)
        {
            if (!_open.TryGetValue(audience, out var lines))
            {
                lines = [];
                _open[audience] = lines;
            }

            lines.Add(line);
        }
    }

    void Close(string audience, Channel<string> line)
    {
        lock (_gate)
        {
            if (!_open.TryGetValue(audience, out var lines))
            {
                return;
            }

            lines.Remove(line);

            if (lines.Count == 0)
            {
                _open.Remove(audience);
            }
        }
    }
}

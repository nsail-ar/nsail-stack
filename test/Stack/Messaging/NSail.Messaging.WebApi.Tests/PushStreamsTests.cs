// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using NSail.Messaging.WebApi.Push;

namespace NSail.Messaging.WebApi.Tests;

public sealed class PushStreamsTests
{
    static readonly TimeSpan Patience = TimeSpan.FromSeconds(40);

    sealed class Line
    {
        readonly CancellationTokenSource _cancel = new();
        readonly SemaphoreSlim _arrived = new(0);

        public ConcurrentQueue<string> Frames { get; } = new();

        public Task Held { get; private set; } = Task.CompletedTask;

        public static async Task<Line> Open(PushStreams streams, string audience, Func<string, CancellationToken, Task>? write = null)
        {
            var line = new Line();
            var registered = new TaskCompletionSource();

            line.Held = streams.Hold(
                audience,
                async (frame, token) =>
                {
                    line.Frames.Enqueue(frame);
                    line._arrived.Release();

                    if (write is not null)
                    {
                        await write(frame, token);
                    }
                },
                line._cancel.Token,
                _ =>
                {
                    registered.SetResult();

                    return Task.CompletedTask;
                });

            await registered.Task.WaitAsync(Patience);

            return line;
        }

        public async Task<string> Next()
        {
            await _arrived.WaitAsync(Patience);

            Frames.TryDequeue(out var frame);

            return frame!;
        }

        public async Task Close()
        {
            _cancel.Cancel();

            await Held.WaitAsync(Patience);
        }
    }

    [Fact]
    public async Task A_publish_reaches_only_the_lines_of_its_audience_as_an_event_frame()
    {
        var streams = new PushStreams();
        var mine = await Line.Open(streams, "a");
        var other = await Line.Open(streams, "b");

        streams.Send("a", "Some.Event", """{"id":1}""");

        Assert.Equal("event: Some.Event\ndata: {\"id\":1}\n\n", await mine.Next());
        Assert.Empty(other.Frames);

        await mine.Close();
        await other.Close();
    }

    [Fact]
    public async Task A_body_with_newlines_is_written_as_several_data_fields()
    {
        var streams = new PushStreams();
        var line = await Line.Open(streams, "a");

        streams.Send("a", "E", "one\r\ntwo");

        Assert.Equal("event: E\ndata: one\ndata: two\n\n", await line.Next());

        await line.Close();
    }

    [Fact]
    public async Task A_line_that_falls_far_behind_is_dropped_not_held()
    {
        var streams = new PushStreams();
        var release = new TaskCompletionSource();
        var slow = await Line.Open(streams, "a", (_, _) => release.Task);

        streams.Send("a", "E", "0");
        await slow.Next();

        // The writer is stuck on frame 0: the channel takes its bound, and the next send overflows it.
        for (var i = 1; i <= 65; i++)
        {
            streams.Send("a", "E", i.ToString());
        }

        release.SetResult();
        await slow.Held.WaitAsync(Patience);

        Assert.True(slow.Frames.Count <= 64);

        streams.Send("a", "E", "after");

        Assert.DoesNotContain(slow.Frames, frame => frame.Contains("after", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancelling_ends_the_hold_and_releases_the_audience()
    {
        var streams = new PushStreams();
        var line = await Line.Open(streams, "a");

        await line.Close();
        streams.Send("a", "E", "late");

        Assert.Empty(line.Frames);
    }

    [Fact]
    public async Task An_idle_line_is_written_a_comment_heartbeat()
    {
        var streams = new PushStreams();
        var line = await Line.Open(streams, "a");

        // The heartbeat is a fixed 20 seconds of real time.
        var arrived = await line.Next();

        Assert.Equal(":\n\n", arrived);

        await line.Close();
    }
}

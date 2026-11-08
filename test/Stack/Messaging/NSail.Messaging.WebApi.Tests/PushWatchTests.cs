// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging.Abstractions;
using NSail.Messaging.WebApi.Push;

namespace NSail.Messaging.WebApi.Tests;

/// <summary>The loop a held SSE line or hub connection runs beside itself: it ends the feed when
/// the audience loses the right to listen, and an ask that fails does not end the watch.</summary>
public sealed class PushWatchTests
{
    static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task A_feed_is_ended_when_its_audience_loses_the_right()
    {
        var asks = 0;
        var lost = 0;

        await PushWatch.Over(
            token => Task.FromResult(++asks < 3),
            () => lost++,
            NullLogger.Instance,
            Tick,
            Timeout());

        Assert.Equal(3, asks);
        Assert.Equal(1, lost);
    }

    [Fact]
    public async Task An_ask_that_throws_is_asked_again_and_the_wall_still_stands()
    {
        var asks = 0;
        var lost = 0;

        await PushWatch.Over(
            token =>
            {
                asks++;

                return asks switch
                {
                    1 => Task.FromResult(true),
                    2 => throw new InvalidOperationException("the store restarted"),
                    3 => Task.FromException<bool>(new TimeoutException()),
                    _ => Task.FromResult(false),
                };
            },
            () => lost++,
            NullLogger.Instance,
            Tick,
            Timeout());

        Assert.Equal(4, asks);
        Assert.Equal(1, lost);
    }

    [Fact]
    public async Task A_feed_that_goes_away_ends_the_watch_quietly()
    {
        using var gone = new CancellationTokenSource();

        var watch = PushWatch.Over(token => Task.FromResult(true), () => throw new InvalidOperationException(), NullLogger.Instance, Tick, gone.Token);

        await gone.CancelAsync();
        await watch;

        Assert.True(watch.IsCompletedSuccessfully);
    }

    static CancellationToken Timeout()
    {
        return new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
    }
}

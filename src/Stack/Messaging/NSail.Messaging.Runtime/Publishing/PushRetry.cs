// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Publishing;

/// <summary>How long a client's feed waits before trying the line again — the first connect, a
/// drop, and a line the server closed cleanly alike. One ladder for every transport: a tab left
/// open keeps knocking forever, and only a refused session stops. The ladder starts over only for a
/// line that stayed up <see cref="Steady"/>.</summary>
public static class PushRetry
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

    /// <summary>How long a line must stay up to count as held, and so start the ladder over. Past
    /// the server's heartbeat: a line that closed sooner is a server or proxy answering and
    /// hanging up, which climbs on.</summary>
    public static readonly TimeSpan Steady = TimeSpan.FromSeconds(30);

    public static TimeSpan Delay(int attempt)
    {
        return Delays[Math.Min(Math.Max(attempt, 0), Delays.Length - 1)];
    }

    /// <summary>Waits out this attempt's delay, and returns rather than throwing if the feed was
    /// closed while it waited — the caller's loop reads the token and stops.</summary>
    public static async Task Wait(int attempt, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Delay(attempt), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}

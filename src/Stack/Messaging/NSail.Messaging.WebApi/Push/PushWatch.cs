// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;

namespace NSail.Messaging.WebApi.Push;

/// <summary>Asks a held feed's audience, once a second, whether it still has the right to
/// listen, and ends the feed when it has not. What an ask costs, and how fresh its answer is,
/// are the audience's to keep down and to state.</summary>
static class PushWatch
{
    static readonly TimeSpan Period = TimeSpan.FromSeconds(1);

    public static Task Over(Func<CancellationToken, Task<bool>> holds, Action lost, ILogger logger, CancellationToken cancellationToken)
    {
        return Over(holds, lost, logger, Period, cancellationToken);
    }

    internal static async Task Over(
        Func<CancellationToken, Task<bool>> holds,
        Action lost,
        ILogger logger,
        TimeSpan period,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(period);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                bool held;

                try
                {
                    held = await holds(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // An ask that failed (the store restarting under the read) is asked again
                    // next tick. Ending the watch on it would leave the feed listening for good,
                    // past every suspension written afterwards.
                    logger.LogWarning(exception, "A held push feed could not ask whether it may still listen; it asks again next tick.");

                    continue;
                }

                if (!held)
                {
                    lost();

                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

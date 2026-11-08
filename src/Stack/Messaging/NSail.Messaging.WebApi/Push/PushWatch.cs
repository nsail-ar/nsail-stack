// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.WebApi.Push;

/// <summary>Asks a held feed's audience, once a second, whether it still has the right to
/// listen, and ends the feed when it has not. The question is cheap where it is asked of a
/// memo, and the answer's own freshness is the audience's to state.</summary>
static class PushWatch
{
    static readonly TimeSpan Period = TimeSpan.FromSeconds(1);

    public static async Task Over(Func<CancellationToken, Task<bool>> holds, Action lost, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Period);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!await holds(cancellationToken))
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

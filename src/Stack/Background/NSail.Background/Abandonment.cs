// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;

namespace NSail.Background;

static class Abandonment
{
    // The scope is what the abandoned work still resolves from and holds a DbContext of, so it
    // is disposed when that work ends, not under it, and so is the lock that fences the pass; the
    // work's fault is observed there too. Work is abandoned when it outlives its budget or the
    // host's stop.
    internal static async ValueTask Release(AsyncServiceScope? scope, Task? abandoned, IAsyncDisposable? held = null)
    {
        if (scope is not { } owned)
        {
            if (held is not null)
            {
                await held.DisposeAsync().ConfigureAwait(false);
            }

            return;
        }

        if (abandoned is null)
        {
            await Dispose(owned, held).ConfigureAwait(false);

            return;
        }

        _ = abandoned.ContinueWith(
                static async (task, state) =>
                {
                    _ = task.Exception;

                    var (scoped, lease) = ((AsyncServiceScope, IAsyncDisposable?))state!;

                    await Dispose(scoped, lease).ConfigureAwait(false);
                },
                (owned, held),
                TaskScheduler.Default)
            .Unwrap()
            .ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    // The lock is freed even when the scope faults on dispose: it rides a connection of its own.
    static async ValueTask Dispose(AsyncServiceScope scope, IAsyncDisposable? held)
    {
        try
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            if (held is not null)
            {
                await held.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

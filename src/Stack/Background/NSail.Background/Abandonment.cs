// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;

namespace NSail.Background;

static class Abandonment
{
    // The scope is what the abandoned work still resolves from and holds a DbContext of, so it
    // is disposed when that work ends, not under it, and the lock that fences the pass with it; the work's fault is observed there too.
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
            await owned.DisposeAsync().ConfigureAwait(false);

            if (held is not null)
            {
                await held.DisposeAsync().ConfigureAwait(false);
            }

            return;
        }

        _ = abandoned.ContinueWith(
                static async (task, state) =>
                {
                    _ = task.Exception;

                    var (scoped, lease) = ((AsyncServiceScope, IAsyncDisposable?))state!;

                    await scoped.DisposeAsync().ConfigureAwait(false);

                    if (lease is not null)
                    {
                        await lease.DisposeAsync().ConfigureAwait(false);
                    }
                },
                (owned, held),
                TaskScheduler.Default)
            .Unwrap()
            .ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }
}

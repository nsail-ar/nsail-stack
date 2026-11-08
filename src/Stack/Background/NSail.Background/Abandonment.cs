// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;

namespace NSail.Background;

static class Abandonment
{
    // The scope is what the abandoned work still resolves from and holds a DbContext of, so it
    // is disposed when that work ends, not under it; the work's fault is observed there too.
    internal static async ValueTask Release(AsyncServiceScope scope, Task? abandoned)
    {
        if (abandoned is null)
        {
            await scope.DisposeAsync().ConfigureAwait(false);

            return;
        }

        _ = abandoned.ContinueWith(
            static async (task, state) =>
            {
                _ = task.Exception;

                await ((AsyncServiceScope)state!).DisposeAsync().ConfigureAwait(false);
            },
            scope,
            TaskScheduler.Default);
    }
}

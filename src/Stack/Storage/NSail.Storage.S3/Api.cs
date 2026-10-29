// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Storage.Providers;

namespace NSail.Storage.S3;

public static class Api
{
    public static void AddS3Storage(this IServiceCollection services)
    {
        // HttpClient's default cuts a send off at 100 seconds and that clock covers the whole
        // body, so a PUT anywhere near the 5 GiB ceiling storage.md writes down dies
        // half-written at the far end — the very outcome the non-seekable refusal exists to
        // prevent — and a get streaming under ResponseHeadersRead dies while the caller is
        // still copying. A transfer of unbounded size has no wall clock Storage could guess:
        // the deadline is the CancellationToken the four verbs already carry.
        services.AddHttpClient(S3StorageProvider.ClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddScoped<IStorageProvider, S3StorageProvider>();
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.Sse;

public static class Setup
{
    /// <summary>Opens the client to the server's push (<see cref="PushFeed"/>) over Server-Sent
    /// Events, at <paramref name="origin"/> — the host the client was served from. Which
    /// transport a client listens over is this call alone: the server serves both.</summary>
    public static IServiceCollection AddPush(this IServiceCollection services, Uri origin, Action<SseOptions>? transport = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new SseConnection(origin, transport));
        services.AddScoped<PushFeed, SseFeed>();

        return services;
    }
}

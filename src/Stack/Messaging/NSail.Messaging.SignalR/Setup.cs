// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Publishing;

namespace NSail.Messaging.SignalR;

public static class Setup
{
    /// <summary>Opens the client to the server's push (<see cref="PushFeed"/>), at
    /// <paramref name="origin"/> — the host the client was served from.</summary>
    public static IServiceCollection AddPush(this IServiceCollection services, Uri origin, Action<HttpConnectionOptions>? transport = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new PushConnection(origin, transport));
        services.AddScoped<PushFeed, HubFeed>();

        return services;
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection.Extensions;
using NSail.Messaging.Runtime.Publishing;
using NSail.Messaging.WebApi.Push;
using NSail.Serialization;

namespace NSail.Messaging.WebApi;

public static class Setup
{
    /// <summary>Binds the generated endpoints to the same wire contract the generated
    /// clients serialize with. The host owns its own options instance, so the converters
    /// are copied rather than the instance shared.</summary>
    public static void AddMessagingJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            foreach (var converter in JsonOptions.Converters)
            {
                options.SerializerOptions.Converters.Add(converter);
            }
        });
    }

    public static void MapEndpoints(this WebApplication app)
    {
        var endpointEntries = app.Services.GetServices<IEndpointEntry>();
        foreach (var entry in endpointEntries)
        {
            entry.Configure(app);
        }
    }

    /// <summary>The server's end of the push: the hub its clients listen on and the audience a
    /// publish reaches. Which messages are pushed is the SignalR.Hubs target's, per kit.</summary>
    public static void AddPush(this IServiceCollection services)
    {
        services.AddSignalR();
        services.TryAddScoped<PushAudience>();
    }

    public static void MapPush(this WebApplication app)
    {
        app.MapHub<PushHub>("/" + PushFeed.Path);
    }

    public static void AddErrorHandler(this IServiceCollection services)
    {
        services.AddScoped<ErrorMiddleware>();
    }

    public static void UseErrorHandler(this WebApplication app)
    {
        app.UseMiddleware<ErrorMiddleware>();
    }
}
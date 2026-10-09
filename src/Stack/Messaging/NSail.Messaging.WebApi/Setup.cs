// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    /// <summary>The server's end of the push: both transports its clients can listen over and
    /// the audience a publish reaches. Which messages are pushed is the Push.Publishers
    /// target's, per kit.</summary>
    public static void AddPush(this IServiceCollection services)
    {
        services.AddSignalR();
        services.TryAddSingleton<PushStreams>();
        services.TryAddScoped<PushAudience>();
    }

    /// <summary>Both roads, always: which one a seat listens over is its own composition's, so
    /// the server serves either and a publish goes out on both.</summary>
    public static void MapPush(this WebApplication app)
    {
        app.MapHub<PushHub>("/" + PushFeed.Path);
        app.MapGet("/" + PushFeed.SsePath, PushSse.Listen).RequireAuthorization();
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
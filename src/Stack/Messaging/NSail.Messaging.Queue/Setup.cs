// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSail.Background;
using NSail.Configuration;
using NSail.Data;

namespace NSail.Messaging.Queue;

public static class Setup
{
    /// <summary>The send side: the outbox entity's slice of the model and what a
    /// <see cref="QueueSender{TMessage}"/> needs. Called by the generated Queue.Producers
    /// holder, so composing the kit that declares a <c>[Queue]</c> message is the opt-in —
    /// a product with none gains no table and its migration chain does not move.</summary>
    public static IServiceCollection AddQueue(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContextSetup<QueueDbContextSetup>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(Options);

        // Asked at startup rather than at the first send: an install that named a broker it
        // cannot have is misconfigured from the moment it boots, and finding that out on the
        // first queued command is finding it out from a customer.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueTransportGuard>());

        return services;
    }

    /// <summary>The receive side: the drain, its job and the registry the stored type name is
    /// resolved through. Called by the generated Queue.Consumers holder.</summary>
    public static IServiceCollection AddQueueDelivery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddQueue();

        services.TryAddSingleton<QueueMeter>();
        services.TryAddSingleton(provider => new QueueConsumers(provider.GetServices<QueueConsumer>()));
        services.TryAddScoped<QueueDrain>();

        services.AddBackgroundJob<QueueDrainJob>();

        return services;
    }

    // Resolved from the configuration where there is one, at its own defaults where there is
    // not: a handler harness composes no configuration and still has to drain.
    static QueueOptions Options(IServiceProvider provider)
    {
        return provider.GetService<IConfiguration>() is { } configuration
            ? configuration.Load<QueueOptions>()
            : new QueueOptions();
    }
}

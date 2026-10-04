// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Packs;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Publishing;
using NSail.Messaging.Runtime.Sending;
using NSail.Metadata;

namespace NSail.Messaging.Runtime;

public static class Setup
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMessageRegistry();

        services.AddScoped<Mediator>();
        services.AddScoped<PackReplayer>();

        // Singleton like IHttpContextAccessor and for the same reason: it holds nothing of its
        // own — the delivery it answers with lives on the async flow, so one instance serves
        // every scope and a test replaces it with a fake by registering its own.
        services.AddSingleton<MessageContextAccessor>();

        services.AddScoped(typeof(SendPipeline<>));
        services.AddScoped(typeof(SendPipeline<,>));
        services.AddScoped(typeof(InProcessSendPipeline<>));
        services.AddScoped(typeof(InProcessSendPipeline<,>));
        services.AddScoped(typeof(InProcessSender<>));
        services.AddScoped(typeof(InProcessSender<,>));

        services.AddScoped(typeof(PublishPipeline<>));
        services.AddScoped(typeof(InProcessPublishPipeline<>));
        services.AddScoped(typeof(InProcessPublisher<>));

        // Publish is broadcast: the in-process publisher coexists with future
        // transport publishers (no exclusivity, unlike senders). No per-publish
        // scope either — subscriptions live in the subscriber's scope.
        services.AddScoped(typeof(IPublisher<>), typeof(InProcessPublisher<>));

        services.AddScoped(typeof(Subscriptions<>));

        // Closed unless a client host composes a transport for it: the end that publishes has
        // nothing to listen to.
        services.TryAddScoped<PushFeed>();

        // Every send is validated against the message's own DataAnnotations, on whichever side
        // it is sent from: the client rejects without a round trip, the server never trusts what
        // arrives. SendPipeline applies this structurally rather than as a registered
        // IInterceptor, so it can never race a gate that another Add* registers later.

        return services;
    }

    /// <summary>The name→type map, built from what the Policies.Handlers generator registered
    /// rather than from a DI scan: the WebAssembly client registers no handler at all, and a
    /// scan would hand it an empty registry exactly where the policy editor needs the keys.
    /// Resolution is deferred, so the Add{Kit}Permissions calls may land after this one.</summary>
    public static void AddMessageRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMetadata();

        services.TryAddSingleton(provider => new MessageRegistry(
            provider.GetRequiredService<MetadataProvider>(),
            provider.GetServices<MessageRegistration>().Select(registration => registration.MessageType)));
    }
}
// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSail.Security;

namespace NSail.Background;

public static class Setup
{
    /// <summary>Contributes a recurring job: the runner resolves it from a fresh scope and
    /// invokes it every Interval, acting as the system. Called by the kit that ships the job,
    /// from the kit's own composition entry — composing the kit is the opt-in, exactly as it
    /// is for the kit's endpoints, and a host never registers a job separately.</summary>
    public static IServiceCollection AddBackgroundJob<TJob>(this IServiceCollection services)
        where TJob : class, IBackgroundJob
    {
        ArgumentNullException.ThrowIfNull(services);

        // The runner's scope sets Session.System() on the SessionProvider; a host that
        // composes a job but not security would otherwise fail at the first tick.
        services.AddSecurity();

        services.TryAddScoped<TJob>();

        // TryAddEnumerable rather than AddHostedService: every contributed job calls this,
        // and IHostedService is a collection — a plain add would give the host one runner per
        // job, each of them running all of them.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BackgroundJobRunner>());

        Jobs(services).Add(typeof(TJob));

        return services;
    }

    /// <summary>Contributes the fire-and-forget queue (<see cref="DeferredWork"/>): a caller
    /// resolves it and enqueues instead of awaiting a send it has no business waiting on. One
    /// runner, composed once — a second call is a no-op, the same shape <c>AddBackgroundJob</c>
    /// keeps for its own hosted service.</summary>
    public static IServiceCollection AddDeferredWork(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSecurity();

        services.TryAddSingleton<DeferredWork>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DeferredWorkRunner>());

        return services;
    }

    // Read off the collection instead of resolved: registration happens before any provider
    // exists, and every call has to append to the same list.
    static BackgroundJobs Jobs(IServiceCollection services)
    {
        if (services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(BackgroundJobs))?.ImplementationInstance is BackgroundJobs registered)
        {
            return registered;
        }

        var jobs = new BackgroundJobs();

        services.AddSingleton(jobs);

        return jobs;
    }
}

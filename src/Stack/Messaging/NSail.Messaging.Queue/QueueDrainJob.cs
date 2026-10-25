// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Background;
using NSail.Data;

namespace NSail.Messaging.Queue;

/// <summary>The drain as a background job, rather than a loop of its own. What a durable queue
/// needs off a clock — one pass per tenant on the roster, each in its own scope with that
/// tenant entered, a suspended customer skipped, the install's default language set, and one
/// tenant's failure costing nobody else's pass — is exactly what
/// <c>BackgroundJobRunner</c> already does, and a second implementation of it would be a
/// second place for all of that to be got wrong.
///
/// <para>The one thing the runner decides that this job does not keep is the session: the
/// runner's scope acts as the system, and <see cref="QueueDrain"/> re-enters the row's own
/// actor before each delivery.</para></summary>
public sealed class QueueDrainJob : IBackgroundJob
{
    readonly IServiceProvider _services;
    readonly QueueOptions _options;

    // The drain is resolved in Run and not taken here, which is the one thing this job cannot
    // spell the obvious way. The runner reads a job's cadence once, before its first tick, from
    // a scope that has entered no tenant (BackgroundJobRunner.Declared) — and QueueDrain needs
    // a DbContext, whose factory asks the scope's TenancyProvider for a connection. Under
    // MultiDb that provider has no tenant to answer with and throws, the runner logs the loop as
    // unstartable and returns, and the queue fills with rows nothing ever delivers. Constructed
    // from nothing but its options, the cadence is answerable with no tenant and the context is
    // built inside Run, which the runner does call with one entered.
    public QueueDrainJob(IServiceProvider services, QueueOptions options)
    {
        _services = services;
        _options = options;
    }

    public TimeSpan Interval
    {
        get { return _options.Interval; }
    }

    // A queue per tenant, under both walls: under SingleDb the rows are the cell's with the
    // tenant in their column, under MultiDb they are in the tenant's own database, and either
    // way one pass per tenant is what reads them.
    public TenancyScope Tenancy
    {
        get { return TenancyScope.Tenant; }
    }

    public Task Run(CancellationToken cancellationToken)
    {
        return _services.GetRequiredService<QueueDrain>().Run(cancellationToken);
    }
}

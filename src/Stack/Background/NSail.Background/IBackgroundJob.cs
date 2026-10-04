// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.Background;

/// <summary>A unit of recurring work, contributed by the kit that ships it and invoked by
/// the runner on a cadence. Recurring work and nothing else: this is not a queue and not a
/// per-item schedule — a job is handed a tick and decides for itself what is due.</summary>
public interface IBackgroundJob
{
    /// <summary>How often the runner invokes Run. The job decides what is due inside —
    /// "run often, ask what expires" — the runner never schedules per item.</summary>
    TimeSpan Interval { get; }

    /// <summary>Whose work one tick is: <see cref="TenancyScope.Tenant"/> for work that touches
    /// rows a tenant owns, <see cref="TenancyScope.Install"/> for work that belongs to the
    /// install as a whole. There is no default, because a job that never answered would answer
    /// by accident — as the install, which under a tenant-scoped mode reads nothing and cannot
    /// write at all.
    ///
    /// <para>The runner acts on the answer and the job never does: a per-tenant job is invoked
    /// once per tenant on the roster (<see cref="TenantRoster"/>), each pass in a scope of its
    /// own with that tenant already entered, so <see cref="Run"/> is written as if there were
    /// one tenant in the world. On an install that resolves none, both answers are the same
    /// single pass.</para></summary>
    TenancyScope Tenancy { get; }

    /// <summary>One tick's worth of work, in a fresh scope whose session is the system's:
    /// every id a send needs must travel in the message, since a system session carries no
    /// organization, no party and no memberships.</summary>
    Task Run(CancellationToken cancellationToken);
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;
using NSail.Security;

namespace NSail.Background;

/// <summary>Fire-and-forget for the one shape that keeps showing up: a handler already saved
/// what it must, and the rest of what it was about to do is a network call to somebody else's
/// server (a mail, a WhatsApp template) that the caller has no business waiting on. Enqueue
/// hands the work to <see cref="DeferredWorkRunner"/> and returns immediately; the runner owns
/// its own scope, its own tenant and a timeout, so a slow vendor costs a queue slot and never a
/// request.
///
/// <para>Not a message transport and not the <c>[Queue]</c> messaging.md reserves for a future
/// durable dispatch (messaging.md, "a queue is a semantic contract"): this is in-memory, and a
/// process that dies between Enqueue and the runner picking it up loses the item. Accepted
/// because every caller here already persisted the fact that matters — the order, the sale —
/// before enqueueing; what is lost on a crash is a notification, not data (ruled 2026-09-23).</para>
///
/// <para>Registered by <see cref="Setup.AddDeferredWork"/>, by hand rather than through the
/// generator: it is a Stack-wide singleton every host gets once, the same shape
/// <see cref="BackgroundJobs"/> is registered in.</para></summary>
public sealed class DeferredWork
{
    readonly Channel<DeferredWorkItem> _channel = Channel.CreateUnbounded<DeferredWorkItem>(
        new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Queues <paramref name="work"/> to run on its own scope, entered into
    /// <paramref name="tenant"/> exactly as the caller's own scope was (<see
    /// cref="ResolvedTenancyProvider.Enter"/>) — the runner has no request to read it from.
    /// <paramref name="kind"/> is a metric tag, never logged as text (LogScrubber strips it
    /// from the export anyway): a short, stable name such as
    /// <c>"Optical.WorkOrders.Confirm"</c>, not a sentence.</summary>
    public void Enqueue(string kind, Tenant tenant, Func<IServiceProvider, CancellationToken, Task> work)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(work);

        // TryWrite never awaits and never fails on an unbounded channel — the point of
        // choosing one: a caller mid-request must not be made to wait on the queue it is
        // trying to get off of.
        _channel.Writer.TryWrite(new DeferredWorkItem(kind, tenant, work));
    }

    internal ChannelReader<DeferredWorkItem> Reader
    {
        get { return _channel.Reader; }
    }

    /// <summary>Runs every item queued so far, inline, on the caller's own await — for
    /// <c>HandlerHost</c>, which raises no <see cref="Microsoft.Extensions.Hosting.IHost"/> and
    /// so never starts <see cref="DeferredWorkRunner"/>: without this a fixture asserting on a
    /// queued send would see nothing, forever. Exceptions are NOT caught here the way the real
    /// runner catches them — a fixture that queued work that throws should fail loudly, the same
    /// as any other assertion, rather than pass on a silently swallowed bug.</summary>
    public async Task DrainAll(IServiceScopeFactory scopes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        while (_channel.Reader.TryRead(out var item))
        {
            await using var scope = scopes.CreateAsyncScope();

            scope.ServiceProvider.GetRequiredService<SessionProvider>().Session = Session.System();

            if (item.Tenant.IsResolved)
            {
                scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(item.Tenant);
            }

            await item.Work(scope.ServiceProvider, cancellationToken);
        }
    }
}

sealed record DeferredWorkItem(string Kind, Tenant Tenant, Func<IServiceProvider, CancellationToken, Task> Work);

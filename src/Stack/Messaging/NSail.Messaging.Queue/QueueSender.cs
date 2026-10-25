// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Sending;
using NSail.Messaging.Runtime.UnitOfWork;
using NSail.Security;
using NSail.Serialization;

namespace NSail.Messaging.Queue;

/// <summary>The send side of <c>[Queue]</c>, registered as the message's <c>ISender</c> by the
/// Queue.Producers holder. It writes one row through the scope's own <c>DbContext</c> and
/// returns: the row commits with the operation that made the send and with nothing else, so an
/// operation that rolls back queues nothing and one that commits has already queued it when
/// the caller is answered.
///
/// <para>It is an <see cref="IAmbientSender"/>, unlike a transport sender, because the row is
/// not across a process boundary — it is in the caller's own transaction. A send nested in an
/// operation joins that unit; one that is nobody's nested send opens its own, so the row
/// commits alone rather than on a connection with no transaction on it.</para></summary>
public sealed class QueueSender<TMessage> : ISender<TMessage>, IAmbientSender
    where TMessage : IMessage
{
    readonly DbContext _db;
    readonly IServiceScopeFactory _scopeFactory;
    readonly SessionProvider _sessions;
    readonly MessageContextAccessor _context;
    readonly TimeProvider _clock;

    public QueueSender(
        DbContext db,
        IServiceScopeFactory scopeFactory,
        SessionProvider sessions,
        MessageContextAccessor context,
        TimeProvider clock)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _sessions = sessions;
        _context = context;
        _clock = clock;
    }

    public Task Run(
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        return AmbientUnitOfWork.Run(_scopeFactory, typeof(TMessage).Name, operation, cancellationToken);
    }

    public Task<TResult> Run<TResult>(
        Func<IServiceProvider, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        return AmbientUnitOfWork.Run(_scopeFactory, typeof(TMessage).Name, operation, cancellationToken);
    }

    public async Task Send(TMessage message, CancellationToken cancellationToken)
    {
        var session = _sessions.Session;
        var now = _clock.GetUtcNow().UtcDateTime;

        _db.Add(new QueuedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = QueueConsumer<TMessage>.Key,
            Body = JsonSerializer.Serialize(message, JsonOptions.Wire),
            Headers = Headers(),
            ActorUserId = session.UserId,
            ActorPartyId = session.PartyId,
            ActorOrganizationId = session.OrganizationId,
            EnqueuedAt = now,
            NextAttemptAt = now,
        });

        // Written now rather than left to the unit's commit: the row has to be INSIDE this
        // operation's transaction, and a handler further down that reads the queue — the drain
        // itself, on a redelivery — would otherwise read a table the change tracker has not
        // reached yet. DbContextUnitOfWork.Commit confirms the transaction and never saves, so
        // without this line there would be no row at all.
        //
        // Saving the context and not one row is unavoidable — EF has no narrower save — so it
        // is part of what [Queue] promises rather than an accident: queueing flushes whatever
        // the operation has tracked so far. QueueAttribute's own doc and messaging.md, The
        // durable queue, say so where the caller reads them.
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    string? Headers()
    {
        if (_context.Context is not { Headers.Count: > 0 } context)
        {
            return null;
        }

        return JsonSerializer.Serialize(context.Headers, JsonOptions.Wire);
    }
}

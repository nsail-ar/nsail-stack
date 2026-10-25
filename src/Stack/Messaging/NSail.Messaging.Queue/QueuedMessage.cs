// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Queue;

/// <summary>One durable send, written through the sending operation's own
/// <c>DbContext</c> and therefore committed or rolled back with it. Tenant-scoped like any
/// other entity — the column, the filter and the stamp come from the model pass — so each
/// tenant's queue is its own under both walls: the cell's database with the tenant in its
/// column under <c>SingleDb</c>, the tenant's own database under <c>MultiDb</c>.</summary>
public class QueuedMessage
{
    public Guid Id { get; set; }

    /// <summary>The message's full type name and nothing about its assembly, written and read
    /// by the one rule both of the queue's ends share (<c>QueueConsumer{TMessage}.Key</c>): the
    /// stored name picks a registered consumer, and never becomes a type on its own.</summary>
    public string MessageType { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>The headers the send carried, as the wire JSON of a string dictionary; null
    /// where it carried none.</summary>
    public string? Headers { get; set; }

    /// <summary>Who asked. The actor's own ids and never a credential: the delivery re-enters
    /// them so what the handler writes names the person, and a row that carried a ticket would
    /// be a credential at rest with a lifetime nobody chose.</summary>
    public Guid? ActorUserId { get; set; }

    public Guid? ActorPartyId { get; set; }

    public Guid? ActorOrganizationId { get; set; }

    public DateTime EnqueuedAt { get; set; }

    /// <summary>When the drain may next attempt it — the enqueue time on a fresh row, and the
    /// backoff from the last failure afterwards.</summary>
    public DateTime NextAttemptAt { get; set; }

    public int Attempts { get; set; }

    /// <summary>Set when the row has failed <see cref="QueueOptions.MaxAttempts"/> times. The
    /// row stays, unattempted from here on: a dead letter is evidence, and deleting it would
    /// be the silence the queue exists not to keep.</summary>
    public DateTime? DeadLetteredAt { get; set; }

    public string? LastError { get; set; }
}

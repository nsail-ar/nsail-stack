// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data;

namespace NSail.Messaging.Queue;

/// <summary>The queue's slice of the EF model, contributed the way a library contributes one:
/// opt-in through <see cref="Setup.AddQueue"/>, so a product with no <c>[Queue]</c> message
/// gains no table and its migration chain does not move.</summary>
public sealed class QueueDbContextSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<QueuedMessage>(queued =>
        {
            queued.ToTable("QueuedMessages");
            queued.HasKey(entity => entity.Id);

            queued.Property(entity => entity.MessageType).HasMaxLength(256).IsRequired();
            queued.Property(entity => entity.Body).IsRequired();
            queued.Property(entity => entity.LastError).HasMaxLength(1024);

            // What one pass asks for, in the order it asks: the due rows of this tenant,
            // oldest first. The tenant column leads it like every other index in this model
            // (TenantColumn.LeadIndexesWithTenant), and the dead-lettered rows a backlog
            // accumulates are never read again.
            queued.HasIndex(entity => new { entity.DeadLetteredAt, entity.NextAttemptAt });
        });
    }
}

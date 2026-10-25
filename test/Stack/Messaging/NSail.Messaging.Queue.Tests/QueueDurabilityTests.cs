// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data.Testing;
using NSail.Problems;
using NSail.Security;

namespace NSail.Messaging.Queue.Tests;

/// <summary>nsail#1930, the second criterion: a queued send is stored inside the sending
/// operation's own transaction. An operation that rolls back queues nothing; one that commits
/// has already queued it when the caller is answered, and the handler runs afterwards — even
/// if the process that made the send never gets to it.</summary>
public sealed class QueueDurabilityTests
{
    static Session Clerk()
    {
        return new Session { IsAuthenticated = true, UserId = Guid.NewGuid(), Roles = [QueueHarness.Clerk] };
    }

    [Fact]
    public async Task A_queued_send_answers_with_the_handler_unrun_and_one_row_standing()
    {
        await using var host = await HandlerHost.Start("queue_stands", QueueHarness.Context, services => QueueHarness.Compose(services));

        await host.Send(new WriteLetter { Text = "to the bank" }, Clerk());

        Assert.Empty(await host.Read(db => db.Set<Letter>().ToListAsync()));

        var row = Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        Assert.Equal(typeof(WriteLetter).FullName, row.MessageType);
        Assert.Equal(0, row.Attempts);
        Assert.Null(row.DeadLetteredAt);
    }

    [Fact]
    public async Task The_row_drained_afterwards_runs_the_handler_once()
    {
        await using var host = await HandlerHost.Start("queue_drains", QueueHarness.Context, services => QueueHarness.Compose(services));

        await host.Send(new WriteLetter { Text = "to the bank" }, Clerk());

        Assert.Equal(1, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

        var letter = Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));

        Assert.Equal("to the bank", letter.Text);

        // Delivered and gone: a second pass has nothing left to find, so the handler cannot
        // run twice on a queue nobody touched in between.
        Assert.Equal(0, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));
        Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));
    }

    /// <summary>The first half of the third criterion. The operation sends the queued command
    /// and then refuses: the row rode the same transaction, so it goes with it and the handler
    /// never runs.</summary>
    [Fact]
    public async Task An_operation_that_rolls_back_queues_nothing()
    {
        await using var host = await HandlerHost.Start("queue_rollback", QueueHarness.Context, services => QueueHarness.Compose(services));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Send(new WriteLetterThenFail { Text = "never" }, Clerk()));

        Assert.Empty(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        Assert.Equal(0, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));
        Assert.Empty(await host.Read(db => db.Set<Letter>().ToListAsync()));
    }

    /// <summary>The second half: "the process died right after the commit" is a second host
    /// over the same database — the row is in Postgres and not in anybody's memory, so the
    /// host that never drained it is not the host that has to.</summary>
    [Fact]
    public async Task A_row_committed_by_a_process_that_died_is_delivered_by_the_next_one()
    {
        await using var sender = await HandlerHost.Start("queue_redeploy", QueueHarness.Context, services => QueueHarness.Compose(services));

        await sender.Send(new WriteLetter { Text = "survives the deploy" }, Clerk());

        // The process that made the send never drains: a second one over the same database is
        // all that is left of it.
        await using var drainer = HandlerHost.Redeploy(sender, services => QueueHarness.Compose(services));

        Assert.Equal(1, await drainer.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

        var letter = Assert.Single(await drainer.Read(db => db.Set<Letter>().ToListAsync()));

        Assert.Equal("survives the deploy", letter.Text);
    }
}

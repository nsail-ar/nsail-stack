// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data.Testing;
using NSail.Security;

namespace NSail.Messaging.Queue.Tests;

/// <summary>nsail#3303, the first and second criteria: drains over one database split the rows
/// instead of delivering one twice, and a drain that loses a row moves on.</summary>
public sealed class QueueDrainClaimTests
{
    static Session Clerk()
    {
        return new Session { IsAuthenticated = true, UserId = Guid.NewGuid(), Roles = [QueueHarness.Clerk] };
    }

    [Fact]
    public async Task Two_drains_over_the_same_rows_deliver_each_row_once()
    {
        const int rows = 20;

        await using var host = await HandlerHost.Start(
            "queue_claim", QueueHarness.Context, services => QueueHarness.Compose(services, new QueueOptions { BatchSize = rows }, new TestClock()));

        for (var index = 0; index < rows; index++)
        {
            await host.Send(new WriteLetter { Text = $"letter {index}" }, Clerk());
        }

        var passes = await Task.WhenAll(
            host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)),
            host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

        // Each drain finished only the rows it won, and none aborted: together they cover all.
        Assert.Equal(rows, passes.Sum());
        Assert.Equal(rows, (await host.Read(db => db.Set<Letter>().ToListAsync())).Count);
        Assert.Empty(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));
    }

    [Fact]
    public async Task A_drain_whose_failing_row_was_finished_by_another_moves_on_to_the_next_row()
    {
        await using var host = await HandlerHost.Start(
            "queue_claim_lost", QueueHarness.Context, services => QueueHarness.Compose(services, new QueueOptions { BatchSize = 10 }, new TestClock()));

        await host.Send(new LoseRowThenFail(), Clerk());
        await host.Send(new WriteLetter { Text = "behind the lost row" }, Clerk());

        // The other drain finishing the row, as the database sees it, while this handler runs.
        var thief = await host.Use<RowThief, RowThief>(found => Task.FromResult(found));
        thief.Steal = () => host.Read(db => db.Set<QueuedMessage>()
            .Where(row => row.MessageType == typeof(LoseRowThenFail).FullName)
            .ExecuteDeleteAsync());

        var handled = await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None));

        Assert.Equal(2, handled);
        Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));
        Assert.Empty(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));
    }
}

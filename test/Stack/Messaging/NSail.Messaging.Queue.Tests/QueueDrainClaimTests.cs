// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data.Testing;
using NSail.Security;

namespace NSail.Messaging.Queue.Tests;

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
}

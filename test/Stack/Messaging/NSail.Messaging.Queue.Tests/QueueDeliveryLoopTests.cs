// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data.Testing;
using NSail.Security;

namespace NSail.Messaging.Queue.Tests;

/// <summary>nsail#1930, the fourth criterion: a handler that keeps failing is retried with
/// backoff and then dead-lettered, counted on a meter and never dropped in silence. The
/// figures are the install's — every number asserted here is read off <see
/// cref="QueueOptions"/> and none of them is a constant in the loop.</summary>
public sealed class QueueDeliveryLoopTests
{
    static Session Clerk()
    {
        return new Session { IsAuthenticated = true, UserId = Guid.NewGuid(), Roles = [QueueHarness.Clerk] };
    }

    static QueueOptions Options()
    {
        return new QueueOptions { MaxAttempts = 3, Backoff = TimeSpan.FromMinutes(1) };
    }

    [Fact]
    public async Task A_handler_that_always_throws_is_attempted_the_configured_number_of_times_and_then_dead_lettered()
    {
        var options = Options();
        var clock = new TestClock();

        using var meters = new QueueReadings();

        await using var host = await HandlerHost.Start(
            "queue_deadletter", QueueHarness.Context, services => QueueHarness.Compose(services, options, clock));

        await host.Send(new AlwaysFails(), Clerk());

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            Assert.Equal(1, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

            var row = Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

            Assert.Equal(attempt, row.Attempts);
            Assert.Contains("The vendor is down.", row.LastError);

            if (attempt < options.MaxAttempts)
            {
                Assert.Null(row.DeadLetteredAt);

                // The backoff doubles, so the next attempt is only due after it: a pass made
                // before that moment finds nothing.
                Assert.Equal(
                    clock.GetUtcNow().UtcDateTime + options.Backoff * Math.Pow(2, attempt - 1),
                    row.NextAttemptAt,
                    TimeSpan.FromSeconds(1));

                Assert.Equal(0, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

                clock.Advance(options.Backoff * Math.Pow(2, attempt - 1) + TimeSpan.FromSeconds(1));
            }
            else
            {
                Assert.NotNull(row.DeadLetteredAt);
            }
        }

        // And it is never attempted again, whatever the clock says.
        clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(0, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));
        Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        // Counted, and by outcome: a dead letter nothing counts is the silence the criterion
        // forbids, and a retry that will come back is a different alert from work the install
        // has stopped doing.
        Assert.Equal(options.MaxAttempts - 1, meters.Finished(typeof(AlwaysFails).FullName!, QueueMetrics.Failed));
        Assert.Equal(1, meters.Finished(typeof(AlwaysFails).FullName!, QueueMetrics.DeadLettered));
        Assert.Equal(0, meters.Finished(typeof(AlwaysFails).FullName!, QueueMetrics.Succeeded));
    }

    /// <summary><c>Queue:Backoff</c> is the install's, so no fixed count of doublings is "the
    /// one that would overflow" — a wait of millennia reaches the end of the calendar on its
    /// first retry. It saturates at the furthest date there is instead of throwing out of the
    /// pass, which would leave the row with its attempt unrecorded and so due again on every
    /// pass forever.</summary>
    [Fact]
    public async Task A_backoff_longer_than_the_calendar_saturates_instead_of_overflowing()
    {
        var options = new QueueOptions { MaxAttempts = 3, Backoff = TimeSpan.FromDays(3_000_000) };
        var clock = new TestClock();

        await using var host = await HandlerHost.Start(
            "queue_backoff_ceiling", QueueHarness.Context, services => QueueHarness.Compose(services, options, clock));

        await host.Send(new AlwaysFails(), Clerk());

        Assert.Equal(1, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

        var row = Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        Assert.Equal(1, row.Attempts);
        Assert.Null(row.DeadLetteredAt);
        Assert.True(
            row.NextAttemptAt > clock.GetUtcNow().UtcDateTime.AddYears(5000),
            $"the row came due again at {row.NextAttemptAt:O}.");
    }

    [Fact]
    public async Task A_delivered_row_is_counted_as_succeeded()
    {
        using var meters = new QueueReadings();

        await using var host = await HandlerHost.Start(
            "queue_counted", QueueHarness.Context, services => QueueHarness.Compose(services));

        await host.Send(new WriteLetter { Text = "counted" }, Clerk());

        await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None));

        Assert.Equal(1, meters.Finished(typeof(WriteLetter).FullName!, QueueMetrics.Succeeded));
    }
}

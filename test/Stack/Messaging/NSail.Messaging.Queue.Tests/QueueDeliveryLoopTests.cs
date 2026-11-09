// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data.Testing;
using NSail.Security;

namespace NSail.Messaging.Queue.Tests;

/// <summary>The delivery loop: a handler that keeps failing is retried with backoff and then
/// dead-lettered, counted on a meter and never dropped in silence; a row of an unknown type
/// waits; dead letters are requeued or purged. The
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
    public async Task A_business_failure_dead_letters_with_the_issue_text()
    {
        var options = Options();
        var clock = new TestClock();

        await using var host = await HandlerHost.Start(
            "queue_rule", QueueHarness.Context, services => QueueHarness.Compose(services, options, clock));

        await host.Send(new FailsWithRule(), Clerk());

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None));
            clock.Advance(options.Backoff * Math.Pow(2, attempt));
        }

        var row = Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        Assert.NotNull(row.DeadLetteredAt);
        Assert.Contains("Letter 42 does not exist.", row.LastError);
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

    static QueuedMessage Row(string type, DateTime now, DateTime? deadLettered = null)
    {
        return new QueuedMessage
        {
            Id = Guid.NewGuid(),
            MessageType = type,
            Body = "{}",
            EnqueuedAt = now,
            NextAttemptAt = now,
            Attempts = deadLettered is null ? 0 : Options().MaxAttempts,
            DeadLetteredAt = deadLettered,
        };
    }

    [Fact]
    public async Task A_row_of_a_type_this_process_does_not_know_stays_pending_and_backed_off()
    {
        var options = Options();
        var clock = new TestClock();

        await using var host = await HandlerHost.Start(
            "queue_unknown_type", QueueHarness.Context, services => QueueHarness.Compose(services, options, clock));

        var now = clock.GetUtcNow().UtcDateTime;

        await host.Seed(db => db.Add(Row("Some.Other.Version.Message", now)));

        for (var pass = 1; pass <= options.MaxAttempts + 2; pass++)
        {
            Assert.Equal(1, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

            var row = Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

            Assert.Null(row.DeadLetteredAt);
            Assert.True(row.NextAttemptAt > clock.GetUtcNow().UtcDateTime);
            Assert.Contains("No consumer", row.LastError);

            Assert.Equal(0, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

            clock.Advance(options.Backoff * Math.Pow(2, options.MaxAttempts) + TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task A_deferred_row_that_later_finds_its_consumer_keeps_the_full_attempt_ladder()
    {
        var options = Options();
        var clock = new TestClock();

        using var meters = new QueueReadings();

        await using var host = await HandlerHost.Start(
            "queue_deferred_then_known", QueueHarness.Context, services => QueueHarness.Compose(services, options, clock));

        var row = Row("Some.Other.Version.Message", clock.GetUtcNow().UtcDateTime);

        await host.Seed(db => db.Add(row));

        for (var pass = 0; pass < options.MaxAttempts + 1; pass++)
        {
            await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None));

            clock.Advance(options.Backoff * Math.Pow(2, options.MaxAttempts) + TimeSpan.FromSeconds(1));
        }

        Assert.Equal(0, meters.Finished("Some.Other.Version.Message", QueueMetrics.Failed));

        await host.Seed(db =>
        {
            db.Set<QueuedMessage>().Single(r => r.Id == row.Id).MessageType = typeof(AlwaysFails).FullName!;
        });

        await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None));

        var after = Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        Assert.Null(after.DeadLetteredAt);
        Assert.Equal(1, after.Attempts);
    }

    [Fact]
    public async Task Dead_letters_can_be_requeued_or_purged()
    {
        var clock = new TestClock();

        await using var host = await HandlerHost.Start(
            "queue_dead_letters", QueueHarness.Context, services => QueueHarness.Compose(services, Options(), clock));

        var now = clock.GetUtcNow().UtcDateTime;
        var requeued = Row("A", now, now);
        var purged = Row("B", now, now);

        await host.Seed(db => db.AddRange(requeued, purged));

        Assert.Equal(1, await host.Use<QueueDeadLetters, int>(letters => letters.Requeue([requeued.Id], CancellationToken.None)));
        Assert.Equal(1, await host.Use<QueueDeadLetters, int>(letters => letters.Purge(null, CancellationToken.None)));

        var rows = await host.Read(db => db.Set<QueuedMessage>().ToListAsync());
        var row = Assert.Single(rows);

        Assert.Equal(requeued.Id, row.Id);
        Assert.Null(row.DeadLetteredAt);
        Assert.Equal(0, row.Attempts);
    }
}

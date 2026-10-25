// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSail.Security;
using NSail.Serialization;

namespace NSail.Messaging.Queue;

/// <summary>One pass over this tenant's due rows. The claim, the backoff and the dead letter
/// are the Stack's own — no vendor retry sits on top of them, which would retry inside a retry
/// and make the configured attempt count a lie.
///
/// <para>Delivery is at least once, and that is the honest reading of the fourth criterion: a
/// process that dies between a handler's commit and this pass deleting its row delivers that
/// row again on the next pass. The alternative — delete first — loses the work instead, which
/// is the one thing a durable queue may not do.</para>
///
/// <para>Single instance is an assumption and not a property, the same one
/// <see cref="Background.IBackgroundJob"/>'s runner already rests on: one container per tenant
/// means nothing drains twice today, and a second replica would deliver every row twice with
/// no error anywhere to say so. A Postgres advisory lock taken around the pass is the cheap fix
/// the day that topology changes, and it is one fix for both loops.</para></summary>
public sealed class QueueDrain
{
    readonly DbContext _db;
    readonly IServiceProvider _services;
    readonly SessionProvider _sessions;
    readonly QueueConsumers _consumers;
    readonly QueueOptions _options;
    readonly QueueMeter _meter;
    readonly TimeProvider _clock;
    readonly ILogger<QueueDrain> _logger;

    public QueueDrain(
        DbContext db,
        IServiceProvider services,
        SessionProvider sessions,
        QueueConsumers consumers,
        QueueOptions options,
        QueueMeter meter,
        TimeProvider clock,
        ILogger<QueueDrain> logger)
    {
        _db = db;
        _services = services;
        _sessions = sessions;
        _consumers = consumers;
        _options = options;
        _meter = meter;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Delivers every row that is due, up to <see cref="QueueOptions.BatchSize"/>, and
    /// answers how many it finished with. The tenant is whoever the caller entered: the query
    /// filter is what makes this tenant's queue its own.</summary>
    public async Task<int> Run(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var due = await _db.Set<QueuedMessage>()
            .Where(row => row.DeadLetteredAt == null && row.NextAttemptAt <= now)
            .OrderBy(row => row.NextAttemptAt)
            .ThenBy(row => row.EnqueuedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var handled = 0;

        foreach (var row in due)
        {
            // Awaited one at a time, like the job runner's own sweep: the actor and the tenant
            // travel on the async flow, and two deliveries in flight on one flow would read
            // each other's.
            await Deliver(row, cancellationToken).ConfigureAwait(false);

            handled++;
        }

        return handled;
    }

    async Task Deliver(QueuedMessage row, CancellationToken cancellationToken)
    {
        if (_consumers.For(row.MessageType) is not { } consumer)
        {
            // A row whose message this composition does not know is not a failure to retry:
            // nothing here will ever grow a consumer for it, and attempting it on every pass
            // would bury the rows that can still be delivered.
            await Settle(row, new InvalidOperationException(
                $"No consumer is registered for '{row.MessageType}'. The host that drains a queued command must compose the kit that declares it."),
                attempts: _options.MaxAttempts, cancellationToken).ConfigureAwait(false);

            return;
        }

        try
        {
            // Re-entered per row and not once per pass: the next row is somebody else's.
            // Written onto the provider the job's scope holds, which is what carries it into
            // the scope the delivery itself opens (SessionProvider, the flowing session).
            _sessions.Session = Actor(row);

            await consumer.Deliver(_services, row.Body, Headers(row), cancellationToken).ConfigureAwait(false);

            _db.Remove(row);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _meter.Finished(row.MessageType, QueueMetrics.Succeeded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Settle(row, exception, row.Attempts + 1, cancellationToken).ConfigureAwait(false);
        }
    }

    async Task Settle(QueuedMessage row, Exception exception, int attempts, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        row.Attempts = attempts;
        row.LastError = Truncate(exception.Message);

        if (attempts >= _options.MaxAttempts)
        {
            row.DeadLetteredAt = now;

            _meter.Finished(row.MessageType, QueueMetrics.DeadLettered);

            _logger.LogError(exception, "A queued command was dead-lettered after {Attempts} attempts; the row stays for whoever reads the queue.", attempts);
        }
        else
        {
            row.NextAttemptAt = Due(now, attempts);

            _meter.Finished(row.MessageType, QueueMetrics.Failed);

            _logger.LogWarning(exception, "A queued command failed on attempt {Attempts}; it is due again after its backoff.", attempts);
        }

        // The change tracker may be holding whatever the failed delivery left on it — the
        // delivery ran on a scope and a context of its own, but a handler that threw before
        // its own save can still have touched this one through a shared service. The row's own
        // state is the only thing this save is about, and a delivery that threw on the very
        // save that deletes the row left it Deleted here: EF ignores what is written to a
        // deleted entity, so without this the attempt and the error this pass just decided
        // would be dropped and the same delete re-emitted.
        if (_db.Entry(row).State == EntityState.Deleted)
        {
            _db.Entry(row).State = EntityState.Modified;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    DateTime Due(DateTime now, int attempts)
    {
        // Doubling, saturated at the furthest date there is rather than at a fixed number of
        // shifts: Queue:Backoff is the install's, so no constant count of doublings is "the one
        // that overflows" — a wait of months reaches it in three. Both the doubling and the sum
        // are kept under the ceiling instead of computed and then checked, because the
        // arithmetic that overflowed is what would have to be inspected.
        var ceiling = (DateTime.MaxValue - now).Ticks;
        var ticks = _options.Backoff.Ticks;

        for (var doubling = 0; doubling < attempts - 1 && ticks <= ceiling / 2; doubling++)
        {
            ticks *= 2;
        }

        return now + TimeSpan.FromTicks(Math.Min(ticks, ceiling));
    }

    Session Actor(QueuedMessage row)
    {
        // The actor's own ids, re-entered. Not Session.System(): a queued act is somebody's,
        // and the stamps the handler writes have to name them (Cap, the envelope carries the
        // id and never a credential). Not authenticated-with-roles either — the gate is exempt
        // for a delivery (AmbientQueue), so there is nothing for a role to answer here, and a
        // role read off a row would be a grant frozen at the send.
        return new Session
        {
            IsAuthenticated = row.ActorUserId is not null,
            UserId = row.ActorUserId,
            PartyId = row.ActorPartyId,
            OrganizationId = row.ActorOrganizationId,
        };
    }

    static IReadOnlyDictionary<string, string>? Headers(QueuedMessage row)
    {
        if (row.Headers is not { Length: > 0 } headers)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(headers, JsonOptions.Wire);
    }

    static string Truncate(string message)
    {
        const int limit = 1024;

        return message.Length <= limit ? message : message[..limit];
    }
}

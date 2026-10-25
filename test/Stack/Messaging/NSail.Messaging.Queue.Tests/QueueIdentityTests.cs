// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;
using NSail.Data.Testing;
using NSail.Problems;
using NSail.Security;

namespace NSail.Messaging.Queue.Tests;

/// <summary>nsail#1930, the first and second criteria: who a queued handler runs as, and what
/// the gate does about it. The actor's own id travels in the envelope and is re-entered, so
/// what the handler writes names the person who asked; the delivery itself is ungated, as a
/// reaction is, while the same message sent directly by an ungranted caller is still
/// refused.</summary>
public sealed class QueueIdentityTests
{
    static Session Clerk(Guid user)
    {
        return new Session { IsAuthenticated = true, UserId = user, Roles = [QueueHarness.Clerk] };
    }

    [Fact]
    public async Task The_handler_runs_as_the_actor_the_send_carried()
    {
        var clerk = Guid.NewGuid();

        await using var host = await HandlerHost.Start("queue_actor", QueueHarness.Context, services => QueueHarness.Compose(services));

        await host.Send(new WriteLetter { Text = "signed" }, Clerk(clerk));

        // The envelope carries the id and never a credential: no ticket, no roles, no
        // memberships are stored, and the id is what the handler stamps.
        var row = Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        Assert.Equal(clerk, row.ActorUserId);

        await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None));

        var letter = Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));

        Assert.Equal(clerk, letter.AuthorId);
    }

    [Fact]
    public async Task The_handler_reads_the_headers_the_send_carried()
    {
        await using var host = await HandlerHost.Start("queue_headers", QueueHarness.Context, services => QueueHarness.Compose(services));

        await host.Use<Runtime.Mediator, bool>(
            async mediator =>
            {
                await mediator.Send(new WriteLetter { Text = "correlated" }, new Dictionary<string, string> { ["correlation"] = "abc-123" });

                return true;
            },
            Clerk(Guid.NewGuid()));

        await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None));

        var letter = Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));

        Assert.Equal("abc-123", letter.Correlation);
    }

    /// <summary>The grants are answered once, at the send. A delivery asking again would
    /// refuse — hours later, with nobody left to tell — a command the caller was already told
    /// was accepted.</summary>
    [Fact]
    public async Task The_delivery_is_not_gated_a_second_time_while_the_same_send_by_an_ungranted_caller_is_refused()
    {
        await using var host = await HandlerHost.Start(
            "queue_gate",
            QueueHarness.Context,
            services => QueueHarness.Compose(services, null, null, QueueHarness.WriteLetterKey));

        await host.Send(new WriteLetter { Text = "granted once" }, Clerk(Guid.NewGuid()));

        // The actor re-entered at delivery holds no role at all, so a gate that asked again
        // would answer Forbidden here.
        Assert.Equal(1, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));
        Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => host.Send(new AlwaysFails(), Clerk(Guid.NewGuid())));

        Assert.Equal("Forbidden", refusal.Code);
        Assert.Empty(await host.Read(db => db.Set<QueuedMessage>().Where(row => row.MessageType == typeof(AlwaysFails).FullName).ToListAsync()));
    }

    /// <summary>The third thing a delivery owes its send, beside the actor and the headers: the
    /// install's default language, which a queued handler gets for the same reason every
    /// background job does — the drain IS one, and the runner sets the culture per loop.
    /// Without it a delivery runs invariant, <c>LanguageProvider</c> seeds itself with "iv",
    /// and a handler that reaches for a template finds one in a language nobody has
    /// (nsail#1108). What is deliberately NOT in the envelope is the sender's own culture: an
    /// operation answered in English queues work the install does in its own language.</summary>
    [Fact]
    public async Task The_handler_runs_in_the_installs_default_language()
    {
        await using var host = await HandlerHost.Start(
            "queue_language",
            QueueHarness.Context,
            services =>
            {
                // The one place the install's language lives, read by the runner the way the
                // request edge reads it (BackgroundJobLanguageTests is the twin).
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Language:Default"] = "es" })
                    .Build());

                QueueHarness.Compose(services);
            });

        await host.Send(new WriteLetter { Text = "en el idioma del install" }, Clerk(Guid.NewGuid()));

        await QueueHarness.Sweep(host, () => host.Read(db => db.Set<Letter>().AnyAsync()));

        var letter = Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));

        Assert.Equal("es", letter.Language);
    }

    /// <summary>Tenant-scoped like any kit entity, and not <c>IInstallScoped</c>: the column,
    /// the filter and the tenant-leading index come from the model pass, so under
    /// <c>SingleDb</c> the row is the cell's with the tenant in its column and under
    /// <c>MultiDb</c> it is in the tenant's own database — each tenant's queue its own either
    /// way, and the per-tenant sweep reads it through the very filter
    /// <c>IInstallScoped</c> would have exempted it from.</summary>
    [Fact]
    public async Task Another_tenants_row_is_not_this_tenants_to_read_or_to_drain()
    {
        await using var host = await HandlerHost.Start("queue_tenanted", QueueHarness.Context, services => QueueHarness.Compose(services));

        await host.Send(new WriteLetter { Text = "ours" }, Clerk(Guid.NewGuid()));

        // The same queue, one row of it somebody else's. Written through the column directly
        // because entering a second tenant is what a fixture resolving none cannot do — and
        // the column is exactly what the filter the model pass installed compares.
        await host.Seed(db =>
        {
            var theirs = new QueuedMessage
            {
                Id = Guid.NewGuid(),
                MessageType = typeof(WriteLetter).FullName!,
                Body = """{"text":"theirs"}""",
                EnqueuedAt = DateTime.UtcNow,
                NextAttemptAt = DateTime.UtcNow,
            };

            db.Add(theirs);
            db.Entry(theirs).Property("TenantId").CurrentValue = Guid.NewGuid();
        });

        Assert.Single(await host.Read(db => db.Set<QueuedMessage>().ToListAsync()));

        Assert.Equal(1, await host.Use<QueueDrain, int>(drain => drain.Run(CancellationToken.None)));

        var letter = Assert.Single(await host.Read(db => db.Set<Letter>().ToListAsync()));

        Assert.Equal("ours", letter.Text);

        // And the sweep that reads it is one pass per tenant, which is what turns that filter
        // into a queue each tenant owns.
        Assert.Equal(TenancyScope.Tenant, await host.Use<QueueDrainJob, TenancyScope>(job => Task.FromResult(job.Tenancy)));
    }
}

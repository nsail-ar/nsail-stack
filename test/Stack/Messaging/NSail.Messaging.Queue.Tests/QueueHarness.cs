// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Sending;
using NSail.Security;

namespace NSail.Messaging.Queue.Tests;

// A chain of exactly one migration, hand-authored for the same reason the tenancy suite's is:
// what a durable queue has to survive is a real Postgres, and an app's own chain would drag a
// product into a Stack suite. The model goes through ApplySetups like a product's, so the
// tenant column the outbox row carries is the one a product gets.
public sealed class QueueDbContext : DbContext, ITenanted, IHasOrgScope
{
    readonly TenancyProvider _tenancy;

    public QueueDbContext(DbContextOptions<QueueDbContext> options, TenancyProvider? tenancy = null)
        : base(options)
    {
        _tenancy = tenancy ?? TenancyProvider.Install;
    }

    public Guid? TenantId
    {
        get { return _tenancy.RowKey; }
    }

    public OrgScope OrgScope
    {
        get { return OrgScope.Everywhere; }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, [new QueueDbContextSetup(), new LetterSetup()]);
    }
}

// What a queued handler writes, so the suite asserts a row rather than a flag: who wrote it
// and in which language are the two things the delivery owes its caller.
public sealed class Letter
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public Guid? AuthorId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Correlation { get; set; } = string.Empty;
}

sealed class LetterSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Letter>(letter =>
        {
            letter.ToTable("Letters");
            letter.HasKey(entity => entity.Id);
            letter.Property(entity => entity.Text).IsRequired();
            letter.Property(entity => entity.Language).IsRequired();
            letter.Property(entity => entity.Correlation).IsRequired();
        });
    }
}

// Spelled with its suffix, which a kit never has to: this suite's own namespace nests under
// NSail.Messaging.Queue, so the bare [Queue] binds to that namespace instead of the attribute.
[QueueAttribute]
public sealed class WriteLetter : IMessage
{
    public string Text { get; set; } = string.Empty;
}

// Queued and refused on purpose: the operation that sends it throws after the send, so the
// row has to go with it.
// Spelled with its suffix, which a kit never has to: this suite's own namespace nests under
// NSail.Messaging.Queue, so the bare [Queue] binds to that namespace instead of the attribute.
[QueueAttribute]
public sealed class WriteLetterThenFail : IMessage
{
    public string Text { get; set; } = string.Empty;
}

[QueueAttribute]
public sealed class AlwaysFails : IMessage
{
}

sealed class WriteLetterHandler : IHandler<WriteLetter>
{
    readonly DbContext _db;
    readonly SessionProvider _sessions;
    readonly MessageContextAccessor _context;

    public WriteLetterHandler(DbContext db, SessionProvider sessions, MessageContextAccessor context)
    {
        _db = db;
        _sessions = sessions;
        _context = context;
    }

    public async Task Handle(WriteLetter message, CancellationToken cancellationToken = default)
    {
        _db.Add(new Letter
        {
            Id = Guid.NewGuid(),
            Text = message.Text,
            AuthorId = _sessions.Session.UserId,
            Language = CultureInfo.CurrentUICulture.Name,
            Correlation = _context.Context?.GetHeader("correlation") ?? string.Empty,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}

sealed class AlwaysFailsHandler : IHandler<AlwaysFails>
{
    public Task Handle(AlwaysFails message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("The vendor is down.");
    }
}

// The operation a caller drives: it queues the command and then refuses, so the whole unit —
// the outbox row included — is undone.
sealed class WriteLetterThenFailHandler : IHandler<WriteLetterThenFail>
{
    readonly Runtime.Mediator _mediator;

    public WriteLetterThenFailHandler(Runtime.Mediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Handle(WriteLetterThenFail message, CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new WriteLetter { Text = message.Text }, cancellationToken);

        throw new InvalidOperationException("The rest of the operation refuses.");
    }
}

// The generator's output for a message with no constrainable field: the key and the audience
// are the whole question, so a policy that applies to it applies.
sealed class FlatPolicyHandler : PolicyHandler
{
    public FlatPolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}

/// <summary>A clock the suite moves by hand: the backoff and the dead letter are judged
/// against wall time, and a test that waited for a real minute would be the slowest in the
/// Stack.</summary>
public sealed class TestClock : TimeProvider
{
    DateTimeOffset _now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan span)
    {
        _now += span;
    }
}

public static class QueueHarness
{
    public static DbContext Context(string connectionString)
    {
        return new QueueDbContext(
            new DbContextOptionsBuilder<QueueDbContext>().UseNpgsql(connectionString).Options);
    }

    public const string WriteLetterKey = "Messaging.Queue.WriteLetter";

    public const string AlwaysFailsKey = "Messaging.Queue.AlwaysFails";

    public const string WriteLetterThenFailKey = "Messaging.Queue.WriteLetterThenFail";

    public const string Clerk = "clerk";

    /// <summary>What a product's two generated holders would register, by hand: the suite is
    /// about the runtime half, and the walk that writes these lines is proven in
    /// <c>NSail.SourceGenerator.Tests</c>. <paramref name="granted"/> is what the caller's
    /// policy names, so a test can send one message and be refused another.</summary>
    public static void Compose(IServiceCollection services, QueueOptions? options = null, TimeProvider? clock = null, params string[] granted)
    {
        if (options is not null)
        {
            services.AddSingleton(options);
        }

        if (clock is not null)
        {
            services.AddSingleton(clock);
        }

        services.AddQueueDelivery();

        // Both registrations stand in for the Policies.Handlers generator, which emits the
        // factory and the registry entry from the same line.
        foreach (var type in new[] { typeof(WriteLetter), typeof(AlwaysFails), typeof(WriteLetterThenFail) })
        {
            services.AddSingleton(new PolicyHandlerFactory(type, policy => new FlatPolicyHandler(policy)));
            services.AddSingleton(new MessageRegistration(type));
        }

        services.AddBuiltInPolicy(new Policy
        {
            Name = "Clerks queue their own work",
            Messages = granted.Length > 0 ? granted : [WriteLetterKey, AlwaysFailsKey, WriteLetterThenFailKey],
            Audience = new PolicyAudience { HasRole = Clerk },
        });

        services.AddScoped<ISender<WriteLetter>, QueueSender<WriteLetter>>();
        services.AddScoped<ISender<AlwaysFails>, QueueSender<AlwaysFails>>();
        services.AddScoped<ISender<WriteLetterThenFail>, InProcessSender<WriteLetterThenFail>>();

        services.AddSingleton<QueueConsumer>(new QueueConsumer<WriteLetter>());
        services.AddSingleton<QueueConsumer>(new QueueConsumer<AlwaysFails>());

        services.AddScoped<IHandler<WriteLetter>, WriteLetterHandler>();
        services.AddScoped<IHandler<AlwaysFails>, AlwaysFailsHandler>();
        services.AddScoped<IHandler<WriteLetterThenFail>, WriteLetterThenFailHandler>();
    }
}

[DbContext(typeof(QueueDbContext))]
[Migration("20261025000000_Queue")]
public sealed class QueueMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "QueuedMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: Guid.Empty),
                MessageType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Body = table.Column<string>(type: "text", nullable: false),
                Headers = table.Column<string>(type: "text", nullable: true),
                ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                EnqueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                DeadLetteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_QueuedMessages", row => row.Id));

        migrationBuilder.CreateIndex(
            name: "IX_QueuedMessages_TenantId_DeadLetteredAt_NextAttemptAt",
            table: "QueuedMessages",
            columns: ["TenantId", "DeadLetteredAt", "NextAttemptAt"]);

        migrationBuilder.CreateTable(
            name: "Letters",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: Guid.Empty),
                Text = table.Column<string>(type: "text", nullable: false),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                Language = table.Column<string>(type: "text", nullable: false),
                Correlation = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Letters", letter => letter.Id));

        // The Stack's own two tables, which a product gets from its scaffolded chain and a
        // hand-authored one has to state: the tenant's seed history and what the install
        // records about a tenant.
        migrationBuilder.CreateTable(
            name: "AppliedSeedSteps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: Guid.Empty),
                Step = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_AppliedSeedSteps", step => step.Id));

        migrationBuilder.CreateTable(
            name: "TenantStates",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Plan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                SuspendedFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SuspendedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_TenantStates", state => state.TenantId));

        migrationBuilder.AddTenantPolicies("QueuedMessages", "Letters", "AppliedSeedSteps");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable("QueuedMessages");
        migrationBuilder.DropTable("Letters");
        migrationBuilder.DropTable("AppliedSeedSteps");
        migrationBuilder.DropTable("TenantStates");
    }
}

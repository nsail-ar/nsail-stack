// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

// A chain of exactly one migration, hand-authored: what first touch has to apply is a real
// EF chain reaching a real Postgres, and an app's own chain would drag a product into a Stack
// suite. The migrations assembly is derived from this context's assembly, which is this one.
// It goes through ApplySetups like a product's own, so the tenant column and its filter are
// the ones a product gets rather than a second implementation written for the suite.
public sealed class TenantDbContext : DbContext, ITenanted, IHasOrgScope
{
    readonly TenancyProvider _tenancy;

    public TenantDbContext(DbContextOptions<TenantDbContext> options, TenancyProvider? tenancy = null)
        : base(options)
    {
        _tenancy = tenancy ?? TenancyProvider.Install;
    }

    public Guid? TenantId
    {
        get { return _tenancy.RowKey; }
    }

    // No branch scope in a Stack fixture: what this context is for is the tenant axis, and the
    // org one answers what a host that resolves no organization answers.
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
        modelBuilder.ApplySetups(this, [new NoteSetup()]);
    }
}

sealed class NoteSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(note =>
        {
            note.ToTable("Notes");
            note.HasKey(entity => entity.Id);
            note.Property(entity => entity.Text).IsRequired();
        });

        modelBuilder.Entity<NoteKind>(kind =>
        {
            kind.ToTable("NoteKinds");
            kind.HasKey(entity => entity.Id);
            kind.Property(entity => entity.Name).IsRequired();
        });
    }
}

public sealed class Note
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;
}

// An app's own tenant seed in miniature: what Optical plants is an organization, an
// administrator and a chart of accounts, and what matters to the seam is the same in one row —
// a chain of named steps, each asked once per tenant, each answering for the tenant it was
// handed and deciding for itself whether there is anything to do.
public sealed class WelcomeSeed : ITenantSeed
{
    public IReadOnlyList<TenantSeedStep> Steps { get; } = [new("Welcome", Welcome)];

    public static async Task Welcome(DbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);

        var text = $"welcome {tenant.Slug}";

        if (await db.Set<Note>().AnyAsync(note => note.Text == text, cancellationToken))
        {
            return;
        }

        db.Add(new Note { Id = Guid.NewGuid(), Text = text });

        await db.SaveChangesAsync(cancellationToken);
    }
}

// The build after the one above, and the only shape a deploy has in a test: the same chain with
// a step appended. Its first step is WelcomeSeed's own, so a tenant that already ran it does not
// run it again and a tenant that never existed still gets it.
public sealed class ReminderSeed : ITenantSeed
{
    public IReadOnlyList<TenantSeedStep> Steps { get; } =
        [new("Welcome", WelcomeSeed.Welcome), new("Reminder", Reminder)];

    static async Task Reminder(DbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);

        db.Add(new Note { Id = Guid.NewGuid(), Text = $"reminder {tenant.Slug}" });

        await db.SaveChangesAsync(cancellationToken);
    }
}

// A step that fails the first time it is asked: what a step whose write hits a constraint, or
// whose connection drops, does to the chain behind it.
public sealed class FlakySeed : ITenantSeed
{
    public static int Attempts;

    public IReadOnlyList<TenantSeedStep> Steps { get; } =
        [new("Welcome", WelcomeSeed.Welcome), new("Flaky", Flaky)];

    static async Task Flaky(DbContext db, Tenant tenant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);

        db.Add(new Note { Id = Guid.NewGuid(), Text = $"flaky {tenant.Slug}" });

        await db.SaveChangesAsync(cancellationToken);

        if (Interlocked.Increment(ref Attempts) == 1)
        {
            throw new InvalidOperationException("The step's first attempt fails.");
        }
    }
}

// The other class of row: reference data the install shares, identical for every tenant and
// meaningless to copy. The marker is the whole of what says so, and what it buys is visibility —
// a tenant reads these rows without owning them, and so does work that resolved no tenant.
public sealed class NoteKind : IInstallScoped
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

[DbContext(typeof(TenantDbContext))]
[Migration("20260824000000_Notes")]
public sealed class Notes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "Notes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: Guid.Empty),
                Text = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_Notes", note => note.Id));

        // The Stack's own table, which a product gets from its scaffolded chain and this
        // hand-authored one has to state: the tenant's seed history (AppliedSeedStep).
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

        migrationBuilder.CreateIndex(
            name: "IX_AppliedSeedSteps_TenantId_Step",
            table: "AppliedSeedSteps",
            columns: ["TenantId", "Step"],
            unique: true);

        // No tenant column, deliberately: an install-scoped table has none, and the rows below
        // are the seed's — planted once, read by every tenant.
        migrationBuilder.CreateTable(
            name: "NoteKinds",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_NoteKinds", kind => kind.Id));

        // The column types are stated because this chain is hand-authored and carries no target
        // model for the generator to read them off — a product's scaffolded migration has one.
        migrationBuilder.InsertData(
            table: "NoteKinds",
            columns: ["Id", "Name"],
            columnTypes: ["uuid", "text"],
            values: [new Guid("d0000000-0000-0000-0000-000000000001"), "reminder"]);

        migrationBuilder.InsertData(
            table: "NoteKinds",
            columns: ["Id", "Name"],
            columnTypes: ["uuid", "text"],
            values: [new Guid("d0000000-0000-0000-0000-000000000002"), "warning"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable("Notes");
        migrationBuilder.DropTable("NoteKinds");
        migrationBuilder.DropTable("AppliedSeedSteps");
    }
}

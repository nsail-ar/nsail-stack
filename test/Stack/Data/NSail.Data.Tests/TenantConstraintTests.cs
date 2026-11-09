// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace NSail.Data.Tests;

/// <summary>What the schema itself refuses, on a real Postgres with two tenants: a referential
/// check bypasses the row policy, so only a constraint that carries the tenant keeps one tenant
/// from naming another's row, and only a per-tenant unique keeps a violation from revealing it.</summary>
public sealed class TenantConstraintTests : IAsyncLifetime
{
    static readonly Guid Lumina = Guid.Parse("11111111-0000-0000-0000-000000000001");
    static readonly Guid Vision = Guid.Parse("22222222-0000-0000-0000-000000000002");

    readonly string _database = "nsail3405_" + Guid.NewGuid().ToString("N")[..12];

    DbContextOptions<ConstraintDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await Execute("postgres", $"CREATE DATABASE \"{_database}\"");

        _options = new DbContextOptionsBuilder<ConstraintDbContext>()
            .UseNpgsql(ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_database}"))
            .Options;

        await using var db = new ConstraintDbContext(_options, Lumina);

        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await Execute("postgres", $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
    }

    [Fact]
    public async Task A_row_naming_another_tenants_row_by_foreign_key_is_refused()
    {
        var parent = Guid.NewGuid();

        await using (var owner = new ConstraintDbContext(_options, Lumina))
        {
            owner.Add(new CParent { Id = parent, Code = "A" });

            await owner.SaveChangesAsync();
        }

        await using var other = new ConstraintDbContext(_options, Vision);

        other.Add(new CChild { Id = Guid.NewGuid(), ParentId = parent });

        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)refusal.InnerException!).SqlState);
    }

    [Fact]
    public async Task An_optional_foreign_key_names_nothing_or_its_own_tenants_row()
    {
        var parent = Guid.NewGuid();

        await using var own = new ConstraintDbContext(_options, Lumina);

        own.Add(new CParent { Id = parent, Code = "A" });
        own.Add(new CChild { Id = Guid.NewGuid(), ParentId = parent });
        own.Add(new CChild { Id = Guid.NewGuid(), ParentId = null });

        await own.SaveChangesAsync();

        Assert.Equal(2, await own.Set<CChild>().CountAsync());
    }

    [Fact]
    public async Task Deleting_a_principal_clears_the_optional_key_of_a_loaded_dependent()
    {
        var parent = Guid.NewGuid();
        var loose = Guid.NewGuid();

        await using (var own = new ConstraintDbContext(_options, Lumina))
        {
            own.Add(new CParent { Id = parent, Code = "A" });
            own.Add(new CLoose { Id = loose, ParentId = parent });

            await own.SaveChangesAsync();
        }

        await using (var db = new ConstraintDbContext(_options, Lumina))
        {
            var row = await db.Set<CLoose>().SingleAsync();
            var principal = await db.Set<CParent>().SingleAsync();

            db.Remove(principal);

            await db.SaveChangesAsync();
        }

        await using var check = new ConstraintDbContext(_options, Lumina);

        Assert.Null((await check.Set<CLoose>().SingleAsync()).ParentId);
    }

    [Fact]
    public async Task A_foreign_key_declared_on_a_derived_type_names_the_tenant_too()
    {
        var parent = Guid.NewGuid();

        await using (var owner = new ConstraintDbContext(_options, Lumina))
        {
            owner.Add(new CParent { Id = parent, Code = "A" });

            await owner.SaveChangesAsync();
        }

        await using var other = new ConstraintDbContext(_options, Vision);

        other.Add(new CDerived { Id = Guid.NewGuid(), OwnerId = parent });

        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)refusal.InnerException!).SqlState);
    }

    [Fact]
    public async Task A_key_to_an_alternate_key_stays_in_the_tenant()
    {
        await using (var owner = new ConstraintDbContext(_options, Lumina))
        {
            owner.Add(new CParent { Id = Guid.NewGuid(), Code = "SHARED" });

            await owner.SaveChangesAsync();
        }

        await using var other = new ConstraintDbContext(_options, Vision);

        other.Add(new CByCode { Id = Guid.NewGuid(), ParentCode = "SHARED" });

        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)refusal.InnerException!).SqlState);
    }

    [Fact]
    public async Task Two_tenants_hold_the_same_alternate_key_and_unique_value()
    {
        foreach (var tenant in new[] { Lumina, Vision })
        {
            await using var db = new ConstraintDbContext(_options, tenant);

            db.Add(new CParent { Id = Guid.NewGuid(), Code = "SAME", Slug = "same" });

            await db.SaveChangesAsync();
        }

        await using var again = new ConstraintDbContext(_options, Lumina);

        again.Add(new CParent { Id = Guid.NewGuid(), Code = "SAME", Slug = "other" });

        await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());
    }

    [Fact]
    public async Task An_install_scoped_row_is_named_by_any_tenant()
    {
        var kind = Guid.NewGuid();

        await using (var install = new ConstraintDbContext(_options, Lumina))
        {
            install.Add(new CKind { Id = kind });

            await install.SaveChangesAsync();
        }

        await using var other = new ConstraintDbContext(_options, Vision);

        other.Add(new CChild { Id = Guid.NewGuid(), KindId = kind });

        await other.SaveChangesAsync();
    }

    [Fact]
    public void An_install_scoped_entity_keeps_its_plain_keys()
    {
        using var db = new ConstraintDbContext(_options, Lumina);

        var kind = db.Model.FindEntityType(typeof(CKind))!;

        Assert.Single(kind.GetKeys());
        Assert.Null(kind.FindProperty("TenantId"));
    }

    static async Task Execute(string database, string sql)
    {
        await using var connection = new NpgsqlConnection(
            ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={database}"));

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync();
    }
}

public sealed class ConstraintDbContext : DbContext, ITenanted, IHasOrgScope
{
    public ConstraintDbContext(DbContextOptions<ConstraintDbContext> options, Guid tenant)
        : base(options)
    {
        TenantId = tenant;
    }

    public Guid? TenantId { get; }

    public OrgScope OrgScope
    {
        get { return OrgScope.Everywhere; }
    }

    // The stamp is AddDataAccess's interceptor and a bare context has none: the column is written
    // here the way it writes it.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added))
        {
            if (!typeof(IInstallScoped).IsAssignableFrom(entry.Metadata.ClrType))
            {
                entry.Property("TenantId").CurrentValue = TenantId;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, [new ConstraintSetup()]);
    }
}

sealed class ConstraintSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CKind>(kind => kind.ToTable("Kinds"));

        modelBuilder.Entity<CParent>(parent =>
        {
            parent.ToTable("Parents");
            parent.HasAlternateKey(row => row.Code);
            parent.HasIndex(row => row.Slug).IsUnique();
        });

        modelBuilder.Entity<CChild>(child =>
        {
            child.ToTable("Children");
            child.HasOne<CParent>().WithMany().HasForeignKey(row => row.ParentId).OnDelete(DeleteBehavior.Restrict);
            child.HasOne<CKind>().WithMany().HasForeignKey(row => row.KindId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CBase>(row => row.ToTable("Bases"));

        modelBuilder.Entity<CDerived>(row =>
        {
            row.HasBaseType<CBase>();
            row.HasOne<CParent>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CLoose>(row =>
        {
            row.ToTable("Loose");
            row.HasOne<CParent>().WithMany().HasForeignKey(r => r.ParentId);
        });

        modelBuilder.Entity<CByCode>(row =>
        {
            row.ToTable("ByCode");
            row.HasOne<CParent>().WithMany().HasForeignKey(r => r.ParentCode).HasPrincipalKey(p => p.Code);
        });
    }
}

public sealed class CKind : IInstallScoped
{
    public Guid Id { get; set; }
}

public sealed class CParent
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Slug { get; set; }
}

public sealed class CChild
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public Guid? KindId { get; set; }
}

public sealed class CByCode
{
    public Guid Id { get; set; }

    public string ParentCode { get; set; } = string.Empty;
}

public sealed class CLoose
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }
}

public class CBase
{
    public Guid Id { get; set; }
}

public sealed class CDerived : CBase
{
    public Guid? OwnerId { get; set; }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.UnitOfWork;

namespace NSail.Data;

public static class DbContextExtensions
{
    /// <summary>Applies every kit's model configuration, the Stack's own seed history, and then
    /// the four rules the Stack sets over the whole model: the row version, the case-insensitive
    /// collation, the tenant column with the filter that rides it, and the org filter over what
    /// the map classifies as having happened at a branch. The context is a parameter because
    /// both filters have to reach the scope of whichever context runs the query — see
    /// <see cref="ITenanted"/> and <see cref="IHasOrgScope"/>.</summary>
    public static void ApplySetups<TDbContext>(
        this ModelBuilder modelBuilder,
        TDbContext context,
        IEnumerable<IDbContextSetup> setups)
        where TDbContext : DbContext, ITenanted, IHasOrgScope
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var setup in setups)
        {
            setup.Configure(modelBuilder);
        }

        ApplySeedHistory(modelBuilder);
        ApplyRowVersion(modelBuilder);
        ApplyCaseInsensitiveText(modelBuilder);
        TenantColumn.Apply(modelBuilder.Model, context);
        OrgFilter.Apply(modelBuilder.Model, context);
    }

    /// <summary>What a product's context states about tenancy in its own
    /// <c>ConfigureConventions</c>, and the only thing it states. EF's foreign-key index
    /// convention is removed because the tenant leads every index this model carries
    /// (data-tenancy.md, The tenant is a column): left in place it would put its own
    /// single-column index back beside each tenant-leading one, and the small tenant would go
    /// on paying the large one's size through the index nobody asked for.</summary>
    public static void ApplyTenancy(this ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    // The tenant's own migration history, and the only table the Stack owns. It is configured
    // here rather than contributed as an IDbContextSetup because a product that forgot to
    // compose it would have a seed chain nothing records — every step re-running on every touch —
    // and the whole point of the pass over the model is that no composition can forget a rule.
    // The tenant column, its filter and the tenant-leading index arrive from the passes below,
    // exactly as they do for a kit's entity.
    static void ApplySeedHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppliedSeedStep>(entity =>
        {
            entity.ToTable("AppliedSeedSteps");
            entity.HasKey(row => row.Id);

            entity.Property(row => row.Step)
                .HasMaxLength(128)
                .IsRequired();

            entity.HasIndex(row => row.Step)
                .IsUnique();
        });
    }

    // IVersioned says only "this entity is edited through a form that outlives its read"; what a
    // version token IS belongs to the provider, and IsRowVersion is the whole of what is said
    // here. The Postgres provider answers it with the row's own transaction id — a system column
    // that costs no storage and that nothing has to remember to bump — and another provider
    // would answer with its own native one. Naming either would tie the seam to a database.
    static void ApplyRowVersion(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes().ToArray())
        {
            if (!typeof(IVersioned).IsAssignableFrom(entity.ClrType))
            {
                continue;
            }

            modelBuilder.Entity(entity.ClrType)
                .Property(nameof(IVersioned.Version))
                .IsRowVersion();
        }
    }

    // Postgres compares text byte for byte, so every `search=` filter would stop matching
    // the moment a lowercase term met a capitalized name. The rule is set once over the
    // whole model instead of per query: a case- and accent-insensitive collation on every
    // text column, so handlers keep writing plain Contains and no call site can forget.
    static void ApplyCaseInsensitiveText(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCollation(
            Collations.CaseInsensitive,
            locale: Collations.CaseInsensitiveLocale,
            provider: Collations.CaseInsensitiveProvider,
            deterministic: false);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType != typeof(string))
                {
                    continue;
                }

                if (property.PropertyInfo?.IsDefined(typeof(CaseSensitiveAttribute), inherit: true) == true)
                {
                    continue;
                }

                property.SetCollation(Collations.CaseInsensitive);
            }
        }
    }

    /// <summary>Rides the version the caller loaded with into the UPDATE's own WHERE, so the
    /// check and the write are one statement and no read can sit between them. Called after
    /// loading the row and before mutating it; a zero version means the caller holds no token
    /// (a background job, a first save) and states no OPINION about one.
    ///
    /// It does not make the write unconditional: the entity's OWN xmin is a concurrency token
    /// on every model (ApplyRowVersion), tracked from the load that produced <paramref
    /// name="entity"/> whether this method says anything or not — a second writer between that
    /// load and the caller's SaveChangesAsync still earns a DbUpdateConcurrencyException. A
    /// caller that means "write over whatever is there" (DbSettingsManager.Save) has to catch
    /// that itself and retry.</summary>
    public static void ExpectVersion(this DbContext db, IVersioned entity, uint version)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (version == 0)
        {
            return;
        }

        db.Entry(entity).Property(nameof(IVersioned.Version)).OriginalValue = version;
    }

    public static IServiceCollection SetDefaultDbContext<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<DbContext, TDbContext>();
        services.AddUnitOfWork();
        services.TryAddScoped<EntityMapper>();

        return services;
    }

    /// <summary>Enlists the scope's <see cref="DbContext"/> in the messaging runtime's ambient
    /// unit of work: one transaction per outermost in-process send, joined by every send nested
    /// inside it. Called by <see cref="SetDefaultDbContext{TDbContext}"/>, and directly by a
    /// harness that composes its context some other way.</summary>
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, DbContextUnitOfWork>());

        // The seam a handler registers on-commit work through, beside the unit it waits for:
        // AddMessaging TryAdds the same one, since a client host has a send and no store, and a
        // host that has a store composes this and not always that (a hand-registered context).
        services.TryAddScoped<AfterCommit>();

        // A host that owns a store is the only one that can lose a concurrency race, and the
        // only one whose pipeline has to answer for it. Registered here rather than beside the
        // context itself so a harness composing its own context the long way gets the same
        // refusal a product gets — TryAddEnumerable cannot speak about open generics, so the
        // duplicate is checked for by hand.
        if (!services.Any(service => service.ImplementationType == typeof(ConcurrencyInterceptor<>)))
        {
            services.AddScoped(typeof(IInterceptor<>), typeof(ConcurrencyInterceptor<>));
            services.AddScoped(typeof(IInterceptor<,>), typeof(ConcurrencyInterceptor<,>));
        }

        return services;
    }

    /// <summary>Whether this host's work runs on a per-tenant connection. A startup pass over
    /// "the install's database" has nothing to run against when it does: the databases are the
    /// tenants' own, each reached — and migrated — by its own first request.</summary>
    public static bool ConnectsPerTenant(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Services.GetService<TenancyOptions>()?.Connection == TenancyScope.Tenant;
    }

    /// <summary>Whether this host resolves a tenant per request at all — either wall. What
    /// depends on the wall itself asks <see cref="ConnectsPerTenant"/>; what depends only on
    /// somebody having said who the caller is (the edge that reads the header, the wall that
    /// compares the credential against it) asks this.</summary>
    public static bool ResolvesTenants(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Services.GetService<TenancyOptions>() is { } tenancy
            && (tenancy.Connection == TenancyScope.Tenant || tenancy.Rows == TenancyScope.Tenant);
    }

    /// <summary>
    /// Applies pending EF migrations for <typeparamref name="TDbContext"/>.
    /// Call once at host startup (after <c>Build()</c>, before serving requests).
    /// Does nothing under a per-tenant connection — see <see cref="ConnectsPerTenant"/>.
    /// </summary>
    public static async Task ApplyMigrations<TDbContext>(this IHost host, CancellationToken cancellationToken = default)
        where TDbContext : DbContext
    {
        if (host.ConnectsPerTenant())
        {
            return;
        }

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TDbContext>().Database.MigrateAsync(cancellationToken);
    }

    /// <summary>Has the context stamp the audit columns of every <see cref="IAudited"/> entity on
    /// save, from the context's own <c>OnConfiguring</c>: the one place every way of building the
    /// context (the host, the design-time factory a test fixture reuses) passes through.</summary>
    public static DbContextOptionsBuilder UseAuditStamps(this DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.AddInterceptors(AuditStamp.Instance);
    }
}

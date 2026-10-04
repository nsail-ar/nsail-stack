// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NSail.Data;

public static class Setup
{
    public static void AddDbContextSetup<TDbContextSetup>(this IServiceCollection services)
        where TDbContextSetup : class, IDbContextSetup
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IDbContextSetup, TDbContextSetup>());
    }

    /// <summary>Registers what an app plants for a tenant on its first touch. Registered by
    /// every mode and asked for by one: where the rows are the install's, a tenant's first
    /// touch never happens.</summary>
    public static void AddTenantSeed<TTenantSeed>(this IServiceCollection services)
        where TTenantSeed : class, ITenantSeed
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITenantSeed, TTenantSeed>());
    }

    /// <summary>Registers a singleton that keeps a tenant's rows in memory, and tells the first
    /// touch to make it forget the tenant it has just migrated or planted.</summary>
    public static void AddTenantCache<TTenantCache>(this IServiceCollection services)
        where TTenantCache : class, ITenantCache
    {
        services.TryAddSingleton<TTenantCache>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITenantCache, TTenantCache>(provider => provider.GetRequiredService<TTenantCache>()));
    }

    /// <summary>Declares ids the install freezes and code names by constant, so the seam that
    /// answers "this row, in this scope" (<see cref="TenancyProvider.Row(Guid)"/>) translates
    /// those and nothing else. Declared where the constants live, never discovered.</summary>
    public static void AddWellKnownRows(this IServiceCollection services, IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWellKnownRows>(new DeclaredRows(ids));
    }

    /// <summary>Declares the ids the seeded rows of an <see cref="IInstallScoped"/> entity carry,
    /// so the seam that answers a document's ids (<see cref="TenancyProvider.Owned(Guid)"/>) knows
    /// which of them name a row every scope shares. Declared where those rows are seeded, never
    /// discovered — a guid is not readable back to the table it came from.</summary>
    public static void AddInstallScopedRows(this IServiceCollection services, IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IInstallScopedRows>(new DeclaredRows(ids));
    }

    /// <summary>The tenancy seam and the product's context in one call: the host states its
    /// install connection string and, optionally, its tenancy mode, and the seam owns
    /// everything between that string and the <see cref="DbContext"/> handlers inject. The
    /// context comes from an <see cref="IDbContextFactory{TContext}"/> asked per scope, from
    /// that scope's own <see cref="TenancyProvider"/> — a resolution that varies per request
    /// needs no second shape here, and a Blazor component whose circuit outlives a request has
    /// a context of its own to open. A mode that connects per tenant additionally registers the
    /// registry, the provider a resolver fills and the first-touch migrator; who fills it is
    /// the host's half (the tenancy middleware), and a host that fills nothing keeps the
    /// refusal. A mode that scopes ROWS to the tenant needs the same resolved provider and
    /// none of the rest: the database is the install's, and the wall is the column the model
    /// puts on every entity.</summary>
    public static IServiceCollection AddDataAccess<TDbContext>(
        this IServiceCollection services,
        string connectionString,
        TenancyOptions? tenancy = null)
        where TDbContext : DbContext, ITenanted, IHasOrgScope
    {
        ArgumentNullException.ThrowIfNull(services);

        tenancy ??= new TenancyOptions();

        services.AddSingleton(tenancy);

        // The org half of what a context is scoped to, at its own default: every branch, which
        // is what a host that resolves no session organization means. Whoever knows where the
        // caller stands enters the subtree onto this scope instead (Iam), and a host that
        // composes no Iam reads the whole company, exactly as it did before the filter existed.
        services.TryAddScoped<OrgScopeProvider>();

        // Registered by every mode and consulted by one, like the seed above: the set is the
        // install's own declaration and does not vary with the wall, so the seam reading it
        // answers the same shape everywhere and the mode alone decides what it does with it.
        services.TryAddSingleton<WellKnownRows>();
        services.TryAddSingleton<InstallScopedRows>();

        if (tenancy.Connection == TenancyScope.Tenant)
        {
            // Constructed here rather than from a factory so a cell that names its product or
            // its cell badly is refused at startup, not on the first request that needs one.
            var databases = new TenantDatabases(tenancy, connectionString);

            services.AddSingleton(databases);

            // The roster of the wall that has a registry: the databases themselves, so a tenant
            // provisioned after this process started is swept without a restart.
            services.AddSingleton(new TenantRoster(tenancy, databases));

            services.AddScoped<TenantMigrator, TenantMigrator<TDbContext>>();
        }

        if (tenancy.Rows == TenancyScope.Tenant)
        {
            // The twin of the migrator above, for the wall that has no database to migrate: a
            // tenant here is a slug and nothing else until its first request plants its rows.
            services.AddSingleton<FirstTouch>();
            services.AddScoped<TenantBootstrap, TenantBootstrap<TDbContext>>();

            // And the roster of the wall that has no registry to read: whoever the install
            // declared, refused here if one of them cannot be a slug.
            services.AddSingleton(new TenantRoster(tenancy));
        }

        if (tenancy.Connection == TenancyScope.Tenant || tenancy.Rows == TenancyScope.Tenant)
        {
            services.AddScoped<ResolvedTenancyProvider>();
            services.AddScoped<TenancyProvider>(provider => provider.GetRequiredService<ResolvedTenancyProvider>());
        }

        services.TryAddScoped<TenancyProvider>();

        // Scoped, not the default singleton: the factory reads the scope's TenancyProvider, and
        // a singleton would bind the first scope's answer for the life of the process. A scoped
        // lifetime is also what makes TDbContext itself resolvable — EF registers it from this
        // factory only at this lifetime, which is why nothing here registers the context.
        services.AddDbContextFactory<TDbContext>(
            (provider, options) =>
            {
                options.UseNpgsql(
                    provider.GetRequiredService<TenancyProvider>().ResolveConnection(connectionString),
                    npgsql => npgsql.MigrationsAssembly(typeof(TDbContext).Assembly.GetName().Name));

                // Every save the process makes goes through one interceptor rather than through
                // a rule each handler keeps: the tenant a row is written under is read from the
                // context, which is the same answer the query filter reads it back with.
                options.AddInterceptors(new TenantStamp());
            },
            ServiceLifetime.Scoped);

        services.SetDefaultDbContext<TDbContext>();

        return services;
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSail.Configuration;

namespace NSail.Data.Tests;

public class TenancySeamTests
{
    const string Install = "Host=localhost;Database=optical_dev1;Username=postgres";

    [Fact]
    public void AbsentSectionIsNone()
    {
        var options = new ConfigurationBuilder().Build().Load<TenancyOptions>();

        Assert.Equal(TenancyMode.None, options.Mode);
        Assert.Equal(TenancyScope.Install, options.Connection);
        Assert.Equal(TenancyScope.Install, options.Rows);
    }

    [Theory]
    [InlineData("None", TenancyMode.None, TenancyScope.Install, TenancyScope.Install)]
    [InlineData("MultiDb", TenancyMode.MultiDb, TenancyScope.Tenant, TenancyScope.Install)]
    [InlineData("SingleDb", TenancyMode.SingleDb, TenancyScope.Install, TenancyScope.Tenant)]
    public void ModeIsAPresetOfTwoIndependentSwitches(
        string configured,
        TenancyMode mode,
        TenancyScope connection,
        TenancyScope rows)
    {
        var options = Configured(configured).Load<TenancyOptions>();

        Assert.Equal(mode, options.Mode);
        Assert.Equal(connection, options.Connection);
        Assert.Equal(rows, options.Rows);
    }

    // A mode that does not name one of the enum's values must fail to bind rather than
    // silently fall back to the default.
    [Fact]
    public void AMisspelledModeRefusesToBind()
    {
        Assert.Throws<InvalidOperationException>(() => Configured("Sngledb").Load<TenancyOptions>());
    }

    // The wall a mode scoping ROWS needs is the resolved provider and nothing else: no registry
    // to ask, no per-tenant database to migrate, and the install's own connection string.
    [Fact]
    public void SingleDbResolvesTenantsOnTheInstallsOwnConnection()
    {
        var services = Services(new TenancyOptions { Mode = TenancyMode.SingleDb });

        Assert.DoesNotContain(services, service => service.ServiceType == typeof(TenantDatabases));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(TenantMigrator));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(Tenant.For("lumina"));

        var context = scope.ServiceProvider.GetRequiredService<SeamDbContext>();

        Assert.Equal(ConnectionStrings.Complete(Install), context.Database.GetConnectionString());
        Assert.Equal(Tenant.For("lumina").RowKey, context.TenantId);
    }

    // The two walls are separate switches, and the row one leaves the connection alone: a tenant
    // isolated by its column has no database of its own to name.
    [Fact]
    public void ATenantWalledByItsColumnCarriesAKeyAndNoDatabase()
    {
        var tenant = Tenant.For("lumina");

        Assert.True(tenant.IsResolved);
        Assert.Null(tenant.Database);
        Assert.NotEqual(Guid.Empty, tenant.RowKey);
        Assert.Equal(tenant.RowKey, Tenant.For("lumina").RowKey);
        Assert.NotEqual(tenant.RowKey, Tenant.For("vision").RowKey);
    }

    // The other wall's tenant is isolated by the connection, so its rows are the install's and
    // the key it compares against is the install's own — the filter matches everything, which is
    // what makes ONE model serve all three modes.
    [Fact]
    public void ATenantWalledByItsDatabaseCarriesTheInstallsKey()
    {
        Assert.Equal(Guid.Empty, Tenant.For("lumina", "optical_sa1_lumina").RowKey);
        Assert.Equal(Guid.Empty, Tenant.None.RowKey);
    }

    // A mode that connects per tenant derives the database name from the cell's own half, so a
    // cell that states neither cannot boot into serving one database to everybody.
    [Fact]
    public void MultiDbWithoutTheCellsOwnHalfIsRefusedAtStartup()
    {
        var services = new ServiceCollection();

        var refusal = Assert.Throws<InvalidOperationException>(
            () => services.AddDataAccess<SeamDbContext>(Install, new TenancyOptions { Mode = TenancyMode.MultiDb }));

        Assert.Contains("Tenancy:Product", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoneServesTheNullAnswer()
    {
        using var provider = Host(new TenancyOptions());
        using var scope = provider.CreateScope();

        var tenancy = scope.ServiceProvider.GetRequiredService<TenancyProvider>();

        Assert.False(tenancy.Current.IsResolved);
        Assert.Null(tenancy.Current.Slug);
        Assert.Null(tenancy.Current.Database);
    }

    [Fact]
    public void NoneConnectsExactlyWhereTheInstallSays()
    {
        using var provider = Host(new TenancyOptions());
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<SeamDbContext>();
        var injected = scope.ServiceProvider.GetRequiredService<DbContext>();

        Assert.Equal(ConnectionStrings.Complete(Install), context.Database.GetConnectionString());
        Assert.Equal(ConnectionStrings.Complete(Install), injected.Database.GetConnectionString());
        Assert.IsType<SeamDbContext>(injected);
    }

    // TenancyMode.None is the enum's zero member, so a caller who states no tenancy at all
    // must land on exactly the same Connection/Rows pair as a caller who states it explicitly.
    [Fact]
    public void TheTenancyArgumentIsOptionalAndDefaultsToNone()
    {
        var services = new ServiceCollection();

        services.AddDataAccess<SeamDbContext>(Install);

        var tenancy = services.BuildServiceProvider().GetRequiredService<TenancyOptions>();

        Assert.Equal(TenancyScope.Install, tenancy.Connection);
        Assert.Equal(TenancyScope.Install, tenancy.Rows);
    }

    // ApplyMigrations resolves exactly this, from a startup scope of its own, and under None it
    // has to reach the one context the install names.
    [Fact]
    public void MigrationsResolveOneContextFromAStartupScope()
    {
        using var provider = Host(new TenancyOptions());
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<SeamDbContext>();

        Assert.Equal(ConnectionStrings.Complete(Install), context.Database.GetConnectionString());
    }

    // The migrations assembly comes from the context's own assembly, never from a name
    // passed at the call site (data.md).
    [Fact]
    public void TheMigrationsAssemblyIsDerivedFromTheContext()
    {
        using var provider = Host(new TenancyOptions());
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SeamDbContext>>();

        Assert.Equal(
            typeof(SeamDbContext).Assembly.GetName().Name,
            options.Extensions.OfType<RelationalOptionsExtension>().Single().MigrationsAssembly);
    }

    [Fact]
    public void TheSeamHandsOutAContextFactory()
    {
        using var provider = Host(new TenancyOptions());
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SeamDbContext>>();

        using var first = factory.CreateDbContext();
        using var second = factory.CreateDbContext();

        Assert.NotSame(first, second);
        Assert.Equal(ConnectionStrings.Complete(Install), first.Database.GetConnectionString());
    }

    // AddDataAccess registers no context of its own: the scoped TDbContext handlers and
    // ApplyMigrations resolve is the one EF hangs off the factory, and it does that only at a
    // scoped lifetime. If a future EF stops, this goes red rather than the seam going silent.
    [Fact]
    public void TheScopedContextComesFromTheFactory()
    {
        var descriptor = Services(new TenancyOptions()).Single(service => service.ServiceType == typeof(SeamDbContext));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Null(descriptor.ImplementationType);
    }

    // MultiDb passes the Rows switch AddDataAccess guards, and a scope that reached a context
    // without anyone resolving a tenant would be served the install's database — the leak the
    // mode was configured to prevent. The provider that resolves tenants keeps that refusal
    // for exactly this case rather than replacing it with a silent fallback.
    [Fact]
    public void MultiDbWithNothingResolvedIsStillRefused()
    {
        using var provider = Host(MultiDb());
        using var scope = provider.CreateScope();

        var refusal = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<SeamDbContext>());

        Assert.Contains("nsail#355", refusal.Message, StringComparison.Ordinal);
    }

    // The one provider in the scope is the one a resolver fills: what the middleware enters a
    // tenant on and what the factory asks for the connection cannot be two objects.
    [Fact]
    public void TheResolvedProviderIsTheScopesProvider()
    {
        using var provider = Host(MultiDb());
        using var scope = provider.CreateScope();

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<TenancyProvider>(),
            scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>());
    }

    [Fact]
    public void AnEnteredTenantIsWhereTheScopeConnects()
    {
        using var provider = Host(MultiDb());
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>()
            .Enter(Tenant.For("lumina", "optical_sa1_lumina"));

        var context = scope.ServiceProvider.GetRequiredService<SeamDbContext>();

        Assert.Equal("lumina", scope.ServiceProvider.GetRequiredService<TenancyProvider>().Current.Slug);
        Assert.Equal(
            "optical_sa1_lumina",
            new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString()).Database);
    }

    // A request's own work opens scopes of its own — a prerendered component resolving a brand,
    // a provider taking a fresh scope so two threads do not share one context — and a child
    // scope that could not see the tenant would refuse where the request had already answered.
    [Fact]
    public void AnEnteredTenantReachesEveryScopeTheWorkOpensUnderIt()
    {
        using var provider = Host(MultiDb());
        using var request = provider.CreateScope();

        request.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>()
            .Enter(Tenant.For("lumina", "optical_sa1_lumina"));

        using var opened = provider.CreateScope();

        Assert.Equal(
            "optical_sa1_lumina",
            new NpgsqlConnectionStringBuilder(
                opened.ServiceProvider.GetRequiredService<SeamDbContext>().Database.GetConnectionString()).Database);
    }

    // The other half of the same sentence: it reaches down, never sideways. Work that another
    // request started is another tenant's, and an ambient that leaked would serve it that one.
    [Fact]
    public async Task ATenantEnteredInOneFlowIsNotTheNextFlowsTenant()
    {
        using var provider = Host(MultiDb());

        await Task.Run(() =>
        {
            using var request = provider.CreateScope();

            request.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>()
                .Enter(Tenant.For("lumina", "optical_sa1_lumina"));
        });

        await Task.Run(() =>
        {
            using var next = provider.CreateScope();

            Assert.False(next.ServiceProvider.GetRequiredService<TenancyProvider>().Current.IsResolved);
        });
    }

    // A Blazor circuit outlives the request that opened it, so the flow the tenant was entered
    // on is long gone by the time a render asks. Entering pins it onto the scope for exactly
    // that: the circuit's own provider keeps answering after its flow ends.
    [Fact]
    public async Task AnEnteredTenantOutlivesTheFlowThatEnteredIt()
    {
        using var provider = Host(MultiDb());
        using var circuit = provider.CreateScope();

        var tenancy = circuit.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>();

        await Task.Run(() => tenancy.Enter(Tenant.For("lumina", "optical_sa1_lumina")));

        Assert.Equal("lumina", tenancy.Current.Slug);
    }

    // A context built on the first tenant keeps its connection, so a second tenant in one scope
    // would serve two databases under one request.
    [Fact]
    public void OneScopeEntersOneTenant()
    {
        using var provider = Host(MultiDb());
        using var scope = provider.CreateScope();

        var tenancy = scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>();

        tenancy.Enter(Tenant.For("lumina", "optical_sa1_lumina"));

        Assert.Throws<InvalidOperationException>(() => tenancy.Enter(Tenant.For("vision", "optical_sa1_vision")));
    }

    [Fact]
    public void NoneRegistersNothingThatReadsATenant()
    {
        var services = Services(new TenancyOptions());

        Assert.DoesNotContain(services, service => service.ServiceType == typeof(TenantDatabases));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(ResolvedTenancyProvider));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(TenantMigrator));
    }

    // What the factory buys over a connection string bound once at registration: the scope's
    // own TenancyProvider is asked every time, so the per-request resolution slice 2 brings
    // needs no second shape here — and replacing the provider is what lifts MultiDb's refusal.
    [Fact]
    public void ConnectionIsResolvedPerScope()
    {
        var tenant = 0;
        var tenancy = MultiDb();
        var services = Services(tenancy);

        services.Replace(ServiceDescriptor.Scoped<TenancyProvider>(
            _ => new StubTenancyProvider(tenancy, $"optical_t{Interlocked.Increment(ref tenant)}")));

        using var provider = services.BuildServiceProvider();

        Assert.Equal("Database=optical_t1", Connection(provider));
        Assert.Equal("Database=optical_t2", Connection(provider));
    }

    static string? Connection(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();

        return scope.ServiceProvider.GetRequiredService<SeamDbContext>().Database.GetConnectionString();
    }

    static TenancyOptions MultiDb()
    {
        return new TenancyOptions { Mode = TenancyMode.MultiDb, Product = "optical", Cell = "sa1" };
    }

    static IConfiguration Configured(string mode)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tenancy:Mode"] = mode })
            .Build();
    }

    static ServiceProvider Host(TenancyOptions tenancy)
    {
        return Services(tenancy).BuildServiceProvider();
    }

    static ServiceCollection Services(TenancyOptions tenancy)
    {
        var services = new ServiceCollection();

        services.AddDataAccess<SeamDbContext>(Install, tenancy);

        return services;
    }

    sealed class StubTenancyProvider : TenancyProvider
    {
        readonly Tenant _tenant;

        public StubTenancyProvider(TenancyOptions tenancy, string database)
            : base(tenancy)
        {
            _tenant = Tenant.For(database, database);
        }

        public override Tenant Current
        {
            get { return _tenant; }
        }

        public override string ResolveConnection(string installConnectionString)
        {
            return $"Database={_tenant.Database}";
        }
    }

    sealed class SeamDbContext : DbContext, ITenanted, IHasOrgScope
    {
        readonly TenancyProvider _tenancy;

        public SeamDbContext(DbContextOptions<SeamDbContext> options, TenancyProvider? tenancy = null)
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
    }
}

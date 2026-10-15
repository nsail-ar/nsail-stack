// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSail.Data;

namespace NSail.Background.Tests;

// A job type of this suite's own, never another class's: the meter's name is a constant, so a
// reading is told from another test class's only by the job attribute on it.
sealed class SuspendableJob : IBackgroundJob
{
    readonly TenancyProvider _tenancy;
    readonly Swept _swept;
    readonly Signal _signal;

    public SuspendableJob(TenancyProvider tenancy, Swept swept, Signal signal)
    {
        _tenancy = tenancy;
        _swept = swept;
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public TenancyScope Tenancy => TenancyScope.Tenant;

    public Task Run(CancellationToken cancellationToken)
    {
        _swept.Add(_tenancy.Current.Slug);
        _signal.Record();

        return Task.CompletedTask;
    }
}

/// <summary>What the install's record about a tenant does to the sweep: a suspended customer
/// gets no work done on its rows, and gets it again the moment the row says so. Over a real
/// Postgres, because the answer is a row and a fake of it would prove the fake.</summary>
public sealed class SuspendedSweepTests : IAsyncLifetime
{
    readonly string _database = "nsail2230_" + Guid.NewGuid().ToString("N")[..12];

    DbContextOptions<StateDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await Execute("postgres", $"CREATE DATABASE \"{_database}\"");

        _options = new DbContextOptionsBuilder<StateDbContext>()
            .UseNpgsql(ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_database}"))
            .Options;

        await using var db = new StateDbContext(_options);

        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await Execute("postgres", $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
    }

    [Fact]
    public async Task A_suspended_tenant_is_not_swept()
    {
        await State("alfa", TenantState.Suspended);

        var swept = new Swept();
        var signal = new Signal();

        await using var provider = Build(swept, signal);

        await Harness.Run(provider, signal.Reached);

        Assert.Equal(["beta"], swept.Slugs);
    }

    // The gauge is the sweep's own count and not the roster's, because a suspended tenant is on
    // the roster and is not swept: an alert reading "late" against a job doing exactly what the
    // install told it to is the false alarm this number exists to avoid.
    [Fact]
    public async Task The_roster_gauge_counts_the_tenants_the_sweep_entered()
    {
        await State("alfa", TenantState.Suspended);

        using var meters = new Meters();
        var signal = new Signal();

        await using var provider = Build(new Swept(), signal);

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.Equal(1d, meters.Gauge(BackgroundJobMetrics.Roster, nameof(SuspendableJob)));
        Assert.Equal(1, meters.Passes(nameof(SuspendableJob), BackgroundJobMetrics.Succeeded));
        Assert.Equal(0, meters.Passes(nameof(SuspendableJob), BackgroundJobMetrics.Failed));
    }

    // The memo is one process's, so a restored customer is swept again by a runner that has not
    // read it yet — and two runners over one database is the shape a test has for that, as it is
    // for "the row changed while we were running".
    [Fact]
    public async Task A_reactivated_tenant_is_swept_again()
    {
        await State("alfa", TenantState.Suspended);

        var refused = new Swept();
        var beta = new Signal();

        await using (var provider = Build(refused, beta))
        {
            await Harness.Run(provider, beta.Reached);
        }

        Assert.Equal(["beta"], refused.Slugs);

        await State("alfa", TenantState.Active);

        var swept = new Swept();
        var signal = new Signal { Target = 2 };

        await using var reactivated = Build(swept, signal);

        await Harness.Run(reactivated, signal.Reached);

        Assert.Equal(["alfa", "beta"], swept.Slugs);
    }

    ServiceProvider Build(Swept swept, Signal signal)
    {
        return Harness.Build(services =>
        {
            // Exactly what AddDataAccess registers for the wall that scopes rows, plus the one
            // thing this suite is about: the reader of the install's record, its memo and the
            // clock the pause window is judged by.
            var tenancy = new TenancyOptions { Mode = TenancyMode.SingleDb, Tenants = "alfa,beta" };

            services.AddSingleton(tenancy);
            services.AddSingleton(new TenantRoster(tenancy));
            services.AddScoped<ResolvedTenancyProvider>();
            services.AddScoped<TenancyProvider>(provider => provider.GetRequiredService<ResolvedTenancyProvider>());
            services.AddScoped<DbContext>(provider => new StateDbContext(_options));
            services.AddScoped<TenantStates>();
            services.AddSingleton<TenantStateMemo>();
            services.AddSingleton(TimeProvider.System);

            services.AddSingleton(swept);
            services.AddSingleton(signal);
            services.AddBackgroundJob<SuspendableJob>();
        });
    }

    // What whoever operates the fleet does, in the only shape a test can do it: the row is
    // written by the tenant's own key, through no scope of the app's.
    async Task State(string slug, string status)
    {
        await using var db = new StateDbContext(_options);

        var key = TenantKey.For(slug);

        if (await db.Set<TenantState>().FirstOrDefaultAsync(row => row.TenantId == key) is { } existing)
        {
            existing.Status = status;
        }
        else
        {
            db.Add(new TenantState { TenantId = key, Status = status });
        }

        await db.SaveChangesAsync();
    }

    static async Task Execute(string database, string statement)
    {
        await using var admin = new NpgsqlConnection(
            ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={database}"));

        await admin.OpenAsync();

        await using var command = new NpgsqlCommand(statement, admin);

        await command.ExecuteNonQueryAsync();
    }
}

// The Stack's own two tables and nothing else: what ApplySetups puts on a model is the subject,
// so the model goes through it rather than naming the table a second time.
sealed class StateDbContext : DbContext, ITenanted, IHasOrgScope
{
    public StateDbContext(DbContextOptions<StateDbContext> options)
        : base(options)
    {
    }

    public Guid? TenantId
    {
        get { return TenancyProvider.Install.RowKey; }
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
        modelBuilder.ApplySetups(this, []);
    }
}

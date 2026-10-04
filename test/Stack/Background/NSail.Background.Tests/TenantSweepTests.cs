// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.Background.Tests;

// The tenants a job ran under, in the order the sweep took them — a null entry is a pass that
// entered none, which is the whole of what an install without tenants does.
sealed class Swept
{
    readonly List<string?> _slugs = [];

    public IReadOnlyList<string?> Slugs
    {
        get
        {
            lock (_slugs)
            {
                return _slugs.ToList();
            }
        }
    }

    public void Add(string? slug)
    {
        lock (_slugs)
        {
            _slugs.Add(slug);
        }
    }
}

sealed class Cadence
{
    // An hour is far outside the harness's bound, so what these tests observe is one tick and
    // nothing else: a count of passes is only readable while no second tick can add to it.
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);
}

sealed class Fails
{
    public string? Tenant { get; init; }
}

// Reads the tenant off the scope's own provider rather than the ambient one: what the contract
// promises a job is that the scope it was resolved from has the tenant entered.
sealed class SweepingJob : IBackgroundJob
{
    readonly TenancyProvider _tenancy;
    readonly Cadence _cadence;
    readonly Fails _fails;
    readonly Swept _swept;
    readonly Signal _signal;

    public SweepingJob(TenancyProvider tenancy, Cadence cadence, Fails fails, Swept swept, Signal signal)
    {
        _tenancy = tenancy;
        _cadence = cadence;
        _fails = fails;
        _swept = swept;
        _signal = signal;
    }

    public TimeSpan Interval => _cadence.Interval;

    public TenancyScope Tenancy => TenancyScope.Tenant;

    public Task Run(CancellationToken cancellationToken)
    {
        var slug = _tenancy.Current.Slug;

        if (slug is not null && slug == _fails.Tenant)
        {
            throw new InvalidOperationException($"tenant {slug} cannot be swept");
        }

        _swept.Add(slug);
        _signal.Record();

        return Task.CompletedTask;
    }
}

sealed class InstallWideJob : IBackgroundJob
{
    readonly TenancyProvider _tenancy;
    readonly Swept _swept;
    readonly Signal _signal;

    public InstallWideJob(TenancyProvider tenancy, Swept swept, Signal signal)
    {
        _tenancy = tenancy;
        _swept = swept;
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public TenancyScope Tenancy => TenancyScope.Install;

    public Task Run(CancellationToken cancellationToken)
    {
        _swept.Add(_tenancy.Current.Slug);
        _signal.Record();

        return Task.CompletedTask;
    }
}

/// <summary>What one tick is worth once a job says whose work it is: a pass per tenant on the
/// roster for the job that touches a tenant's rows, one pass for the job that does not, and the
/// same single pass either way on an install that resolves no tenant at all.</summary>
public sealed class TenantSweepTests
{
    [Fact]
    public async Task A_per_tenant_job_runs_once_for_every_tenant_on_the_roster()
    {
        var swept = new Swept();
        var signal = new Signal { Target = 2 };

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, "alfa,beta");
            Job<SweepingJob>(services, swept, signal);
        });

        await Harness.Run(provider, signal.Reached);

        Assert.Equal(["alfa", "beta"], swept.Slugs);
    }

    // The sweep is one job's tick, not one job per tenant: a tenant whose pass throws costs its
    // own pass and the log line that says so, and every tenant after it still runs.
    [Fact]
    public async Task A_tenant_that_fails_does_not_take_the_rest_of_the_sweep_with_it()
    {
        var swept = new Swept();
        var signal = new Signal();
        var logs = new LogRecorder();

        await using var provider = Harness.Build(
            services =>
            {
                Tenants(services, "alfa,beta");
                Job<SweepingJob>(services, swept, signal, new Fails { Tenant = "alfa" });
            },
            logs);

        await Harness.Run(provider, signal.Reached);

        Assert.Equal(["beta"], swept.Slugs);

        var error = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);

        Assert.Contains(nameof(SweepingJob), error.Message);
        Assert.Contains("alfa", error.Message);
        Assert.Equal("tenant alfa cannot be swept", Assert.IsType<InvalidOperationException>(error.Exception).Message);
    }

    // The invariant the contract must not break: an install with no tenants runs exactly what it
    // ran before any job answered this question — one pass, under nobody.
    [Fact]
    public async Task An_install_that_resolves_no_tenant_runs_a_per_tenant_job_once_with_none_entered()
    {
        var swept = new Swept();
        var signal = new Signal();

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton(new TenancyOptions());
            services.AddScoped<TenancyProvider>();
            Job<SweepingJob>(services, swept, signal);
        });

        await Harness.Run(provider, signal.Reached);

        Assert.Equal([null], swept.Slugs);
    }

    [Fact]
    public async Task An_install_wide_job_runs_once_however_many_tenants_the_install_holds()
    {
        var swept = new Swept();
        var signal = new Signal();

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, "alfa,beta");
            Job<InstallWideJob>(services, swept, signal);
        });

        await Harness.Run(provider, signal.Reached);

        Assert.Equal([null], swept.Slugs);
    }

    // A mode that scopes work to tenants and holds none of them is a standing misconfiguration:
    // every per-tenant job runs for nobody, and nothing else in the log would say so. The
    // companion job's ticks are what make the count meaningful — the loops share a cadence, so
    // by the third of them the sweep has been asked several times and still said it once.
    [Fact]
    public async Task An_empty_roster_is_said_once_at_warning()
    {
        var swept = new Swept();
        var ticks = new Signal { Target = 3 };
        var logs = new LogRecorder();

        await using var provider = Harness.Build(
            services =>
            {
                Tenants(services, roster: null);
                Job<SweepingJob>(services, swept, ticks, cadence: new Cadence { Interval = TimeSpan.FromMilliseconds(20) });
                services.AddBackgroundJob<CountingJob>();
            },
            logs);

        await Harness.Run(provider, ticks.Reached);

        Assert.Empty(swept.Slugs);

        var warning = Assert.Single(logs.Entries, entry => entry.Level >= LogLevel.Warning);

        Assert.Contains(nameof(SweepingJob), warning.Message);
        Assert.Contains("Tenancy:Tenants", warning.Message);
    }

    // Exactly what AddDataAccess registers for the wall that scopes rows: the roster the install
    // declared, and the provider a resolver — here the runner — fills.
    static void Tenants(IServiceCollection services, string? roster)
    {
        var tenancy = new TenancyOptions { Mode = TenancyMode.SingleDb, Tenants = roster };

        services.AddSingleton(tenancy);
        services.AddSingleton(new TenantRoster(tenancy));
        services.AddScoped<ResolvedTenancyProvider>();
        services.AddScoped<TenancyProvider>(provider => provider.GetRequiredService<ResolvedTenancyProvider>());
    }

    static void Job<TJob>(IServiceCollection services, Swept swept, Signal signal, Fails? fails = null, Cadence? cadence = null)
        where TJob : class, IBackgroundJob
    {
        services.AddSingleton(swept);
        services.AddSingleton(signal);
        services.AddSingleton(fails ?? new Fails());
        services.AddSingleton(cadence ?? new Cadence());
        services.AddBackgroundJob<TJob>();
    }
}

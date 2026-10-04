// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.Background.Tests;

sealed class Metered
{
    // An hour is far outside the harness's bound, so a count of passes is only readable while
    // no second tick can add to it.
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);

    public bool Throws { get; init; }
}

// Job types of this suite's own, never the sweep suite's: the meter's name is a constant, so a
// reading is told from another test class's only by the job attribute on it, and sharing a job
// type would let two classes running in parallel read each other's numbers.
sealed class MeteredJob : IBackgroundJob
{
    readonly Metered _metered;
    readonly Signal _signal;

    public MeteredJob(Metered metered, Signal signal)
    {
        _metered = metered;
        _signal = signal;
    }

    public TimeSpan Interval => _metered.Interval;

    public TenancyScope Tenancy => TenancyScope.Tenant;

    public Task Run(CancellationToken cancellationToken)
    {
        _signal.Record();

        if (_metered.Throws)
        {
            throw new InvalidOperationException("this pass cannot be completed");
        }

        return Task.CompletedTask;
    }
}

sealed class MeteredInstallJob : IBackgroundJob
{
    readonly Metered _metered;
    readonly Signal _signal;

    public MeteredInstallJob(Metered metered, Signal signal)
    {
        _metered = metered;
        _signal = signal;
    }

    public TimeSpan Interval => _metered.Interval;

    public TenancyScope Tenancy => TenancyScope.Install;

    public Task Run(CancellationToken cancellationToken)
    {
        _signal.Record();

        return Task.CompletedTask;
    }
}

/// <summary>What the runner says where a log line cannot be heard. Every one of these readings
/// is taken the way the exporter takes it — off the meter, by instrument name, with the
/// attributes attached — because the alert that has to name the install, the job and the reason
/// is written against exactly these three numbers and nothing else.</summary>
public sealed class JobMetricsTests
{
    [Fact]
    public async Task A_pass_that_completes_is_counted_by_job_and_outcome()
    {
        using var meters = new Meters();
        var signal = new Signal { Target = 2 };

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, "alfa,beta");
            Job<MeteredJob>(services, signal);
        });

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.Equal(2, meters.Passes(nameof(MeteredJob), BackgroundJobMetrics.Succeeded));
        Assert.Equal(0, meters.Passes(nameof(MeteredJob), BackgroundJobMetrics.Failed));
        Assert.Equal(2d, meters.Gauge(BackgroundJobMetrics.Roster, nameof(MeteredJob)));
    }

    // The reason an alert has to be able to tell apart from the one below it: the job is fine
    // and the install is not, so nothing failed and nothing ran.
    [Fact]
    public async Task A_job_that_runs_for_nobody_reports_a_roster_of_zero_and_no_passes()
    {
        using var meters = new Meters();

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, roster: null);
            Job<MeteredJob>(services, new Signal());
        });

        await Harness.Run(provider, () => Read(meters, BackgroundJobMetrics.Roster) == 0d);

        Assert.Equal(0d, meters.Gauge(BackgroundJobMetrics.Roster, nameof(MeteredJob)));
        Assert.Equal(0, meters.Passes(nameof(MeteredJob), BackgroundJobMetrics.Succeeded));
        Assert.Equal(0, meters.Passes(nameof(MeteredJob), BackgroundJobMetrics.Failed));
    }

    // The other reason: the roster is there and the job is what is broken.
    [Fact]
    public async Task A_pass_that_throws_is_counted_failed_and_leaves_the_job_uncompleted()
    {
        using var meters = new Meters();
        var signal = new Signal();

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, "alfa");
            Job<MeteredJob>(services, signal, new Metered { Throws = true });
        });

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.Equal(1, meters.Passes(nameof(MeteredJob), BackgroundJobMetrics.Failed));
        Assert.Equal(0, meters.Passes(nameof(MeteredJob), BackgroundJobMetrics.Succeeded));
        Assert.Equal(1d, meters.Gauge(BackgroundJobMetrics.Roster, nameof(MeteredJob)));
        Assert.True(meters.Gauge(BackgroundJobMetrics.Overdue, nameof(MeteredJob)) > 0d);
    }

    // The number the standing rule is written against, at the threshold it is written at: three
    // cadences missed, whatever the cadence is, so one rule covers an hourly job and a six-hourly
    // one without either interval being typed into it.
    [Fact]
    public async Task A_job_that_never_completes_a_pass_climbs_past_three_of_its_own_cadences()
    {
        using var meters = new Meters();

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, roster: null);
            Job<MeteredJob>(services, new Signal(), new Metered { Interval = TimeSpan.FromMilliseconds(50) });
        });

        await Harness.Run(provider, () => Read(meters, BackgroundJobMetrics.Overdue) > 3d);

        Assert.True(meters.Gauge(BackgroundJobMetrics.Overdue, nameof(MeteredJob)) > 3d);
    }

    [Fact]
    public async Task A_completed_pass_puts_the_job_back_inside_one_cadence()
    {
        using var meters = new Meters();
        var signal = new Signal();

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, "alfa");
            Job<MeteredJob>(services, signal);
        });

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.True(meters.Gauge(BackgroundJobMetrics.Overdue, nameof(MeteredJob)) < 1d);
    }

    // An install-wide job is never late for a roster it does not have: one pass is its whole
    // tick, so the reading the rule sees is one and never zero.
    [Fact]
    public async Task An_install_wide_job_reports_a_roster_of_one_however_many_tenants_the_install_holds()
    {
        using var meters = new Meters();
        var signal = new Signal();

        await using var provider = Harness.Build(services =>
        {
            Tenants(services, "alfa,beta");
            Job<MeteredInstallJob>(services, signal);
        });

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.Equal(1d, meters.Gauge(BackgroundJobMetrics.Roster, nameof(MeteredInstallJob)));
        Assert.Equal(1, meters.Passes(nameof(MeteredInstallJob), BackgroundJobMetrics.Succeeded));
    }

    // A job whose loop dies before it has a cadence has no cadence to be late against, so it
    // emits no gauge at all — the counter is the only place it can say anything.
    [Fact]
    public async Task A_job_the_runner_could_not_start_is_counted_as_a_failed_pass()
    {
        using var meters = new Meters();
        var logs = new LogRecorder();

        await using var provider = Harness.Build(
            services =>
            {
                Tenants(services, "alfa");
                Job<MeteredJob>(services, new Signal(), new Metered { Interval = TimeSpan.Zero });
            },
            logs);

        await Harness.Run(provider, logs.Awaits(entry => entry.Message.Contains("could not be started", StringComparison.Ordinal)));

        meters.Poll();

        Assert.Equal(1, meters.Passes(nameof(MeteredJob), BackgroundJobMetrics.Failed));
        Assert.Null(meters.Gauge(BackgroundJobMetrics.Roster, nameof(MeteredJob)));
        Assert.Null(meters.Gauge(BackgroundJobMetrics.Overdue, nameof(MeteredJob)));
    }

    // Polling is what reading a gauge IS: the instrument holds no value of its own and answers
    // when it is asked, which is what the exporter does on its own interval.
    static double? Read(Meters meters, string instrument)
    {
        meters.Poll();

        return meters.Gauge(instrument, nameof(MeteredJob));
    }

    static void Tenants(IServiceCollection services, string? roster)
    {
        var tenancy = new TenancyOptions { Mode = TenancyMode.SingleDb, Tenants = roster };

        services.AddSingleton(tenancy);
        services.AddSingleton(new TenantRoster(tenancy));
        services.AddScoped<ResolvedTenancyProvider>();
        services.AddScoped<TenancyProvider>(provider => provider.GetRequiredService<ResolvedTenancyProvider>());
    }

    static void Job<TJob>(IServiceCollection services, Signal signal, Metered? metered = null)
        where TJob : class, IBackgroundJob
    {
        services.AddSingleton(signal);
        services.AddSingleton(metered ?? new Metered());
        services.AddBackgroundJob<TJob>();
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSail.Data;

namespace NSail.Background.Tests;

sealed class Gate
{
    public TaskCompletionSource Open { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

// Holds its pass open until the gate is released, so a second runner ticks while the first is
// mid-pass.
sealed class LockedJob : IBackgroundJob
{
    readonly Gate _gate;
    readonly Signal _signal;

    public LockedJob(Gate gate, Signal signal)
    {
        _gate = gate;
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public TenancyScope Tenancy => TenancyScope.Install;

    public async Task Run(CancellationToken cancellationToken)
    {
        _signal.Record();

        await _gate.Open.Task.WaitAsync(cancellationToken);
    }
}

/// <summary>Two replicas over one database: the pass one holds is not run by the other, and the
/// lock is free again once the pass ends. Over a real Postgres, because the lock
/// is its.</summary>
public sealed class JobLockTests : IAsyncLifetime
{
    readonly string _database = "nsail3410_" + Guid.NewGuid().ToString("N")[..12];

    DbContextOptions<StateDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await Harness.Execute("postgres", $"CREATE DATABASE \"{_database}\"");

        _options = new DbContextOptionsBuilder<StateDbContext>()
            .UseNpgsql(ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_database}"))
            .Options;
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await Harness.Execute("postgres", $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
    }

    [Fact]
    public async Task A_second_runner_skips_the_pass_the_first_holds()
    {
        using var meters = new Meters();
        var gate = new Gate();
        var first = new Signal();
        var second = new Signal();

        await using var one = Build(gate, first);
        var logs = new LogRecorder();

        await using var two = Build(gate, second, logs);

        var runnerOne = Harness.Runner(one);
        var runnerTwo = Harness.Runner(two);

        await runnerOne.StartAsync(CancellationToken.None);
        await first.Reached.WaitAsync(TimeSpan.FromSeconds(10));

        // The second runner's refusal is the signal, so the count is read after it tried.
        var refused = logs.Awaits(entry => entry.Message.Contains("another runner holds its lock", StringComparison.Ordinal));

        await runnerTwo.StartAsync(CancellationToken.None);
        await refused.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, first.Count);
        Assert.Equal(0, second.Count);

        meters.Poll();

        Assert.Equal(1, meters.Passes(nameof(LockedJob), BackgroundJobMetrics.Skipped));
        Assert.Equal(0, meters.Passes(nameof(LockedJob), BackgroundJobMetrics.Failed));

        gate.Open.SetResult();

        await runnerOne.StopAsync(CancellationToken.None);
        await runnerTwo.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_lock_is_free_once_the_pass_ends()
    {
        var gate = new Gate();
        gate.Open.SetResult();

        var first = new Signal();
        var second = new Signal();

        await using var one = Build(gate, first);

        await Harness.Run(one, first.Reached);

        await using var two = Build(gate, second);

        await Harness.Run(two, second.Reached);

        Assert.Equal(1, second.Count);
    }

    // Under a mode that connects per tenant the DbContext's factory throws with no tenant entered,
    // so install-wide work must not be asked for one to lock on.
    [Fact]
    public async Task An_install_job_under_a_per_tenant_connection_with_no_cluster_known_runs_without_a_lock()
    {
        var signal = new Signal();
        var gate = new Gate();
        gate.Open.SetResult();

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton(new TenancyOptions { Mode = TenancyMode.MultiDb });
            services.AddScoped<DbContext>(_ => throw new InvalidOperationException("nothing resolves a tenant"));
            services.AddSingleton(gate);
            services.AddSingleton(signal);
            services.AddBackgroundJob<LockedJob>();
        });

        await Harness.Run(provider, signal.Reached);
    }

    // The maintenance database is the cluster's: a sibling of the same cell yields, a neighbouring
    // cell's pass of the same job is not its sibling.
    [Fact]
    public async Task An_install_job_under_a_per_tenant_connection_locks_on_the_cluster_per_cell()
    {
        var gate = new Gate();
        var first = new Signal();
        var sameCell = new Signal();
        var otherCell = new Signal();

        await using var one = BuildPerTenant("eu1", gate, first);
        await using var twin = BuildPerTenant("eu1", gate, sameCell);
        await using var neighbour = BuildPerTenant("us1", gate, otherCell);

        var runnerOne = Harness.Runner(one);
        var runnerTwin = Harness.Runner(twin);
        var runnerNeighbour = Harness.Runner(neighbour);

        await runnerOne.StartAsync(CancellationToken.None);
        await first.Reached.WaitAsync(TimeSpan.FromSeconds(10));

        await runnerNeighbour.StartAsync(CancellationToken.None);
        await otherCell.Reached.WaitAsync(TimeSpan.FromSeconds(10));

        await runnerTwin.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Equal(1, first.Count);
        Assert.Equal(1, otherCell.Count);
        Assert.Equal(0, sameCell.Count);

        gate.Open.SetResult();

        await runnerOne.StopAsync(CancellationToken.None);
        await runnerTwin.StopAsync(CancellationToken.None);
        await runnerNeighbour.StopAsync(CancellationToken.None);
    }

    ServiceProvider BuildPerTenant(string cell, Gate gate, Signal signal)
    {
        var tenancy = new TenancyOptions { Mode = TenancyMode.MultiDb, Product = "nsail3410", Cell = cell };

        return Harness.Build(services =>
        {
            services.AddSingleton(tenancy);
            services.AddSingleton(new TenantDatabases(tenancy, "Host=localhost;Username=postgres;Database=postgres"));
            services.AddScoped<DbContext>(_ => throw new InvalidOperationException("nothing resolves a tenant"));
            services.AddSingleton(gate);
            services.AddSingleton(signal);
            services.AddBackgroundJob<LockedJob>();
        });
    }

    ServiceProvider Build(Gate gate, Signal signal, LogRecorder? logs = null)
    {
        return Harness.Build(services =>
        {
            services.AddScoped<DbContext>(_ => new StateDbContext(_options));
            services.AddSingleton(gate);
            services.AddSingleton(signal);
            services.AddBackgroundJob<LockedJob>();
        }, logs);
    }
}

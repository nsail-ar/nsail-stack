// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSail.Data;

namespace NSail.Background.Tests;

/// <summary>What happened to a pass, in order: its end and its scope's disposal.</summary>
sealed class Ledger
{
    readonly List<string> _events = [];
    readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<string> Events
    {
        get
        {
            lock (_events)
            {
                return _events.ToList();
            }
        }
    }

    public Task Released => _released.Task;

    public void Add(string what)
    {
        lock (_events)
        {
            _events.Add(what);
        }

        if (what == "released")
        {
            _released.TrySetResult();
        }
    }
}

// Scoped beside the job's DbContext, and disposed with it: it says when the scope went.
sealed class Witness : IAsyncDisposable
{
    readonly Ledger _ledger;

    public Witness(Ledger ledger)
    {
        _ledger = ledger;
    }

    public bool Busy { get; set; }

    // A resource still in use refuses its dispose, as Npgsql's connection does mid-command.
    public ValueTask DisposeAsync()
    {
        if (Busy)
        {
            throw new InvalidOperationException("disposed while in use");
        }

        _ledger.Add("released");

        return ValueTask.CompletedTask;
    }
}

// Inside a database call that ignores its token when the host stops. What only implements
// IAsyncDisposable is resolved inside Run: the scope that reads the job's cadence is disposed
// synchronously.
sealed class SleepingJob : IBackgroundJob
{
    readonly IServiceProvider _services;
    readonly Ledger _ledger;
    readonly Signal _signal;

    public SleepingJob(IServiceProvider services, Ledger ledger, Signal signal)
    {
        _services = services;
        _ledger = ledger;
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public TenancyScope Tenancy => TenancyScope.Install;

    public async Task Run(CancellationToken cancellationToken)
    {
        var db = _services.GetRequiredService<DbContext>();

        _services.GetRequiredService<Witness>();
        _signal.Record();

        await db.Database.ExecuteSqlRawAsync("SELECT pg_sleep(1)", CancellationToken.None);

        _ledger.Add("ended");
    }
}

sealed class FaultyRelease : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        throw new InvalidOperationException("this scope cannot be released");
    }
}

sealed class FaultyReleaseJob : IBackgroundJob
{
    readonly IServiceProvider _services;
    readonly Signal _signal;

    public FaultyReleaseJob(IServiceProvider services, Signal signal)
    {
        _services = services;
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromMilliseconds(50);

    public TenancyScope Tenancy => TenancyScope.Install;

    public Task Run(CancellationToken cancellationToken)
    {
        _services.GetRequiredService<FaultyRelease>();
        _signal.Record();

        return Task.CompletedTask;
    }
}

sealed class QuietJob : IBackgroundJob
{
    readonly Signal _signal;

    public QuietJob(Signal signal)
    {
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public TenancyScope Tenancy => TenancyScope.Install;

    public Task Run(CancellationToken cancellationToken)
    {
        _signal.Record();

        return Task.CompletedTask;
    }
}

/// <summary>A host stop leaves work running that the runner no longer waits for: its scope is
/// released when it ends, and nothing of it escapes a loop.</summary>
public sealed class HostStopTests : IAsyncLifetime
{
    static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    readonly string _database = "nsail4518_" + Guid.NewGuid().ToString("N")[..12];

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
    public async Task A_pass_inside_a_database_call_when_the_host_stops_keeps_its_scope_until_it_ends()
    {
        var logs = new LogRecorder();
        var ledger = new Ledger();
        var signal = new Signal();

        await using var provider = Harness.Build(
            services =>
            {
                services.AddScoped<DbContext>(_ => new StateDbContext(_options));
                services.AddScoped<Witness>();
                services.AddSingleton(ledger);
                services.AddSingleton(signal);
                services.AddBackgroundJob<SleepingJob>();
            },
            logs);

        var runner = Harness.Runner(provider);

        await runner.StartAsync(CancellationToken.None);
        await signal.Reached.WaitAsync(_bound);
        await Sleeping();
        await runner.StopAsync(CancellationToken.None);
        await ledger.Released.WaitAsync(_bound);

        Assert.Empty(logs.Entries.Where(entry => entry.Level >= LogLevel.Error).Select(entry => entry.Message));
        Assert.Equal(["ended", "released"], ledger.Events);
    }

    [Fact]
    public async Task A_pass_whose_release_faults_is_counted_failed_and_logged_and_the_next_tick_runs()
    {
        using var meters = new Meters();

        var logs = new LogRecorder();
        var signal = new Signal { Target = 2 };

        await using var provider = Harness.Build(
            services =>
            {
                services.AddScoped<FaultyRelease>();
                services.AddSingleton(signal);
                services.AddBackgroundJob<FaultyReleaseJob>();
            },
            logs);

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.True(meters.Passes(nameof(FaultyReleaseJob), BackgroundJobMetrics.Failed) >= 1);
        Assert.Equal(0, meters.Passes(nameof(FaultyReleaseJob), BackgroundJobMetrics.Succeeded));

        var error = Assert.Single(logs.Entries.Where(entry => entry.Level == LogLevel.Error).DistinctBy(entry => entry.Message));

        Assert.Contains("could not release its pass", error.Message);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }

    [Fact]
    public async Task Deferred_work_running_when_the_host_stops_keeps_its_scope_and_the_queue_ends_quietly()
    {
        var logs = new LogRecorder();
        var ledger = new Ledger();
        var started = new Signal();
        var gate = new Gate();

        await using var provider = Harness.Build(
            services =>
            {
                services.AddScoped<Witness>();
                services.AddSingleton(ledger);
                services.AddDeferredWork();
            },
            logs);

        provider.GetRequiredService<DeferredWork>().Enqueue("Tests.Stopped", Tenant.None, async (scope, _) =>
        {
            var witness = scope.GetRequiredService<Witness>();

            witness.Busy = true;
            started.Record();

            await gate.Open.Task;

            witness.Busy = false;
            ledger.Add("ended");
        });

        var runner = (BackgroundService)Harness.Runner(provider);

        await runner.StartAsync(CancellationToken.None);
        await started.Reached.WaitAsync(_bound);
        await runner.StopAsync(CancellationToken.None);

        gate.Open.SetResult();

        await ledger.Released.WaitAsync(_bound);

        Assert.Equal(["ended", "released"], ledger.Events);
        Assert.False(runner.ExecuteTask!.IsFaulted);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task A_stop_with_no_pass_in_flight_ends_the_loop_quietly()
    {
        var logs = new LogRecorder();
        var signal = new Signal();

        await using var provider = Harness.Build(
            services =>
            {
                services.AddSingleton(signal);
                services.AddBackgroundJob<QuietJob>();
            },
            logs);

        await Harness.Run(provider, signal.Reached);

        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    // The pass is inside its query once the server runs it, not once the job asked for it.
    async Task Sleeping()
    {
        await using var admin = new NpgsqlConnection(
            ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres;Pooling=false"));

        await admin.OpenAsync();

        await using var count = new NpgsqlCommand(
            $"SELECT count(*) FROM pg_stat_activity WHERE datname = '{_database}' AND state = 'active' AND query LIKE '%pg_sleep%'",
            admin);

        var deadline = Stopwatch.StartNew();

        while ((long)(await count.ExecuteScalarAsync())! == 0)
        {
            Assert.True(deadline.Elapsed < _bound, $"the pass did not reach its query within {_bound.TotalSeconds} seconds.");

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }
}

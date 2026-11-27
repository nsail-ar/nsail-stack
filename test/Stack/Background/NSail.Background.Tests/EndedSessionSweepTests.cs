// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSail.Data;

namespace NSail.Background.Tests;

sealed class EndedSessionJob : IBackgroundJob
{
    readonly DbContext _db;
    readonly Signal _signal;

    public EndedSessionJob(DbContext db, Signal signal)
    {
        _db = db;
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public TenancyScope Tenancy => TenancyScope.Tenant;

    public async Task Run(CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

        _signal.Record();
    }
}

/// <summary>A tenant's pass whose first rented connection the server already ended: the
/// tenant's state is the first thing it reads, and the tenant setting is the first statement on
/// that connection. Wired through AddDataAccess, because the setting is what the seam writes on
/// every open and a hand-built context would not carry it. Run alone: another class clearing the
/// pools between the terminate and the sweep would hand the pass a fresh session.</summary>
[Collection(nameof(EndedSessionSweepTests))]
public sealed class EndedSessionSweepTests : IAsyncLifetime
{
    readonly string _database = "nsail4431_" + Guid.NewGuid().ToString("N")[..12];

    string _connection = null!;

    public async Task InitializeAsync()
    {
        await Harness.Execute("postgres", $"CREATE DATABASE \"{_database}\"");

        _connection = ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_database}");

        await using var db = new StateDbContext(new DbContextOptionsBuilder<StateDbContext>().UseNpgsql(_connection).Options);

        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearPool(new NpgsqlConnection(_connection));

        await Harness.Execute("postgres", $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
    }

    [Fact]
    public async Task A_pass_whose_pooled_session_the_server_ended_reads_the_state_and_runs()
    {
        await using (var warm = Build(new Signal()))
        {
            await Harness.Run(warm, warm.GetRequiredService<Signal>().Reached);
        }

        await Harness.Terminate(_database);

        using var meters = new Meters();
        var logs = new LogRecorder();
        var signal = new Signal();

        await using var provider = Build(signal, logs);

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.Equal(1, meters.Passes(nameof(EndedSessionJob), BackgroundJobMetrics.Succeeded));
        Assert.Equal(0, meters.Passes(nameof(EndedSessionJob), BackgroundJobMetrics.Failed));
        Assert.DoesNotContain(logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    ServiceProvider Build(Signal signal, LogRecorder? logs = null)
    {
        return Harness.Build(services =>
        {
            services.AddDataAccess<StateDbContext>(_connection, new TenancyOptions { Mode = TenancyMode.SingleDb, Tenants = "alfa" });

            // The wall's startup check is not this suite's subject, and the harness runs the
            // one hosted service the runner is.
            services.RemoveAll<IHostedService>();

            services.AddSingleton(signal);
            services.AddBackgroundJob<EndedSessionJob>();
        }, logs);
    }
}

[CollectionDefinition(nameof(EndedSessionSweepTests), DisableParallelization = true)]
public sealed class EndedSessionSweepCollection
{
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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
/// lock is free again once the pass ends. Over a real Postgres, because the lock is its.</summary>
public sealed class JobLockTests : IAsyncLifetime
{
    readonly string _database = "nsail3410_" + Guid.NewGuid().ToString("N")[..12];

    DbContextOptions<StateDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await Execute("postgres", $"CREATE DATABASE \"{_database}\"");

        _options = new DbContextOptionsBuilder<StateDbContext>()
            .UseNpgsql(ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_database}"))
            .Options;
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await Execute("postgres", $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
    }

    [Fact]
    public async Task A_second_runner_skips_the_pass_the_first_holds()
    {
        var gate = new Gate();
        var first = new Signal();
        var second = new Signal();

        await using var one = Build(gate, first);
        await using var two = Build(gate, second);

        var runnerOne = Runner(one);
        var runnerTwo = Runner(two);

        await runnerOne.StartAsync(CancellationToken.None);
        await first.Reached.WaitAsync(TimeSpan.FromSeconds(10));

        // StartAsync returns once the loop has begun, so the second runner's up-front tick is
        // given its time before the count is read.
        await runnerTwo.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.Equal(1, first.Count);
        Assert.Equal(0, second.Count);

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

    static IHostedService Runner(ServiceProvider provider)
    {
        return provider.GetServices<IHostedService>().Single(h => h.GetType().Name != "SecurityProviderCheck");
    }

    ServiceProvider Build(Gate gate, Signal signal)
    {
        return Harness.Build(services =>
        {
            services.AddScoped<DbContext>(provider => new StateDbContext(_options));
            services.AddSingleton(gate);
            services.AddSingleton(signal);
            services.AddBackgroundJob<LockedJob>();
        });
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

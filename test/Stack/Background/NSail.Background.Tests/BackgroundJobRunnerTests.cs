// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;

namespace NSail.Background.Tests;

sealed class CountingJob : IBackgroundJob
{
    readonly Signal _signal;

    public CountingJob(Signal signal)
    {
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromMilliseconds(20);

    public TenancyScope Tenancy => TenancyScope.Install;

    public Task Run(CancellationToken cancellationToken)
    {
        _signal.Record();

        return Task.CompletedTask;
    }
}

sealed class ThrowingJob : IBackgroundJob
{
    readonly Signal _signal;

    public ThrowingJob(Signal signal)
    {
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromMilliseconds(20);

    public TenancyScope Tenancy => TenancyScope.Install;

    public Task Run(CancellationToken cancellationToken)
    {
        if (_signal.Record() == 1)
            throw new InvalidOperationException("the first tick fails");

        return Task.CompletedTask;
    }
}

// Interval is deliberately far outside the harness's 10-second bound, mirroring
// BnaRateJob's hour and HorizonJob's six hours: the only way this job's signal can be
// reached before the bound times out is a first tick that runs before the PeriodicTimer
// is ever awaited.
sealed class SlowCadenceJob : IBackgroundJob
{
    readonly Signal _signal;

    public SlowCadenceJob(Signal signal)
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

sealed class ProbingJob : IBackgroundJob
{
    readonly Signal _signal;
    readonly Seen _seen;
    readonly Scoped _scoped;

    public ProbingJob(Signal signal, Seen seen, Scoped scoped)
    {
        _signal = signal;
        _seen = seen;
        _scoped = scoped;
    }

    public TimeSpan Interval => TimeSpan.FromMilliseconds(20);

    public TenancyScope Tenancy => TenancyScope.Install;

    public Task Run(CancellationToken cancellationToken)
    {
        _seen.Add(_scoped.Id);
        _signal.Record();

        return Task.CompletedTask;
    }
}

public sealed class BackgroundJobRunnerTests
{
    [Fact]
    public async Task A_job_runs_again_on_its_next_tick()
    {
        var signal = new Signal { Target = 2 };

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton(signal);
            services.AddBackgroundJob<CountingJob>();
        });

        await Harness.Run(provider, signal.Reached);

        Assert.True(signal.Count >= 2, $"the job ran {signal.Count} time(s): the cadence produced no second tick.");
    }

    [Fact]
    public async Task A_job_s_first_execution_happens_promptly_not_after_a_full_interval()
    {
        var signal = new Signal();

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton(signal);
            services.AddBackgroundJob<SlowCadenceJob>();
        });

        await Harness.Run(provider, signal.Reached);

        Assert.Equal(1, signal.Count);
    }

    [Fact]
    public async Task A_job_that_throws_does_not_take_the_runner_with_it()
    {
        var signal = new Signal { Target = 2 };

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton(signal);
            services.AddBackgroundJob<ThrowingJob>();
        });

        await Harness.Run(provider, signal.Reached);

        Assert.True(signal.Count >= 2, "the tick after the failing one never happened: the loop died with the job.");
    }

    [Fact]
    public async Task A_job_that_throws_is_logged_at_error_with_its_exception()
    {
        var signal = new Signal { Target = 2 };
        var logs = new LogRecorder();

        await using var provider = Harness.Build(
            services =>
            {
                services.AddSingleton(signal);
                services.AddBackgroundJob<ThrowingJob>();
            },
            logs);

        await Harness.Run(provider, signal.Reached);

        var error = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);

        Assert.Contains(nameof(ThrowingJob), error.Message);
        Assert.Equal("the first tick fails", Assert.IsType<InvalidOperationException>(error.Exception).Message);
    }

    [Fact]
    public async Task Each_tick_resolves_the_job_from_a_scope_of_its_own()
    {
        var signal = new Signal { Target = 2 };
        var seen = new Seen();

        await using var provider = Harness.Build(services =>
        {
            services.AddSingleton(signal);
            services.AddSingleton(seen);
            services.AddScoped<Scoped>();
            services.AddBackgroundJob<ProbingJob>();
        });

        await Harness.Run(provider, signal.Reached);

        Assert.True(seen.Ids.Count >= 2, $"only {seen.Ids.Count} run(s) were observed.");
        Assert.Equal(seen.Ids.Count, seen.Ids.Distinct().Count());
    }
}

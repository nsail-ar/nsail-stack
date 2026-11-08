// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Data;

namespace NSail.Background.Tests;

// Hangs for the tenant it is told to and never looks at its token: only the runner's own
// WaitAsync can free the sweep.
sealed class HangingJob : IBackgroundJob
{
    readonly TenancyProvider _tenancy;
    readonly Swept _swept;
    readonly Signal _signal;

    public HangingJob(TenancyProvider tenancy, Swept swept, Signal signal)
    {
        _tenancy = tenancy;
        _swept = swept;
        _signal = signal;
    }

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public TenancyScope Tenancy => TenancyScope.Tenant;

    public Task Run(CancellationToken cancellationToken)
    {
        var slug = _tenancy.Current.Slug;

        if (slug == "alfa")
        {
            return new TaskCompletionSource().Task;
        }

        _swept.Add(slug);
        _signal.Record();

        return Task.CompletedTask;
    }
}

sealed class TickingHangingJob : IBackgroundJob
{
    readonly TenancyProvider _tenancy;
    readonly Swept _swept;

    public TickingHangingJob(TenancyProvider tenancy, Swept swept)
    {
        _tenancy = tenancy;
        _swept = swept;
    }

    public TimeSpan Interval => TimeSpan.FromMilliseconds(50);

    public TenancyScope Tenancy => TenancyScope.Tenant;

    public Task Run(CancellationToken cancellationToken)
    {
        var slug = _tenancy.Current.Slug;

        _swept.Add(slug);

        return slug == "alfa" ? new TaskCompletionSource().Task : Task.CompletedTask;
    }
}

public sealed class JobTimeoutTests
{
    [Fact]
    public async Task A_tenant_whose_abandoned_pass_still_runs_is_skipped_and_the_others_keep_running()
    {
        var swept = new Swept();
        var logs = new LogRecorder();

        await using var provider = Harness.Build(
            services =>
            {
                var tenancy = new TenancyOptions { Mode = TenancyMode.SingleDb, Tenants = "alfa,beta" };

                services.AddSingleton(tenancy);
                services.AddSingleton(new TenantRoster(tenancy));
                services.AddScoped<ResolvedTenancyProvider>();
                services.AddScoped<TenancyProvider>(p => p.GetRequiredService<ResolvedTenancyProvider>());
                services.AddSingleton(swept);
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Background:JobTimeouts:TickingHangingJob"] = "00:00:00.100" })
                    .Build());
                services.AddBackgroundJob<TickingHangingJob>();
            },
            logs);

        var skipped = logs.Awaits(entry => entry.Level == LogLevel.Error && entry.Message.Contains("skipped tenant alfa"));

        await Harness.Run(provider, skipped);

        Assert.Equal(1, swept.Slugs.Count(slug => slug == "alfa"));
        Assert.True(swept.Slugs.Count(slug => slug == "beta") >= 2);
    }

    [Fact]
    public async Task A_pass_that_never_returns_is_abandoned_counted_failed_and_the_next_tenant_runs()
    {
        using var meters = new Meters();

        var swept = new Swept();
        var signal = new Signal();
        var logs = new LogRecorder();

        await using var provider = Harness.Build(
            services =>
            {
                var tenancy = new TenancyOptions { Mode = TenancyMode.SingleDb, Tenants = "alfa,beta" };

                services.AddSingleton(tenancy);
                services.AddSingleton(new TenantRoster(tenancy));
                services.AddScoped<ResolvedTenancyProvider>();
                services.AddScoped<TenancyProvider>(p => p.GetRequiredService<ResolvedTenancyProvider>());
                services.AddSingleton(swept);
                services.AddSingleton(signal);
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Background:JobTimeouts:HangingJob"] = "00:00:00.200" })
                    .Build());
                services.AddBackgroundJob<HangingJob>();
            },
            logs);

        await Harness.Run(provider, signal.Reached);

        Assert.Equal(["beta"], swept.Slugs);

        meters.Poll();

        Assert.Equal(1, meters.Passes("HangingJob", BackgroundJobMetrics.Failed));

        var error = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);

        Assert.Contains("HangingJob", error.Message);
        Assert.Contains("alfa", error.Message);
    }

    [Fact]
    public async Task Deferred_work_that_ignores_its_token_is_abandoned_logged_and_the_queue_continues()
    {
        using var meters = new Meters();

        var logs = new LogRecorder();
        var signal = new Signal();

        await using var provider = Harness.Build(
            services =>
            {
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Background:DeferredTimeout"] = "00:00:00.200" })
                    .Build());
                services.AddDeferredWork();
            },
            logs);

        var queue = provider.GetRequiredService<DeferredWork>();

        queue.Enqueue("Tests.Hung", Tenant.None, (_, _) => new TaskCompletionSource().Task);
        queue.Enqueue("Tests.After", Tenant.None, (_, _) =>
        {
            signal.Record();

            return Task.CompletedTask;
        });

        await Harness.Run(provider, signal.Reached);

        meters.Poll();

        Assert.Equal(1, meters.Items("Tests.Hung", DeferredWorkMetrics.TimedOut));
        Assert.Equal(1, meters.Items("Tests.After", DeferredWorkMetrics.Succeeded));
        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("abandoned"));
    }

    [Fact]
    public void A_job_timeout_outside_its_bounds_is_refused()
    {
        var options = new BackgroundOptions { JobTimeouts = { ["X"] = TimeSpan.Zero } };

        Assert.NotEmpty(options.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(options)));
    }
}

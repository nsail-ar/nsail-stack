// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSail.Configuration;
using NSail.Data;
using NSail.Localization;
using NSail.Security;

namespace NSail.Background;

// One independent loop per registered job, so a slow or failing job delays only its own
// cadence and never another's.
//
// Single instance is an assumption, not a property. deploy/tenant-template/compose.yml runs
// one container per tenant, so nothing runs twice today — a second replica would silently
// double every job, with no error anywhere to say so. A Postgres advisory lock taken around
// the tick is the cheap fix the day that topology changes.
sealed class BackgroundJobRunner : BackgroundService
{
    readonly ConcurrentDictionary<Type, byte> _emptyRosterSaid = new();

    // Its own signal beside the log lines, and not a duplicate of them: the export seam drops
    // every string attribute a log record carries (LogScrubber), so the line naming the job
    // reaches the backend with the job cut out of it and no alert could name what broke.
    readonly JobMeter _meter = new();

    readonly IServiceScopeFactory _scopes;
    readonly BackgroundJobs _jobs;
    readonly ILogger<BackgroundJobRunner> _logger;
    readonly TenantRoster? _tenants;
    readonly CultureInfo? _culture;

    // The roster is optional because only a mode that resolves tenants registers one, and this
    // runner is composed by every host: nothing registered means an install with a single scope,
    // which is what every job ran under before any of them were per-tenant.
    //
    // The configuration is optional for the harness that composes no host; a host always has
    // one, and it is where the install's language lives.
    public BackgroundJobRunner(
        IServiceScopeFactory scopes,
        BackgroundJobs jobs,
        ILogger<BackgroundJobRunner> logger,
        TenantRoster? tenants = null,
        IConfiguration? configuration = null)
    {
        _scopes = scopes;
        _jobs = jobs;
        _logger = logger;
        _tenants = tenants;
        _culture = configuration is null ? null : CultureInfo.GetCultureInfo(configuration.Load<LanguageOptions>().Default);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(_jobs.Types.Select(jobType => Loop(jobType, stoppingToken)));
    }

    public override void Dispose()
    {
        _meter.Dispose();

        base.Dispose();
    }

    async Task Loop(Type jobType, CancellationToken stoppingToken)
    {
        // Exactly what the request edge does (UseRequestLanguage), from the one place that has
        // no request: a job runs in the install's default language. Without it the loop runs
        // in the invariant culture, LanguageProvider seeds itself with "iv", and every send a
        // job makes looks for a template in a language nobody has — the reminder that never
        // reached a phone (nsail#1108). Per loop, and so per async flow: nothing here outlives
        // the job or reaches a request.
        if (_culture is { } culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        try
        {
            var declared = Declared(jobType);

            _meter.Scheduled(jobType.Name, declared.Interval);

            using var timer = new PeriodicTimer(declared.Interval);

            // WaitForNextTickAsync waits before it ticks, so a loop that started with the
            // while would run its job for the first time one whole Interval after boot — an
            // hour of silence for BnaRateJob, six for HorizonJob. This tick runs once, up
            // front, before the timer is ever awaited.
            await Tick(jobType, declared.Tenancy, stoppingToken).ConfigureAwait(false);

            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await Tick(jobType, declared.Tenancy, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            // Caught here rather than left to escape: the loops are joined by a WhenAll that
            // only completes at shutdown, so an exception thrown out of one would sit
            // unobserved in that task until the host stopped, and this job's silence would be
            // the only symptom.
            _logger.LogError(exception, "Background job {Job} could not be started and will not run.", jobType.Name);

            // Counted as a failed pass because it is the one failure the gauges cannot carry:
            // a job that never got a cadence has no cadence to be late against, so it would
            // emit no roster and no overdue and the standing rules would never see it.
            _meter.Passed(jobType.Name, succeeded: false);
        }
    }

    // One tick is one pass for an install-wide job and one pass per tenant for the other kind.
    // The job never enters a tenant itself: it is written as if there were one, and this is the
    // one place that decides how many there are.
    async Task Tick(Type jobType, TenancyScope tenancy, CancellationToken stoppingToken)
    {
        if (tenancy == TenancyScope.Install || _tenants is null)
        {
            _meter.Swept(jobType.Name, passes: 1);

            await Run(jobType, tenant: null, stoppingToken).ConfigureAwait(false);

            return;
        }

        IReadOnlyList<Tenant> roster;

        try
        {
            roster = await _tenants.Tenants(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The tenant roster could not be read, so background job {Job} ran for nobody this tick.", jobType.Name);

            // The same number an empty roster reports, because it is the same outcome for the
            // job: this tick entered nobody. What tells the two apart is the error beside it.
            _meter.Swept(jobType.Name, passes: 0);

            return;
        }

        _meter.Swept(jobType.Name, roster.Count);

        if (roster.Count == 0 && _emptyRosterSaid.TryAdd(jobType, 0))
        {
            // Said once rather than every tick: an install with no tenants is a standing
            // condition, not an event, and a per-tick repeat would bury the errors this job's
            // own failures are logged as.
            _logger.LogWarning(
                "The tenant roster is empty, so per-tenant background job {Job} runs for nobody. An install whose rows belong to tenants declares them in Tenancy:Tenants; one that connects per tenant has no provisioned database.",
                jobType.Name);
        }

        foreach (var tenant in roster)
        {
            // Sequential, and each pass is awaited whole: a tenant that fails must not cut the
            // sweep, and running them together would multiply one job's load on the database by
            // the size of the roster for no cadence anybody asked for.
            await Run(jobType, tenant, stoppingToken).ConfigureAwait(false);
        }
    }

    // Its own async method, and that is load-bearing twice over: the tenant is entered on an
    // AsyncLocal, which an async method's execution context does not carry back to its caller,
    // so the next tenant on the sweep starts from none — and the catch is per pass, so one
    // tenant's failure costs that tenant's tick and nobody else's.
    async Task Run(Type jobType, Tenant? tenant, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = Scope();

            if (tenant is not null)
            {
                // Exactly what the request edge does (TenancyMiddleware), from the one place
                // that has no request. What is deliberately NOT done here is the first touch
                // beside it: a job provisions nothing, so a tenant whose schema or rows no
                // request has ever asked for fails this pass, loudly, instead of having a
                // migration applied by a clock.
                scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(tenant);
            }

            await ((IBackgroundJob)scope.ServiceProvider.GetRequiredService(jobType)).Run(stoppingToken).ConfigureAwait(false);

            _meter.Passed(jobType.Name, succeeded: true);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _meter.Passed(jobType.Name, succeeded: false);

            // Every occurrence, not the first one only. A job failing on a minutes-scale
            // cadence is not log spam, and degrading the repeats to Debug — the shape used
            // for deliberate degradations elsewhere — is how a broken job stays broken with
            // nobody looking.
            if (tenant is null)
            {
                _logger.LogError(exception, "Background job {Job} failed; the runner continues on its next tick.", jobType.Name);
            }
            else
            {
                _logger.LogError(
                    exception,
                    "Background job {Job} failed for tenant {Tenant}; the sweep continues with the next tenant.",
                    jobType.Name,
                    tenant.Slug);
            }
        }
    }

    (TimeSpan Interval, TenancyScope Tenancy) Declared(Type jobType)
    {
        using var scope = Scope();

        var job = (IBackgroundJob)scope.ServiceProvider.GetRequiredService(jobType);

        if (job.Interval <= TimeSpan.Zero)
            throw new InvalidOperationException($"Background job {jobType.Name} declares a non-positive Interval ({job.Interval}) — a cadence is what the runner has to schedule on.");

        return (job.Interval, job.Tenancy);
    }

    // The one place a job's session is decided. A job acts for nobody, so the deny-by-default
    // gate would refuse every send it makes with the anonymous default; Session.System() is
    // the seam NSail.Security already carries for exactly this, short-circuited to allowed
    // inside SecurityManager rather than modelled as a role or an audience to be granted.
    //
    // It holds no OrganizationId, no PartyId and no memberships. A job passes every id
    // explicitly in the messages it sends and must never lean on ambient organization
    // context: a handler or an audience keyed on @current sees nothing here.
    AsyncServiceScope Scope()
    {
        var scope = _scopes.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<SessionProvider>().Session = Session.System();

        return scope;
    }
}

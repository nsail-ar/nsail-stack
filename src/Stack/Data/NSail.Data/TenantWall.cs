// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NSail.Data;

// A forced policy walls every role but the two Postgres lets through: a superuser, and a role
// carrying BYPASSRLS. An install whose app connects as either has the DDL and none of the wall,
// and the defect is silent — every query answers exactly as it did before. So the install is
// asked once, at startup, and says what it found.
//
// The answer is a warning and a metric by default, because provisioning the role is an operator
// pass on a live box and the pin that carries the policies must not take a cell down for work
// nobody has been given yet. A cell that HAS done that pass says so (Tenancy:RequireRowSecurity)
// and gets the refusal instead of the warning.
sealed class TenantWall : IHostedService
{
    const string Bypasses = "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user";

    // Not asked yet, which is not the same answer as "the wall is off": the gauge reports
    // nothing until startup has asked, so the alert on zero cannot fire on a boot.
    const int Unasked = -1;

    readonly IServiceScopeFactory _scopes;
    readonly TenancyOptions _tenancy;
    readonly ILogger<TenantWall> _log;

    int _stands = Unasked;

    // The meter comes from the container's factory and not from `new Meter`: the install is one
    // process and one wall, so the gauge carries no attribute to tell two of them apart, and the
    // factory is what ties this instrument to the container that composed it — which is also how
    // a suite reads THIS install's answer rather than another install's in the same process.
    public TenantWall(IServiceScopeFactory scopes, TenancyOptions tenancy, ILogger<TenantWall> log, IMeterFactory meters)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(tenancy);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(meters);

        _scopes = scopes;
        _tenancy = tenancy;
        _log = log;

        var meter = meters.Create(new MeterOptions(TenantPolicies.MeterName));

        meter.CreateObservableGauge(
            TenantPolicies.Standing,
            Observe,
            description: "Whether the app's Postgres role is subject to the tenant policies: 1 walled, 0 able to bypass.");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        await db.Database.OpenConnectionAsync(cancellationToken);

        bool bypasses;

        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();

            command.CommandText = Bypasses;

            bypasses = await command.ExecuteScalarAsync(cancellationToken) is true;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        _stands = bypasses ? 0 : 1;

        if (!bypasses)
        {
            return;
        }

        if (_tenancy.RequireRowSecurity)
        {
            throw new InvalidOperationException(
                "Tenancy:RequireRowSecurity is on and this install's Postgres role can bypass row-level security, so the tenant policies are DDL with nothing behind them. Connect as a role that is neither a superuser nor BYPASSRLS, or clear the switch and accept the query filter as the only wall.");
        }

        _log.LogWarning(
            "The tenant policies are in the schema but this install's Postgres role can bypass row-level security, so the EF query filter is the only wall between tenants. Provision a role that is neither a superuser nor BYPASSRLS, point the connection string at it, and turn Tenancy:RequireRowSecurity on.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    // Nothing is reported before the question has been asked: a gauge that read 0 during startup
    // would fire the alert that the wall is off on every deploy.
    IEnumerable<Measurement<int>> Observe()
    {
        return _stands == Unasked ? [] : [new Measurement<int>(_stands)];
    }
}

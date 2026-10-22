// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSail.Data;
using NSail.Data.Testing;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The second wall: a forced row policy per tenant-scoped table, over a connection
/// that says whose rows it may touch. Under <c>SingleDb</c> the query filter is the first wall
/// and a forgotten one is a leak; what is measured here is that the same forgotten filter is a
/// query returning nothing instead.
///
/// <para>So this suite — and only this suite — connects as a role that is neither a superuser
/// nor <c>BYPASSRLS</c>: Postgres lets either of those through whatever a table says, and every
/// other suite in the house connects as <c>postgres</c>. Re-pointing the shared test database
/// would change how every suite connects for no proof gained.</para></summary>
public sealed class TenantPolicyWallTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    // The suite's own premise, asserted rather than assumed: every claim below is about a role
    // the policies apply to, and against a superuser every one of them would pass by accident
    // in the other direction.
    [Fact]
    public async Task TheAppRunsAsARoleThePoliciesApplyTo()
    {
        Assert.False(await _host.RoleCanBypass());
    }

    // The sweep a product's own suite runs over its chain: the model says which tables owe a
    // policy and the migrated database says which have one. A table a later migration adds and
    // does not wall reds here, which is what keeps the chain's hand-written list true.
    [Fact]
    public async Task EveryTenantScopedTableOfTheChainIsWalled()
    {
        var unwalled = await Unwalled();

        // Asserted through the sweep's own sentence, which is what a product's suite prints when
        // this reds: the call to paste into the migration that forgot it.
        Assert.True(unwalled.Count is 0, TenantPolicyAudit.Describe("The Stack's tenancy chain", unwalled));
    }

    // The acceptance criterion, in one test. The query carries no filter at all — raw SQL, so
    // the predicate was never compiled in rather than merely skipped — and what comes back is
    // what Postgres decided to show this connection.
    [Fact]
    public async Task AQueryThatBypassesTheFilterStillReadsOnlyItsOwnTenant()
    {
        await Write("lumina", "lumina's note");
        await Write("vision", "vision's note");

        Assert.Equal(["lumina's note"], await Unfiltered("/notes/unfiltered", "lumina"));
        Assert.Equal(["vision's note"], await Unfiltered("/notes/unfiltered", "vision"));

        // And both rows are there to be missed: the answers above are the policy, not an empty
        // table.
        Assert.Equal(2, await Rows("Notes"));
    }

    // The write half of the same policy. The stamp already overwrites whatever a caller put on
    // the tracker; this is the attempt that never reaches the tracker at all.
    [Fact]
    public async Task AWriteThatBypassesTheStampCannotNameAnotherTenant()
    {
        using var response = await _host.Client.SendAsync(
            Post($"/notes/unfiltered?text=forged&tenant={Tenant.For("vision").RowKey}", "lumina"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await Rows("Notes"));
    }

    // A scope that resolved no tenant writes no setting at all, so the policy compares against
    // NULL and no row equals it. The same sentence the filter already says, said by Postgres —
    // which is what makes it true of a query that forgot the filter.
    [Fact]
    public async Task AnUnfilteredQueryUnderNoTenantReadsNothing()
    {
        await Write("lumina", "lumina's note");

        await using var scope = _host.Services.CreateAsyncScope();

        var unscoped = scope.ServiceProvider.GetRequiredService<DbContext>();

        Assert.Empty(await Unfiltered(unscoped));
        Assert.Equal(1, await Rows("Notes"));
    }

    // Migrations keep working, which under a forced policy is not free: the chain's own INSERTs
    // write rows that are nobody's tenant, from a startup scope that resolved none. The install's
    // own pass says so on its connection, and the row is in the table.
    [Fact]
    public async Task TheChainsOwnRowsAreWritten()
    {
        Assert.Equal(1, await Rows("Journals"));
    }

    // And they stay nobody's: the install's unstamped rows match no tenant's policy either, so a
    // tenant reads none of them even past the filter.
    [Theory]
    [InlineData("lumina")]
    [InlineData("vision")]
    public async Task NoTenantReadsTheChainsOwnRows(string tenant)
    {
        Assert.Empty(await Unfiltered("/journals/unfiltered", tenant));
    }

    // What the chain's own connection costs, which is the other half of the rule above: it
    // carries the INSTALL's key, so a later data migration's UPDATE over a walled table reaches
    // the install's rows and no tenant's — and Postgres narrows the statement instead of
    // refusing it, so the migration is recorded as applied with every tenant's row untouched.
    // The shape that reaches them is the one the page names, and it is visible in the chain.
    [Fact]
    public async Task ADataMigrationReachesATenantsRowsOnlyByDroppingTheWallInTheSameMigration()
    {
        await Write("lumina", "lumina's note");
        await Write("vision", "vision's note");

        Assert.Equal(0, await AsTheChain("UPDATE \"Notes\" SET \"Text\" = 'backfilled'"));
        Assert.Equal(2, await AsTheChain("UPDATE \"Notes\" SET \"Text\" = 'backfilled'", droppingTheWall: true));
    }

    async Task<IReadOnlyList<string>> Unwalled()
    {
        using var response = await _host.Client.SendAsync(_host.Get("/unwalled", "lumina"));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    async Task Write(string tenant, string text)
    {
        using var response = await _host.Client.SendAsync(
            Post($"/notes?text={Uri.EscapeDataString(text)}", tenant));

        response.EnsureSuccessStatusCode();
    }

    async Task<IReadOnlyList<string>> Unfiltered(string path, string tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get(path, tenant));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    static async Task<IReadOnlyList<string>> Unfiltered(DbContext db)
    {
        return await db.Database
            .SqlQueryRaw<string>("SELECT \"Text\" AS \"Value\" FROM \"Notes\"")
            .ToListAsync();
    }

    static HttpRequestMessage Post(string path, string tenant)
    {
        return new HttpRequestMessage(HttpMethod.Post, new Uri($"https://demo.nsail.ar{path}"))
        {
            Headers = { { TenancyMiddleware.TenantHeader, tenant } },
        };
    }

    // A statement run the way ApplyMigrations runs the chain: on the app's own role, over a
    // connection carrying the install's key, which is what TenantSession writes onto it. The
    // answer is how many rows the statement reached.
    async Task<int> AsTheChain(string statement, bool droppingTheWall = false)
    {
        await using var connection = new NpgsqlConnection(_host.InstallConnection);

        await connection.OpenAsync();

        await Execute(connection, $"SELECT set_config('{TenantPolicies.Setting}', '{Guid.Empty:D}', false)");

        if (!droppingTheWall)
        {
            return await Execute(connection, statement);
        }

        await Execute(connection, TenantPolicies.Drop("Notes"));

        var touched = await Execute(connection, statement);

        await Execute(connection, TenantPolicies.Create("Notes"));

        return touched;
    }

    static async Task<int> Execute(NpgsqlConnection connection, string statement)
    {
        await using var command = new NpgsqlCommand(statement, connection);

        return await command.ExecuteNonQueryAsync();
    }

    // Counted as postgres, which the policies do not apply to: what the assertions above are
    // about is the difference between what is THERE and what a walled connection is shown.
    async Task<int> Rows(string table)
    {
        await using var connection = new NpgsqlConnection(
            ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_host.DatabaseOf(null)}"));

        await connection.OpenAsync();

        await using var query = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\"", connection);

        return (int)(long)(await query.ExecuteScalarAsync())!;
    }
}

/// <summary>What the install does about its own role, which is the half of the wall no DDL can
/// answer: a superuser or a <c>BYPASSRLS</c> role has the policies and none of the wall, and the
/// defect is silent. Each of these raises its own install, because the subject is startup.</summary>
public sealed class TenantWallStartupTests
{
    // The default, and why the pin carrying the policies takes no cell down: provisioning a role
    // that cannot bypass is an operator pass on a live box, so an install that has not had it
    // starts and serves. What that costs is the second assertion, stated as a test rather than
    // left as a sentence — the DDL is there and the wall is not, which is exactly what the suite
    // above is measuring the absence of.
    [Fact]
    public async Task AnInstallWhoseRoleCanBypassStartsAnywayAndHasNoWall()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, bypassable: true);

        Assert.True(await host.RoleCanBypass());

        using var written = await host.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, new Uri("https://demo.nsail.ar/notes?text=lumina"))
            {
                Headers = { { TenancyMiddleware.TenantHeader, "lumina" } },
            });

        written.EnsureSuccessStatusCode();

        using var read = await host.Client.SendAsync(host.Get("/notes/unfiltered", "vision"));

        read.EnsureSuccessStatusCode();

        Assert.Equal(["lumina"], await read.Content.ReadFromJsonAsync<List<string>>());
    }

    // The startup pass opens the connection itself to write the install's own key onto it, which
    // it cannot do before the database exists — and a fresh slot and a fresh cell both lean on
    // the chain creating one. Postgres, because the app's own role is deliberately NOCREATEDB.
    [Fact]
    public async Task AnInstallWhoseDatabaseDoesNotExistYetGetsOneAndTheChainOnIt()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, bypassable: true, provisions: false);

        Assert.Equal(["20260824000000_Notes", "20260824000001_Journals"], await host.AppliedMigrations(null));
    }

    // The cell that HAS done the pass says so, and gets the hard guarantee instead of a log
    // line: nothing changes for it beyond the refusal being available.
    // The other half of what the ruling bought, and in the default path the ONLY sign the wall
    // is off: the warning, and the gauge beside it because the export seam drops a log record's
    // string attributes and an alert has to name what is off. A refactor that dropped either
    // would leave an install with no wall and nobody who could know.
    [Fact]
    public async Task AnInstallWhoseRoleCanBypassWarnsAndReportsIt()
    {
        var trail = new WallTrail();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, bypassable: true, logs: trail);

        var warning = Assert.Single(trail.Warnings, said => said.Contains("row-level security", StringComparison.Ordinal));

        Assert.Contains("Tenancy:RequireRowSecurity", warning, StringComparison.Ordinal);
        Assert.Equal(0, Assert.Single(Standing(host)));
    }

    // And the install that has the wall reports the other number and says nothing: the warning
    // is what the default path costs, not a line every cell logs.
    [Fact]
    public async Task AnInstallWhoseRoleCannotBypassReportsTheWallStanding()
    {
        var trail = new WallTrail();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, logs: trail);

        Assert.DoesNotContain(trail.Warnings, said => said.Contains("row-level security", StringComparison.Ordinal));
        Assert.Equal(1, Assert.Single(Standing(host)));
    }

    [Fact]
    public async Task AnInstallThatRequiresTheWallStartsWhenItsRoleCannotBypass()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, requireWall: true);

        Assert.False(await host.RoleCanBypass());
    }

    [Fact]
    public async Task AnInstallThatRequiresTheWallRefusesToStartWithoutIt()
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TenancyHost.Start(TenancyMode.SingleDb, requireWall: true, bypassable: true));

        Assert.Contains("bypass row-level security", refusal.Message, StringComparison.Ordinal);
    }

    // The gauge, read off THIS install's own instrument. The meter's name is a constant and the
    // measurement carries no attribute — an install is one process and one wall — so what keeps a
    // sibling suite's install out of the answer is the factory the meter came from, which is the
    // host's own container (NSail.Background.Testing.Meters keeps its two apart by the job name,
    // which is the same problem with a tag available).
    static IReadOnlyList<int> Standing(TenancyHost host)
    {
        var meter = host.Services
            .GetRequiredService<IMeterFactory>()
            .Create(new MeterOptions(TenantPolicies.MeterName));

        var measured = new List<int>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, subscriber) =>
        {
            if (ReferenceEquals(instrument.Meter, meter) && instrument.Name == TenantPolicies.Standing)
            {
                subscriber.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<int>((_, measurement, _, _) => measured.Add(measurement));
        listener.Start();

        // What the exporter does on its own interval, and the only way a gauge is ever read.
        listener.RecordObservableInstruments();

        return measured;
    }
}

/// <summary>What the install said at warning or above on its way up. A provider rather than a
/// fake <c>ILogger&lt;T&gt;</c>: the wall's logger is typed on a class internal to its own
/// module, so naming it from here would need that module to open itself up for a test.</summary>
sealed class WallTrail : ILoggerProvider
{
    readonly List<string> _warnings = [];

    public IReadOnlyList<string> Warnings
    {
        get
        {
            lock (_warnings)
            {
                return _warnings.ToList();
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Pen(this);
    }

    public void Dispose()
    {
    }

    void Say(string line)
    {
        lock (_warnings)
        {
            _warnings.Add(line);
        }
    }

    sealed class Pen : ILogger
    {
        readonly WallTrail _trail;

        public Pen(WallTrail trail)
        {
            _trail = trail;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (logLevel >= LogLevel.Warning)
            {
                _trail.Say(formatter(state, exception));
            }
        }
    }
}

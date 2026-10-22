// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSail.Data;

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
        Assert.Empty(await Unwalled());
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
}

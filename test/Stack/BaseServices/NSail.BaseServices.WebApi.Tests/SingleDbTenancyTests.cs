// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The other wall, through the real pipeline and against real Postgres: many tenants
/// in ONE database, kept apart by a column and a filter instead of by a connection. What is
/// checked here is what the mode promises — a tenant sees its own rows and no others, a write
/// belongs to whoever the edge resolved and not to whoever the caller named, and the model
/// underneath is the one every other mode runs.</summary>
public sealed class SingleDbTenancyTests : IAsyncLifetime
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

    [Fact]
    public async Task TheInstallsOwnDatabaseIsMigratedAtStartup()
    {
        Assert.Equal(["20260824000000_Notes"], await _host.AppliedMigrations(null));
    }

    // The whole mode in one test, and the one that would catch the filter being compiled with
    // the first request's tenant baked into it: two tenants, one process, one model, one
    // database — and each reads back only what it wrote.
    [Fact]
    public async Task ATenantOnlyEverSeesItsOwnRows()
    {
        await Write("lumina", "lumina's note");
        await Write("vision", "vision's note");

        Assert.Equal(["lumina's note"], await Read("lumina"));
        Assert.Equal(["vision's note"], await Read("vision"));
    }

    // The other half of the same sentence: one database, not two. A mode that quietly connected
    // somewhere else would pass the test above for the wrong reason.
    [Theory]
    [InlineData("lumina")]
    [InlineData("vision")]
    public async Task EveryTenantRunsOnTheInstallsOwnDatabase(string tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/where", tenant));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var where = (await response.Content.ReadFromJsonAsync<Where>())!;

        Assert.Equal(tenant, where.Slug);
        Assert.Null(where.Database);
        Assert.Equal(_host.DatabaseOf(null), where.Connection);
    }

    // The stamp is the resolved identity's, and the caller's word for it is discarded — even
    // when the caller reaches the tracker itself, which is closer than any payload can get.
    [Fact]
    public async Task AWriteBelongsToTheResolvedTenantAndNeverToTheOneTheCallerNamed()
    {
        var forged = Tenant.For("vision").RowKey;

        using var response = await _host.Client.SendAsync(
            Post($"/notes/forged?text=forged&tenant={forged}", "lumina"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(["forged"], await Read("lumina"));
        Assert.Empty(await Read("vision"));
        Assert.Equal(Tenant.For("lumina").RowKey, await StoredTenantOf("forged"));
    }

    // A tenant column that never left Guid.Empty would pass every reading test above by hiding
    // nothing from nobody. The row carries the derived key, in the database, as a value.
    [Fact]
    public async Task TheRowCarriesTheTenantsOwnKey()
    {
        await Write("lumina", "stamped");

        Assert.Equal(Tenant.For("lumina").RowKey, await StoredTenantOf("stamped"));
        Assert.NotEqual(Guid.Empty, await StoredTenantOf("stamped"));
    }

    // The edge refuses before anything a tenant's rows could answer, exactly as it does under
    // the other wall: no header is not a tenant, and a malformed slug is refused rather than
    // repaired into one.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lu_mina")]
    [InlineData("-lumina")]
    [InlineData("lumina,lumina")]
    public async Task AnythingThatIsNotASlugIsRefusedAheadOfEverything(string? tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/notes", tenant));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Under this wall the connection is not a wall at all, so the credential check is the only
    // thing standing between one tenant's ticket and another tenant's rows.
    [Fact]
    public async Task ATicketMintedOnOneTenantIsNobodyOnAnother()
    {
        var ticket = await _host.SignIn("lumina");

        using var held = await _host.Client.SendAsync(_host.Get("/guarded", "lumina", ticket: ticket));

        Assert.Equal(HttpStatusCode.OK, held.StatusCode);

        using var carried = await _host.Client.SendAsync(_host.Get("/guarded", "vision", ticket: ticket));

        Assert.Equal(HttpStatusCode.Unauthorized, carried.StatusCode);
    }

    // The wall's third face, and the one with no attacker in it: a scope that resolved nobody.
    // Under the other wall the connection refuses it; here nothing would, so the filter compares
    // against NULL and no row equals that — not another tenant's, and not the seed's unstamped
    // ones either. A background job that entered no tenant reads NOTHING.
    [Fact]
    public async Task AScopeThatResolvedNoTenantReadsNothing()
    {
        await Write("lumina", "lumina's note");
        await Write("vision", "vision's note");

        await using var scope = _host.Services.CreateAsyncScope();

        var unscoped = scope.ServiceProvider.GetRequiredService<DbContext>();

        Assert.Empty(await unscoped.Set<Note>().ToListAsync());

        // And the rows are there to be missed: the emptiness above is the filter, not an empty
        // table.
        Assert.Equal(2, await Rows());
    }

    // The other class of row, and the half that makes a tenant usable rather than merely isolated:
    // reference data the install shares. The marker takes the column and the filter off it, so one
    // planted row answers every tenant — and the seed's own rows are reachable rather than hidden
    // behind a predicate no tenant can satisfy.
    [Theory]
    [InlineData("lumina")]
    [InlineData("vision")]
    public async Task EveryTenantReadsTheSameInstallScopedRows(string tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/kinds", tenant));

        response.EnsureSuccessStatusCode();

        Assert.Equal(["reminder", "warning"], await response.Content.ReadFromJsonAsync<List<string>>());
    }

    // And a scope that resolved nobody reads them too — where the same scope reads no note at all.
    // The difference between the two classes IS the marker, in one assertion.
    [Fact]
    public async Task AScopeThatResolvedNoTenantStillReadsTheInstallsOwnRows()
    {
        await using var scope = _host.Services.CreateAsyncScope();

        var unscoped = scope.ServiceProvider.GetRequiredService<DbContext>();

        Assert.Equal(2, await unscoped.Set<NoteKind>().CountAsync());
    }

    // The read half answers "nothing" by comparing against NULL; the write half cannot, because
    // every value it could stamp would be somebody's. So it is refused at the seam rather than
    // written where nobody will ever read it back.
    [Fact]
    public async Task ATenantScopedWriteWithNoTenantThrows()
    {
        await using var scope = _host.Services.CreateAsyncScope();

        var unscoped = scope.ServiceProvider.GetRequiredService<DbContext>();

        unscoped.Add(new Note { Id = Guid.NewGuid(), Text = "nobody's" });

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unscoped.SaveChangesAsync());

        Assert.Contains("resolved no tenant", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(0, await Rows());
    }

    // The refusal is the tenant-scoped half's alone. An install-scoped row belongs to nobody in
    // particular, so the work that plants one — a migration, a job — needs no tenant to be in.
    [Fact]
    public async Task AnInstallScopedWriteWithNoTenantIsFine()
    {
        await using var scope = _host.Services.CreateAsyncScope();

        var unscoped = scope.ServiceProvider.GetRequiredService<DbContext>();

        unscoped.Add(new NoteKind { Id = Guid.NewGuid(), Name = "everybody's" });

        await unscoped.SaveChangesAsync();

        Assert.Equal(3, await unscoped.Set<NoteKind>().CountAsync());
    }

    async Task Write(string tenant, string text)
    {
        using var response = await _host.Client.SendAsync(
            Post($"/notes?text={Uri.EscapeDataString(text)}", tenant));

        response.EnsureSuccessStatusCode();
    }

    static HttpRequestMessage Post(string path, string tenant)
    {
        return new HttpRequestMessage(HttpMethod.Post, new Uri($"https://demo.nsail.ar{path}"))
        {
            Headers = { { TenancyMiddleware.TenantHeader, tenant } },
        };
    }

    async Task<IReadOnlyList<string>> Read(string tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/notes", tenant));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    // Read past EF, because what the filter hides is exactly what an assertion through EF cannot
    // see: the column is asked for by name, on the install's own database.
    async Task<Guid> StoredTenantOf(string text)
    {
        return (Guid)(await Query("SELECT \"TenantId\" FROM \"Notes\" WHERE \"Text\" = @text", text))!;
    }

    async Task<int> Rows()
    {
        return (int)(long)(await Query("SELECT count(*) FROM \"Notes\""))!;
    }

    async Task<object?> Query(string sql, string? text = null)
    {
        await using var connection = new NpgsqlConnection(
            ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_host.DatabaseOf(null)}"));

        await connection.OpenAsync();

        await using var query = new NpgsqlCommand(sql, connection);

        if (text is not null)
        {
            query.Parameters.AddWithValue("text", text);
        }

        return await query.ExecuteScalarAsync();
    }
}

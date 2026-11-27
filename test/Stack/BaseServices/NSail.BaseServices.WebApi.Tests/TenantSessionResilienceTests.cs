// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net.Http.Json;
using Npgsql;
using NSail.Data;
using NSail.Data.Testing;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>A cluster restart as a request sees it: every session the install's pool holds idle
/// is ended by the server, and the next requests take those connections. Over the role the row
/// policies apply to, so the replacement session is proven to carry its tenant by Postgres
/// itself and not by the query filter.</summary>
// Alone: the pool is the process's, and a sibling host's ClearAllPools would empty it before the
// requests below take its connections, leaving nothing dead for them to find.
[Collection(nameof(TenantSessionResilienceTests))]
public sealed class TenantSessionResilienceTests : IAsyncLifetime
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
    public async Task AQueryAfterTheServerTerminatedAPooledConnectionReadsItsRows()
    {
        Assert.False(await _host.RoleCanBypass());

        await Write("lumina", "lumina's note");
        await Write("vision", "vision's note");

        Assert.True(await TerminateThePool() > 0);

        Assert.Equal(["lumina's note"], await Unfiltered("lumina"));
        Assert.Equal(["vision's note"], await Unfiltered("vision"));
        Assert.Equal(["lumina's note"], await Unfiltered("lumina"));
    }

    async Task Write(string tenant, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://demo.nsail.ar/notes?text={Uri.EscapeDataString(text)}"))
        {
            Headers = { { TenancyMiddleware.TenantHeader, tenant } },
        };

        using var response = await _host.Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }

    // Raw SQL with no filter compiled in: what comes back is what the policy shows the session.
    async Task<IReadOnlyList<string>> Unfiltered(string tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/notes/unfiltered", tenant));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    // From a connection of its own, and only back once every backend is gone, so each idle
    // connection in the pool holds a dead session when a request takes it.
    async Task<int> TerminateThePool()
    {
        await using var admin = new NpgsqlConnection(
            ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres;Pooling=false"));

        await admin.OpenAsync();

        await using var terminate = new NpgsqlCommand(
            "SELECT count(*) FILTER (WHERE pg_terminate_backend(pid, 5000)) FROM pg_stat_activity WHERE datname = @database AND usename = @role",
            admin);

        terminate.Parameters.AddWithValue("database", _host.DatabaseOf(null));
        terminate.Parameters.AddWithValue("role", new NpgsqlConnectionStringBuilder(_host.InstallConnection).Username!);

        return (int)(long)(await terminate.ExecuteScalarAsync())!;
    }
}

[CollectionDefinition(nameof(TenantSessionResilienceTests), DisableParallelization = true)]
public sealed class TenantSessionResilienceCollection
{
}

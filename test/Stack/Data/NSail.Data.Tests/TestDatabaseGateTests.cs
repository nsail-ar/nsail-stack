// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;
using NSail.Data.Testing;
using NSail.Data.Tests.Mapping;

namespace NSail.Data.Tests;

// The gate bounds how many fixtures may be alive on one Postgres at once, and what makes that
// bound true is WHERE a permit is held: in the server itself, so every process that reaches it
// — any test host, in any container, on any runner slot of the machine — counts against one
// figure. A permit kept anywhere a single container owns bounds that container alone.
public sealed class TestDatabaseGateTests
{
    // The gate's own keyspace as a two-int advisory lock reports it: its first key in classid,
    // the permit's index in objid.
    const long GateKeyspace = 0x4E53_4442;

    [Fact]
    public async Task a_reserved_database_holds_its_permit_on_the_server_every_slot_shares()
    {
        await using var database = new TestDatabase(
            "gate",
            connection => new AggregateTokenPostgresTests.ListContext(connection));

        await database.Reserve();

        // Read from a connection of its own, which is the whole claim — the permit is the
        // server's to report, not this process's to remember.
        Assert.True(await PermitsHeldOnTheServer() >= 1, "a reserved fixture holds no permit the server can see");
    }

    static async Task<long> PermitsHeldOnTheServer()
    {
        await using var probe = new NpgsqlConnection(
            ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres"));

        await probe.OpenAsync();

        await using var held = new NpgsqlCommand(
            "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND classid::bigint = @keyspace AND granted",
            probe);

        held.Parameters.AddWithValue("keyspace", GateKeyspace);

        return (long)(await held.ExecuteScalarAsync())!;
    }
}

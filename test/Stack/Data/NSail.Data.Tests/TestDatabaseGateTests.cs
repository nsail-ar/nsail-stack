// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSail.Data.Testing;

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
        await using var database = new TestDatabase("gate", connection => new GateContext(connection));

        await database.Reserve();

        Assert.Equal(1, await PermitsHeldFor(database.Name));
    }

    [Fact]
    public async Task a_disposed_database_hands_its_permit_back_to_the_server()
    {
        var database = new TestDatabase("gate", connection => new GateContext(connection));

        await database.Reserve();

        Assert.Equal(1, await PermitsHeldFor(database.Name));

        await database.DisposeAsync();

        Assert.True(
            await PermitGoneWithin(database.Name, TimeSpan.FromSeconds(10)),
            "a disposed fixture's permit is still held on the server");
    }

    // The handback is the server's own act — it drops a session's advisory locks when that
    // session's backend ends, which is what keeps a crashed run from wedging the gate — and a
    // backend ends shortly after its client closes the socket, not inside the close. So the
    // drop is waited for and never read once: a single read right after DisposeAsync catches a
    // dying backend still holding its permit, which a loaded machine reaches.
    static async Task<bool> PermitGoneWithin(string database, TimeSpan patience)
    {
        var deadline = DateTime.UtcNow + patience;

        while (DateTime.UtcNow < deadline)
        {
            if (await PermitsHeldFor(database) == 0)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return await PermitsHeldFor(database) == 0;
    }

    // Read from a connection of its own, which is the whole claim — the permit is the server's
    // to report, not this process's to remember — and counted by the session that holds it,
    // which the fixture names after its own database. Every other fixture alive on this server,
    // in this process or in any other, holds a permit of its own, so a count over the keyspace
    // alone would be satisfied by a sibling's.
    static async Task<long> PermitsHeldFor(string database)
    {
        await using var probe = new NpgsqlConnection(
            ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres"));

        await probe.OpenAsync();

        await using var held = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_locks permit
            JOIN pg_stat_activity holder ON holder.pid = permit.pid
            WHERE permit.locktype = 'advisory'
              AND permit.classid::bigint = @keyspace
              AND permit.granted
              AND holder.application_name = left(@database, 63)
            """,
            probe);

        held.Parameters.AddWithValue("keyspace", GateKeyspace);
        held.Parameters.AddWithValue("database", database);

        return (long)(await held.ExecuteScalarAsync())!;
    }

    // Reserve applies no migration and the drop on the way out needs no model, so the gate's
    // own context carries no entity: borrowing another test's would tie this proof to a model
    // that has nothing to do with it.
    sealed class GateContext : DbContext
    {
        readonly string _connection;

        public GateContext(string connection)
        {
            _connection = connection;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseNpgsql(_connection);
        }
    }
}

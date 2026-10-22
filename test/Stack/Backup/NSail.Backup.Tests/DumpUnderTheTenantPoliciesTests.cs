// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;
using NSail.Data;

namespace NSail.Backup.Tests;

/// <summary>What the second wall costs the backup verb. `pg_dump` reads with
/// <c>row_security off</c>, which Postgres refuses to a role a policy applies to, and
/// <c>FORCE ROW LEVEL SECURITY</c> is exactly what subjects the owner of a walled table to its
/// own policy — so a cell that has repointed its app at a role that cannot bypass has to hand
/// the backup another credential. The verb says so instead of ending in pg_dump's exit
/// code.</summary>
public sealed class DumpUnderTheTenantPoliciesTests : IAsyncLifetime
{
    static readonly string Server =
        ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres");

    readonly string _name = "nsail_dump_" + Guid.NewGuid().ToString("N")[..12];
    readonly string _dumpFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dump");

    public async Task InitializeAsync()
    {
        await AsPostgres($"CREATE ROLE \"{_name}\" LOGIN PASSWORD '{_name}' NOSUPERUSER NOBYPASSRLS NOCREATEDB");
        await AsPostgres($"CREATE DATABASE \"{_name}\" OWNER \"{_name}\"");

        await using var connection = new NpgsqlConnection(Install);

        await connection.OpenAsync();

        await using var walled = new NpgsqlCommand(
            $"CREATE TABLE \"Notes\" (\"Id\" uuid PRIMARY KEY, \"TenantId\" uuid NOT NULL);{TenantPolicies.Create("Notes")}",
            connection);

        await walled.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await AsPostgres($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");
        await AsPostgres($"DROP ROLE IF EXISTS \"{_name}\"");
    }

    // The refusal, and it is not pg_dump's own: without it the verb reaches the process and comes
    // back with "exited with 1" and the name of one table, which tells an operator nothing about
    // the credential it needs.
    [Fact]
    public async Task TheDumpRefusesARoleTheTenantPoliciesApplyTo()
    {
        var refusal = await Assert.ThrowsAsync<BackupFailure>(() => Postgres.Dump(Install, _dumpFilePath));

        Assert.Contains("BYPASSRLS", refusal.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_dumpFilePath));
    }

    string Install
    {
        get
        {
            var builder = new NpgsqlConnectionStringBuilder(Server)
            {
                Database = _name,
                Username = _name,
                Password = _name,
            };

            return builder.ConnectionString;
        }
    }

    static async Task AsPostgres(string statement)
    {
        await using var admin = new NpgsqlConnection(Server);

        await admin.OpenAsync();

        await using var command = new NpgsqlCommand(statement, admin);

        await command.ExecuteNonQueryAsync();
    }
}

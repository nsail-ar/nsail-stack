// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NSail.Data.Testing;

/// <summary>Reads a MIGRATED database back against the model and names every tenant-scoped
/// table whose wall is not standing. The list in a product's migration cannot be derived from
/// the chain (<see cref="MigrationBuilderExtensions.AddTenantPolicies"/>), so what keeps it
/// true is this sweep: a table added by a later migration and not walled by it reds here, and
/// the failure prints the call that fixes it.
///
/// <para>It is here and not in the runtime module because the question it answers is a
/// chain's, asked once per build over a database CI just built. The install asks a different
/// and cheaper one at startup — whether its ROLE is subject to the policies at all
/// (<c>Tenancy:RequireRowSecurity</c>).</para></summary>
public static class TenantPolicyAudit
{
    const string Standing = """
        SELECT c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = current_schema()
          AND c.relkind = 'r'
          AND c.relrowsecurity
          AND c.relforcerowsecurity
          AND EXISTS (SELECT 1 FROM pg_policy p WHERE p.polrelid = c.oid AND p.polname = @policy)
        """;

    /// <summary>The tenant-scoped tables of <paramref name="db"/>'s model that the database it
    /// is connected to does not wall — row-level security off, not forced, or carrying no
    /// policy by the Stack's name. Empty is the only passing answer.</summary>
    public static async Task<IReadOnlyList<string>> Unwalled(DbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var owed = TenantPolicies.Tables(db.Model);
        var walled = new HashSet<string>(StringComparer.Ordinal);

        await db.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = (NpgsqlCommand)db.Database.GetDbConnection().CreateCommand();

            command.CommandText = Standing;
            command.Parameters.AddWithValue("policy", TenantPolicies.Name);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                walled.Add(reader.GetString(0));
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        return owed.Where(table => !walled.Contains(table)).ToList();
    }

    /// <summary>What a failing sweep says: the tables, and the one line a migration adds to
    /// wall them.</summary>
    public static string Describe(string name, IReadOnlyList<string> unwalled)
    {
        ArgumentNullException.ThrowIfNull(unwalled);

        var quoted = string.Join(", ", unwalled.Select(table => $"\"{table}\""));

        return $"{name} carries {unwalled.Count} tenant-scoped table(s) with no forced {TenantPolicies.Name} policy, " +
            "so under SingleDb the EF query filter is their only wall. Add a migration that calls " +
            $"migrationBuilder.AddTenantPolicies({quoted});";
    }
}

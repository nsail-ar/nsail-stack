// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NSail.Data;

namespace NSail.Sample;

public static class SampleRole
{
    public const string Name = "sample_app";

    public static async Task MigrateAndGrant(string adminConnectionString, string appConnectionString, CancellationToken cancellationToken = default)
    {
        var admin = ConnectionStrings.Complete(adminConnectionString);
        var password = new NpgsqlConnectionStringBuilder(ConnectionStrings.Complete(appConnectionString)).Password;

        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("The sample's role needs a password: set PGPASSWORD or Password= in Sample:ConnectionString.");
        }

        var options = new DbContextOptionsBuilder<SampleDbContext>().UseNpgsql(admin).Options;

        await using (var context = new SampleDbContext(options, [new DbContextSetup()]))
        {
            var db = context.Database;
            var creator = db.GetService<IRelationalDatabaseCreator>();

            if (!await creator.ExistsAsync(cancellationToken))
            {
                await creator.CreateAsync(cancellationToken);
            }

            // The chain is the install's own work: the tenant policies are forced, so the key is
            // written on the connection that runs it, as ApplyMigrations does.
            await db.OpenConnectionAsync(cancellationToken);

            try
            {
                await using var enter = new NpgsqlCommand($"SELECT set_config('{TenantPolicies.Setting}', @tenant, false)", (NpgsqlConnection)db.GetDbConnection());
                enter.Parameters.AddWithValue("tenant", Tenant.None.RowKey.ToString());
                await enter.ExecuteNonQueryAsync(cancellationToken);
                await db.MigrateAsync(cancellationToken);
            }
            finally
            {
                await db.CloseConnectionAsync();
            }
        }

        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync(cancellationToken);

        var database = new NpgsqlConnectionStringBuilder(admin).Database;

        await Execute(connection, $"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{Name}') THEN
                    CREATE ROLE {Name} LOGIN;
                END IF;
            END
            $$;
            ALTER ROLE {Name} LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
            """, cancellationToken);

        await using (var format = new NpgsqlCommand($"SELECT format('ALTER ROLE {Name} PASSWORD %L', @password)", connection))
        {
            format.Parameters.AddWithValue("password", password);
            var alter = (string)(await format.ExecuteScalarAsync(cancellationToken))!;
            await Execute(connection, alter, cancellationToken);
        }

        await Execute(connection, $"""
            GRANT CONNECT ON DATABASE "{database!.Replace("\"", "\"\"")}" TO {Name};
            GRANT USAGE ON SCHEMA public TO {Name};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {Name};
            REVOKE ALL ON "__EFMigrationsHistory" FROM {Name};
            GRANT SELECT ON "__EFMigrationsHistory" TO {Name};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {Name};
            """, cancellationToken);
    }

    static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

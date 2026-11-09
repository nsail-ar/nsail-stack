// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSail.Data;

namespace NSail.Sample;

public static class SampleRole
{
    public const string Name = "sample_app";

    public static async Task Prepare(string adminConnectionString, string appConnectionString, CancellationToken cancellationToken = default)
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
            await context.Database.MigrateAsync(cancellationToken);
        }

        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync(cancellationToken);

        var database = new NpgsqlConnectionStringBuilder(admin).Database;

        await Execute(connection, $"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{Name}') THEN
                    CREATE ROLE {Name} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
                END IF;
            END
            $$;
            """, cancellationToken);

        await using (var alter = new NpgsqlCommand($"ALTER ROLE {Name} PASSWORD '{password.Replace("'", "''")}'", connection))
        {
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }

        await Execute(connection, $"""
            GRANT CONNECT ON DATABASE "{database!.Replace("\"", "\"\"")}" TO {Name};
            GRANT USAGE ON SCHEMA public TO {Name};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {Name};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {Name};
            """, cancellationToken);
    }

    static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

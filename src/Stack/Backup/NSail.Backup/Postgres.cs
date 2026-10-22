// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using Npgsql;

namespace NSail.Backup;

/// <summary>The install's own database, dumped and restored by the server's own tools.
/// pg_dump/pg_restore and nothing else: a backup returns an install to the exact state it was
/// in — same version, same schema, byte for byte — which an EF-shaped export cannot promise.</summary>
public static class Postgres
{
    public const string DumpEntryName = "database.dump";

    // pg_dump copies with row_security off, and Postgres refuses a statement run that way by a
    // role the policy APPLIES to — which is what FORCE ROW LEVEL SECURITY does to the owner of a
    // walled table (data-tenancy.md, The second wall). So the dump needs a role that can bypass,
    // and the cell that has repointed its app at one that cannot owes the backup a credential of
    // its own. Asked before the process starts, because pg_dump's own answer is an exit code and
    // the name of one table. --enable-row-security is not the way out: it would dump the subset
    // the connection is shown, and a console process resolves no tenant.
    const string Unreadable = """
        SELECT EXISTS (
                SELECT 1
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = current_schema() AND c.relkind = 'r' AND c.relforcerowsecurity)
            AND NOT (SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user)
        """;

    public static async Task<string> ServerVersion(string connectionString, CancellationToken cancelToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancelToken);

        return connection.PostgreSqlVersion.ToString();
    }

    /// <summary>Custom format (-Fc): what pg_restore reads, compressed, and the only shape that
    /// lets a restore drop and rebuild what the dump owns in one pass.</summary>
    public static async Task Dump(string connectionString, string dumpFilePath, CancellationToken cancelToken = default)
    {
        var target = new NpgsqlConnectionStringBuilder(connectionString);

        await RefuseAWalledRole(connectionString, cancelToken);

        var arguments = new[]
        {
            "--format=custom",
            "--no-owner",
            "--no-privileges",
            $"--file={dumpFilePath}",
            $"--host={target.Host}",
            $"--port={target.Port}",
            $"--username={target.Username}",
            $"--dbname={target.Database}"
        };

        await Run("pg_dump", arguments, target.Password, cancelToken);
    }

    /// <summary>Restore is replace, not merge: the schema is dropped whole and rebuilt from the
    /// dump, so neither a row deleted after the backup nor a table created after it survives
    /// the restore.</summary>
    public static async Task Restore(string connectionString, string dumpFilePath, CancellationToken cancelToken = default)
    {
        var target = new NpgsqlConnectionStringBuilder(connectionString);

        // Not pg_restore --clean: that drops only what the dump itself owns, so anything the
        // install grew after the backup would outlive the restore and the database would come
        // back as a mixture of two moments.
        await Reset(connectionString, cancelToken);

        var arguments = new[]
        {
            "--no-owner",
            "--no-privileges",
            "--single-transaction",
            $"--host={target.Host}",
            $"--port={target.Port}",
            $"--username={target.Username}",
            $"--dbname={target.Database}",
            dumpFilePath
        };

        await Run("pg_restore", arguments, target.Password, cancelToken);
    }

    // Only the dump: pg_restore writes an archive's data before it creates its policies, so a
    // role the policies apply to restores one without stepping over anything.
    static async Task RefuseAWalledRole(string connectionString, CancellationToken cancelToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancelToken);

        await using var command = new NpgsqlCommand(Unreadable, connection);

        if (await command.ExecuteScalarAsync(cancelToken) is not true)
        {
            return;
        }

        throw new BackupFailure(
            "This database's tenant tables force row-level security and this role is subject to them, so pg_dump — which reads with row_security off — is refused on the first of them. Take the backup as a role that is a superuser or carries BYPASSRLS.");
    }

    static async Task Reset(string connectionString, CancellationToken cancelToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancelToken);

        await using var command = new NpgsqlCommand("drop schema public cascade; create schema public", connection);

        await command.ExecuteNonQueryAsync(cancelToken);
    }

    public static string MajorVersion(string version)
    {
        var separator = version.IndexOf('.');

        return separator < 0 ? version : version[..separator];
    }

    static async Task Run(string executable, IReadOnlyList<string> arguments, string? password, CancellationToken cancelToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // The password never becomes an argument: a command line is world-readable on the host.
        if (!string.IsNullOrEmpty(password))
        {
            start.Environment["PGPASSWORD"] = password;
        }

        using var process = Process.Start(start);

        if (process is null)
        {
            throw new BackupFailure($"'{executable}' could not be started. The runtime image needs postgres-client.");
        }

        var error = await process.StandardError.ReadToEndAsync(cancelToken);

        await process.WaitForExitAsync(cancelToken);

        if (process.ExitCode != 0)
        {
            throw new BackupFailure($"'{executable}' exited with {process.ExitCode}: {error.Trim()}");
        }
    }
}

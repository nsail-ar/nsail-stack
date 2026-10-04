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

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;
using NSail.Data;

namespace NSail.Backup.Tests;

/// <summary>A restore that fails changes nothing, and one that succeeds keeps the audit trail.</summary>
public sealed class RestoreAtomicityTests : IAsyncLifetime
{
    static readonly string Server =
        ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres");

    readonly string _name = "nsail_rest_" + Guid.NewGuid().ToString("N")[..12];
    readonly string _dumpFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dump");

    string Install
    {
        get
        {
            return new NpgsqlConnectionStringBuilder(Server) { Database = _name }.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        await Sql(Server, $"CREATE DATABASE \"{_name}\"");
        await Sql(Install, "CREATE TABLE notes (id int PRIMARY KEY, body text); INSERT INTO notes VALUES (1, 'before')");
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await Sql(Server, $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");

        File.Delete(_dumpFilePath);
    }

    [Fact]
    public async Task ACorruptDumpLeavesTheDataIntact()
    {
        await File.WriteAllTextAsync(_dumpFilePath, "this is not a dump");

        await Assert.ThrowsAsync<BackupFailure>(() => Postgres.Restore(Install, _dumpFilePath));

        Assert.Equal("before", await Scalar("SELECT body FROM notes WHERE id = 1"));
    }

    [Fact]
    public async Task ARestoreKeepsTheAuditRowsWrittenAfterTheBackup()
    {
        await Postgres.Dump(Install, _dumpFilePath);

        await Sql(Install, BackupAuditRow("after-the-backup"));

        await Sql(Install, "UPDATE notes SET body = 'changed'");

        await Postgres.Restore(Install, _dumpFilePath);

        Assert.Equal("before", await Scalar("SELECT body FROM notes WHERE id = 1"));
        Assert.Equal("1", await Scalar($"SELECT count(*) FROM {BackupAudit.TableName} WHERE archive = 'after-the-backup'"));
    }

    [Fact]
    public async Task ARestoreLeavesNoObjectTheDumpDidNotHold()
    {
        await Postgres.Dump(Install, _dumpFilePath);

        await Sql(Install, "CREATE TABLE later (id int)");

        await Postgres.Restore(Install, _dumpFilePath);

        Assert.Equal("", await Scalar("SELECT to_regclass('public.later')::text"));
    }

    [Fact]
    public async Task AFailedRestoreLeavesThePreviousRingIntactAndNoStagingBehind()
    {
        var ring = Directory.CreateTempSubdirectory("nsail-ring");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(ring.FullName, "key-old.xml"), "old");

            await using var archive = new MemoryStream();

            using (var zip = new System.IO.Compression.ZipArchive(archive, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                await WriteManifest(zip);

                await using (var dump = zip.CreateEntry(Postgres.DumpEntryName).Open())
                {
                    await dump.WriteAsync("not a dump"u8.ToArray());
                }

                zip.CreateEntry(KeyRing.ArchiveFolder + "key-new.xml");
            }

            archive.Position = 0;

            var install = new InstallDescriptor
            {
                Product = "Test",
                ConnectionString = Install,
                KeyRingPath = ring.FullName,
                SchemaVersion = "x"
            };

            await Assert.ThrowsAsync<BackupFailure>(() => InstallArchive.Restore(install, archive));

            Assert.Equal(["key-old.xml"], ring.GetFileSystemInfos().Select(entry => entry.Name).ToArray());
        }
        finally
        {
            ring.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ARestoreReplacesTheRingWithTheArchivesKeys()
    {
        var ring = Directory.CreateTempSubdirectory("nsail-ring");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(ring.FullName, "key-old.xml"), "old");

            await Postgres.Dump(Install, _dumpFilePath);

            await using var archive = new MemoryStream();

            using (var zip = new System.IO.Compression.ZipArchive(archive, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                await WriteManifest(zip);

                System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(zip, _dumpFilePath, Postgres.DumpEntryName);
                zip.CreateEntry(KeyRing.ArchiveFolder + "key-new.xml");
            }

            archive.Position = 0;

            var install = new InstallDescriptor
            {
                Product = "Test",
                ConnectionString = Install,
                KeyRingPath = ring.FullName,
                SchemaVersion = "x"
            };

            await InstallArchive.Restore(install, archive);

            Assert.Equal(["key-new.xml"], ring.GetFileSystemInfos().Select(entry => entry.Name).ToArray());
        }
        finally
        {
            ring.Delete(recursive: true);
        }
    }

    static async Task WriteManifest(System.IO.Compression.ZipArchive zip)
    {
        var manifest = new BackupManifest
        {
            Product = "Test",
            Database = "test",
            PostgresVersion = "18",
            SchemaVersion = "x",
            TakenAtUtc = DateTime.UtcNow,
            HasKeyRing = true
        };

        await using var entry = zip.CreateEntry(BackupManifest.EntryName).Open();

        await System.Text.Json.JsonSerializer.SerializeAsync(entry, manifest);
    }

    static string BackupAuditRow(string archive)
    {
        return $"""
            create table if not exists {BackupAudit.TableName} (
                id uuid primary key, operation text not null, actor text not null,
                occurred_at timestamptz not null, product text not null, schema_version text not null,
                postgres_version text not null, archive text not null, archive_taken_at timestamptz not null);
            insert into {BackupAudit.TableName} values (gen_random_uuid(), 'backup', 'a', now(), 'p', 's', 'v', '{archive}', now())
            """;
    }

    static async Task Sql(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync();
    }

    async Task<string> Scalar(string sql)
    {
        await using var connection = new NpgsqlConnection(Install);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        return Convert.ToString(await command.ExecuteScalarAsync()) ?? "";
    }
}

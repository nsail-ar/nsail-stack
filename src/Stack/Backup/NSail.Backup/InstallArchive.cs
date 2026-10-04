// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.IO.Compression;
using System.Text.Json;

namespace NSail.Backup;

/// <summary>One archive that carries everything an install IS: its data, its settings — they
/// are rows — and the DataProtection key ring that makes its sealed secrets readable again.
/// The manifest travels inside so the archive can be refused before a byte of it is written
/// anywhere.</summary>
public static class InstallArchive
{
    static readonly JsonSerializerOptions ManifestFormat = new() { WriteIndented = true };

    public static async Task<BackupManifest> Create(InstallDescriptor install, Stream destination, CancellationToken cancelToken = default)
    {
        var workspace = Directory.CreateTempSubdirectory("nsail-backup");

        try
        {
            var dumpFilePath = Path.Combine(workspace.FullName, Postgres.DumpEntryName);

            await Postgres.Dump(install.ConnectionString, dumpFilePath, cancelToken);

            var keyRing = new DirectoryInfo(install.KeyRingPath);

            var manifest = new BackupManifest
            {
                Product = install.Product,
                Database = DatabaseName(install.ConnectionString),
                PostgresVersion = await Postgres.ServerVersion(install.ConnectionString, cancelToken),
                SchemaVersion = install.SchemaVersion,
                TakenAtUtc = DateTime.UtcNow,
                HasKeyRing = keyRing.Exists && keyRing.EnumerateFiles().Any()
            };

            using (var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
            {
                var manifestEntry = archive.CreateEntry(BackupManifest.EntryName);

                await using (var manifestStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(manifestStream, manifest, ManifestFormat, cancelToken);
                }

                archive.CreateEntryFromFile(dumpFilePath, Postgres.DumpEntryName, CompressionLevel.SmallestSize);

                if (keyRing.Exists)
                {
                    foreach (var key in keyRing.EnumerateFiles("*.xml"))
                    {
                        archive.CreateEntryFromFile(key.FullName, KeyRing.ArchiveFolder + key.Name);
                    }
                }
            }

            return manifest;
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    public static async Task<BackupManifest> ReadManifest(Stream source, CancellationToken cancelToken = default)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        return await ReadManifest(archive, cancelToken);
    }

    /// <summary>Replace, not merge: the database is rebuilt from the dump and the key ring
    /// directory is emptied of the keys the archive replaces. Every refusal this can raise has
    /// already been raised by <see cref="RestorePlan"/>; nothing here asks a question.</summary>
    public static async Task Restore(InstallDescriptor install, Stream source, CancellationToken cancelToken = default)
    {
        var workspace = Directory.CreateTempSubdirectory("nsail-restore");

        try
        {
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

            var dumpEntry = archive.GetEntry(Postgres.DumpEntryName)
                ?? throw new BackupFailure($"The archive carries no '{Postgres.DumpEntryName}'.");

            var dumpFilePath = Path.Combine(workspace.FullName, Postgres.DumpEntryName);

            dumpEntry.ExtractToFile(dumpFilePath, overwrite: true);

            await Postgres.Restore(install.ConnectionString, dumpFilePath, cancelToken);

            var keys = archive.Entries
                .Where(entry => entry.FullName.StartsWith(KeyRing.ArchiveFolder, StringComparison.Ordinal))
                .Where(entry => Path.GetFileName(entry.FullName).Length > 0)
                .ToList();

            if (keys.Count == 0)
            {
                return;
            }

            var keyRing = Directory.CreateDirectory(install.KeyRingPath);

            foreach (var stale in keyRing.EnumerateFiles("*.xml"))
            {
                stale.Delete();
            }

            foreach (var key in keys)
            {
                key.ExtractToFile(Path.Combine(keyRing.FullName, Path.GetFileName(key.FullName)), overwrite: true);
            }
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    public static string NameFor(BackupManifest manifest)
    {
        return $"{manifest.Product.ToLowerInvariant()}-{manifest.TakenAtUtc:yyyyMMdd-HHmmss}.nsail.zip";
    }

    internal static async Task<BackupManifest> ReadManifest(ZipArchive archive, CancellationToken cancelToken)
    {
        var entry = archive.GetEntry(BackupManifest.EntryName)
            ?? throw new BackupFailure($"The archive carries no '{BackupManifest.EntryName}' and cannot be identified.");

        await using var stream = entry.Open();

        return await JsonSerializer.DeserializeAsync<BackupManifest>(stream, cancellationToken: cancelToken)
            ?? throw new BackupFailure("The archive's manifest is empty.");
    }

    static string DatabaseName(string connectionString)
    {
        return new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database ?? "";
    }
}

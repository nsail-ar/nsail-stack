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

    public static async Task<BackupManifest> Create(InstallDescriptor install, Stream destination, string? passphrase = null, CancellationToken cancelToken = default)
    {
        var workspace = Directory.CreateTempSubdirectory("nsail-backup");

        try
        {
            var dumpFilePath = Path.Combine(workspace.FullName, Postgres.DumpEntryName);

            await Postgres.Dump(install.ConnectionString, dumpFilePath, cancelToken);

            var keyRing = new DirectoryInfo(install.KeyRingPath);

            var salt = passphrase is null ? [] : ArchiveCipher.NewSalt();
            var cipher = passphrase is null ? null : ArchiveCipher.Derive(passphrase, salt, ArchiveCipher.DefaultIterations);

            var manifest = new BackupManifest
            {
                ArchiveVersion = cipher is null ? BackupManifest.PlainArchiveVersion : BackupManifest.CurrentArchiveVersion,
                Encrypted = cipher is not null,
                KeySalt = Convert.ToBase64String(salt),
                KeyIterations = cipher is null ? 0 : ArchiveCipher.DefaultIterations,
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

                await AddFile(archive, dumpFilePath, Postgres.DumpEntryName, cipher, cancelToken);

                if (keyRing.Exists)
                {
                    foreach (var key in keyRing.EnumerateFiles("*.xml"))
                    {
                        await AddFile(archive, key.FullName, KeyRing.ArchiveFolder + key.Name, cipher, cancelToken);
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

    /// <summary>The archive file, created owner-only: it holds the whole database and the key
    /// ring, and a umask-default mode would let any local account read both.</summary>
    public static FileStream CreateFile(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(path, options);
    }

    public static async Task<BackupManifest> ReadManifest(Stream source, CancellationToken cancelToken = default)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

        return await ReadManifest(archive, cancelToken);
    }

    /// <summary>Replace, not merge: the database is rebuilt from the dump and the key ring
    /// directory is emptied of the keys the archive replaces. Every refusal this can raise has
    /// already been raised by <see cref="RestorePlan"/>; nothing here asks a question.</summary>
    public static async Task Restore(InstallDescriptor install, Stream source, string? passphrase = null, CancellationToken cancelToken = default)
    {
        var workspace = Directory.CreateTempSubdirectory("nsail-restore");

        try
        {
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

            var manifest = await ReadManifest(archive, cancelToken);

            var cipher = manifest.Encrypted
                ? ArchiveCipher.Derive(
                    passphrase ?? throw new BackupFailure("This archive is encrypted and needs its passphrase."),
                    Convert.FromBase64String(manifest.KeySalt),
                    manifest.KeyIterations)
                : null;

            var dumpEntry = archive.GetEntry(Postgres.DumpEntryName)
                ?? throw new BackupFailure($"The archive carries no '{Postgres.DumpEntryName}'.");

            var dumpFilePath = Path.Combine(workspace.FullName, Postgres.DumpEntryName);

            await Extract(dumpEntry, dumpFilePath, cipher, cancelToken);

            var keys = archive.Entries
                .Where(entry => entry.FullName.StartsWith(KeyRing.ArchiveFolder, StringComparison.Ordinal))
                .Where(entry => Path.GetFileName(entry.FullName).Length > 0)
                .ToList();

            // Unsealed into the workspace before anything is replaced: a wrong passphrase fails
            // here, with the install untouched.
            var unsealed = new List<(string Name, string Path)>();

            foreach (var key in keys)
            {
                var name = Path.GetFileName(key.FullName);
                var staged = Path.Combine(workspace.FullName, "key-" + unsealed.Count);

                await Extract(key, staged, cipher, cancelToken);

                unsealed.Add((name, staged));
            }

            await Postgres.Restore(install.ConnectionString, dumpFilePath, cancelToken);

            if (keys.Count == 0)
            {
                return;
            }

            var keyRing = Directory.CreateDirectory(install.KeyRingPath);

            foreach (var stale in keyRing.EnumerateFiles("*.xml"))
            {
                stale.Delete();
            }

            foreach (var (name, staged) in unsealed)
            {
                File.Copy(staged, Path.Combine(keyRing.FullName, name), overwrite: true);
            }
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    static async Task AddFile(ZipArchive archive, string path, string entryName, ArchiveCipher? cipher, CancellationToken cancelToken)
    {
        var entry = archive.CreateEntry(entryName, entryName == Postgres.DumpEntryName ? CompressionLevel.SmallestSize : CompressionLevel.Optimal);

        await using var entryStream = entry.Open();
        await using var file = File.OpenRead(path);

        if (cipher is null)
        {
            await file.CopyToAsync(entryStream, cancelToken);

            return;
        }

        await cipher.Encrypt(file, entryStream, entryName, cancelToken);
    }

    static async Task Extract(ZipArchiveEntry entry, string path, ArchiveCipher? cipher, CancellationToken cancelToken)
    {
        await using var entryStream = entry.Open();
        await using var file = File.Create(path);

        if (cipher is null)
        {
            await entryStream.CopyToAsync(file, cancelToken);

            return;
        }

        await cipher.Decrypt(entryStream, file, entry.FullName, cancelToken);
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

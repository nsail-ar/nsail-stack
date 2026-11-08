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
    /// directory is emptied of the keys the archive replaces. <see cref="RestorePlan"/> raises the
    /// refusals the manifest decides; an encrypted archive's missing or wrong passphrase, and a
    /// truncated one, raise <see cref="BackupFailure"/> here, before the database is touched.</summary>
    public static async Task Restore(InstallDescriptor install, Stream source, string? passphrase = null, CancellationToken cancelToken = default)
    {
        var workspace = Directory.CreateTempSubdirectory("nsail-restore");

        try
        {
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);

            var manifest = await ReadManifest(archive, cancelToken);

            var cipher = manifest.Encrypted ? DeriveCipher(manifest, passphrase) : null;

            var dumpEntry = archive.GetEntry(Postgres.DumpEntryName)
                ?? throw new BackupFailure($"The archive carries no '{Postgres.DumpEntryName}'.");

            var dumpFilePath = Path.Combine(workspace.FullName, Postgres.DumpEntryName);

            await Extract(dumpEntry, dumpFilePath, cipher, cancelToken);

            var keys = archive.Entries
                .Where(entry => entry.FullName.StartsWith(KeyRing.ArchiveFolder, StringComparison.Ordinal))
                .Where(entry => Path.GetFileName(entry.FullName).Length > 0)
                .ToList();

            // Unsealed inside the ring's own directory (a mount cannot be renamed, and a move
            // across volumes is a copy) before anything is replaced: a wrong passphrase or a
            // failed extract fails here, with the install untouched.
            var keyRing = Directory.CreateDirectory(install.KeyRingPath);

            var staging = keys.Count == 0 ? null : keyRing.CreateSubdirectory(".restore-" + Guid.NewGuid().ToString("N"));

            try
            {
                foreach (var key in keys)
                {
                    await Extract(key, Path.Combine(staging!.FullName, Path.GetFileName(key.FullName)), cipher, cancelToken);
                }

                await Postgres.Restore(install.ConnectionString, dumpFilePath, cancelToken);

                if (staging is null)
                {
                    return;
                }

                foreach (var stale in keyRing.EnumerateFiles("*.xml"))
                {
                    stale.Delete();
                }

                foreach (var key in staging.EnumerateFiles())
                {
                    key.MoveTo(Path.Combine(keyRing.FullName, key.Name), overwrite: true);
                }
            }
            finally
            {
                staging?.Refresh();

                if (staging?.Exists == true)
                {
                    staging.Delete(recursive: true);
                }
            }
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    static ArchiveCipher DeriveCipher(BackupManifest manifest, string? passphrase)
    {
        if (passphrase is null)
        {
            throw new BackupFailure("This archive is encrypted and needs its passphrase.");
        }

        try
        {
            return ArchiveCipher.Derive(passphrase, Convert.FromBase64String(manifest.KeySalt), manifest.KeyIterations);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new BackupFailure("This archive's manifest carries no usable encryption parameters.");
        }
    }

    static async Task AddFile(ZipArchive archive, string path, string entryName, ArchiveCipher? cipher, CancellationToken cancelToken)
    {
        // A sealed entry is incompressible.
        var level = cipher is not null
            ? CompressionLevel.NoCompression
            : entryName == Postgres.DumpEntryName ? CompressionLevel.SmallestSize : CompressionLevel.Optimal;
        var entry = archive.CreateEntry(entryName, level);

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

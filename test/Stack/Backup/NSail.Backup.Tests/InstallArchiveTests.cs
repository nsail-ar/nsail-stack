// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.IO.Compression;
using System.Text.Json;

namespace NSail.Backup.Tests;

public sealed class InstallArchiveTests
{
    [Fact]
    public async Task TheManifestIsReadBackWithBothVersionStamps()
    {
        var manifest = Manifest();

        await using var archive = Archive(manifest);

        var read = await InstallArchive.ReadManifest(archive);

        Assert.Equal(manifest.PostgresVersion, read.PostgresVersion);
        Assert.Equal(manifest.SchemaVersion, read.SchemaVersion);
        Assert.Equal(manifest.Product, read.Product);
        Assert.Equal(BackupManifest.CurrentArchiveVersion, read.ArchiveVersion);
    }

    [Fact]
    public async Task AnArchiveWithoutAManifestCannotBeIdentifiedAndIsRefused()
    {
        await using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry(Postgres.DumpEntryName);
        }

        stream.Position = 0;

        var failure = await Assert.ThrowsAsync<BackupFailure>(() => InstallArchive.ReadManifest(stream));

        Assert.Contains(BackupManifest.EntryName, failure.Message);
    }

    [Fact]
    public void TheArchiveNamesItselfAfterTheInstallAndTheMomentItWasTaken()
    {
        Assert.Equal("optical-20260827-100000.nsail.zip", InstallArchive.NameFor(Manifest()));
    }

    static MemoryStream Archive(BackupManifest manifest)
    {
        var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = zip.CreateEntry(BackupManifest.EntryName).Open();

            JsonSerializer.Serialize(entry, manifest);
        }

        stream.Position = 0;

        return stream;
    }

    static BackupManifest Manifest()
    {
        return new BackupManifest
        {
            Product = "Optical",
            Database = "optical_main",
            PostgresVersion = "16.10",
            SchemaVersion = "20260102000000_Seed",
            TakenAtUtc = new DateTime(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc),
            HasKeyRing = true
        };
    }
}

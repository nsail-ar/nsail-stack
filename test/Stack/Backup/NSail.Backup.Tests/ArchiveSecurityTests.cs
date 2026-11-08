// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;

namespace NSail.Backup.Tests;

public sealed class ArchiveSecurityTests
{
    [Fact]
    public async Task AnArchiveFileIsCreatedReadableByItsOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nsail.zip");

        try
        {
            await using (var file = InstallArchive.CreateFile(path))
            {
                await file.WriteAsync(new byte[] { 1 });
            }

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ASealedEntryOpensWithItsPassphraseAcrossChunks()
    {
        var plain = new byte[ArchiveCipher.ChunkSize * 2 + 17];

        Random.Shared.NextBytes(plain);

        var salt = ArchiveCipher.NewSalt();
        var sealer = ArchiveCipher.Derive("correct horse", salt, 1000);

        using var sealedBytes = new MemoryStream();

        await sealer.Encrypt(new MemoryStream(plain), sealedBytes, "database.dump");

        sealedBytes.Position = 0;

        using var opened = new MemoryStream();

        await ArchiveCipher.Derive("correct horse", salt, 1000).Decrypt(sealedBytes, opened, "database.dump");

        Assert.Equal(plain, opened.ToArray());
    }

    [Fact]
    public async Task AWrongPassphraseATruncationAndASwappedNameAreRefused()
    {
        var salt = ArchiveCipher.NewSalt();
        var sealer = ArchiveCipher.Derive("right", salt, 1000);

        using var sealedBytes = new MemoryStream();

        await sealer.Encrypt(new MemoryStream(new byte[ArchiveCipher.ChunkSize + 5]), sealedBytes, "a");

        var bytes = sealedBytes.ToArray();

        await Assert.ThrowsAsync<BackupFailure>(() =>
            ArchiveCipher.Derive("wrong", salt, 1000).Decrypt(new MemoryStream(bytes), new MemoryStream(), "a"));

        await Assert.ThrowsAsync<BackupFailure>(() =>
            sealer.Decrypt(new MemoryStream(bytes), new MemoryStream(), "b"));

        await Assert.ThrowsAsync<BackupFailure>(() =>
            sealer.Decrypt(new MemoryStream(bytes[..^10]), new MemoryStream(), "a"));
    }

    [Fact]
    public void AManifestWrittenBeforeEncryptionExistedReadsAsUnencrypted()
    {
        const string old = """
            {"ArchiveVersion":1,"Product":"Optical","Database":"d","PostgresVersion":"16.1","SchemaVersion":"s","TakenAtUtc":"2026-08-27T10:00:00Z","HasKeyRing":true}
            """;

        var manifest = JsonSerializer.Deserialize<BackupManifest>(old)!;

        Assert.False(manifest.Encrypted);
        Assert.Equal(BackupManifest.PlainArchiveVersion, manifest.ArchiveVersion);
    }
}

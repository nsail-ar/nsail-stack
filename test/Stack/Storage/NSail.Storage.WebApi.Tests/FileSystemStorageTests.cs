// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Storage.FileSystem;
using System.Security.Cryptography;
using System.Text;

namespace NSail.Storage.Tests;

public sealed class FileSystemStorageTests
{
    // Sixteen mebibytes: far past the provider's 80 KiB copy buffer, so a copy that materialized
    // the object would be visible as a file that stays empty until the source is exhausted, and
    // small enough that the suite pays under a second for it.
    const long Big = 16L * 1024 * 1024;

    readonly FileSystemStorageProvider _provider = new();

    [Fact]
    public async Task PutsAndGetsAnObjectBackByteForByte()
    {
        using var container = new Container();

        var payload = Encoding.UTF8.GetBytes("the archive nsail#326 will write");

        using (var content = new MemoryStream(payload))
        {
            await _provider.Put(container.Target, "backups/2026-08-21.zip", content, "application/zip");
        }

        using var stored = await _provider.Get(container.Target, "backups/2026-08-21.zip");

        Assert.NotNull(stored);

        using var readBack = new MemoryStream();
        await stored.Content.CopyToAsync(readBack);

        Assert.Equal(payload, readBack.ToArray());
        Assert.Equal(payload.Length, stored.Length);
    }

    [Fact]
    public async Task AnswersNullForAnObjectThatIsNotThere()
    {
        using var container = new Container();

        Assert.Null(await _provider.Get(container.Target, "backups/never-written.zip"));
    }

    [Fact]
    public async Task ListsWhatWasWritten()
    {
        using var container = new Container();

        foreach (var key in (string[])["backups/a.zip", "backups/b.zip", "exports/c.csv"])
        {
            using var content = new MemoryStream([1, 2, 3]);

            await _provider.Put(container.Target, key, content, null);
        }

        var all = await _provider.List(container.Target, null);
        var backups = await _provider.List(container.Target, "backups/");

        Assert.Equal(["backups/a.zip", "backups/b.zip", "exports/c.csv"], all.Select(stored => stored.Key));
        Assert.Equal(["backups/a.zip", "backups/b.zip"], backups.Select(stored => stored.Key));
        Assert.All(all, stored => Assert.Equal(3, stored.Length));
    }

    [Fact]
    public async Task DeletesAnObjectAndSaysNothingAboutOneThatWasAlreadyGone()
    {
        using var container = new Container();

        using (var content = new MemoryStream([1]))
        {
            await _provider.Put(container.Target, "backups/a.zip", content, null);
        }

        await _provider.Delete(container.Target, "backups/a.zip");
        await _provider.Delete(container.Target, "backups/a.zip");

        Assert.Null(await _provider.Get(container.Target, "backups/a.zip"));
    }

    // The story's own claim, made measurable: an archive larger than memory is neither loaded
    // nor buffered whole. The source is not seekable and refuses to state its length, so nothing
    // downstream could size a buffer even if it wanted to — and the destination is watched WHILE
    // the source is still producing. Bytes on disk before the last chunk was read is the whole
    // proof: a provider that materialized the object would leave an empty file until the end.
    [Fact]
    public async Task WritesWhileTheSourceIsStillProducing()
    {
        using var container = new Container();

        var path = Path.Combine(container.Path, "big.bin");
        var landedEarly = 0L;

        var source = new ProbeStream(Big, produced =>
        {
            if (produced >= Big / 2 && landedEarly == 0 && File.Exists(path))
            {
                landedEarly = new FileInfo(path).Length;
            }
        });

        await _provider.Put(container.Target, "big.bin", source, null);

        Assert.True(
            landedEarly > 0,
            $"The destination held {landedEarly} bytes when half the source had been read; the object was buffered whole before anything was written.");

        Assert.Equal(Big, new FileInfo(path).Length);
    }

    // The other half of the same claim: a get hands over an open read, not the object. Anything
    // materialized would come back over memory, so the type of the stream IS the assertion.
    [Fact]
    public async Task ReadsBackWithoutMaterializingTheObject()
    {
        using var container = new Container();

        var written = await Write(container, "big.bin");

        using var stored = await _provider.Get(container.Target, "big.bin");

        Assert.NotNull(stored);
        Assert.IsNotType<MemoryStream>(stored.Content);
        Assert.Equal(Big, stored.Length);
        Assert.Equal(written, await Hash(stored.Content));
    }

    [Theory]
    [InlineData("../escaped.zip")]
    [InlineData("backups/../../escaped.zip")]
    public async Task RefusesAKeyThatClimbsOutOfItsContainer(string key)
    {
        using var container = new Container();
        using var content = new MemoryStream([1]);

        // Reached through the provider directly rather than through StorageManager: the manager
        // guards the door, and this is the second lock behind it.
        await Assert.ThrowsAsync<BusinessException>(
            () => _provider.Put(container.Target, key, content, null));

        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(container.Path)!, "escaped.zip")));
    }

    async Task<string> Write(Container container, string key)
    {
        var source = new ProbeStream(Big, _ => { });
        var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];

        // Hashed on the way in from the same generator the provider is about to be handed, so
        // the expected value is never a copy of the object held anywhere either.
        using (var expected = new ProbeStream(Big, _ => { }))
        {
            int read;

            while ((read = await expected.ReadAsync(buffer)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }

        await _provider.Put(container.Target, key, source, null);

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    static async Task<string> Hash(Stream content)
    {
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(content));
    }
}

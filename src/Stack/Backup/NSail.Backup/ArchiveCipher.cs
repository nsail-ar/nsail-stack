// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NSail.Backup;

/// <summary>Seals one archive entry under a key derived from the operator's passphrase
/// (PBKDF2-SHA256, AES-256-GCM). An entry is a random nonce prefix followed by chunks, each
/// <c>length, tag, ciphertext</c>, ending in a chunk shorter than <see cref="ChunkSize"/> so a
/// truncated entry fails instead of restoring short. The entry name is authenticated, so one
/// sealed entry cannot be passed off as another.</summary>
public sealed class ArchiveCipher
{
    public const int ChunkSize = 64 * 1024;

    public const int DefaultIterations = 600_000;

    const int KeySize = 32;
    const int NoncePrefixSize = 8;
    const int TagSize = 16;

    readonly byte[] key;

    ArchiveCipher(byte[] key)
    {
        this.key = key;
    }

    public static byte[] NewSalt()
    {
        return RandomNumberGenerator.GetBytes(16);
    }

    public static ArchiveCipher Derive(string passphrase, byte[] salt, int iterations)
    {
        return new ArchiveCipher(Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, HashAlgorithmName.SHA256, KeySize));
    }

    public async Task Encrypt(Stream plain, Stream sealedOutput, string entryName, CancellationToken cancelToken = default)
    {
        using var aes = new AesGcm(key, TagSize);

        var nonce = new byte[12];

        RandomNumberGenerator.Fill(nonce.AsSpan(0, NoncePrefixSize));

        await sealedOutput.WriteAsync(nonce.AsMemory(0, NoncePrefixSize), cancelToken);

        var associated = Encoding.UTF8.GetBytes(entryName);
        var buffer = new byte[ChunkSize];
        var cipher = new byte[ChunkSize];
        var tag = new byte[TagSize];
        var header = new byte[4];
        var index = 0u;

        while (true)
        {
            var read = await plain.ReadAtLeastAsync(buffer, ChunkSize, throwOnEndOfStream: false, cancelToken);

            BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixSize), index++);
            aes.Encrypt(nonce, buffer.AsSpan(0, read), cipher.AsSpan(0, read), tag, associated);
            BinaryPrimitives.WriteInt32BigEndian(header, read);

            await sealedOutput.WriteAsync(header, cancelToken);
            await sealedOutput.WriteAsync(tag, cancelToken);
            await sealedOutput.WriteAsync(cipher.AsMemory(0, read), cancelToken);

            if (read < ChunkSize)
            {
                return;
            }
        }
    }

    public async Task Decrypt(Stream sealedInput, Stream plain, string entryName, CancellationToken cancelToken = default)
    {
        using var aes = new AesGcm(key, TagSize);

        var nonce = new byte[12];

        await ReadExactly(sealedInput, nonce.AsMemory(0, NoncePrefixSize), cancelToken);

        var associated = Encoding.UTF8.GetBytes(entryName);
        var cipher = new byte[ChunkSize];
        var buffer = new byte[ChunkSize];
        var tag = new byte[TagSize];
        var header = new byte[4];
        var index = 0u;

        while (true)
        {
            await ReadExactly(sealedInput, header, cancelToken);

            var length = BinaryPrimitives.ReadInt32BigEndian(header);

            if (length < 0 || length > ChunkSize)
            {
                throw new BackupFailure($"'{entryName}' is not a sealed entry this build can read.");
            }

            await ReadExactly(sealedInput, tag, cancelToken);
            await ReadExactly(sealedInput, cipher.AsMemory(0, length), cancelToken);

            BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixSize), index++);

            try
            {
                aes.Decrypt(nonce, cipher.AsSpan(0, length), tag, buffer.AsSpan(0, length), associated);
            }
            catch (CryptographicException)
            {
                throw new BackupFailure($"'{entryName}' did not unseal: the passphrase is wrong or the archive is damaged.");
            }

            await plain.WriteAsync(buffer.AsMemory(0, length), cancelToken);

            if (length < ChunkSize)
            {
                return;
            }
        }
    }

    static async Task ReadExactly(Stream source, Memory<byte> destination, CancellationToken cancelToken)
    {
        try
        {
            await source.ReadExactlyAsync(destination, cancelToken);
        }
        catch (EndOfStreamException)
        {
            throw new BackupFailure("A sealed entry ends early: the archive is truncated.");
        }
    }
}

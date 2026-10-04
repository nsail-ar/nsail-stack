// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;

namespace NSail.Security;

/// <summary>
/// Reversible protection for stored secrets (tokens, keys) using AES-GCM.
/// Payload format: base64(nonce | tag | ciphertext).
/// </summary>
public class KeyProtector
{
    readonly byte[] _key;

    public KeyProtector(string encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("An encryption key is required.", nameof(encryptionKey));

        // Derive a fixed-size key from the configured value.
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
    }

    public string Protect(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, nonce.Length);
        ciphertext.CopyTo(payload, nonce.Length + tag.Length);

        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        var payload = Convert.FromBase64String(protectedValue);

        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;

        var nonce = payload.AsSpan(0, nonceSize);
        var tag = payload.AsSpan(nonceSize, tagSize);
        var ciphertext = payload.AsSpan(nonceSize + tagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}

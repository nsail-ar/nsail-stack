// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;

namespace NSail.Security;

/// <summary>
/// Reversible protection for stored secrets (tokens, keys) using AES-GCM.
/// Payload format: "v2." + base64(salt | nonce | tag | ciphertext), the key derived per payload
/// (HKDF-SHA256 over the configured value and the salt). A payload without the prefix is the
/// original format, base64(nonce | tag | ciphertext) under SHA256 of the configured value, and
/// still unseals.
/// </summary>
public class KeyProtector
{
    static readonly byte[] Info = Encoding.UTF8.GetBytes("NSail.KeyProtector");

    const string Version = "v2.";
    const int SaltSize = 16;

    readonly byte[] _secret;
    readonly byte[] _legacyKey;

    public KeyProtector(string encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("An encryption key is required.", nameof(encryptionKey));

        _secret = Encoding.UTF8.GetBytes(encryptionKey);
        _legacyKey = SHA256.HashData(_secret);
    }

    public string Protect(string value)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(Derive(salt), tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[salt.Length + nonce.Length + tag.Length + ciphertext.Length];
        salt.CopyTo(payload, 0);
        nonce.CopyTo(payload, salt.Length);
        tag.CopyTo(payload, salt.Length + nonce.Length);
        ciphertext.CopyTo(payload, salt.Length + nonce.Length + tag.Length);

        return Version + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        if (protectedValue.StartsWith(Version, StringComparison.Ordinal))
        {
            var payload = Convert.FromBase64String(protectedValue[Version.Length..]);

            if (payload.Length < SaltSize)
                throw new CryptographicException("The protected value is malformed.");

            return Open(Derive(payload[..SaltSize]), payload.AsSpan(SaltSize));
        }

        return Open(_legacyKey, Convert.FromBase64String(protectedValue));
    }

    byte[] Derive(byte[] salt)
    {
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, _secret, 32, salt, Info);
    }

    static string Open(byte[] key, ReadOnlySpan<byte> payload)
    {
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;

        if (payload.Length < nonceSize + tagSize)
            throw new CryptographicException("The protected value is malformed.");

        var nonce = payload[..nonceSize];
        var tag = payload.Slice(nonceSize, tagSize);
        var ciphertext = payload[(nonceSize + tagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}

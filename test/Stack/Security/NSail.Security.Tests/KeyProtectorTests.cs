// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;

namespace NSail.Security.Tests;

public sealed class KeyProtectorTests
{
    // The original payload format: base64(nonce | tag | ciphertext) under SHA256(key); stored payloads keep it.
    static string LegacyProtect(string key, string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(SHA256.HashData(Encoding.UTF8.GetBytes(key)), tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
    }

    [Fact]
    public void Round_trip()
    {
        var protector = new KeyProtector("k");
        var sealedValue = protector.Protect("secret ñ");

        Assert.StartsWith("v2.", sealedValue);
        Assert.Equal("secret ñ", protector.Unprotect(sealedValue));
    }

    [Fact]
    public void Each_payload_is_salted()
    {
        var protector = new KeyProtector("k");

        Assert.NotEqual(protector.Protect("x"), protector.Protect("x"));
    }

    [Fact]
    public void An_old_payload_still_unseals()
    {
        Assert.Equal("secret", new KeyProtector("k").Unprotect(LegacyProtect("k", "secret")));
    }

    [Fact]
    public void A_tampered_payload_throws()
    {
        var protector = new KeyProtector("k");
        var raw = Convert.FromBase64String(protector.Protect("secret")[3..]);
        raw[^1] ^= 1;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect("v2." + Convert.ToBase64String(raw)));
    }

    [Fact]
    public void Another_key_throws()
    {
        var sealedValue = new KeyProtector("k").Protect("secret");

        Assert.ThrowsAny<CryptographicException>(() => new KeyProtector("other").Unprotect(sealedValue));
    }

    [Fact]
    public void A_truncated_payload_throws()
    {
        Assert.ThrowsAny<CryptographicException>(() => new KeyProtector("k").Unprotect("v2.AAAA"));
        Assert.ThrowsAny<CryptographicException>(() => new KeyProtector("k").Unprotect("AAAA"));
    }
}

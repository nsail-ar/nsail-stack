// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;

namespace NSail.Security.Tests;

public sealed class PasswordHashTests
{
    // Written by the previous version: 100,000 iterations.
    static string LegacyHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

        return $"100000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    [Fact]
    public void Round_trip_at_the_current_count_needs_no_rehash()
    {
        var hashed = PasswordHash.Hash("s3cret");

        Assert.StartsWith($"{PasswordHash.Iterations}.", hashed);
        Assert.True(PasswordHash.Verify("s3cret", hashed, out var needsRehash));
        Assert.False(needsRehash);
        Assert.True(PasswordHash.Verify("s3cret", hashed));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        Assert.False(PasswordHash.Verify("nope", PasswordHash.Hash("s3cret"), out var needsRehash));
        Assert.False(needsRehash);
    }

    [Fact]
    public void A_hash_written_before_still_verifies_and_asks_for_a_rehash()
    {
        Assert.True(PasswordHash.Verify("s3cret", LegacyHash("s3cret"), out var needsRehash));
        Assert.True(needsRehash);
    }

    [Fact]
    public void A_hash_below_the_floor_is_refused()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2("s3cret", salt, 1, HashAlgorithmName.SHA256, 32);
        var weak = $"1.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        Assert.False(PasswordHash.Verify("s3cret", weak, out _));
    }

    [Fact]
    public void A_hash_above_the_ceiling_is_refused()
    {
        var parts = PasswordHash.Hash("s3cret").Split('.');

        Assert.False(PasswordHash.Verify("s3cret", $"{PasswordHash.MaximumIterations + 1}.{parts[1]}.{parts[2]}"));
    }

    [Fact]
    public void A_tampered_hash_fails()
    {
        var parts = PasswordHash.Hash("s3cret").Split('.');
        var bytes = Convert.FromBase64String(parts[2]);
        bytes[0] ^= 1;

        Assert.False(PasswordHash.Verify("s3cret", $"{parts[0]}.{parts[1]}.{Convert.ToBase64String(bytes)}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("600000.!!.!!")]
    [InlineData("600000.AAAA")]
    [InlineData("600000..")]
    public void A_malformed_hash_fails_without_throwing(string hashed)
    {
        Assert.False(PasswordHash.Verify("s3cret", hashed));
    }
}

public sealed class KeyProtectorTests
{
    // The previous payload: base64(nonce | tag | ciphertext) under SHA256(key).
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

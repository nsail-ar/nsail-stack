// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;

namespace NSail.Security;

/// <summary>
/// One-way password hashing based on PBKDF2-SHA256. Format: "{iterations}.{salt}.{hash}" (base64).
/// A stored hash under <see cref="MinimumIterations"/> or over <see cref="MaximumIterations"/> is refused.
/// </summary>
public static class PasswordHash
{
    public const int Iterations = 600_000;
    public const int MinimumIterations = 100_000;
    public const int MaximumIterations = 10_000_000;
    const int SaltSize = 16;
    const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string hashed)
    {
        return Verify(password, hashed, out _);
    }

    /// <summary>On success, <paramref name="needsRehash"/> says the stored hash has fewer iterations
    /// than <see cref="Iterations"/> and should be rewritten with <see cref="Hash"/>.</summary>
    public static bool Verify(string password, string hashed, out bool needsRehash)
    {
        needsRehash = false;

        if (string.IsNullOrEmpty(hashed))
            return false;

        var parts = hashed.Split('.');

        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)
            || iterations < MinimumIterations || iterations > MaximumIterations)
            return false;

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expected.Length != HashSize || salt.Length != SaltSize)
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return false;

        needsRehash = iterations < Iterations;

        return true;
    }
}

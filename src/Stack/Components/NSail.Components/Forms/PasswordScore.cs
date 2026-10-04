// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Scores a password from length and character variety. Vendor-free and pure so the
/// buckets can be pinned by a test rather than read off a rendered bar.</summary>
public static class PasswordScore
{
    const int ShortLength = 8;
    const int LongLength = 12;

    /// <summary>The bucket a password falls in, or null when there is nothing to score yet —
    /// an empty field earns no verdict, so the bar stays off screen until the first keystroke.</summary>
    public static PasswordStrength? Measure(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return null;
        }

        var variety = Variety(password);

        // Length alone buys nothing: a long password drawn from one alphabet is the case a
        // bar that only counted characters would call Strong.
        if (password.Length < ShortLength || variety < 2)
        {
            return PasswordStrength.Weak;
        }

        if (password.Length >= LongLength && variety >= 3)
        {
            return PasswordStrength.Strong;
        }

        return PasswordStrength.Medium;
    }

    static int Variety(string password)
    {
        var lower = false;
        var upper = false;
        var digit = false;
        var other = false;

        foreach (var character in password)
        {
            if (char.IsLower(character))
            {
                lower = true;
            }
            else if (char.IsUpper(character))
            {
                upper = true;
            }
            else if (char.IsDigit(character))
            {
                digit = true;
            }
            else
            {
                other = true;
            }
        }

        return (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (other ? 1 : 0);
    }
}

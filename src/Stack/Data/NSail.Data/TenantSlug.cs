// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>The one gate between what a proxy says the tenant is and what reaches a database
/// name. The working set is lowercase alphanumerics plus the hyphen, and anything outside it
/// is <b>refused rather than repaired</b>: stripping a character folds two different words
/// onto one tenant, which is the leak, not the fix. Case is the single normalization, because
/// a host label is case-insensitive to begin with.</summary>
public static class TenantSlug
{
    // A Postgres identifier is cut at 63 bytes without a word, so nothing longer can name a
    // database honestly — and the composed name is checked again where it is composed.
    public const int MaxLength = 63;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return null;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                continue;
            }

            // A hyphen separates, so it is never an edge: a name that opens or closes with one
            // is the truncation of some other name, and two names must not meet here.
            if (character == '-' && index > 0 && index < value.Length - 1)
            {
                continue;
            }

            return null;
        }

        return value.ToLowerInvariant();
    }
}

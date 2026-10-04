// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Components;

/// <summary>The one rule for matching what a user typed against what a screen shows:
/// case- and accent-insensitive, so "credito" finds "Crédito". It is the same rule the
/// database already applies through its model-wide collation, kept in one place so a
/// client-side filter and a server-side one cannot answer differently.</summary>
public static class NsSearchText
{
    public static bool Contains(string? text, string? term)
    {
        if (string.IsNullOrEmpty(term))
        {
            return true;
        }

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            text, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    }
}

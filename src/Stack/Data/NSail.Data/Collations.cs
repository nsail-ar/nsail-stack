// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Collations the database declares and the model assigns by name.</summary>
public static class Collations
{
    /// <summary>Case- and accent-insensitive comparison. Every text column uses it unless
    /// the property is marked <see cref="CaseSensitiveAttribute"/>.</summary>
    public const string CaseInsensitive = "case_insensitive";

    internal const string CaseInsensitiveLocale = "und-u-ks-level1";

    internal const string CaseInsensitiveProvider = "icu";
}

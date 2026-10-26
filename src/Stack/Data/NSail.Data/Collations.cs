// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Collations the database declares and the model assigns by name.</summary>
public static class Collations
{
    /// <summary>Case- and accent-insensitive comparison. Every text column uses it unless
    /// the property is marked <see cref="CaseSensitiveAttribute"/>.</summary>
    public const string CaseInsensitive = "case_insensitive";

    internal const string CaseInsensitiveLocale = "und-u-ks-level1";

    internal const string CaseInsensitiveProvider = "icu";

    // Postgres types that hold a string but are not text, so a COLLATE on them is refused and
    // the migration that creates the column fails. The roster is the exception to
    // `DbContextExtensions.ApplyCaseInsensitiveText`, which is the rule, and it is spelled out
    // rather than inverted: skipping every column type the model
    // NAMED would also skip `text`, `varchar(120)` and `citext`, which are the plain text this
    // is all about, and dropping the rule for one of those stops a search matching with nothing
    // on the entity to say why. A type nobody listed here fails its own migration out loud,
    // which is the cheaper mistake (principles.md 4).
    static readonly string[] Untextual =
        ["json", "jsonb", "bytea", "uuid", "xml", "inet", "cidr", "macaddr", "tsvector", "tsquery", "hstore"];

    /// <summary>Whether a column of this type can carry a collation at all — asked of the type
    /// the model named for the property, and true where it named none.</summary>
    internal static bool Collatable(string? columnType)
    {
        if (string.IsNullOrEmpty(columnType))
        {
            return true;
        }

        var name = columnType.Split('(')[0].Trim();

        return !Untextual.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}

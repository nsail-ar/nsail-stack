// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Privacy;

/// <summary>The words a <see cref="PersonalData"/> kind is written with in a column's
/// comment, and read back from one. The comment is the only place a program outside the
/// app can learn that a column holds a person's datum — Postgres's <c>col_description</c>,
/// which every migration carries and every catalog read answers — so the writer and the
/// reader share this one spelling instead of each keeping its own.</summary>
public static class PersonalDataComment
{
    /// <summary>What marks the comment as this declaration and not prose somebody wrote
    /// about the column. A comment without it declares nothing, which is what a column of
    /// a database older than the rule says.</summary>
    public const string Prefix = "personal:";

    // The word per kind, written out rather than taken from the enum member's name: the
    // comment is on columns of databases that outlive this assembly, so renaming a kind in
    // code must not silently re-spell what is already in the catalog.
    static readonly (PersonalData Kind, string Word)[] Words =
    [
        (PersonalData.Name, "name"),
        (PersonalData.Document, "document"),
        (PersonalData.Email, "email"),
        (PersonalData.Phone, "phone"),
        (PersonalData.Address, "address"),
        (PersonalData.WebAddress, "web-address"),
        (PersonalData.FreeText, "free-text"),
        (PersonalData.BirthDate, "birth-date"),
    ];

    /// <summary>The comment that declares <paramref name="kind"/>. Throws for a kind that
    /// has no word, so a kind added to the enum alone fails where it is written and not on
    /// a catalog read months later.</summary>
    public static string For(PersonalData kind)
    {
        foreach (var (candidate, word) in Words)
        {
            if (candidate == kind)
            {
                return Prefix + word;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, "No comment word is declared for this personal-data kind.");
    }

    /// <summary>The kind <paramref name="comment"/> declares, or null where it declares
    /// none — no comment, prose, or a word this version does not know. An unknown word
    /// reads as no declaration rather than throwing: the catalog is written by whatever
    /// version ran the migration, and a copy must not fall over because a newer kind
    /// reached it.</summary>
    public static PersonalData? In(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var trimmed = comment.Trim();

        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var word = trimmed[Prefix.Length..];

        foreach (var (kind, candidate) in Words)
        {
            if (string.Equals(word, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }
}

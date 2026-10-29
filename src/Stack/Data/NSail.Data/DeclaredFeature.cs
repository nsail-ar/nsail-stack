// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>One entitlement key the build running on this install declares — what it is called,
/// which of the two kinds it is, and what it answers for a tenant nobody has written it for.
/// <b>The install publishes it; nothing in the app reads it.</b> It is the mirror of
/// <see cref="TenantState"/>: that row is written from outside and read here, this one is written
/// here and read from outside, and the reason is the same one twice over — whoever operates the
/// fleet cannot be made to hold in their head what each version offers to sell, and the program
/// that writes the bags may be newer than the install it writes them to.
///
/// <para>The app's own answer about a key is <c>Features</c> (NSail.Security), which this table is
/// written from: a second reader here would be the same question answered twice.</para>
///
/// <para>It is <see cref="IInstallScoped"/> for the plain reason that a key is the install's and
/// not a tenant's: no tenant column is stamped on it and no filter hides it. And it is
/// <b>rewritten whole on every start</b>, so the table states what THIS build declares — a key
/// that left the build leaves the table with it, and the same build twice says the same
/// thing.</para></summary>
public class DeclaredFeature : IInstallScoped
{
    /// <summary>The key, as the module that enforces it spells it. The primary key, under the
    /// model's own case-insensitive collation, which is the <c>Features</c> catalog's rule for
    /// two spellings of one key.</summary>
    [NotPersonal]
    public required string Key { get; set; }

    /// <summary>Which of the two kinds the key is, as <c>FeatureKind</c> spells the word. Text
    /// rather than an enum for the reason the whole of this contract is text: the column is read
    /// by a program outside this codebase, in plain SQL, and a word that reader does not know has
    /// to leave it reading the rest of the row rather than failing on it.</summary>
    [NotPersonal]
    public required string Kind { get; set; }

    /// <summary>What the key answers where a tenant's bag is silent, for a flag.</summary>
    public bool Enabled { get; set; }

    /// <summary>What it answers where the bag is silent, for a ceiling, or null for no ceiling at
    /// all.</summary>
    public decimal? Limit { get; set; }
}

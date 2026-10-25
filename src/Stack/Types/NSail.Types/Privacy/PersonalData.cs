// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Privacy;

/// <summary>What kind of person's datum a column holds, as declared on the property
/// (<c>[Personal]</c>) and carried to the column's comment. Down here in
/// <c>NSail.Types</c> because both ends of that carriage read the same words and neither
/// may own them: the model pass that writes the comment reaches this through
/// <c>NSail.Data</c>, and whatever reads the catalog back — a fleet runner anonymizing a
/// tenant's copy, which knows no product and no entity — through the Sdk it is built
/// from.
///
/// <para>The kind says what a plausible fake looks like, and nothing else: it is not a
/// sensitivity level and not a retention rule. A value that is secret rather than personal
/// — a key, a token, a hash — is <c>[NotPersonal]</c> and belongs to whatever neutralizes
/// credentials.</para></summary>
public enum PersonalData
{
    /// <summary>A person's or a household's name, in whole or in part.</summary>
    Name,

    /// <summary>A number an authority issued to one person — an identity document, a tax
    /// id, a licence, an insurance member number.</summary>
    Document,

    /// <summary>An email address.</summary>
    Email,

    /// <summary>A telephone number, in any shape it was written.</summary>
    Phone,

    /// <summary>Where somebody is — a street, a floor, a city written against a person, a
    /// pair of coordinates.</summary>
    Address,

    /// <summary>Prose somebody wrote about a person and nobody can parse: a note, a
    /// diagnosis, a reason, a serialized payload. Nothing can be preserved about its shape,
    /// so a fake replaces the whole of it.</summary>
    FreeText,

    /// <summary>A date that identifies a person — a date of birth, a date of death.</summary>
    BirthDate,
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Privacy;

namespace NSail.Data;

/// <summary>Declares that the column holds a person's datum, of this kind. Read by one pass
/// over the whole model (<c>ApplySetups</c>) that writes the kind into the column's comment,
/// so the declaration rides the migration into the catalog and a program that knows no
/// product — a fleet runner copying a tenant's rows somewhere people walk — can read which
/// columns it has to fake.
///
/// <para>Allowed on any property, demanded on every text one: a date of birth is a date
/// column and declares itself here too, while a text column carrying neither this nor
/// <see cref="NotPersonalAttribute"/> reds the suite that sweeps the model. The pair is
/// exhaustive on purpose — silence would read as "not personal" and that is the answer
/// nobody should be able to give by forgetting.</para></summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonalAttribute : Attribute
{
    public PersonalAttribute(PersonalData kind)
    {
        Kind = kind;
    }

    /// <summary>What the column holds, which is what says how a fake of it is made.</summary>
    public PersonalData Kind { get; }
}

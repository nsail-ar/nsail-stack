// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Declares that the column holds no person's datum — a code, a status, a slug, a
/// currency, a key. The other half of <see cref="PersonalAttribute"/>, and the reason a
/// text column cannot stay unanswered.
///
/// <para>It writes nothing: a column with no comment is a column with no personal datum,
/// so ~250 "not personal" comments would be schema every reader has to discount. What
/// makes the declaration unforgettable is the sweep that demands one, not a mark in the
/// catalog.</para></summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotPersonalAttribute : Attribute
{
}

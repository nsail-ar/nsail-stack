// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Components;

/// <summary>One thing a module found for what was typed in the drawer's search box. It is not
/// a <see cref="NavMenuItem"/>: an entry is a door the app always has and the merge arranges,
/// while this is a record the term produced — nothing merges it, nothing counts it, and it is
/// gone with the next keystroke.</summary>
public sealed class NavMenuResult
{
    /// <summary>The word drawn on the row, already composed by whoever found it: a result is
    /// data and not a door, so there is no NavMenu.{Name} catalog entry behind it.</summary>
    public required string Label { get; init; }

    /// <summary>The second line under the label — what tells two records wearing one label
    /// apart, and what shows the part of the record that actually matched where it is not the
    /// label. Absent draws one line.</summary>
    public string? Detail { get; init; }

    /// <summary>Where pressing it lands, resolved by whoever found it: a result's address is a
    /// route the app's own RouteTable answers for, plus whatever query the surface it opens
    /// travels as, and neither is a thing the drawer can derive.</summary>
    public required string Url { get; init; }

    public Glyph? Icon { get; init; }
}

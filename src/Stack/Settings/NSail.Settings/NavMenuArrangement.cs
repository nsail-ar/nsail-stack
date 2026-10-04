// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Settings;

/// <summary>What one install did to its own menu: the rows it reordered, renamed or hid. Stored
/// as a settings POCO under System scope — the drawer is the company's, not a way of working —
/// so a shop changes it without waiting for a release. It says only what it CHANGES: an entry no
/// row names keeps exactly the place its module contributed, which is what lets a release add a
/// screen to an install that has already arranged its menu.</summary>
[SystemSettings]
public sealed class NavMenuArrangement
{
    public List<NavMenuArrangementEntry> Entries { get; set; } = [];
}

/// <summary>One stored row of an install's arrangement. The flat shape NavMenuEntry already
/// declares, minus what only code can carry: a row names no page, so an install arranges the
/// doors its modules planted and cannot invent one that opens nowhere.</summary>
public sealed class NavMenuArrangementEntry
{
    /// <summary>How long a word an install may give one of its own doors. Generous for a label
    /// and mean for a drawer: past it the entry is no longer a name, it is a sentence, and the
    /// drawer it has to fit in is one gutter wide. Refused on the wire by
    /// NavMenuArrangementValidator, which is where a bound on a list's ITEMS can be read.</summary>
    public const int LabelLength = 100;

    /// <summary>How much markup a glyph is. An icon is a path, not a picture: past this it is
    /// something else pasted into a menu row.</summary>
    public const int IconLength = 8000;

    /// <summary>The entry this row is about — the same Name the module contributed, which is
    /// the merge key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The Name of the entry this one hangs under; null leaves it where it was
    /// contributed.</summary>
    public string? Parent { get; set; }

    /// <summary>Position among its siblings; null says nothing, which is what a row that only
    /// renames or hides carries.</summary>
    public int? Order { get; set; }

    public bool Shown { get; set; } = true;

    public Glyph? Icon { get; set; }

    /// <summary>The install's own word for the entry, by language code — the shop's noun, not a
    /// translation of the module's, so it is stored beside the arrangement and not in a catalog.
    /// A language nobody wrote a word for reads the module's own string.</summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>The install's word for this language, null where it wrote none. Null is not a
    /// gap to fill with another language's word: a shop that renamed its drawer in the one
    /// language it serves said nothing about the others, and the module's own string is a real
    /// answer there — where inventing a translation from a word the shop typed is not.</summary>
    public string? Label(string language)
    {
        if (Labels is null || language is null)
        {
            return null;
        }

        return Labels.TryGetValue(language, out var own) ? own : null;
    }
}

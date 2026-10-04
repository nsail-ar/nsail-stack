// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
using NSail.Settings;

namespace NSail.Components;

/// <summary>One row of an app's declared arrangement: a name, where it hangs, what order it
/// takes and whether it shows, plus what a leaf needs to open its page. Flat on purpose — the
/// nesting is a Parent naming another row, so the whole arrangement is a list of values a
/// settings row could hold and Resolve could read in the declaration's place.</summary>
public sealed class NavMenuEntry
{
    public required string Name { get; init; }

    /// <summary>The Name of the entry this one hangs under; null leaves it at the level the
    /// entry of that Name was contributed at, which for a row of the app's own is the first.</summary>
    public string? Parent { get; init; }

    /// <summary>Position among its siblings, and the Weight the resolved item carries into the
    /// merge — so an app's own entries share one scale with every kit's. Null says nothing
    /// about the position, which is what a row that only hides or re-icons a kit's entry
    /// carries.</summary>
    public int? Order { get; init; }

    public bool Shown { get; init; } = true;

    /// <summary>The word to draw the entry with, for a row that renames one. Null leaves the
    /// label to the NavMenu.{Name} string, which is how every declared row carries it: a product
    /// that writes its market's noun writes a string, and only an install arranging its own menu
    /// from stored data has a word that no catalog can hold.</summary>
    public string? Label { get; init; }

    public Glyph? Icon { get; init; }

    public Type? PageType { get; init; }

    public object? Parameters { get; init; }

    /// <summary>The message a session must be able to send for the row to be drawn, ANDed with
    /// the destination page's own gate. A row carrying only this is how an app tightens a door a
    /// kit planted — the drawer's half of a permission, never the route's (permissions.md).</summary>
    public Type? Permission { get; init; }

    public bool IsSeparator { get; init; }

    /// <summary>A line at the given order, between the entries above and below it.</summary>
    public static NavMenuEntry Separator(int order)
    {
        return new NavMenuEntry
        {
            Name = string.Empty,
            Order = order,
            IsSeparator = true
        };
    }

    /// <summary>The arrangement as the menu takes it: one item per row, each carrying the
    /// Parent it named, because NavMenuItem.Merge is what nests them and it can only unite a
    /// row with a kit's entry of the same Name if the two arrive at one level. A row that does
    /// not show arrives marked rather than dropped, so it takes the kit's entry of that Name
    /// out of the drawer with it; a Parent nobody contributes throws there too, where the
    /// whole tree is known and a kit's group counts as declared.</summary>
    public static IReadOnlyList<NavMenuItem> Resolve(IReadOnlyList<NavMenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Verify(entries);

        return entries
            .OrderBy(entry => entry.Order ?? 0)
            .Select(Convert)
            .ToList();
    }

    /// <summary>An install's STORED arrangement as the menu takes it, read against what the
    /// modules actually contributed. Two things make it a different reading from the declared
    /// one above, and both come from the rows being data rather than code:
    ///
    /// A row naming an entry nobody contributed is IGNORED, where a declaration throws. A
    /// declaration is written against the composition it ships with, so a name nothing answers
    /// is a typo somebody can fix; a stored row outlives the release that could — a kit
    /// unmounted, a screen retired — and a throw there would take the whole drawer down at every
    /// install that had ever arranged its menu, with no release able to undo it.
    ///
    /// A row says only what it CHANGES, so an entry no row names arrives untouched: the
    /// arrangement is merged onto the contributions, never substituted for them, and a screen a
    /// release adds surfaces at an install that arranged its menu years ago. It carries no
    /// Permission for the same reason it carries no PageType: an install arranges the doors its
    /// modules planted and cannot author a gate, which is a grant's job and not a drawer's.</summary>
    public static IReadOnlyList<NavMenuItem> Resolve(
        NavMenuArrangement arrangement,
        IEnumerable<NavMenuItem> contributed,
        string language)
    {
        ArgumentNullException.ThrowIfNull(arrangement);

        var parents = new Dictionary<string, string?>(StringComparer.Ordinal);

        Collect(contributed, parents);

        var rows = new List<NavMenuArrangementEntry>();
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in arrangement.Entries.OrderBy(row => row.Order ?? 0))
        {
            if (row is null || !parents.ContainsKey(row.Name) || !named.Add(row.Name))
            {
                continue;
            }

            rows.Add(row);

            if (row.Parent is not null && parents.ContainsKey(row.Parent))
            {
                parents[row.Name] = row.Parent;
            }
        }

        return rows
            .Select(row => new NavMenuItem
            {
                Name = row.Name,
                // A parent nothing answers to is dropped and the entry stays where it was
                // contributed, for the same reason the row itself is: this half of a stored row
                // can go stale on its own, when the group it named leaves with its kit. A parent
                // that closes a loop goes the same way — Merge throws on one, and a drawer an
                // install can put out of reach of its own editor is not a drawer.
                Parent = Hangs(row.Name, parents) ? parents[row.Name] : null,
                Icon = row.Icon,
                Weight = row.Order,
                Visible = row.Shown ? null : false,
                Label = row.Label(language)
            })
            .ToList();
    }

    static bool Hangs(string name, Dictionary<string, string?> parents)
    {
        var walked = new HashSet<string>(StringComparer.Ordinal) { name };
        var parent = parents[name];

        while (parent is not null)
        {
            if (!walked.Add(parent))
            {
                return false;
            }

            parent = parents.TryGetValue(parent, out var next) ? next : null;
        }

        return parents[name] is not null;
    }

    static void Collect(IEnumerable<NavMenuItem> items, Dictionary<string, string?> parents)
    {
        foreach (var item in items)
        {
            if (!item.IsSeparator)
            {
                parents[item.Name] = item.Parent ?? (parents.TryGetValue(item.Name, out var known) ? known : null);
            }

            Collect(item.Items, parents);
        }
    }

    static NavMenuItem Convert(NavMenuEntry entry)
    {
        if (entry.IsSeparator)
        {
            return NavMenuItem.Separator(entry.Order ?? 0);
        }

        return new NavMenuItem
        {
            Name = entry.Name,
            Parent = entry.Parent,
            Icon = entry.Icon,
            PageType = entry.PageType,
            Parameters = entry.Parameters,
            Permission = entry.Permission,
            Weight = entry.Order,
            // Shown is the row's answer only when it is NO: an arrangement says what it
            // changes, and a row that shows is saying nothing about a kit's own decision.
            Visible = entry.Shown ? null : false
        };
    }

    static void Verify(IReadOnlyList<NavMenuEntry> entries)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                continue;
            }

            if (!declared.Add(entry.Name))
            {
                throw new InvalidOperationException(
                    $"The arrangement declares '{entry.Name}' twice; a name is the merge key and cannot name two places.");
            }
        }
    }
}

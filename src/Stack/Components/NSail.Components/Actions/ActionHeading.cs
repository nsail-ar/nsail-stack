// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
using NSail.Localization;

namespace NSail.Components;

/// <summary>A heading over the acts that name it (ActionItem.Heading), as a presenter that
/// groups draws it: its key, its glyph and its acts in the order they were handed.</summary>
public sealed class ActionHeading
{
    public required string Name { get; init; }

    public Glyph? Icon { get; init; }

    public required IReadOnlyList<ActionItem> Items { get; init; }

    /// <summary>"Actions.Headings.{Name}", otherwise the raw name — resolved at render, never a
    /// string the contributor wrote, since a contributor has no culture.</summary>
    public string GetLabel(StringManager strings)
    {
        ArgumentNullException.ThrowIfNull(strings);

        return strings.Translate($"Actions.Headings.{Name}", Name);
    }

    /// <summary>The headings the acts name, in the order each first appears; an act with no
    /// Heading sits under none and is the presenter's to place. A heading's glyph is the last
    /// one its acts set.</summary>
    public static IReadOnlyList<ActionHeading> Of(IEnumerable<ActionItem> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        return actions
            .Where(a => a.Heading is not null)
            .GroupBy(a => a.Heading!, StringComparer.Ordinal)
            .Select(g => new ActionHeading
            {
                Name = g.Key,
                Icon = g.LastOrDefault(a => a.HeadingIcon is not null)?.HeadingIcon,
                Items = g.ToList(),
            })
            .ToList();
    }
}

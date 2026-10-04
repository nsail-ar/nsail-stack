// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Settings;

/// <summary>Node of the settings tree: a group has Items, a leaf links to the page the
/// module draws itself.</summary>
public sealed class SettingsItem
{
    public required string Name { get; init; }

    public Glyph? Icon { get; init; }

    /// <summary>Destination page of a leaf; a group leaves it null.</summary>
    public Type? PageType { get; init; }

    public IReadOnlyList<SettingsItem> Items { get; init; } = [];

    /// <summary>Shared bucket name a leaf or group joins across contributors — every item
    /// naming the same Group ends up under one menu entry, merged by NavMenuItem.Merge (the
    /// same mechanism that already unifies same-named top-level menu contributions), so no
    /// two kits need to know about each other to land in it. Null keeps today's flat
    /// rendering, still the default.</summary>
    public string? Group { get; init; }

    /// <summary>Icon for the shared Group entry this item joins, not for the item itself —
    /// ignored when Group is null. Contributors sharing a Group need not agree: the last one
    /// contributed wins, same as any other merged icon.</summary>
    public Glyph? GroupIcon { get; init; }

    /// <summary>Sort key across all contributors, same meaning as NavMenuItem.Weight — the
    /// trade's own material (domain, 10-50) orders before cross-cutting machinery (system,
    /// 80+). On a grouped item this is the GROUP's weight, not the leaf's own position
    /// inside it (same convention as GroupIcon): contributors sharing a Group need not
    /// agree, the last one wins once NavMenuItem.Merge folds them.</summary>
    public int Weight { get; init; }
}

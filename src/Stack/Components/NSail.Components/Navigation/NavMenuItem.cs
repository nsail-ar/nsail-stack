// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Components;

/// <summary>Flat navigation entry: a leaf names its destination page, a group has Items.
/// The URL is resolved from PageType through the RouteTable, never written.</summary>
public sealed class NavMenuItem
{
    public required string Name { get; init; }

    public Glyph? Icon { get; init; }

    public Type? PageType { get; init; }

    /// <summary>Route parameters as an anonymous object, for a page whose template has tokens.</summary>
    public object? Parameters { get; init; }

    public IReadOnlyList<NavMenuItem> Items { get; init; } = [];

    /// <summary>Name of the entry this one hangs under, null leaving it where it was
    /// contributed. Merge reads it as a statement about the entry of this Name wherever that
    /// entry came from, which is how an app moves a kit's door without rewriting it.</summary>
    public string? Parent { get; init; }

    /// <summary>Sort key across all contributors; ties keep contribution order. Null says
    /// nothing about the position, which is what an override that only renames or hides
    /// carries, and sorts where a zero would.</summary>
    public int? Weight { get; init; }

    /// <summary>False takes the entry and its whole branch out of the drawer. The entry stays
    /// in the merged tree: Find still answers for the pages under it, so a screen reached some
    /// other way keeps the glyph on its own title bar.</summary>
    public bool? Visible { get; init; }

    /// <summary>The word this entry is drawn with, when somebody said one. Null is the ordinary
    /// case and leaves the label where it has always been — the NavMenu.{Name} string, resolved
    /// through the catalogs — so only an install that renamed the entry in its own stored
    /// arrangement carries text here.</summary>
    public string? Label { get; init; }

    /// <summary>A rule between two families of entries rather than a place to go: it names no
    /// page, holds no children, and is drawn where whoever composed the menu put it.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>The message a session must be able to send for this door to be drawn, ANDed
    /// with whatever the branch below already said — never a replacement for it, and never a
    /// role name. It is the one case the destination page cannot answer: two doors onto one
    /// page, where the page's own gate is the same for both and only the door differs. It
    /// hides the door and not the route: the page keeps deciding who may open it (permissions.md).</summary>
    public Type? Permission { get; init; }

    /// <summary>A line at the given weight. Nameless: a separator that carries a label is a
    /// section header, the same primitive with a second job, and that one is not built.</summary>
    public static NavMenuItem Separator(int weight)
    {
        return new NavMenuItem
        {
            Name = string.Empty,
            IsSeparator = true,
            Weight = weight
        };
    }

    /// <summary>Merges contributions sharing a Name into one entry, at every level. The
    /// Name is the merge key and already the NavMenu.{Name} localization key, so merged
    /// entries carry identical labels by construction — which is why renaming a kit's door
    /// is a string an app registers after the kit's and never a field here.
    ///
    /// A kit plants its menu and the app arranges it: Weight, Icon, Parent, Visible and
    /// Permission take the LAST non-null value contributed, so a contributor registered after the kits emits
    /// the same Name carrying only the fields it wants to change and leaves the rest as the
    /// kit put them. PageType and Parameters go the other way — the first non-null wins —
    /// because they are what the entry IS, not where it sits. Parent moves the entry under
    /// the named group, where it merges with whatever already answers to its Name; Visible
    /// false takes it and its branch out of the drawer without taking it out of the tree.
    /// A separator is exempt from all of it: it carries no name to merge on, so it passes
    /// through by identity and takes its place among the merged entries by Weight alone.</summary>
    /// <param name="collapseSingleChildGroups">Whether a group left holding exactly one child
    /// gives up its level to that child. The SETTINGS tree asks for it, and only it: a
    /// settings group is a list of screens, so a fold around a single screen is empty
    /// ceremony (Leonardo, on Talleres). The main nav never asks, because the fold hands the
    /// place to the CHILD, label and all, and there the section's name is the map: Compras
    /// stays Compras, it does not become Órdenes de Compra (Leonardo, on Compras: "me gustaba
    /// el menu compras, que vuelva"). A main-nav section with one screen is written flat by
    /// its own contributor instead, keeping the name (navigation.md).</param>
    public static IReadOnlyList<NavMenuItem> Merge(
        IEnumerable<NavMenuItem> items,
        bool collapseSingleChildGroups = false)
    {
        var united = Unite(items, collapseSingleChildGroups);
        var arranged = Arrange(united, collapseSingleChildGroups);

        // OrderBy is stable, so equal weights keep the first-appearance order Unite left.
        return arranged
            .Select(item => Fold(item, collapseSingleChildGroups))
            .OrderBy(item => item.Weight ?? 0)
            .ToList();
    }

    /// <summary>The glyph a title bar wears at an ADDRESS: the entry naming the page routed
    /// there, or the nearest ancestor address that has one — so a create, an edit and a detail
    /// hanging under a list wear the list's glyph without being given it, and nobody writes by
    /// hand what the route table and the merged tree already say together.
    ///
    /// The walk stops BEFORE the app root: every address ends there, so the home's glyph would
    /// otherwise fill the slot of every page that derived none, and icons are chosen rather
    /// than defaulted (intentional-ui.md). The root's own address is the empty path and is
    /// answered by the first step, like any other.</summary>
    public static Glyph? GlyphFor(IReadOnlyList<NavMenuItem> items, RouteTable routes, string address)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(address);

        // The path alone: the walk steps up by cutting at the last separator, and a '/' inside
        // a surface's own query string is not a step in the address.
        for (var candidate = SurfaceQuery.RoutePath(address)!; ; )
        {
            if (routes.Match(candidate)?.PageType is { } pageType
                && Find(items, pageType)?.Icon is { } icon)
            {
                return icon;
            }

            var step = candidate.LastIndexOf('/');

            if (step <= 0)
            {
                return null;
            }

            candidate = candidate[..step];
        }
    }

    /// <summary>The item, at any depth, whose PageType matches — what GlyphFor asks at each
    /// address it walks. Null when the page has no menu entry.</summary>
    public static NavMenuItem? Find(IEnumerable<NavMenuItem> items, Type pageType)
    {
        foreach (var item in items)
        {
            if (item.PageType == pageType)
            {
                return item;
            }

            if (Find(item.Items, pageType) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // One entry per Name, in first-appearance order, each holding what every contribution
    // under that Name said. The slots hold every separator by itself: a separator is placed,
    // not named, so folding two of them by their shared empty name would swallow one of the
    // two lines somebody asked for.
    static List<NavMenuItem> Unite(IEnumerable<NavMenuItem> items, bool collapseSingleChildGroups)
    {
        var order = new List<NavMenuItem>();
        var grouped = new Dictionary<string, List<NavMenuItem>>();

        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                order.Add(item);

                continue;
            }

            if (!grouped.TryGetValue(item.Name, out var group))
            {
                group = [];
                grouped[item.Name] = group;
                order.Add(item);
            }

            group.Add(item);
        }

        var result = new List<NavMenuItem>();

        foreach (var first in order)
        {
            if (first.IsSeparator)
            {
                result.Add(first);

                continue;
            }

            var group = grouped[first.Name];

            result.Add(new NavMenuItem
            {
                Name = first.Name,
                Icon = group.Select(item => item.Icon).LastOrDefault(icon => icon is not null),
                PageType = group.Select(item => item.PageType).FirstOrDefault(pageType => pageType is not null),
                Parameters = group.Select(item => item.Parameters).FirstOrDefault(parameters => parameters is not null),
                Parent = group.Select(item => item.Parent).LastOrDefault(parent => parent is not null),
                Weight = group.Select(item => item.Weight).LastOrDefault(weight => weight is not null),
                Visible = group.Select(item => item.Visible).LastOrDefault(visible => visible is not null),
                Label = group.Select(item => item.Label).LastOrDefault(label => label is not null),
                Permission = group.Select(item => item.Permission).LastOrDefault(permission => permission is not null),
                Items = Merge(group.SelectMany(item => item.Items), collapseSingleChildGroups)
            });
        }

        return result;
    }

    // Where each entry hangs, once every contribution to its Name has been read: an entry
    // naming a Parent leaves this level and joins that group's children, which is the whole
    // of an app moving a kit's first-level door into a section of its own.
    static List<NavMenuItem> Arrange(List<NavMenuItem> level, bool collapseSingleChildGroups)
    {
        if (!level.Any(item => item.Parent is not null))
        {
            return level;
        }

        var byName = level
            .Where(item => !item.IsSeparator)
            .ToDictionary(item => item.Name, StringComparer.Ordinal);

        foreach (var item in level.Where(item => item.Parent is not null))
        {
            Verify(item, byName);
        }

        var moved = level
            .Where(item => item.Parent is not null)
            .ToLookup(item => item.Parent!, StringComparer.Ordinal);

        return level
            .Where(item => item.Parent is null)
            .Select(item => Attach(item, moved, collapseSingleChildGroups))
            .ToList();
    }

    static void Verify(NavMenuItem item, Dictionary<string, NavMenuItem> byName)
    {
        var walked = new HashSet<string>(StringComparer.Ordinal) { item.Name };
        var parent = item.Parent;

        while (parent is not null)
        {
            // Silently leaving the entry where it was would hide the typo behind a door that
            // still opens, the same call the arrangement makes about a name declared twice.
            if (!byName.TryGetValue(parent, out var target))
            {
                throw new InvalidOperationException(
                    $"Nav entry '{item.Name}' hangs under '{parent}', which nothing contributes beside it.");
            }

            if (!walked.Add(parent))
            {
                throw new InvalidOperationException(
                    $"Nav entry '{item.Name}' hangs under itself through '{parent}'.");
            }

            parent = target.Parent;
        }
    }

    static NavMenuItem Attach(
        NavMenuItem item,
        ILookup<string, NavMenuItem> moved,
        bool collapseSingleChildGroups)
    {
        if (item.IsSeparator || !moved.Contains(item.Name))
        {
            return item;
        }

        // The moved entry arrives LAST among the group's children, so its own Weight and Icon
        // win the second merge the same way they won the first. Its Parent is spent getting
        // it here and does not travel down, where it would name a group that is not there.
        var children = item.Items.Concat(
            moved[item.Name].Select(child => Detach(Attach(child, moved, collapseSingleChildGroups))));

        return new NavMenuItem
        {
            Name = item.Name,
            Icon = item.Icon,
            PageType = item.PageType,
            Parameters = item.Parameters,
            Parent = item.Parent,
            Weight = item.Weight,
            Visible = item.Visible,
            Label = item.Label,
            Permission = item.Permission,
            Items = Merge(children, collapseSingleChildGroups)
        };
    }

    static NavMenuItem Detach(NavMenuItem item)
    {
        return new NavMenuItem
        {
            Name = item.Name,
            Icon = item.Icon,
            PageType = item.PageType,
            Parameters = item.Parameters,
            Weight = item.Weight,
            Visible = item.Visible,
            Label = item.Label,
            Permission = item.Permission,
            IsSeparator = item.IsSeparator,
            Items = item.Items
        };
    }

    // A group that folds down to exactly one child does not earn a level of its own: the
    // child takes the group's place, inheriting its position, but keeps its own icon and
    // label. A leaf never has Items, so this only ever fires on a genuine group, and it
    // fires again one level up if that collapse leaves its own parent down to one child too.
    // A group somebody hid is not folded — there is no place to hand over, and a group that
    // declared a Permission is not folded for the same reason: one slot cannot hold both gates,
    // and handing the place over while dropping one of them opens a door somebody closed. The
    // child's own Permission travels with it into the slot, which is the same rule from the
    // other side.
    static NavMenuItem Fold(NavMenuItem item, bool collapseSingleChildGroups)
    {
        if (!collapseSingleChildGroups
            || item.Items.Count != 1
            || item.Visible == false
            || item.Permission is not null)
        {
            return item;
        }

        var only = item.Items[0];

        return new NavMenuItem
        {
            Name = only.Name,
            Icon = only.Icon,
            PageType = only.PageType,
            Parameters = only.Parameters,
            Parent = item.Parent,
            Weight = item.Weight,
            Visible = only.Visible,
            Label = only.Label,
            Permission = only.Permission,
            Items = only.Items
        };
    }
}

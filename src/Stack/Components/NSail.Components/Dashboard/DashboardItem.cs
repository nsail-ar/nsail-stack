// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
using NSail.Security;

namespace NSail.Components;

/// <summary>Contributed dashboard card: CardType is the component that renders it, and its
/// own authorize attributes are what gate it — a card the session may not open is absent.
/// Name is the localization key, the same role it plays on NavMenuItem.</summary>
public sealed class DashboardItem
{
    public required string Name { get; init; }

    /// <summary>The component that renders the card. Null is what an OVERRIDE carries: a
    /// contributor that only hides or reorders names the card and nothing else, and Merge
    /// keeps the first non-null — the card is what its planter said it is.</summary>
    public Type? CardType { get; init; }

    /// <summary>The card's own glyph — NavMenuItem.Icon's shape, and chosen the same way: it
    /// says what THIS card is about, so two cards on one home never share one and a module's
    /// section glyph is never it (intentional-ui.md). A card standing for a screen that has a
    /// door usually wears that entry's glyph, because the entry named the same thing; a card
    /// whose word nothing in a catalog names gets a glyph of its own (the new-icon skill).</summary>
    public Glyph? Icon { get; init; }

    /// <summary>The card's own screen, when it has one — NavMenuItem.PageType's shape: a
    /// destination type, never a route string, so the host derives the URL from the route
    /// table. A card with nothing to open (an act, not a place — StarterCatalog's import)
    /// leaves this null and renders no door.</summary>
    public Type? PageType { get; init; }

    /// <summary>Sort key across all contributors; ties keep contribution order. Null says
    /// nothing about the position, which is what an override that only hides carries, and
    /// sorts where a zero would.</summary>
    public int? Weight { get; init; }

    /// <summary>The card stands two ordinary rows tall — for the one card that is a surface
    /// to look INTO (a day's agenda) beside cards that are numbers to glance at. At most one
    /// per dashboard reads well; the grid enforces nothing.</summary>
    public bool? Tall { get; init; }

    /// <summary>False takes the card off the dashboard. Unlike a hidden nav entry, which
    /// stays in the tree so Find still answers for the pages under it, a dropped card has
    /// nothing left to answer: Merge leaves it out.</summary>
    public bool? Visible { get; init; }

    /// <summary>The card's own refinement of the gate above, for the one shape a message-level
    /// grant cannot see: a session holding a genuine, constrained grant on the card's message —
    /// a portal's own party — that the card's own use of it (an org-wide alert, a professional's
    /// own agenda) can never satisfy. `PreAuthorize`/`RequiresMe` answer this without a graph
    /// query (permissions.md), so a card names one instead of learning it the hard way — a
    /// send its own message-level gate let through denied on the field it never fills. Null is
    /// every existing card: nothing here narrows what the coarse gate already decided.</summary>
    public Func<SecurityManager?, Session?, bool>? Eligible { get; init; }

    /// <summary>Merges contributions sharing a Name into one card and orders them by Weight.
    /// The Name is the merge key and already the localization key, the same role and the same
    /// rule NavMenuItem.Merge applies to the drawer.
    ///
    /// A kit plants its cards and the app arranges them: Weight, Icon, Tall, Visible and
    /// Eligible take the LAST non-null value contributed, so a contributor registered after
    /// the kits emits the same Name carrying only the fields it wants to change and leaves
    /// the rest as the kit put them. CardType and PageType go the other way — the first
    /// non-null wins — because they are what the card IS, not where it sits, which is what
    /// lets an override name the card and nothing else. Visible false drops it here rather
    /// than at render: a card off the dashboard has no second reader to keep it for.</summary>
    public static IReadOnlyList<DashboardItem> Merge(IEnumerable<DashboardItem> items)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<DashboardItem>>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (!grouped.TryGetValue(item.Name, out var group))
            {
                group = [];
                grouped[item.Name] = group;
                order.Add(item.Name);
            }

            group.Add(item);
        }

        var merged = new List<DashboardItem>(order.Count);

        foreach (var name in order)
        {
            var united = Unite(name, grouped[name]);

            if (united.Visible == false)
            {
                continue;
            }

            merged.Add(united);
        }

        // OrderBy is stable, so equal weights keep the first-appearance order above.
        return merged.OrderBy(item => item.Weight ?? 0).ToList();
    }

    static DashboardItem Unite(string name, List<DashboardItem> group)
    {
        // An override is told from a card by the absence of a CardType, which the drawer's
        // merge cannot do — so the dashboard spends the information it has instead of leaving
        // a contributor that names nobody's card to arrange a home in silence.
        if (group.Select(item => item.CardType).FirstOrDefault(type => type is not null) is not { } cardType)
        {
            throw new InvalidOperationException(
                $"Dashboard card '{name}' is overridden by a contributor, and nothing contributes the card itself.");
        }

        return new DashboardItem
        {
            Name = name,
            CardType = cardType,
            Icon = group.Select(item => item.Icon).LastOrDefault(icon => icon is not null),
            PageType = group.Select(item => item.PageType).FirstOrDefault(pageType => pageType is not null),
            Weight = group.Select(item => item.Weight).LastOrDefault(weight => weight is not null),
            Tall = group.Select(item => item.Tall).LastOrDefault(tall => tall is not null),
            Visible = group.Select(item => item.Visible).LastOrDefault(visible => visible is not null),
            Eligible = group.Select(item => item.Eligible).LastOrDefault(eligible => eligible is not null)
        };
    }
}

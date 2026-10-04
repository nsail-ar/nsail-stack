// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.Authorization;
using NSail.Localization;

namespace NSail.Components;

/// <summary>The merged navigation tree, asked of the contributors once for the app and handed
/// to everything that wants it: the drawer, and every title bar deriving its page's glyph.
/// A contributor is a module's chance to ask its own server — the sign-in kits each spend an
/// HTTP round trip on it — so who asks and how often is a decision, not an implementation
/// detail of whichever component happened to want the tree.</summary>
public sealed class NavMenu
{
    readonly IEnumerable<INavMenuContributor> _contributors;

    readonly IEnumerable<INavMenuCount> _counts;

    readonly IEnumerable<INavMenuArrangement> _arrangements;

    readonly LanguageProvider _language;

    Task<IReadOnlyList<NavMenuItem>>? _items;

    Task<AuthenticationState>? _session;

    /// <summary>An app that stores no arrangement registers none and gets the menu its modules
    /// contributed, which is why the arrangements arrive as a collection and not as one
    /// collaborator somebody has to remember to register.</summary>
    public NavMenu(
        IEnumerable<INavMenuContributor> contributors,
        IEnumerable<INavMenuCount> counts,
        IEnumerable<INavMenuArrangement>? arrangements = null,
        LanguageProvider? language = null)
    {
        _contributors = contributors;
        _counts = counts;
        _arrangements = arrangements ?? [];
        _language = language ?? new LanguageProvider();
    }

    /// <summary>The tree, built once for the session it is asked against. The session is the
    /// only thing a contributor may answer differently for, so a different one — a sign-in, an
    /// organization switch — is what earns a rebuild. Nothing else does, a caller that is a
    /// brand-new component instance included: the drawer and every title bar on a screen share
    /// one answer for as long as the session lasts.</summary>
    public Task<IReadOnlyList<NavMenuItem>> GetItems(Task<AuthenticationState>? session)
    {
        // What is remembered is the TASK and not the items, and the decision is taken with no
        // await in front of it: callers arriving while the first ask is still in flight — the
        // ordinary case, since a screen renders its drawer and its title bar in one pass — join
        // that ask instead of each starting one. A faulted ask is not an answer, so the next
        // caller asks again rather than inheriting the failure the first one met.
        if (_items is { IsFaulted: false } items && ReferenceEquals(_session, session))
        {
            return items;
        }

        _session = session;

        return _items = Build();
    }

    /// <summary>What each counting contributor says right now, by entry Name. Not remembered,
    /// unlike the tree above: the tree is a function of the session and the number is a function
    /// of the work, so caching it would draw the count the session opened with for as long as
    /// the session lasts — and whoever publishes <see cref="NavMenuCountsChanged"/> is saying
    /// exactly that the last answer is spent. A contributor that cannot answer is left out
    /// rather than allowed to take the drawer down with it — a number is chrome, and the map
    /// is not.</summary>
    public async Task<IReadOnlyDictionary<string, int>> GetCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var contributor in _counts)
        {
            try
            {
                var count = await contributor.GetCount();

                if (count > 0)
                {
                    counts[contributor.Name] = count;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                continue;
            }
        }

        return counts;
    }

    async Task<IReadOnlyList<NavMenuItem>> Build()
    {
        var items = new List<NavMenuItem>();

        foreach (var contributor in _contributors)
        {
            items.AddRange(await contributor.GetItems());
        }

        // What was arranged over what was contributed, applied after every contributor and here
        // rather than in a contributor of its own: what a module offers and where a shop put it
        // are two acts, and only one of them may be answered by a registration order. Each
        // arrangement is read against everything said before it — the contributions, and any
        // arrangement ahead of it in the cascade — so a row can only arrange a door that is
        // really there, and the last word belongs to whoever sits last.
        foreach (var arranger in _arrangements)
        {
            var arrangement = await arranger.GetArrangement();

            if (arrangement.Entries.Count == 0)
            {
                continue;
            }

            items.AddRange(NavMenuEntry.Resolve(arrangement, items, _language.Current));
        }

        return NavMenuItem.Merge(items);
    }
}

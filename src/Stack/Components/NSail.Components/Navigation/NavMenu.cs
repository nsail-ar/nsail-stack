// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using NSail.Localization;
using NSail.Settings;

namespace NSail.Components;

/// <summary>The merged navigation tree, asked of the contributors once for the app and handed
/// to everything that wants it: the drawer, and every title bar deriving its page's glyph.
/// A contributor is a module's chance to ask its own server — the sign-in kits each spend an
/// HTTP round trip on it — so who asks and how often is a decision, not an implementation
/// detail of whichever component happened to want the tree.
///
/// <para>What the prerender spent on the wire — the install's arrangement and the entries'
/// counts — crosses to the client as persisted state instead of being asked a second time
/// (intentional-ui.md, "One arrival, one loading"). The tree itself does not cross: merging it
/// is pure CPU once the arrangement is in hand, and an entry carries a page Type and route
/// parameters that no wire shape holds.</para></summary>
public sealed class NavMenu : IDisposable
{
    const string HandoffKey = "NSail.NavMenu";

    readonly IEnumerable<INavMenuContributor> _contributors;

    readonly IEnumerable<INavMenuCount> _counts;

    readonly IEnumerable<INavMenuArrangement> _arrangements;

    readonly LanguageProvider _language;

    readonly PersistentComponentState? _handoff;

    Task<IReadOnlyList<NavMenuItem>>? _items;

    Task<AuthenticationState>? _session;

    IReadOnlyList<NavMenuArrangement>? _arranged;

    IReadOnlyDictionary<string, int>? _handedCounts;

    bool _registered;

    PersistingComponentStateSubscription _persisting;

    /// <summary>An app that stores no arrangement registers none and gets the menu its modules
    /// contributed, which is why the arrangements arrive as a collection and not as one
    /// collaborator somebody has to remember to register.</summary>
    public NavMenu(
        IEnumerable<INavMenuContributor> contributors,
        IEnumerable<INavMenuCount> counts,
        IEnumerable<INavMenuArrangement>? arrangements = null,
        LanguageProvider? language = null,
        PersistentComponentState? handoff = null)
    {
        _contributors = contributors;
        _counts = counts;
        _arrangements = arrangements ?? [];
        _language = language ?? new LanguageProvider();
        _handoff = handoff;
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

        // A rebuild re-reads the arrangement: an install's rows are an authorized read, so the
        // session that was denied them and the session that was granted them do not get one
        // answer between them.
        _arranged = null;

        // Taken SYNCHRONOUSLY, before Build is entered: the whole value of the handoff is that
        // the first build completes without a single await that has not already finished, so
        // the drawer carries its entries in the client's first render batch rather than in the
        // one after the splash has already come off over an empty map.
        Adopt();

        return _items = Build();
    }

    /// <summary>What each counting contributor says right now, by entry Name. Not remembered,
    /// unlike the tree above: the tree is a function of the session and the number is a function
    /// of the work, so caching it would draw the count the session opened with for as long as
    /// the session lasts — and whoever publishes <see cref="NavMenuCountsChanged"/> is saying
    /// exactly that the last answer is spent. A contributor that cannot answer is left out
    /// rather than allowed to take the drawer down with it — a number is chrome, and the map
    /// is not.</summary>
    public Task<IReadOnlyDictionary<string, int>> GetCounts()
    {
        // The arrival's numbers, resolved by the prerender and handed over. TAKEN rather than
        // read: this is the one ask of the session the handoff answers, and every later one —
        // a navigation, a NavMenuCountsChanged — is somebody saying the last answer is spent.
        if (_handedCounts is { } handed)
        {
            _handedCounts = null;

            return Task.FromResult(handed);
        }

        return Ask();
    }

    public void Dispose()
    {
        _persisting.Dispose();
    }

    async Task<IReadOnlyDictionary<string, int>> Ask()
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

    void Adopt()
    {
        if (_handoff is null)
        {
            return;
        }

        if (_handoff.TryTakeFromJson<NavMenuHandoff>(HandoffKey, out var handed) && handed is not null)
        {
            _arranged = handed.Arrangements;
            _handedCounts = handed.Counts;

            return;
        }

        // Nothing was handed over, so this side is the one that asks — and therefore the one
        // with an answer worth persisting. The render mode is named because the callback
        // belongs to a service rather than a component, and .NET refuses to guess for one; it
        // is WebAssembly and only WebAssembly because that is the only client that starts a
        // second time in a second process.
        // Once, not per build: a sign-in or an organization switch re-enters here, and a
        // callback registered again each time is a subscription leak.
        if (!_registered)
        {
            _registered = true;
            _persisting = _handoff.RegisterOnPersisting(Persist, RenderMode.InteractiveWebAssembly);
        }
    }

    async Task Persist()
    {
        if (_handoff is null || _arranged is not { } arranged)
        {
            return;
        }

        // The counts are asked at persist time rather than carried out of the render: in the
        // prerendering scope this is an in-process send with the session already in hand, and
        // it is a number — the later it is read the truer it is.
        _handoff.PersistAsJson(HandoffKey, new NavMenuHandoff
        {
            Arrangements = arranged,
            Counts = await Ask()
        });
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
        foreach (var arrangement in await Arranged())
        {
            if (arrangement.Entries.Count == 0)
            {
                continue;
            }

            items.AddRange(NavMenuEntry.Resolve(arrangement, items, _language.Current));
        }

        return NavMenuItem.Merge(items);
    }

    // What each arranger stored, in cascade order — handed over where the prerender already
    // paid for it, and remembered either way so the persist callback writes the arrangement
    // this tree was actually built from. The rows and not the tree, because resolving them
    // against the contributions is local work and the LANGUAGE it resolves under is the
    // client's own.
    Task<IReadOnlyList<NavMenuArrangement>> Arranged()
    {
        if (_arranged is { } arranged)
        {
            return Task.FromResult(arranged);
        }

        return Read();
    }

    async Task<IReadOnlyList<NavMenuArrangement>> Read()
    {
        var arranged = new List<NavMenuArrangement>();

        foreach (var arranger in _arrangements)
        {
            arranged.Add(await arranger.GetArrangement());
        }

        _arranged = arranged;

        return arranged;
    }
}

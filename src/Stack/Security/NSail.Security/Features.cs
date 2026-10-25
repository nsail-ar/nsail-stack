// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Every entitlement key this install knows, gathered from the modules composed into it
/// (<see cref="IFeatureContributor"/>) — which is the whole of what the Stack knows about keys:
/// it holds no list of its own and names none. What it answers is what a key was DECLARED as, so
/// a bag that is silent about one still has an answer, and a
/// <see cref="Annotations.RequiresFeatureAttribute"/> naming a key nobody declared can be found
/// by whoever sweeps the composed products.
///
/// <para>A key declared twice is one key: two modules may both own a road and neither can be
/// made to know the other exists. The first declaration stands, so the gathering is stable
/// whatever order a host composes in.</para></summary>
public sealed class Features
{
    readonly Dictionary<string, FeatureItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public Features(IEnumerable<IFeatureContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        foreach (var contributor in contributors)
        {
            foreach (var item in contributor.GetItems())
            {
                _items.TryAdd(item.Key, item);
            }
        }
    }

    /// <summary>Every declared key, for whoever has to show an operator what can be sold and for
    /// the sweep that reads the marks against it.</summary>
    public IReadOnlyCollection<FeatureItem> All
    {
        get { return _items.Values; }
    }

    /// <summary>What this key was declared as, or null for one nobody declared.</summary>
    public FeatureItem? Of(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _items.GetValueOrDefault(key);
    }

    /// <summary>Whether the feature is open for a tenant whose bag reads <paramref name="bag"/>:
    /// what the bag says in as many words, and the key's declared default where it says nothing.
    /// A key nobody declared is open — a mark naming one is a defect the suite catches, not a
    /// door to close on a running install.</summary>
    public bool IsEnabled(string feature, Entitlements bag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentNullException.ThrowIfNull(bag);

        return bag.Flag(feature) ?? Of(feature)?.Enabled ?? true;
    }

    /// <summary>The ceiling in force for a tenant whose bag reads <paramref name="bag"/>: the
    /// figure the bag states, and the key's declared default where it states none. Null is no
    /// ceiling, and no ceiling ever blocks the work.</summary>
    public decimal? Limit(string key, Entitlements bag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(bag);

        return bag.Limit(key) ?? Of(key)?.Limit;
    }
}

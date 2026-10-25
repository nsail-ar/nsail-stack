// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Answers what this scope's tenant is entitled to, for the gate that refuses a message
/// wearing a <see cref="Annotations.RequiresFeatureAttribute"/> whose feature is off and for
/// every screen that states a ceiling. The base is deliberately permissive
/// (<see cref="Entitlements.Unrestricted"/>): it serves the WASM client, a composition with no
/// store to read the bag from, and every install nobody has sold a plan to — the same shape and
/// the same reason as <see cref="RelationProvider"/>.
///
/// <para><b>Two reads, as the gate has two.</b> <see cref="Current"/> is what
/// <see cref="SecurityManager.Authorize"/> awaits and the only one a refusal may rest on;
/// <see cref="Assumed"/> is what <see cref="SecurityManager.PreAuthorize"/> reads to decide
/// whether a page, a menu entry or an action is offered at all, and a bag not yet in hand reads
/// as unrestricted — offering something the strict gate then refuses is recoverable, hiding
/// something allowed is not.</para></summary>
public class FeatureProvider
{
    readonly Features? _catalog;

    public FeatureProvider(Features? catalog = null)
    {
        _catalog = catalog;
    }

    public virtual Task<Entitlements> Current(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Entitlements.Unrestricted);
    }

    /// <summary>The bag this scope already has in hand, with no read of its own — unrestricted
    /// where there is none. What visibility is derived from, which cannot await.</summary>
    public virtual Entitlements Assumed
    {
        get { return Entitlements.Unrestricted; }
    }

    /// <summary>Whether the feature is open for this tenant, the key's declared default
    /// included — the whole question, which is what a caller asks rather than reading the bag
    /// and resolving the default itself.</summary>
    public async Task<bool> IsEnabled(string feature, CancellationToken cancellationToken = default)
    {
        return Resolve(feature, await Current(cancellationToken).ConfigureAwait(false));
    }

    /// <summary><see cref="IsEnabled"/> for the callers that cannot await — render code deriving
    /// what to offer — off <see cref="Assumed"/> and optimistic in the same way.</summary>
    public bool Assumes(string feature)
    {
        return Resolve(feature, Assumed);
    }

    /// <summary>The ceiling in force for this key, or null for none. <b>It never blocks the
    /// work</b>: what reads it states how much of the allowance is used and carries on.</summary>
    public async Task<decimal?> Limit(string key, CancellationToken cancellationToken = default)
    {
        var bag = await Current(cancellationToken).ConfigureAwait(false);

        return _catalog is { } catalog ? catalog.Limit(key, bag) : bag.Limit(key);
    }

    bool Resolve(string feature, Entitlements bag)
    {
        return _catalog is { } catalog ? catalog.IsEnabled(feature, bag) : bag.IsEnabled(feature);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Answers what this scope's tenant is entitled to, for the gate that refuses a
/// message wearing a <see cref="Annotations.RequiresFeatureAttribute"/> whose feature is off.
/// The base is deliberately permissive (<see cref="Entitlements.Unrestricted"/>): it serves the
/// WASM client, a composition with no store to read the bag from, and every install nobody has
/// sold a plan to — the same shape and the same reason as <see cref="RelationProvider"/>.
///
/// <para><b>Two reads, as the gate has two.</b> <see cref="Current"/> is what
/// <see cref="SecurityManager.Authorize"/> awaits and the only one a refusal may rest on;
/// <see cref="Assumed"/> is what <see cref="SecurityManager.PreAuthorize"/> reads to decide
/// whether a page, a menu entry or an action is offered at all, and it is optimistic by the same
/// rule — a bag not yet in hand reads as unrestricted, because offering something the strict
/// gate then refuses is recoverable and hiding something allowed is not.</para></summary>
public class FeatureProvider
{
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
}

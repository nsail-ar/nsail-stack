// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>The entitlements of this scope's tenant, read off what the install records about it
/// (<see cref="TenantState.Entitlements"/>) — the server half of <see cref="FeatureProvider"/>,
/// registered here because this is the composition that has the store. The twin of Iam's
/// DB-backed <see cref="RelationProvider"/>, and permissive for the same reasons: a mode that
/// resolves no tenant registers no <see cref="TenantStates"/>, and an install nobody has written
/// a bag for behaves exactly as it did before the bag existed.
///
/// <para>The row itself costs nothing to ask for — <see cref="TenantStates"/> reads it through
/// the process memo — so what is held here is the PARSE, keyed by the tenant it was parsed for:
/// the tenant of a scope is whoever was entered into it by the time it asks, not by the time it
/// was built.</para></summary>
public sealed class TenantStateFeatureProvider : FeatureProvider
{
    readonly TenancyProvider _tenancy;
    readonly TenantStates? _states;
    readonly TenantStateMemo? _memo;

    Guid? _parsedFor;
    Entitlements? _parsed;

    public TenantStateFeatureProvider(
        TenancyProvider tenancy,
        TenantStates? states = null,
        TenantStateMemo? memo = null)
    {
        ArgumentNullException.ThrowIfNull(tenancy);

        _tenancy = tenancy;
        _states = states;
        _memo = memo;
    }

    public override async Task<Entitlements> Current(CancellationToken cancellationToken = default)
    {
        if (Parsed() is { } held)
        {
            return held;
        }

        if (_states is null)
        {
            return Entitlements.Unrestricted;
        }

        var state = await _states.Current(cancellationToken).ConfigureAwait(false);

        return Remember(Entitlements.Read(state?.Entitlements));
    }

    public override Entitlements Assumed
    {
        get
        {
            if (Parsed() is { } held)
            {
                return held;
            }

            if (_memo is null || _tenancy.RowKey is not { } key || !_memo.Holds(key, out var state))
            {
                return Entitlements.Unrestricted;
            }

            return Remember(Entitlements.Read(state?.Entitlements));
        }
    }

    Entitlements? Parsed()
    {
        return _parsed is { } parsed && _parsedFor == _tenancy.RowKey ? parsed : null;
    }

    Entitlements Remember(Entitlements entitlements)
    {
        _parsedFor = _tenancy.RowKey;
        _parsed = entitlements;

        return entitlements;
    }
}

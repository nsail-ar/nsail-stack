// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A gate that is shut: everything goes to its one destination, and that destination
/// answers ITSELF -- the claim IRouteGate's contract names, which is the shape every gate with
/// a single screen takes (an install's OnboardingGate, Iam's SignUpGate).</summary>
public sealed class ClosedRouteGate(Type destination, int weight = 0) : IRouteGate
{
    public int Weight
    {
        get { return weight; }
    }

    public Task<Type?> GetRedirect(Type page, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Type?>(destination);
    }
}

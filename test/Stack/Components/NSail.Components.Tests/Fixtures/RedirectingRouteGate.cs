// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A gate that names a real redirect destination. FakeRouteGate only ever answers
/// null -- LayoutPersistenceTests depends on that -- so a gate that actually redirects is a
/// fixture of its own rather than a change to it.</summary>
public sealed class RedirectingRouteGate(Type from, Type to, int weight = 0) : IRouteGate
{
    public int Weight
    {
        get { return weight; }
    }

    public Task<Type?> GetRedirect(Type page, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(page == from ? to : null);
    }
}

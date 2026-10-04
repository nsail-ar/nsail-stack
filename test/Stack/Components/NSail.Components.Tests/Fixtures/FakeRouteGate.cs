// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A gate that answers from cache: the Task IRouteGate hands back is already
/// completed, so the await inside NsRouteGate never yields -- the exact shape 1bb12a22 gave
/// OnboardingGate.Send once onboarding settles. Registered so LayoutPersistenceTests exercises
/// the real contract IRouteGate's own doc names (a settled gate never draws NsRouteGate's
/// momentary empty frame) rather than the vacuous zero-gates case, which would pass whether or
/// not the cache path exists at all.</summary>
public sealed class FakeRouteGate : IRouteGate
{
    public Task<Type?> GetRedirect(Type page, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Type?>(null);
    }
}

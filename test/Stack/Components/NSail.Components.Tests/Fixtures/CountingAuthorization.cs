// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The real evaluator with a tally in front of it, so "the gate answered" and "the
/// gate was ASKED" can be pinned apart — which is the only deterministic way to read a cache
/// whose whole subject is a cost (testing.md: milliseconds are not asserted).</summary>
public sealed class CountingAuthorization(IAuthorizationService authorization) : IAuthorizationService
{
    public int Calls { get; private set; }

    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        Calls++;

        return authorization.AuthorizeAsync(user, resource, requirements);
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
    {
        Calls++;

        return authorization.AuthorizeAsync(user, resource, policyName);
    }
}

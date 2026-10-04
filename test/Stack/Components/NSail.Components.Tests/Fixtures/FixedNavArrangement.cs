// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Settings;

namespace NSail.Components.Tests.Fixtures;

/// <summary>An install's stored arrangement, without a database: what a shop saved is data, so
/// a test states the rows and the tree that comes out of them is the whole mechanism.</summary>
public sealed class FixedNavArrangement(params NavMenuArrangementEntry[] entries) : INavMenuArrangement
{
    public Task<NavMenuArrangement> GetArrangement(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NavMenuArrangement { Entries = [.. entries] });
    }
}

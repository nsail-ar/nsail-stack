// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A module's guide, given rather than composed: the chapters a test wants, in the
/// order it wrote them.</summary>
public sealed class FixedGuideContributor(params GuideItem[] items) : IGuideContributor
{
    public Task<IReadOnlyList<GuideItem>> GetItems()
    {
        return Task.FromResult<IReadOnlyList<GuideItem>>(items);
    }
}

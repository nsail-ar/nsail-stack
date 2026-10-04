// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A count for one entry that says how often it was asked, and answers whatever the
/// test last put in Answer — so "the number changed" and "the drawer asked again" can be
/// pinned apart from each other.</summary>
public sealed class CountingNavCount : INavMenuCount
{
    public CountingNavCount(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public int Answer { get; set; } = 2;

    public int Calls { get; private set; }

    public Task<int> GetCount()
    {
        Calls++;

        return Task.FromResult(Answer);
    }
}

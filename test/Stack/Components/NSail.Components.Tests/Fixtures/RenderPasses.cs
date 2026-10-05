// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>Counts the render passes each probe page took, by name, and snapshots the tally at
/// each step of a submit so a pass can be attributed to what raised it. A field on the page
/// cannot answer this and neither can bUnit's own RenderCount: the last thing a submit in an
/// overlay does is close it, and the tally has to outlive the subtree that close tears down.
/// </summary>
public sealed class RenderPasses
{
    readonly Dictionary<string, int> _passes = [];
    readonly List<Step> _steps = [];

    public void Drew(string page)
    {
        _passes[page] = Count(page) + 1;
    }

    public int Count(string page)
    {
        return _passes.TryGetValue(page, out var passes) ? passes : 0;
    }

    /// <summary>Records where the submit has got to, with the tally as it stands there.</summary>
    public void Reached(string step)
    {
        _steps.Add(new Step(step, new Dictionary<string, int>(_passes)));
    }

    public IReadOnlyList<Step> Steps
    {
        get { return _steps; }
    }

    public void Reset()
    {
        _passes.Clear();
        _steps.Clear();
    }

    public sealed record Step(string Name, IReadOnlyDictionary<string, int> Passes)
    {
        public int Count(string page)
        {
            return Passes.TryGetValue(page, out var passes) ? passes : 0;
        }
    }
}

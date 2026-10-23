// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A module's results for a term, with the terms it was actually asked about kept: a
/// search is a round trip, so what was asked and how often are the two things worth pinning
/// beside what came back.</summary>
public sealed class CountingNavSearch : INavMenuSearch
{
    readonly List<NavMenuResult> _results = [];

    readonly TaskCompletionSource _answered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    TaskCompletionSource? _held;

    bool _failing;

    public List<string> Terms { get; } = [];

    /// <summary>Completes when an ask has handed its answer back, which is the settle point a
    /// test asserting that something was NOT drawn has to wait for: there is no render to wait
    /// on when the right behaviour is to leave the drawer alone.</summary>
    public Task Answered
    {
        get { return _answered.Task; }
    }

    public CountingNavSearch Answers(params NavMenuResult[] results)
    {
        _results.Clear();
        _results.AddRange(results);

        return this;
    }

    /// <summary>A server that is not answering — the drawer's own behaviour is what is under
    /// test, so the failure is a plain throw and not a transport's.</summary>
    public CountingNavSearch Fails()
    {
        _failing = true;

        return this;
    }

    /// <summary>Holds the next answers open, which is the only way to put a second term inside
    /// the window of the first one's ask.</summary>
    public CountingNavSearch Holds()
    {
        _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return this;
    }

    public void Release()
    {
        var held = _held;

        _held = null;

        held?.TrySetResult();
    }

    public async Task<IReadOnlyList<NavMenuResult>> Find(string term)
    {
        Terms.Add(term);

        if (_held is { } held)
        {
            await held.Task;
        }

        try
        {
            if (_failing)
            {
                throw new InvalidOperationException("the API is not answering");
            }

            return _results.ToList();
        }
        finally
        {
            _answered.TrySetResult();
        }
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The reads a dashboard's cards have in flight, cascaded by the dashboard and counted
/// by NsPartial.Handoff — the one door every card reads through. A card that has not answered
/// renders nothing, exactly like one that answered with nothing, so this count is what tells the
/// dashboard which of the two its silence is.</summary>
internal sealed class DashboardReads
{
    int _pending;

    public bool Pending => _pending > 0;

    public event Action? Settled;

    public void Started()
    {
        _pending++;
    }

    public void Answered()
    {
        _pending--;

        if (_pending == 0)
        {
            Settled?.Invoke();
        }
    }
}

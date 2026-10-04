// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Builds;

/// <summary>What the transport heard the server answer with. Every API response carries the
/// build that served it, so a client left open across a deploy learns on its next call instead
/// of on the first value its own build cannot read.
///
/// <para>Silence is the answer to everything but a different word: no header, a blank one, or
/// the build this process is itself running all leave it quiet — a host that predates the stamp
/// and a developer's unstamped pair must never raise the offer. It moves once, because a tab
/// keeps calling after a deploy and the offer is one offer.</para>
///
/// <para>Nothing here reloads anything: it says the server moved, and what to do about it is
/// the operator's.</para></summary>
public sealed class ServerBuild
{
    int _moved;

    public event Action? Changed;

    public bool Moved
    {
        get { return _moved == 1; }
    }

    public void Answered(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)
            || string.Equals(version, BuildVersion.Current, StringComparison.Ordinal))
        {
            return;
        }

        if (Interlocked.Exchange(ref _moved, 1) == 1)
        {
            return;
        }

        Changed?.Invoke();
    }
}

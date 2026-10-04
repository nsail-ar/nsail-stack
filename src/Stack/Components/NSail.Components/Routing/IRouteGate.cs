// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>A veto the router asks before a route renders. Answering a page sends the visitor
/// there instead — nothing of the destination is constructed, so a layout an install has not
/// earned yet never draws. Answering null lets the route through.
///
/// A Type and not a path, like every other destination the Stack names: the URL comes from the
/// route table. Which pages stay reachable while a gate is closed is the gate's own knowledge,
/// because the door it must not lock is the one that opens it.
///
/// The FIRST gate that answers is the one the visitor obeys, and a gate answering <em>the page
/// it was asked about</em> is claiming that route: the visitor stays, and nothing lighter is
/// asked. A gate that sends everyone to one page answers its own page that way while it is
/// closed, which is what makes its destination a place the visitor can stand — without it, two
/// closed gates would each send the visitor to the other's destination forever. The claim is a
/// fixed point, not a veto over the whole set: a gate that has to be cleared first says so with
/// its Weight, so a claim never silences a gate that outranks it.
///
/// Answer from cache once the verdict has settled: the router draws nothing while an answer
/// is in flight, so a gate that genuinely awaits on every ask tears down and rebuilds the
/// entire tree — layout included — on every navigation.</summary>
public interface IRouteGate
{
    /// <summary>Ascending, ties keeping registration order — the same Weight every other
    /// contributed sequence sorts by. It is who gets asked first, so it is who decides: a
    /// blocker that must be cleared before another weighs less than it. The default asks for
    /// no precedence.</summary>
    int Weight
    {
        get { return 0; }
    }

    Task<Type?> GetRedirect(Type page, CancellationToken cancellationToken = default);
}

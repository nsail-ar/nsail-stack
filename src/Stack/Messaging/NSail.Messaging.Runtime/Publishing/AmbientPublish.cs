// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Publishing;

/// <summary>Whether this async flow is inside a Publish — the broadcast itself, every
/// subscriber it reaches, and everything they send in reaction. A reaction is the system
/// acting on a decision that was already authorized, not a caller asking for something, so
/// the security gate reads this and does not ask a second time.</summary>
public static class AmbientPublish
{
    // The alternative — leave the gate alone and let policies name the event keys — was
    // measured and refused: the chain crosses into kits the policy author never heard of, so
    // enumerating keys grants a broadcast and then loses to the next Send one frame down.
    //
    // What may publish is what the app composed: a domain event carries no [Http], so nothing
    // off the wire arrives here. That is the same boundary a background job's total
    // permissions already rest on (permissions.md, Session) — code review of the
    // registrations, not runtime policy. It also stops at a process boundary by construction:
    // an HttpSender's message reaches the other side as an ordinary caller's send and is
    // gated there.
    //
    // It travels by AsyncLocal for the reason AmbientUnitOfWork does — the only channel a
    // subscriber cannot forget to pass.
    static readonly AsyncLocal<bool> Inside = new();

    public static bool Active
    {
        get { return Inside.Value; }
    }

    // The flag is written, the broadcast is STARTED, and the write is undone before this
    // method ever awaits: the flow that was started captured it, and the caller — which has
    // not awaited yet — must not keep it. Same order of operations as AmbientUnitOfWork.Invoke
    // and for the same reason. Entering is internal to the runtime, so PublishPipeline is the
    // only thing that can ever turn the gate's question off.
    internal static Task Run(Func<Task> start)
    {
        var previous = Inside.Value;

        Inside.Value = true;

        try
        {
            return start();
        }
        finally
        {
            Inside.Value = previous;
        }
    }
}

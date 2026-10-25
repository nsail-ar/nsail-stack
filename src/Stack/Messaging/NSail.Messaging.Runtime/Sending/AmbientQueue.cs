// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Sending;

/// <summary>Whether this async flow is a queued command's delivery — the drain re-entering a
/// command whose send was already answered. The caller's grants were read at the send, hours
/// earlier, and the operation that made it has long committed; asking again would refuse, with
/// nobody left to tell, a command the caller was already told was accepted. So the security
/// gate reads this and does not ask a second time, exactly as it does for a reaction
/// (<see cref="Publishing.AmbientPublish"/>).</summary>
public static class AmbientQueue
{
    // What may be delivered is what a [Queue] message's producer stored, and a [Queue] message
    // carries no [Http] by construction (NSG015), so nothing off the wire arrives here: the
    // boundary is the same code review of the registrations a reaction's exemption already
    // rests on (permissions.md, Evaluation).
    //
    // It travels by AsyncLocal for the reason AmbientUnitOfWork and AmbientPublish do — the one
    // channel the delivery's own nested sends cannot forget to pass.
    static readonly AsyncLocal<bool> Inside = new();

    public static bool Active
    {
        get { return Inside.Value; }
    }

    // The flag is written, the delivery is STARTED, and the write is undone before this method
    // ever awaits — the same order of operations as AmbientPublish.Run, and for the same
    // reason. Entering is internal to the runtime and visible to NSail.Messaging.Queue alone,
    // so the drain is the only thing that can ever turn the gate's question off this way.
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

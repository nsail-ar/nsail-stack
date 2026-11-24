// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Jobs;

// Idle: the job found nothing to do (a story closed, held, already landed); the loop leaves it
// alone for a while instead of planning it again on the next pass.
// Event: how the job ended, in its type's words (WorkerTypes: Opened, Ruling, Clean...), for the
// worker's actions on it. Failed: its work went wrong (a red build, a dead voyage, no verdict); the
// worker's Failed actions run instead of its Succeeded ones. Report: what the job wrote for the story,
// quoted by an action's comment as {report}. A job that ended on the agent's quota
// wall or found nothing to do ends in no event: neither is the story's doing. Again: the job did its
// work and reached no verdict for a reason outside the story (a gate out of time with nothing red):
// it fires no event, is backed off as an idle one is, and still counts as the crew's work.
public sealed record JobOutcome(string Summary, DateTimeOffset? QuotaUntil = null, bool Idle = false, string? Event = null, bool Failed = false, string? Report = null, bool Again = false)
{
    public bool Quiet => Idle || QuotaUntil is not null;

    // Ended in no event: quiet, or to be done again.
    public bool Silent => Quiet || Again;
}

// How a job's work ended, before the loop's bookkeeping: a plain summary is a success with no event
// of its own.
public sealed record Ending(string Summary, string? Event = null, bool Failed = false)
{
    public static implicit operator Ending(string summary)
    {
        return new Ending(summary);
    }

    public JobOutcome Outcome(DateTimeOffset? quotaUntil)
    {
        return new JobOutcome(Summary, quotaUntil, Event: Event, Failed: Failed);
    }
}

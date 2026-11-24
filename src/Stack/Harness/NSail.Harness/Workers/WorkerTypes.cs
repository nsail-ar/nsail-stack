// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Planning;

namespace NSail.Harness.Workers;

// What each worker Type is: the job it runs, whether it runs over stories or on its own (a release,
// main's verification, a QA drain), what it needs from the machine, and its defaults — the filter a
// worker of the type starts from, the events its jobs end in, and what each event does on the board
// unless the worker says otherwise. A worker changes only what it names.
public sealed record WorkerType(
    string Name,
    WorkKind? Kind,
    bool OverStories,
    bool HasAgent,
    FilterOptions Filter,
    IReadOnlyList<string> Events,
    IReadOnlyDictionary<string, IReadOnlyList<ActionOptions>> Defaults,
    int Minutes)
{
    // The events every job ends in: before it runs, then succeeded or failed, then always.
    public static readonly string[] BaseEvents = ["Starting", "Succeeded", "Failed", "Executed"];
}

public static class WorkerTypes
{
    // The columns a story with an open PR may stand in while the PR is worked: a Merged story's
    // second PR (a pin, a follow-up) is still read and rounded.
    const string PrStages = $"{Stages.Ready}|{Stages.InProgress}|{Stages.Analyze}|{Stages.Merged}";

    // The holds a story waits under for someone's word; locks (a job's own labels) are the engine's.
    static readonly List<string> Holds = ["!ruling", "!stalled", "!blocked", "!hand", "!unwalkable"];

    static ActionOptions MoveTo(string stage)
    {
        return new() { MoveTo = stage };
    }

    static ActionOptions Add(string labels)
    {
        return new() { AddLabel = labels };
    }

    static ActionOptions Remove(string labels)
    {
        return new() { RemoveLabel = labels };
    }

    static ActionOptions Say(string comment)
    {
        return new() { Comment = comment };
    }

    static ActionOptions Close()
    {
        return new() { Close = true };
    }

    // A bug's comment carries the marker the next walk counts (BugAgain).
    public const string QaBugMarker = "<!-- qa-bug -->";
    const string QaBug = "QA caminó esto y no pasa.\n\n{report}\n\n" + QaBugMarker;
    public const string QaDoubtMarker = "<!-- qa-doubt -->";
    const string QaDoubt = "QA no pudo caminarla.\n\n{report}\n\n" + QaDoubtMarker;

    static readonly IReadOnlyDictionary<string, IReadOnlyList<ActionOptions>> None = new Dictionary<string, IReadOnlyList<ActionOptions>>();

    public static readonly IReadOnlyDictionary<string, WorkerType> All = new Dictionary<string, WorkerType>(StringComparer.OrdinalIgnoreCase)
    {
        // Ready: shaped for a cold builder. Ruling: a question waits for a hand. Unwalkable: the story
        // cannot be proven by a walk and nothing in it can be changed so it can — it waits where it is
        // for a hand to provide what is missing, and asks nobody a question. Either way a stalled
        // story's analysis ends its stall. Split: filed again as parts under it, which are what is built.
        // Elsewhere: its parts in other repositories are filed first, and it waits for them blocked.
        ["Analyze"] = new("Analyze", WorkKind.Analyze, true, true,
            new FilterOptions { Status = Stages.Analyze, SubIssues = "0", Pr = "none", Labels = Holds },
            [.. WorkerType.BaseEvents, "Ready", "Ruling", "Unwalkable", "Done", "Split", "Elsewhere"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Ready"] = [MoveTo(Stages.Ready), Remove(Labels.Stalled)],
                ["Ruling"] = [Add(Labels.Ruling), Remove(Labels.Stalled)],
                ["Unwalkable"] = [Add(Labels.Unwalkable), Remove(Labels.Stalled)],
                ["Done"] = [MoveTo(Stages.Merged), Remove(Labels.Stalled)],
                ["Split"] = [MoveTo(Stages.Backlog)],
                ["Elsewhere"] = [MoveTo(Stages.Backlog)],
            }, Minutes: 45),
        // Started: a builder took it. Opened, Pushed: the gate decides what follows. Ruling: the builder's
        // question waits for a hand's word. Reanalyze: a release red's question, or a change that has to
        // land in another repository first, which goes to the analyst with no hold. HandedOff: the next voyage takes it. Walled: something outside the story must change
        // first, so it waits blocked in Backlog, where the rule that lifts `blocked` lifts it. Landed: it
        // rode main meanwhile. Unfinished: the voyage ended with no PR open (it died, its push was refused,
        // the quota wall), so a builder takes it again. Stalled: two dead voyages, for the analyst.
        ["Develop"] = new("Develop", WorkKind.Dev, true, true,
            new FilterOptions { Status = Stages.Ready, SubIssues = "0", Pr = "none", Unblocked = true, Labels = Holds },
            [.. WorkerType.BaseEvents, "Started", "Opened", "Pushed", "Ruling", "Reanalyze", "HandedOff", "Walled", "Landed", "Unfinished", "Stalled"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Started"] = [MoveTo(Stages.InProgress)],
                ["Ruling"] = [Add(Labels.Ruling), MoveTo(Stages.Analyze)],
                ["Reanalyze"] = [MoveTo(Stages.Analyze)],
                ["HandedOff"] = [MoveTo(Stages.Ready)],
                ["Walled"] = [Add(Labels.Blocked), MoveTo(Stages.Backlog)],
                ["Landed"] = [MoveTo(Stages.Merged)],
                ["Unfinished"] = [MoveTo(Stages.Ready)],
                ["Stalled"] = [MoveTo(Stages.Analyze)],
            }, Minutes: 240),
        // Clean: merged, nothing of the story left open. StillOpen: merged, another PR of it open.
        // Findings: Ready again, where the planner sends it to a round. Ruling: the PR found the story
        // itself wrong, and it waits for a hand's word: the PR stays, so a review would read it again.
        // A release red waits for no hand: its crack goes back to the builder as Findings. MergeRefused:
        // a clean read the code host would not merge. Stalled: no verdict, or findings on the fourth read.
        // Approved: clean, from a worker that does not merge (Merge false): the head carries the
        // reviewer's status (Markers.ReviewContext) and the PR waits for a hand's merge.
        ["Review"] = new("Review", WorkKind.Review, true, true,
            new FilterOptions { Status = PrStages, Pr = "open", Checks = "unread", Labels = Holds },
            [.. WorkerType.BaseEvents, "Clean", "StillOpen", "Findings", "Ruling", "MergeRefused", "Stalled", "Approved"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Clean"] = [MoveTo(Stages.Merged), Remove($"{Labels.Claimed},{Labels.Stalled}")],
                ["StillOpen"] = [Remove(Labels.Claimed), MoveTo(Stages.Ready)],
                ["Findings"] = [MoveTo(Stages.Ready)],
                ["Ruling"] = [Add(Labels.Ruling), MoveTo(Stages.Analyze)],
                ["MergeRefused"] = [MoveTo(Stages.Ready)],
                ["Stalled"] = [MoveTo(Stages.Analyze)],
                ["Approved"] = [Remove(Labels.Stalled)],
            }, Minutes: 60),
        // Verify a PR (Target pr) runs over stories; Verify main (Target main) on its own: see Kind.
        // OutOfTime: a PR's head red by time alone, past GateJob.LateRuns runs out of time with nothing
        // red; no builder can make the machine faster, so it waits for a hand.
        ["Verify"] = new("Verify", WorkKind.Gate, true, false,
            new FilterOptions { Status = PrStages, Pr = "open", Checks = "ungated", Labels = Holds },
            [.. WorkerType.BaseEvents, Jobs.GateJob.OutOfTimeEvent],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                [Jobs.GateJob.OutOfTimeEvent] = [Add(Labels.Hand)],
            }, Minutes: 45),
        // One story walked in a browser on the test tenant, the one its release went live on; its
        // events are the walker's verdicts, and {report} in a comment is what the walker wrote. Ok and
        // NotApplicable close it. Bug sends it back to Analyze with the report; BugAgain is a second
        // bug since a hand last lifted a hold — a fix that did not hold waits for a hand. Unwalkable: the
        // story cannot be walked as it stands (it needs something nobody provided: an account, an
        // environment, a person, a payment), so it stays Released wearing `unwalkable` and the report
        // says what a hand provides; the label off, it is walked again. NotLive: its tenant runs an older build, it stays for the next cut.
        // Doubt: the walk could not be made for a reason outside the story (the tenant is down, the
        // tools failed); its report goes on the story, nothing moves and it is tried once more after
        // QaJob.DoubtRecheck. DoubtAgain is a second doubt since a hand last lifted a hold: the wall
        // did not go away by itself, so the story waits for a hand where it stands. NoVerdict: the
        // walker died. The story stands in QA only while it is walked (QaJob.Walk); one a dead harness left
        // there is walked again.
        ["Test"] = new("Test", WorkKind.Qa, true, false,
            new FilterOptions { Status = $"{Stages.Released}|{Stages.Qa}", InVersion = true, Labels = Holds },
            [.. WorkerType.BaseEvents, "Ok", "NotApplicable", "Bug", "BugAgain", "Unwalkable", "NotLive", "Doubt", "DoubtAgain", "NoVerdict"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Ok"] = [Say("{report}"), MoveTo(Stages.Closed), Close()],
                ["NotApplicable"] = [Say("{report}"), MoveTo(Stages.Closed), Close()],
                ["Bug"] = [Say(QaBug), MoveTo(Stages.Analyze)],
                ["BugAgain"] = [Say(QaBug), Add(Labels.Stalled), MoveTo(Stages.Analyze)],
                ["Unwalkable"] = [Say("**QA no puede caminarla**\n\n{report}"), Add(Labels.Unwalkable)],
                ["Doubt"] = [Say(QaDoubt)],
                ["DoubtAgain"] = [Say(QaDoubt), Add(Labels.Stalled)],
                ["NoVerdict"] = [Say("QA terminó sin veredicto.\n\n{report}")],
            }, Minutes: 60),
        ["Release"] = new("Release", WorkKind.Release, false, false, new FilterOptions(), WorkerType.BaseEvents, None, Minutes: 150),
        // Reads a log source for errors and files a story per new one (Watching): no agent, no story of its own.
        ["Watch"] = new("Watch", WorkKind.Watch, false, false, new FilterOptions(), WorkerType.BaseEvents, None, Minutes: 10),
        ["UpdateBoard"] = new("UpdateBoard", null, true, false, new FilterOptions(), ["Executed"], None, Minutes: 0),
    };

    // The type that runs a kind of job.
    public static WorkerType? Of(WorkKind kind)
    {
        return All.Values.FirstOrDefault(t => t.Kind == kind);
    }
}

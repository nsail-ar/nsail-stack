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
    static readonly List<string> Holds = ["!ruling", "!stalled", "!blocked", "!hand"];

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

    static readonly IReadOnlyDictionary<string, IReadOnlyList<ActionOptions>> None = new Dictionary<string, IReadOnlyList<ActionOptions>>();

    public static readonly IReadOnlyDictionary<string, WorkerType> All = new Dictionary<string, WorkerType>(StringComparer.OrdinalIgnoreCase)
    {
        // Ready: shaped for a cold builder. Ruling: a question waits for a hand. Either way a stalled
        // story's analysis ends its stall.
        ["Analyze"] = new("Analyze", WorkKind.Analyze, true, true,
            new FilterOptions { Status = Stages.Analyze, SubIssues = "0", Pr = "none", Labels = Holds },
            [.. WorkerType.BaseEvents, "Ready", "Ruling", "Done"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Ready"] = [MoveTo(Stages.Ready), Remove(Labels.Stalled)],
                ["Ruling"] = [Add(Labels.Ruling), Remove(Labels.Stalled)],
                ["Done"] = [MoveTo(Stages.Merged), Remove(Labels.Stalled)],
            }, Minutes: 45),
        // Opened, Pushed: the gate decides what follows. Ruling: the builder's question goes back to
        // the analyst. Reanalyze: a release red's question, which goes to the analyst with no hold.
        // HandedOff: the next voyage takes it. Landed: it rode main meanwhile.
        ["Develop"] = new("Develop", WorkKind.Dev, true, true,
            new FilterOptions { Status = Stages.Ready, SubIssues = "0", Pr = "none", Unblocked = true, Labels = Holds },
            [.. WorkerType.BaseEvents, "Opened", "Pushed", "Ruling", "Reanalyze", "HandedOff", "Landed"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Ruling"] = [Add(Labels.Ruling), MoveTo(Stages.Analyze)],
                ["Reanalyze"] = [MoveTo(Stages.Analyze)],
                ["HandedOff"] = [MoveTo(Stages.Ready)],
                ["Landed"] = [MoveTo(Stages.Merged)],
            }, Minutes: 240),
        // Clean: merged, nothing of the story left open. StillOpen: merged, another PR of it open.
        // Findings: Ready again, where the planner sends it to a round. Ruling: the PR found the story
        // itself wrong, and it waits for a hand's word: the PR stays, so a review would read it again.
        // A release red waits for no hand: its crack goes back to the builder as Findings.
        ["Review"] = new("Review", WorkKind.Review, true, true,
            new FilterOptions { Status = PrStages, Pr = "open", Checks = "unread", Labels = Holds },
            [.. WorkerType.BaseEvents, "Clean", "StillOpen", "Findings", "Ruling"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Clean"] = [MoveTo(Stages.Merged), Remove($"{Labels.Claimed},{Labels.Stalled}")],
                ["StillOpen"] = [Remove(Labels.Claimed), MoveTo(Stages.Ready)],
                ["Findings"] = [MoveTo(Stages.Ready)],
                ["Ruling"] = [Add(Labels.Ruling), MoveTo(Stages.Analyze)],
            }, Minutes: 60),
        // Verify a PR (Target pr) runs over stories; Verify main (Target main) on its own: see Kind.
        ["Verify"] = new("Verify", WorkKind.Gate, true, false,
            new FilterOptions { Status = PrStages, Pr = "open", Checks = "ungated", Labels = Holds },
            WorkerType.BaseEvents, None, Minutes: 45),
        // One story walked in a browser on the test tenant, the one its release went live on; its
        // events are the walker's verdicts, and {report} in a comment is what the walker wrote. Ok and
        // NotApplicable close it. Bug sends it back to Analyze with the report; BugAgain is a second
        // bug since a hand last lifted a hold — a fix that did not hold waits for a hand. Asked: a
        // person must do or answer something, and the story waits in QA. Waiting: still no answer.
        // NotLive: its tenant runs an older build, it stays for the next cut. Doubt: it could not be
        // walked, which is an ask too: a story in QA wears qa-asked, or no walk ever reads it again.
        // NoVerdict: the walker died.
        ["Test"] = new("Test", WorkKind.Qa, true, false,
            new FilterOptions { Status = Stages.Released, InVersion = true, Labels = Holds },
            [.. WorkerType.BaseEvents, "Ok", "NotApplicable", "Bug", "BugAgain", "Asked", "Waiting", "NotLive", "Doubt", "NoVerdict"],
            new Dictionary<string, IReadOnlyList<ActionOptions>>
            {
                ["Ok"] = [Remove(Labels.QaAsked), Say("{report}"), MoveTo(Stages.Closed), Close()],
                ["NotApplicable"] = [Remove(Labels.QaAsked), Say("{report}"), MoveTo(Stages.Closed), Close()],
                ["Bug"] = [Say(QaBug), Remove(Labels.QaAsked), MoveTo(Stages.Analyze)],
                ["BugAgain"] = [Say(QaBug), Remove(Labels.QaAsked), Add(Labels.Stalled), MoveTo(Stages.Analyze)],
                ["Asked"] = [Say("**QA pregunta**\n\n{report}\n\n<!-- qa-asked: -->"), Add(Labels.QaAsked), MoveTo(Stages.Qa)],
                ["Doubt"] = [Say("**QA no pudo caminarla**\n\n{report}\n\n<!-- qa-asked: -->"), Add(Labels.QaAsked), MoveTo(Stages.Qa)],
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

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// Every word the harness itself posts on a board or a PR, as templates with `{name}` slots. Bound
// from `Harness:Texts`, so a project may say them in its own language; a seat's own words are its
// prompt's business, never these.
public sealed class HarnessTexts
{
    public string Signature { get; set; } = "— harness";

    public string TwoDeadVoyages { get; set; } = "Two dead voyages in a row — `stalled` instead of a third try: the analyst reads why. The transcripts are in the harness's log ({machine}).";
    public string QuotaWall { get; set; } = "The voyage died on the account's usage limit, not on the story: the harness starts nothing new until {until} UTC.";
    public string NothingPushed { get; set; } = "The voyage ended with nothing new pushed on PR #{pr}: the round did not advance. A second in a row goes to `stalled`.";
    public string DeadVoyage { get; set; } = "The voyage ended with no PR and no hand-off ({why}{pushed}): the story can be taken again. A second in a row with no progress goes to `stalled`.";
    public string PushRefused { get; set; } = "The push to `{branch}` was refused (the branch moved past the head the voyage started from, or the network failed): the builder's commits are on `{rescue}`. A hand reconciles them.";
    public string PushRefusedKept { get; set; } = "The push to `{branch}` was refused and the rescue push failed too: the builder's commits stay on the harness's local branch only. A hand reconciles them before the story's next voyage.";
    public string WorkPushed { get; set; } = ", what it committed pushed";
    public string NotOwnPr { get; set; } = "The builder asked to close PR #{pr}, which is not a PR of this story: nothing was closed.";
    public string ProtectedPaths { get; set; } = "The builder's commits touch protected paths ({paths}): nothing was pushed, and the story waits as `ruling` instead of being built again. Should the screen lift for it? A hand who says yes adds `{label}`: the hold comes off by itself, and the kept work is pushed if the branch has not moved.";
    public string ProtectedAhead { get; set; } = "The analysis finds this story's change must touch protected paths ({paths}), which no builder's push carries without a hand's word: it waits as `ruling` before any voyage. Should the screen lift for it? A hand who says yes adds `{label}`, and the hold comes off by itself.";
    public string CannotContinue { get; set; } = "This PR cannot be continued.";
    public string PrStory { get; set; } = "Story: {story}.";
    public string LandedNothingToBuild { get; set; } = "Nothing left to build: {evidence}. The voyage added nothing, so the story goes to **Merged** instead of back to a builder.";
    public string MainRedFixed { get; set; } = "Closed by the harness: {evidence}. A red that comes back files this story again.";

    public string ReviewNotGreen { get; set; } = "The review did not read PR #{pr}: its CI is not green ({ci}). Back to the queue.";
    public string ReviewNoVerdict { get; set; } = "The review of PR #{pr} died without a verdict — `stalled`: the analyst reads why.";
    public string ReviewStoryCrack { get; set; } = "Reading PR #{pr}, the reviewer found a crack in the story's logic, not in the code. Back to Analyze to rule it; the branch `{branch}` stays.";
    public string ReviewFourthRead { get; set; } = "Fourth read with findings on PR #{pr} — `stalled` instead of another round: the analyst reads why it does not converge.";
    public string MergeRefused { get; set; } = "The review came back clean but PR #{pr} did not merge (the head moved after the review read it, a conflict with main, or the API down). Back to Ready: the next round brings main in and asks for a review again.";
    public string Merged { get; set; } = "Merged: PR #{pr} — {title}";
    public string StillOpen { get; set; } = "Still open for this story: {prs}. Back to **Ready** instead of Merged: the story rides main only when none is left.";

    public string JobBroke { get; set; } = "The harness's {kind} job broke ({error}) on {machine}: it is tried once more. A second break in a row goes to `stalled`.";
    public string JobBrokeTwice { get; set; } = "The harness's {kind} job broke twice in a row ({error}) — `stalled` instead of a third try: a broken job breaks the same way every time. The log is in the harness ({machine}).";
    public string AnalysisDied { get; set; } = "The analysis ended without a verdict on {machine}: it is sent again. A second in a row goes to `stalled` and `ruling`.";

    public string AnalysisDiedTwice { get; set; } = "The analysis died twice in a row without a verdict — `stalled` and `ruling`: it is not taken again until a hand looks. The log is in the harness ({machine}).";

    public string GateVerdict { get; set; } = "**Gate (harness), {word} at `{sha}`** — {text}";
    public string GateGreen { get; set; } = "build and affected tests green.";
    public string GateRed { get; set; } = "{why}. That is the next round's work:";
    public string GateRedTests { get; set; } = "build or tests red";
    public string GateWaits { get; set; } = "red only where `main` is red at `{main}`: not this PR's round. It is gated again once `main` moves.";
    public string GateTimedOut { get; set; } = "passed the {minutes} min";
    public string GateConflict { get; set; } = "the head does not merge with `main`: there are conflicts. The next round brings `main` in and resolves them.";
    public string GateBroke { get; set; } = "the gate broke on infrastructure, not on this PR ({error}). The head is gated again after a short wait.";

    public string SuiteRedTitle { get; set; } = "Regression red on main ({product})";
    public string SuiteRed { get; set; } = "**{product}**'s browser suite went {why} on `{sha}` ({where}).\n\n**What went red:**\n```\n{failed}\n```\n\nThe run's log and evidence: `{log}` on {machine}.";
    public string MainRedTitle { get; set; } = "CI red on main";
    public string MainRed { get; set; } = "`main`'s gate went {why} at `{sha}` — the commits in {range} are the suspects.\n\n**What went red:**\n```\n{failed}\n```\n\nThe gate's log: `{log}` on {machine}.";
    public string MainRedWithoutFix { get; set; } = "`{sha}` does not hold this story's merge `{merge}`: its red is no news about the fix, and the story stays in {stage}.";

    // The template with its `{name}` slots filled.
    public static string Fill(string template, params (string Name, object? Value)[] values)
    {
        var text = template;
        foreach (var (name, value) in values)
        {
            text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "");
        }
        return text;
    }
}

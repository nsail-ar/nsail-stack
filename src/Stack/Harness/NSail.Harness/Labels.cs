// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// The labels that hold or lock a story, and the ones that order it.
public static class Labels
{
    // Locks: an agent holds the story for the length of its job.
    public const string Claimed = "claimed";
    public const string Review = "review";
    public const string Analyzing = "analyzing";
    public const string Walking = "walking";

    // Holds: whose word the story waits for.
    public const string Ruling = "ruling";
    public const string Stalled = "stalled";
    public const string Blocked = "blocked";
    public const string Hand = "hand";

    // QA asked a person for something and waits for the answer.
    public const string QaAsked = "qa-asked";

    // The analyst's word that nothing of the story shows on a screen: the gate and its tests are its
    // proof, and no walk is spent on it.
    public const string NoWalk = "no-walk";

    // Broken where a customer runs: before everything, a release and any finishing work included.
    public const string Bug = "bug";

    // The next important story: first among the rest.
    public const string P1 = "p1";

    // A story the telemetry watch opened from an error the fleet logged.
    public const string Telemetry = "telemetry";

    // A hand's word that the story's builder may touch the protected paths (HarnessOptions.ProtectedPaths):
    // on a story the screen holds, it also releases the hold.
    public const string AllowProtected = "allow-protected";

    // Worn beside `ruling` when the question is the screen's: `allow-protected` lifts both, and a
    // `ruling` without it is some other question's, which the label does not lift.
    public const string Screened = "screened";

    // A story's product area: `area:{name}`.
    public const string AreaPrefix = "area:";

    // What a seat's labels.txt never sets: the machine's locks and holds, and a hand's priority.
    public static readonly string[] SeatForbidden = [Claimed, Review, Analyzing, Walking, Ruling, Stalled, Blocked, Hand, P1, AllowProtected, Screened];

    public static readonly string[] Locks = [Claimed, Review, Analyzing, Walking];

    // What a hand lifts to send a story back to the machine: the counts start again from there.
    public static readonly string[] Lifts = [Stalled, Ruling];
}

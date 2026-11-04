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

    // First in the queue.
    public const string P1 = "p1";

    // A story's product area: `area:{name}`.
    public const string AreaPrefix = "area:";

    public static readonly string[] Locks = [Claimed, Review, Analyzing, Walking];

    // What a hand lifts to send a story back to the machine: the counts start again from there.
    public static readonly string[] Lifts = [Stalled, Ruling];
}

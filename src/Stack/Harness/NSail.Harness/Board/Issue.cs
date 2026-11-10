// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Board;

// A story as the planner reads it. InVersion: the board names the version it shipped in (on GitHub,
// a milestone). ParentPercent is -1 when the story has no parent epic; Blockers
// counts every blocker, closed ones too, and OpenBlockers lists the open ones. TouchedAt is the
// last comment, label change or column move on it; null when the board does not say.
public sealed record Issue(
    int Number,
    string Title,
    IReadOnlySet<string> Labels,
    string Stage,
    int SubIssues,
    IReadOnlyList<int> OpenBlockers,
    double ParentPercent,
    bool InVersion,
    int Parent,
    double OwnPercent,
    int Blockers = 0)
{
    public DateTimeOffset? TouchedAt { get; init; }

    // When the board says the story was filed.
    public DateTimeOffset? CreatedAt { get; init; }

    public bool Has(string label)
    {
        return Labels.Contains(label);
    }
}

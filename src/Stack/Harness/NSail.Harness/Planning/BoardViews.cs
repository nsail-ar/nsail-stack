// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Repositories;

namespace NSail.Harness.Planning;

// The board a pass read, as its status and its report to Ops carry it.
public static class BoardViews
{
    // A gate's red is a hold of its own beside the labels: its PR waits for a fix.
    public const string RedGate = "gate";

    static readonly string[] HoldLabels = [Labels.Ruling, Labels.Stalled, Labels.Unwalkable];

    public static BoardView Of(BoardSnapshot snapshot)
    {
        var open = snapshot.Issues.Where(i => Stages.Open.Contains(i.Stage)).ToList();
        var stages = Stages.Open.Select(s => new StageCount(s, open.Count(i => i.Stage == s))).ToList();
        var red = snapshot.Prs.Where(p => p.Gate == CiState.Red).Select(p => p.Story).ToHashSet();
        var held = new List<HeldStory>();
        foreach (var issue in open)
        {
            foreach (var label in HoldLabels.Where(issue.Has)) held.Add(new HeldStory(issue.Number, issue.Title, issue.Stage, label));
            if (red.Contains(issue.Number)) held.Add(new HeldStory(issue.Number, issue.Title, issue.Stage, RedGate));
        }
        return new BoardView(snapshot.Repo, snapshot.ReadAt, stages, held);
    }
}

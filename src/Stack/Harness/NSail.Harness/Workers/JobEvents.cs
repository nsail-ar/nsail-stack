// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Jobs;

namespace NSail.Harness.Workers;

// The events a finished job fires on its story, in order: its own (Opened, Clean...), then Succeeded
// or Failed, then Executed. A job that ended on the quota wall or found nothing to do fires none,
// and neither does a job that broke: that is the harness's, said on the story apart (Markers.Broke).
public static class JobEvents
{
    public static IReadOnlyList<string> Fired(JobOutcome outcome)
    {
        if (outcome.Quiet) return [];
        var fired = new List<string>();
        if (outcome.Event is { } own) fired.Add(own);
        fired.Add(outcome.Failed ? "Failed" : "Succeeded");
        fired.Add("Executed");
        return fired;
    }

    // The actions for what fired, in that order: the worker's (Worker.On), or its type's defaults.
    public static IReadOnlyList<(string Event, ActionOptions Action)> Actions(IReadOnlyDictionary<string, IReadOnlyList<ActionOptions>> on, JobOutcome outcome)
    {
        return Fired(outcome)
            .SelectMany(e => on.TryGetValue(e, out var actions) ? actions.Select(a => (e, a)) : [])
            .ToList();
    }

    public static async Task Apply(IBoard board, int story, ActionOptions a, string signature, string? report, CancellationToken ct)
    {
        if (a.MoveTo is { } stage) await board.SetStage(story, stage, ct);
        if (a.AddLabel is { } add) await board.AddLabels(story, [.. ActionOptions.Names(add)]);
        if (a.RemoveLabel is { } remove)
        {
            foreach (var l in ActionOptions.Names(remove)) await board.RemoveLabel(story, l);
        }
        if (a.Comment is { } comment) await board.Comment(story, Said(comment, signature, report));
        if (a.Close is true) await board.Close(story, ct);
    }

    // A one-line comment signs on its line, a longer one under it.
    public static string Said(string comment, string signature, string? report)
    {
        var text = comment.Replace("{report}", report?.Trim() ?? "", StringComparison.Ordinal);
        return text.Contains('\n') ? $"{text}\n\n{signature}" : $"{text} {signature}";
    }
}

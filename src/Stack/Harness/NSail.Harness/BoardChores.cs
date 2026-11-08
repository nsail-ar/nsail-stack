// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;
using NSail.Harness.Workers;

namespace NSail.Harness;

// Writes a pass's chores. A story whose chore failed or whose move was dropped gets none of the rest
// this pass: a comment that follows a move must not be said while the move does not happen.
static class BoardChores
{
    // Each story's column as the pass's read saw it.
    public static IReadOnlyDictionary<int, string> ReadOf(IEnumerable<Issue> issues)
    {
        return issues.DistinctBy(i => i.Number).ToDictionary(i => i.Number, i => i.Stage);
    }

    // A keeper's move is written only over the column the pass read: one a hand set since stays. A
    // story a keeper moves is in the read. The board has no conditional write, so this narrows the
    // race to one read and one write. A worker's Starting move is the job's and is written as planned.
    public static async Task Do(IBoard board, IReadOnlyList<Chore> chores, IReadOnlyDictionary<int, string> read,
        string signature, ILogger log, CancellationToken ct)
    {
        var expected = new Dictionary<int, string>(read);
        var dropped = new HashSet<int>();
        foreach (var c in chores)
        {
            if (dropped.Contains(c.Story)) continue;
            try
            {
                switch (c.Kind)
                {
                    case ChoreKind.Unlock:
                        await board.RemoveLabel(c.Story, c.Value);
                        break;
                    case ChoreKind.MoveStage:
                        if (WorkerPlanner.Kept(c))
                        {
                            var saw = expected[c.Story];
                            var now = await board.StageOf(c.Story, ct);
                            if (now != saw)
                            {
                                dropped.Add(c.Story);
                                log.LogInformation("#{Story}: in {Found}, not {Read} — move to {Stage} dropped ({Why})", c.Story, now, saw, c.Value, c.Why);
                                continue;
                            }
                        }
                        await board.SetStage(c.Story, c.Value, ct);
                        expected[c.Story] = c.Value;
                        break;
                    case ChoreKind.AddLabel:
                        await board.AddLabels(c.Story, c.Value);
                        break;
                    case ChoreKind.Comment:
                        await board.Comment(c.Story, $"{c.Value} {signature}");
                        break;
                    case ChoreKind.Close:
                        await board.Close(c.Story, ct);
                        break;
                }
                log.LogInformation("chore: {Kind} #{Story} {Value} — {Why}", c.Kind, c.Story, c.Value, c.Why);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                dropped.Add(c.Story);
                log.LogWarning("chore {Kind} #{Story} failed: {Error}", c.Kind, c.Story, e.Message);
            }
        }
    }
}

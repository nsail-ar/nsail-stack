// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Repositories;

namespace NSail.Harness.Planning;

// Gate: a story PR in the default repository is judged by the harness's own build gate instead of the
// code host's CI; another repository's PRs keep their own CI either way.
public sealed record PlannerOptions(bool Gate = false)
{
    // Drivers a story's label may name (`{driver}` or `{driver}:{model}`) that no harness here can run: they hold it.
    public IReadOnlyCollection<string> UnrunnableDrivers { get; init; } = [];

    // How the stories the harness files for a red suite and a red main are titled: they come first.
    public string SuiteRedPrefix { get; init; } = "Regression red on main (";
    public string MainRedPrefix { get; init; } = "CI red on main";

    // A lock with no job behind it for this long is a dispatch that never started.
    public TimeSpan StaleLock { get; init; } = TimeSpan.FromMinutes(10);

    // A green head in another repository unread for this long has no review coming: the read its CI
    // starts gives up by then (nsail-stack's .github/workflows/review.yml, timeout-minutes).
    public TimeSpan UnreadElsewhere { get; init; } = TimeSpan.FromMinutes(60);

    // How many stories may be under way at once (an open PR nobody holds, or a voyage still
    // without one) before a new one leaves the queue: closing comes before starting. 0 is no limit.
    public int WipLimit { get; init; }

    // A story In-Progress is moved by a keeper with no open PR (`Pr: none`) only once nothing touched
    // it for this long: a job's ending (its label off, its PR opening) lands inside it. As long as
    // StaleLock, the age a `claimed` with no job behind it is lifted at.
    public TimeSpan OrphanGrace { get; init; } = TimeSpan.FromMinutes(10);
}

// What every planner shares: the urgency of a tier for whoever waits for a turn, the holds that
// keep a story waiting, and the release of a lock no job holds and of a hold the screen set. The
// order of the work is the workers' (WorkerPlanner).
public static class Planner
{
    // How urgent a tier is, for whoever must wait for a turn: the planner's own order, 0 first.
    public static int Rank(string tier)
    {
        return tier switch
        {
            "bug" => -1,
            "release" or "manual" => 0,
            "red" => 1,
            "round" => 2,
            "review" or "gate" or "prs" => 3,
            "qa" => 4,
            "stalled" => 5,
            "analyze" => 6,
            "p1" => 7,
            "epic" => 8,
            _ => 9,
        };
    }

    public static string? HoldOf(Issue i, PlannerOptions options)
    {
        foreach (var hold in new[] { Labels.Claimed, Labels.Review, Labels.Ruling, Labels.Stalled, Labels.Blocked, Labels.Analyzing, Labels.Walking, Labels.Hand })
        {
            if (i.Has(hold)) return hold;
        }
        foreach (var driver in options.UnrunnableDrivers)
        {
            if (i.Labels.Any(l => l == driver || l.StartsWith(driver + ":", StringComparison.Ordinal))) return $"no {driver} driver here";
        }
        return null;
    }

    // A lock with no job behind it comes off, out loud, and the story is simply in the queue again; so
    // does the screen's hold once a hand allows the protected paths.
    internal static List<Issue> ReleaseStaleLocks(BoardSnapshot board, PlannerOptions options, List<Chore> chores, List<string> notes)
    {
        var reviewByStory = board.Prs.Where(p => p.Repo == board.Repo).GroupBy(p => p.Story)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Number).ToList());
        var result = new List<Issue>(board.Issues.Count);
        foreach (var i in board.Issues)
        {
            var drop = new List<string>();
            // A lock a harness holds is that harness's job: never stale.
            if (board.LockHolders.ContainsKey(i.Number))
            {
                result.Add(i);
                continue;
            }
            if (i.Has(Labels.Analyzing) && !board.Flight.AnalyzeStories.Contains(i.Number) && Stale(i.Number, Labels.Analyzing))
            {
                drop.Add(Labels.Analyzing);
            }
            if (i.Has(Labels.Claimed) && !board.Flight.DevStories.Contains(i.Number) && Stale(i.Number, Labels.Claimed))
            {
                drop.Add(Labels.Claimed);
            }
            if (i.Has(Labels.Review))
            {
                var prs = reviewByStory.GetValueOrDefault(i.Number) ?? [];
                if (!prs.Any(board.Flight.ReviewPrs.Contains) && Stale(i.Number, Labels.Review))
                {
                    drop.Add(Labels.Review);
                }
            }
            foreach (var label in drop)
            {
                chores.Add(new Chore(ChoreKind.Unlock, i.Number, label, $"`{label}` with no job behind it"));
                notes.Add($"#{i.Number} wore `{label}` with no job behind it — released, back in the queue");
            }
            var lifted = LiftScreen(i, chores, notes);
            drop.AddRange(lifted);
            result.Add(drop.Count == 0 ? i : i with { Labels = i.Labels.Except(drop).ToHashSet() });
        }
        return result;

        bool Stale(int n, string label)
        {
            return board.LockAges.TryGetValue((n, label), out var age) && age > options.StaleLock;
        }
    }

    // The screen's hold is the harness's own: `allow-protected` lifts it with no hand removing a label.
    // A `screened` left after a hand lifted the `ruling` itself is only a stale mark.
    static List<string> LiftScreen(Issue i, List<Chore> chores, List<string> notes)
    {
        if (!i.Has(Labels.Screened)) return [];
        if (!i.Has(Labels.Ruling))
        {
            chores.Add(new Chore(ChoreKind.Unlock, i.Number, Labels.Screened, $"`{Labels.Ruling}` already lifted"));
            return [Labels.Screened];
        }
        if (!i.Has(Labels.AllowProtected)) return [];
        chores.Add(new Chore(ChoreKind.Unlock, i.Number, Labels.Ruling, $"`{Labels.AllowProtected}` lifts the screen"));
        chores.Add(new Chore(ChoreKind.Unlock, i.Number, Labels.Screened, $"`{Labels.AllowProtected}` lifts the screen"));
        notes.Add($"#{i.Number} wears `{Labels.AllowProtected}` — the screen's `{Labels.Ruling}` lifted, back in the queue");
        return [Labels.Ruling, Labels.Screened];
    }
}

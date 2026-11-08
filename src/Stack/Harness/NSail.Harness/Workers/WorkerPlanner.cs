// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Board;
using NSail.Harness.Planning;
using NSail.Harness.Repositories;

namespace NSail.Harness.Workers;

// The order of the work, as the workers say it. One pass:
//   1. the board keepers (UpdateBoard): every story their filter matches gets their actions;
//   2. the workers over stories, by Priority, over the `bug` stories only;
//   3. the workers that run on their own (a release, main's verification, a QA drain), by Priority;
//   4. the workers over stories, by Priority, over the p1 stories (and a red main's or suite's);
//   5. the same workers over every other story.
// Inside a worker, stories go by the configured Order (epic progress, then age). A story gets one
// job per pass: the first worker that takes it. Locks (a job's own labels), a story already worked
// here and a driver this machine cannot run hold it below any filter.
public static class WorkerPlanner
{
    const string Executed = "Executed";

    public static Plan Make(BoardSnapshot board, IReadOnlyList<Worker> workers, PlannerOptions options, Func<Worker, bool>? may = null)
    {
        may ??= _ => true;
        var chores = new List<Chore>();
        var notes = new List<string>();
        var issues = Planner.ReleaseStaleLocks(board, options, chores, notes);
        var lookup = new StoryLookup(issues, board.Prs, board.MergedStories);
        var flight = board.Flight;
        var prsByStory = board.Prs.GroupBy(p => p.Story).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Number).ToList());

        foreach (var keeper in workers.Where(w => w.Kind is null && may(w)))
        {
            // A story a job works is that job's to move.
            foreach (var i in issues.Where(i => !Busy(i, flight, options) && keeper.Takes(i, null, null, lookup)))
            {
                chores.AddRange(ChoresOf(keeper, Executed, i));
            }
        }

        // A stalled story the analyst cannot take from where it stands is a hand's to move.
        foreach (var i in issues.Where(i => i.Has(Labels.Stalled) && !i.Has(Labels.Ruling) && i.Stage is not (Stages.Ready or Stages.InProgress or Stages.Analyze)))
        {
            notes.Add($"#{i.Number}: stalled in {i.Stage}, not Analyze — a hand moves it");
        }

        // QA holds what waits on an ask; a story there with no ask and no hold is walked by nobody.
        foreach (var i in issues.Where(i => i.Stage == Stages.Qa && !i.Has(Labels.QaAsked) && Planner.HoldOf(i, options) is null))
        {
            notes.Add($"#{i.Number}: in QA with no ask — no walk reads it again");
        }

        var items = new List<WorkItem>();
        var taken = new HashSet<int>();
        var wip = prsByStory.Keys.Where(n => issues.FirstOrDefault(i => i.Number == n) is { } i && Planner.HoldOf(i, options) is not (Labels.Ruling or Labels.Blocked or Labels.Hand or Labels.Stalled))
            .Union(flight.DevStories).Distinct().Count();
        var full = options.WipLimit > 0 && wip >= options.WipLimit;
        if (full) notes.Add($"{wip} stories under way (limit {options.WipLimit}): no new story starts until one closes");

        var sea = issues.Where(i => flight.DevStories.Contains(i.Number))
            .SelectMany(i => i.Labels.Where(l => l.StartsWith(Labels.AreaPrefix, StringComparison.Ordinal)))
            .ToHashSet();

        // A bug goes before everything: the workers on their own (a release, main's gate) wait behind it,
        // and the WIP limit does not hold it.
        Sweep(i => i.Has(Labels.Bug), bug: true);
        foreach (var w in workers.Where(w => w.Kind is not null && !w.OverStories && may(w)))
        {
            items.AddRange(OnItsOwn(w, board, issues, options));
        }
        // A red main's or red suite's story is swept with the p1 ones, whatever its labels.
        Sweep(i => !i.Has(Labels.Bug) && (i.Has(Labels.P1) || Red(i, options)), bug: false);
        Sweep(i => !i.Has(Labels.Bug) && !i.Has(Labels.P1) && !Red(i, options), bug: false);
        return new Plan(items, chores, notes);

        void Sweep(Func<Issue, bool> which, bool bug)
        {
            foreach (var w in workers.Where(w => w.Kind is not null && w.OverStories && may(w)))
            {
                if (w.TakesNew && full && !bug) continue;
                var stories = Ordered(issues.Where(which), w, options);
                // Lanes in different parts of the tree conflict less: a new story in an area no lane
                // builds goes first.
                if (w.TakesNew) stories = stories.Where(i => !sea.Any(i.Has)).Concat(stories.Where(i => sea.Any(i.Has))).ToList();
                foreach (var i in stories)
                {
                    if (taken.Contains(i.Number) || Busy(i, flight, options)) continue;
                    var pr = Deciding(prsByStory.GetValueOrDefault(i.Number), board, flight);
                    var state = pr is null ? null : StateOf(pr, board, options);
                    if (!w.Takes(i, w.Filter.Pr == "open" ? pr : null, state, lookup)) continue;
                    taken.Add(i.Number);
                    chores.AddRange(ChoresOf(w, "Starting", i));
                    items.Add(ItemOf(w, i, w.Filter.Pr == "open" ? pr : null, state, board, options));
                }
            }
        }
    }

    // A red main's or red suite's story first, whatever the worker; then oldest first, and a worker
    // that starts new stories takes those of the epics closest to done first.
    static List<Issue> Ordered(IEnumerable<Issue> issues, Worker w, PlannerOptions options)
    {
        var red = issues.OrderBy(i => Red(i, options) ? 0 : 1);
        return w.TakesNew
            ? red.ThenByDescending(i => Math.Max(i.ParentPercent, 0)).ThenBy(i => i.Number).ToList()
            : red.ThenBy(i => i.Number).ToList();
    }

    static bool Red(Issue i, PlannerOptions options)
    {
        return i.Title.StartsWith(options.SuiteRedPrefix, StringComparison.Ordinal) || i.Title.StartsWith(options.MainRedPrefix, StringComparison.Ordinal);
    }

    // A story some job already works, here or on another machine, or one this machine cannot run.
    static bool Busy(Issue i, Flight flight, PlannerOptions options)
    {
        if (flight.DevStories.Contains(i.Number) || flight.AnalyzeStories.Contains(i.Number) || flight.TestStories.Contains(i.Number)) return true;
        if (Labels.Locks.Any(i.Has)) return true;
        return Planner.HoldOf(i, options) is { } hold && hold.StartsWith("no ", StringComparison.Ordinal);
    }

    // The PR a story's PR work is about: its first open one nobody here is already reading.
    static StoryPr? Deciding(List<StoryPr>? prs, BoardSnapshot board, Flight flight)
    {
        return prs?.FirstOrDefault(p => p.Repo != board.Repo || !flight.ReviewPrs.Contains(p.Number));
    }

    // A PR's state at its head, as a filter's Checks names it.
    public static string StateOf(StoryPr pr, BoardSnapshot board, PlannerOptions options)
    {
        var home = pr.Repo == board.Repo;
        if (home && options.Gate && pr.Ci != CiState.Conflict)
        {
            switch (pr.Gate)
            {
                case null: return "ungated";
                case CiState.Pending: return "pending";
                case CiState.Red: return "red";
            }
        }
        else
        {
            switch (pr.Ci)
            {
                case CiState.Pending: return "pending";
                case CiState.Red: return "red";
                case CiState.Conflict: return "conflict";
            }
        }
        if (pr.Word is not null) return "read";
        // A green head in another repository is read by that repository's own review, which its CI starts.
        return home ? "unread" : "elsewhere";
    }

    static IEnumerable<WorkItem> OnItsOwn(Worker w, BoardSnapshot board, IReadOnlyList<Issue> issues, PlannerOptions options)
    {
        var flight = board.Flight;
        switch (w.Kind)
        {
            // A worker's product: its newest release to walk, its red fix merged (a cut now), or its
            // day's cut due. The walk moves only the board, so it goes while a cut runs.
            case WorkKind.Release when w.Options.Workflow is { } wf:
                if (w.Options.Product is { } product && board.Unshipped.TryGetValue(product.ToLowerInvariant(), out var tag) && !flight.ShipTags.Contains(tag))
                {
                    yield return new WorkItem(WorkKind.Ship, null, null, "release", $"{tag} published", wf, Worker: w.Name, ProductName: product, Tag: tag);
                }
                if (flight.ReleaseWorkflows.Contains(wf)) break;
                if (board.Releases.FirstOrDefault(r => r.Workflow == wf) is { } fix)
                {
                    yield return new WorkItem(WorkKind.Release, fix.Story, null, "release", $"the red fix #{fix.Story} merged", wf, fix.Sha, Worker: w.Name, ProductName: w.Options.Product);
                }
                else if (board.DueReleases.Contains(wf))
                {
                    yield return new WorkItem(WorkKind.Release, null, null, "release", "the day's cut", wf, Worker: w.Name, ProductName: w.Options.Product);
                }
                break;
            case WorkKind.Watch when board.DueWatches.Contains(w.Name):
                yield return new WorkItem(WorkKind.Watch, null, null, "watch", "due", Worker: w.Name);
                break;
            // main moved past what the gate last judged: it is judged before anything is built on it.
            case WorkKind.Main when options.Gate && board.MainSha is { } head && head != board.MainGated && !flight.MainGating:
                yield return new WorkItem(WorkKind.Main, null, null, "red", "main moved", Sha: head, Worker: w.Name);
                break;
        }
    }

    static WorkItem ItemOf(Worker w, Issue i, StoryPr? pr, string? state, BoardSnapshot board, PlannerOptions options)
    {
        var red = Red(i, options);
        var bug = i.Has(Labels.Bug);
        if (pr is not null)
        {
            var home = pr.Repo == board.Repo;
            var where = home ? $"PR #{pr.Number}" : $"{pr.Repo.Split('/')[1]} PR #{pr.Number}";
            return w.Kind switch
            {
                WorkKind.Gate => new WorkItem(WorkKind.Gate, i.Number, pr.Number, bug ? "bug" : red ? "red" : "gate", "no gate at this head", Worker: w.Name),
                WorkKind.Review => new WorkItem(WorkKind.Review, i.Number, pr.Number, bug ? "bug" : red ? "red" : "review", "green, unread", Worker: w.Name),
                _ => new WorkItem(w.Kind!.Value, i.Number, pr.Number, bug ? "bug" : red ? "red" : "round", state switch
                {
                    "red" when home && options.Gate => $"round ({where}: gate red)",
                    "red" => $"round ({where}: CI red)",
                    "conflict" => $"round ({where}: CI conflict)",
                    _ => $"round ({where} read {pr.Word} at {pr.Sha[..Math.Min(8, pr.Sha.Length)]})",
                }, PrRepo: pr.Repo, Worker: w.Name),
            };
        }
        if (w.Kind == WorkKind.Qa) return new WorkItem(WorkKind.Qa, i.Number, null, "qa", "released", Worker: w.Name);
        if (w.Kind == WorkKind.Analyze)
        {
            var stalled = i.Has(Labels.Stalled);
            return new WorkItem(WorkKind.Analyze, i.Number, null, bug ? "bug" : stalled ? "stalled" : "analyze", stalled ? "stalled" : "analyze", Worker: w.Name);
        }
        var tier = bug ? "bug" : red ? "red" : i.Has(Labels.P1) ? "p1" : i.ParentPercent > 0 ? "epic" : "rest";
        return new WorkItem(w.Kind!.Value, i.Number, null, tier, tier, Worker: w.Name);
    }

    // A keeper's chore: planned from the pass's read, with no job's lock on the story behind it.
    internal static bool Kept(Chore c)
    {
        return c.Why.EndsWith($": {Executed}", StringComparison.Ordinal);
    }

    // A worker's actions on a story, as chores the loop applies: what is already so is left out.
    static IEnumerable<Chore> ChoresOf(Worker w, string evt, Issue i)
    {
        if (!w.On.TryGetValue(evt, out var actions)) yield break;
        var why = $"{w.Name}: {evt}";
        foreach (var a in actions)
        {
            if (a.MoveTo is { } stage && stage != i.Stage) yield return new Chore(ChoreKind.MoveStage, i.Number, stage, why);
            if (a.RemoveLabel is { } remove)
            {
                foreach (var l in ActionOptions.Names(remove).Where(i.Has)) yield return new Chore(ChoreKind.Unlock, i.Number, l, why);
            }
            if (a.AddLabel is { } add)
            {
                foreach (var l in ActionOptions.Names(add).Where(l => !i.Has(l))) yield return new Chore(ChoreKind.AddLabel, i.Number, l, why);
            }
            if (a.Comment is { } comment) yield return new Chore(ChoreKind.Comment, i.Number, comment, why);
            if (a.Close is true) yield return new Chore(ChoreKind.Close, i.Number, "", why);
        }
    }
}

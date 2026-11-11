// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using NSail.Harness.Board;
using NSail.Harness.Planning;

namespace NSail.Harness.Workers;

// A worker as the engine runs it: its options merged over its type's defaults, checked once at the
// start. A configuration that cannot run fails the start, naming the worker and the field.
public sealed class Worker
{
    public string Name { get; }
    public WorkerType Type { get; }
    public WorkerOptions Options { get; }
    public FilterOptions Filter { get; }
    public int Priority => Options.Priority;

    // The job it runs: Verify on main is main's own gate, not a story's.
    public WorkKind? Kind => Type.Name == "Verify" && IsMain ? WorkKind.Main : Type.Kind;

    // Runs over stories, or on its own (a release, main's verification, a QA drain).
    public bool OverStories => Type.OverStories && !IsMain;

    // A worker that takes new stories (a Develop with no PR yet): the WIP limit and the agent kept
    // free for finishing work hold it back.
    public bool TakesNew => Type.Name == "Develop" && Filter.Pr == "none";

    // The actions of each event: the worker's where it names one, else its type's.
    public IReadOnlyDictionary<string, IReadOnlyList<ActionOptions>> On { get; }

    bool IsMain => string.Equals(Options.Target, "main", StringComparison.OrdinalIgnoreCase);

    readonly Regex? _title;

    Worker(string name, WorkerType type, WorkerOptions options, FilterOptions filter)
    {
        Name = name;
        Type = type;
        Options = options;
        Filter = filter;
        // The type's actions per event, an event the worker names replaced whole.
        var on = new Dictionary<string, IReadOnlyList<ActionOptions>>(type.Defaults, StringComparer.OrdinalIgnoreCase);
        foreach (var (evt, actions) in options.On) on[evt] = actions;
        On = on;
        _title = filter.Title is { } t && ValidRegex(t) ? new Regex(t, RegexOptions.CultureInvariant) : null;
    }

    // Every enabled worker, by Priority then name; the problems found stop the start.
    public static IReadOnlyList<Worker> Compile(HarnessOptions o)
    {
        var problems = new List<string>();
        var workers = new List<Worker>();
        foreach (var (name, c) in o.Conditions) problems.AddRange(Conditions.Problems($"Harness:Conditions:{name}", c));
        foreach (var (name, seat) in o.Seats.Where(s => string.IsNullOrWhiteSpace(s.Value.Prompt) == string.IsNullOrWhiteSpace(s.Value.PromptFile)))
        {
            problems.Add($"Harness:Seats:{name} takes a Prompt or a PromptFile, one of them");
        }
        foreach (var (name, w) in o.Workers.Where(p => p.Value.Enabled))
        {
            var where = $"Harness:Workers:{name}";
            if (!WorkerTypes.All.TryGetValue(w.Type, out var type))
            {
                problems.Add($"{where}: no type {(w.Type.Length == 0 ? "given" : w.Type)} (one of {string.Join(", ", WorkerTypes.All.Keys)})");
                continue;
            }
            var filter = Merge(type.Filter, w.Filter);
            problems.AddRange(Problems(where, type, w, filter, o));
            workers.Add(new Worker(name, type, w, filter));
        }
        if (problems.Count > 0) throw new InvalidOperationException(string.Join("; ", problems));
        return workers.OrderBy(w => w.Priority).ThenBy(w => w.Name, StringComparer.Ordinal).ToList();
    }

    static FilterOptions Merge(FilterOptions type, FilterOptions own)
    {
        return new FilterOptions
        {
            Status = own.Status ?? type.Status,
            Labels = own.Labels ?? type.Labels,
            Pr = own.Pr ?? type.Pr,
            Checks = own.Checks ?? type.Checks,
            SubIssues = own.SubIssues ?? type.SubIssues,
            Blockers = own.Blockers ?? type.Blockers,
            Unblocked = own.Unblocked ?? type.Unblocked,
            EpicStarted = own.EpicStarted ?? type.EpicStarted,
            EpicDone = own.EpicDone ?? type.EpicDone,
            PrMerged = own.PrMerged ?? type.PrMerged,
            InVersion = own.InVersion ?? type.InVersion,
            Title = own.Title ?? type.Title,
        };
    }

    static readonly string[] PrValues = ["none", "open", "any"];
    static readonly string[] CheckValues = ["ungated", "pending", "red", "conflict", "unread", "read", "elsewhere"];
    static readonly string[] CountValues = ["0", ">0"];

    static IEnumerable<string> Problems(string where, WorkerType type, WorkerOptions w, FilterOptions f, HarnessOptions o)
    {
        if (type.HasAgent && w.Seat is null) yield return $"{where}: a {type.Name} needs a Seat";
        if (w.Seat is { } seat && !o.Seats.ContainsKey(seat)) yield return $"{where}: no seat {seat} in Harness:Seats";
        if (!string.IsNullOrWhiteSpace(w.Prompt) && !string.IsNullOrWhiteSpace(w.PromptFile)) yield return $"{where}: a Prompt or a PromptFile, one of them";
        if (type.Name == "Verify" && w.Target is not null && !string.Equals(w.Target, "main", StringComparison.OrdinalIgnoreCase) && !string.Equals(w.Target, "pr", StringComparison.OrdinalIgnoreCase))
            yield return $"{where}: Target is pr or main, not {w.Target}";
        if (type.Name == "Test" && string.IsNullOrWhiteSpace(w.Command) && w.Seat is null) yield return $"{where}: a Test needs a Seat (its walker) or a Command";
        if (type.Name == "Verify" && string.IsNullOrWhiteSpace(w.Command) && o.Repositories.FirstOrDefault(r => r.Label is null)?.Build is null) yield return $"{where}: a Verify needs a Command or the repository's Build";
        if (type.Name == "Release" && w.Suite is not null && o.Repositories.FirstOrDefault(r => r.Label is null)?.Build is null) yield return $"{where}: a Suite runs on the repository's Build, and it names none";
        if (type.Name == "Release")
        {
            foreach (var (field, value) in new[] { ("Product", w.Product), ("Workflow", w.Workflow), ("Paths", w.Paths) })
            {
                if (string.IsNullOrWhiteSpace(value)) yield return $"{where}: a Release needs a {field}";
            }
        }
        if (w.Max is < 0) yield return $"{where}: Max can't be negative";
        if (f.Pr is { } pr && !PrValues.Contains(pr)) yield return $"{where}: Filter Pr is one of {string.Join(", ", PrValues)}";
        if (f.Checks is { } checks && checks.Split('|').FirstOrDefault(c => !CheckValues.Contains(c)) is { } badCheck)
            yield return $"{where}: Filter Checks has no state {badCheck} (one of {string.Join(", ", CheckValues)})";
        if (f.SubIssues is { } subs && !CountValues.Contains(subs)) yield return $"{where}: Filter SubIssues is 0 or >0";
        if (f.Blockers is { } blockers && !CountValues.Contains(blockers)) yield return $"{where}: Filter Blockers is 0 or >0";
        if (f.Title is { } title && !ValidRegex(title)) yield return $"{where}: Filter Title is not a regular expression";
        foreach (var u in w.When.Use.Where(u => !o.Conditions.ContainsKey(u))) yield return $"{where}: When uses {u}, not in Harness:Conditions";
        foreach (var p in Conditions.Problems($"{where}:When", w.When)) yield return p;
        if (type.Name == "Release" && w.When.Schedule is null) yield return $"{where}: a Release needs When.Schedule (its day's cut)";
        if (type.Name == "Watch" && string.IsNullOrWhiteSpace(w.Source)) yield return $"{where}: a Watch needs a Source (a log source provider's name)";
        if (type.Name == "Watch" && w.Every is <= 0) yield return $"{where}: Every is minutes, more than zero";
        if (type.Name == "Watch" && w.IngestLag is < 0) yield return $"{where}: IngestLag is minutes, zero or more";
        if (type.Name == "UpdateBoard" && !w.On.TryGetValue("Executed", out _)) yield return $"{where}: an UpdateBoard does nothing without On Executed";
        var alone = !type.OverStories || (type.Name == "Verify" && string.Equals(w.Target, "main", StringComparison.OrdinalIgnoreCase));
        if (alone && w.On.Count > 0) yield return $"{where}: a worker on its own has no story to act on — no On";
        foreach (var (name, actions) in w.On)
        {
            if (!type.Events.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                yield return $"{where}: On {name} — a {type.Name} has no such event (its events: {string.Join(", ", type.Events)})";
                continue;
            }
            foreach (var a in actions.Where(a => a.Count != 1)) yield return $"{where}: On {name} has an action with {a.Count} fields set — exactly one";
        }
    }

    static bool ValidRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // Whether the story stands where this worker takes it; the PR, if the worker acts on one, is
    // the first open one whose state the filter asks for.
    public bool Takes(Issue i, StoryPr? pr, string? prState, StoryLookup board)
    {
        var f = Filter;
        if (f.Status is { } status && !StatusHolds(status, i.Stage)) return false;
        if (f.Labels is { } labels && !labels.All(l => LabelHolds(l, i.Labels))) return false;
        if (f.SubIssues is { } subs && (subs == "0") != (i.SubIssues == 0)) return false;
        if (f.Blockers is { } blockers && (blockers == "0") != (i.Blockers == 0)) return false;
        if (f.Unblocked is { } unblocked && unblocked != board.Unblocked(i)) return false;
        if (f.EpicStarted is { } started && started != board.EpicStarted(i)) return false;
        if (f.EpicDone is { } done && done != (i.SubIssues > 0 && i.OwnPercent >= 100)) return false;
        if (f.InVersion is { } inVersion && inVersion != i.InVersion) return false;
        if (f.PrMerged is { } merged && merged != board.Merged(i.Number)) return false;
        if (_title is not null && !_title.IsMatch(i.Title)) return false;
        switch (f.Pr)
        {
            case "none" when board.HasPr(i.Number):
            case "open" when pr is null || prState is null || (f.Checks is { } c && !c.Split('|').Contains(prState)):
                return false;
        }
        return true;
    }

    public static bool StatusHolds(string status, string stage)
    {
        var not = status.StartsWith('!');
        var names = (not ? status[1..] : status).Split('|').Select(s => s == "none" ? "-" : s);
        return names.Contains(stage) != not;
    }

    public static bool LabelHolds(string term, IReadOnlySet<string> labels)
    {
        return term.StartsWith('!') ? !labels.Contains(term[1..]) : term.Split('|').Any(labels.Contains);
    }

    public override string ToString()
    {
        return Name;
    }
}

// What a filter asks of the board beyond the story itself: its PRs, its blockers, its children.
public sealed class StoryLookup(IReadOnlyList<Issue> issues, IReadOnlyList<StoryPr> prs, IReadOnlySet<int>? merged = null, IReadOnlySet<int>? awaitingPin = null)
{
    readonly Dictionary<int, Issue> _byNumber = issues.ToDictionary(i => i.Number);
    readonly HashSet<int> _withPr = prs.Select(p => p.Story).ToHashSet();

    public bool HasPr(int story)
    {
        return _withPr.Contains(story);
    }

    public bool Merged(int story)
    {
        return merged?.Contains(story) ?? false;
    }

    // A blocker unblocks at Merged, not at its close: the code a dependent needs is on main. One
    // merged in a pinned repository is there only once the pin holds it (BoardSnapshot.AwaitingPin).
    public bool Unblocked(Issue i)
    {
        return i.OpenBlockers.All(b => _byNumber.TryGetValue(b, out var blocker) && Stages.Landed.Contains(blocker.Stage) && awaitingPin?.Contains(b) != true);
    }

    public bool EpicStarted(Issue epic)
    {
        return epic.OwnPercent > 0
            || _byNumber.Values.Any(c => c.Parent == epic.Number && c.Stage is Stages.InProgress or Stages.Merged or Stages.Released or Stages.Qa);
    }
}

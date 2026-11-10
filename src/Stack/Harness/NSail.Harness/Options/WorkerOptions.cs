// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A worker: one kind of work the harness takes, by its Type (Analyze, Develop, Review, Verify, Test,
// Release, Watch, UpdateBoard), over the stories its Filter matches, when its When holds. Workers live in a
// dictionary by name, so a machine's appsettings.Local.json changes one by its name; Priority orders
// them (lower first), because a configuration's keys come back sorted, not in the order written.
// What a Type does by default — its filter, its conditions, its events — is the type's own
// (WorkerTypes): a worker only says what it changes.
public sealed class WorkerOptions
{
    public string Type { get; set; } = "";
    public int Priority { get; set; }

    // Off without deleting it: a machine turns a worker of the shared file off in its Local.
    public bool Enabled { get; set; } = true;

    // The seat (Harness:Seats) an agent worker runs in; none for a worker with no agent.
    public string? Seat { get; set; }

    // Added after the seat's own prompt: the worker's narrower words.
    public string? Prompt { get; set; }
    public string? PromptFile { get; set; }

    // Verify: what it verifies — `pr` (a story PR's head merged with main) or `main` (main's head).
    public string? Target { get; set; }

    // A command (Verify, Release, Test): a process or a script by its extension, its arguments.
    public string? Command { get; set; }
    public List<string> Args { get; set; } = [];

    // Release: the product it cuts (the command's argument, `{product}` in texts), the workflow its
    // green presses on the code host, and the default repository's file listing the paths whose
    // change makes the product worth cutting.
    public string? Product { get; set; }
    public string? Workflow { get; set; }
    public string? Paths { get; set; }

    // A Release's browser suite, run by the repository's builder: a test project, `{Product}` the
    // product's name. A Command runs instead when the worker names one.
    public string? Suite { get; set; }

    // The MCP servers its agent runs with, by name, and the tools it may call beyond its access
    // (a Test's browser, a repository's own script).
    public Dictionary<string, CommandOptions> Mcp { get; set; } = [];
    public List<string> Tools { get; set; } = [];

    // A Test's story waiting on a person is walked again when someone wrote on it since its last
    // walk, or after these minutes (an answer on the tenant leaves no comment); none, six hours.
    public int? Recheck { get; set; }

    // Watch: the log source it reads (a provider's Name), every how many minutes (none, an hour),
    // how many hours its first pass reads back (none, a day), the most stories one pass opens (none,
    // five), the area label a service the source names takes when no product (Harness:Products) has
    // that name, and the suffixes of a cell's name that are not production (none: test, qa, canary).
    public string? Source { get; set; }
    public int? Every { get; set; }
    public int? Lookback { get; set; }
    public int? MaxStories { get; set; }
    public Dictionary<string, string> Areas { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string>? NotProd { get; set; }

    // Watch: minutes the window's end trails now, for a line the source ingests late (none, five).
    public int? IngestLag { get; set; }

    // The job is cut at this age; none takes the type's.
    public int? Minutes { get; set; }

    // The most jobs of this worker at once on this machine, under the machine's own Capacity.
    public int? Max { get; set; }

    // Added to the lane's variables for this worker's jobs.
    public Dictionary<string, string> Variables { get; set; } = [];

    // Which stories it takes; unset fields keep the type's filter.
    public FilterOptions Filter { get; set; } = new();

    // When it may start; every condition set must hold. Unset keeps the type's.
    public WhenOptions When { get; set; } = new();

    // What happens on the board after each event of the job (WorkerTypes lists each type's events);
    // an event named here replaces the type's actions for it.
    public Dictionary<string, List<ActionOptions>> On { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// Which stories a worker takes: every field set must hold. Lists of names take `a|b` for any of them;
// a label entry takes `!x` for "must not carry x".
public sealed class FilterOptions
{
    // The story's column: `Ready`, `Ready|In-Progress`, `!Closed`; `none` is a story with no column yet.
    public string? Status { get; set; }

    // Each entry must hold: `x` carries it, `!x` does not, `a|b` carries one of them. Set, it
    // replaces the type's list whole.
    public List<string>? Labels { get; set; }

    // `none`: no open PR; a keeper's over a story In-Progress also waits for no job on any harness and
    // the story quiet past PlannerOptions.OrphanGrace, and its move reads the PRs again before it is
    // written. `open`: an open PR, and the worker acts on it. `any`: either.
    public string? Pr { get; set; }

    // The open PR's state at its head, any of: ungated (no verdict of the harness's own Verify),
    // pending, red, conflict, unread (green, no reviewer's word), read (green, a reviewer's word),
    // elsewhere (green in another repository, whose own review reads it; unread past
    // PlannerOptions.UnreadElsewhere, a plan's note).
    public string? Checks { get; set; }

    // `0`: no sub-issues (a story). `>0`: an epic.
    public string? SubIssues { get; set; }

    // Blockers linked to it, closed ones too: `0` or `>0`.
    public string? Blockers { get; set; }

    // Every open blocker linked to it has landed (true too with none linked).
    public bool? Unblocked { get; set; }

    // An epic under way: some of it done, or a child In-Progress or past it.
    public bool? EpicStarted { get; set; }

    // The board names the version it shipped in (on GitHub, a milestone).
    public bool? InVersion { get; set; }

    // A PR of it has merged. Read for the stories In-Progress with no open PR (the ones it decides:
    // a story left there by a job that died), false for every other.
    public bool? PrMerged { get; set; }

    // The story's title, as a regular expression.
    public string? Title { get; set; }
}

// When a worker may start: every condition set must hold; Use brings named ones (Harness:Conditions).
public sealed class WhenOptions
{
    public List<string> Use { get; set; } = [];
    public ScheduleCondition? Schedule { get; set; }
    public LoadCondition? Cpu { get; set; }
    public LoadCondition? Memory { get; set; }
    public UsageCondition? Usage { get; set; }

    // `host:port` endpoints that must answer (a test database a build needs).
    public List<string> Reachable { get; set; } = [];

    public bool IsEmpty => Use.Count == 0 && Schedule is null && Cpu is null && Memory is null && Usage is null && Reachable.Count == 0;
}

// One action on the board after an event: exactly one of its fields set. Labels are one name or
// several, comma-separated (`"p1,triaged"`): a configuration value is a string.
public sealed class ActionOptions
{
    public string? MoveTo { get; set; }
    public string? AddLabel { get; set; }
    public string? RemoveLabel { get; set; }
    public string? Comment { get; set; }
    public bool? Close { get; set; }

    public int Count => new object?[] { MoveTo, AddLabel, RemoveLabel, Comment, Close }.Count(v => v is not null);

    public static IReadOnlyList<string> Names(string labels)
    {
        return labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public override string ToString()
    {
        return MoveTo is not null ? $"MoveTo {MoveTo}"
            : AddLabel is not null ? $"AddLabel {AddLabel}"
            : RemoveLabel is not null ? $"RemoveLabel {RemoveLabel}"
            : Comment is not null ? "Comment"
            : Close is not null ? "Close"
            : "(empty)";
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// Everything a project sets about itself lives here, bound from the `Harness` section: the defaults
// are neutral, and a project's board, repositories, workers, seats, variables and texts come from its configuration.
public sealed class HarnessOptions
{
    public const string Section = "Harness";

    public HarnessMode Mode { get; set; } = HarnessMode.Debug;
    public string Machine { get; set; } = Environment.MachineName.ToLowerInvariant();

    // Where the stories live: one board per harness. Two boards are two harnesses.
    public BoardOptions Board { get; set; } = new();

    // Where the code lives. The one without a Label is the default: a story is built there unless its
    // only area label routes it elsewhere, and its main is what the gate judges and a release cuts.
    public List<RepositoryOptions> Repositories { get; set; } = [];

    // The workers, by name (WorkerOptions): what the harness takes, in their Priority. A machine's
    // Local changes or disables one by its name.
    public Dictionary<string, WorkerOptions> Workers { get; set; } = [];

    // The seats an agent worker names: how it signs, its model, its prompt.
    public Dictionary<string, AgentOptions> Seats { get; set; } = [];

    // Conditions a worker's When brings by name (`"Use": ["cpu-free"]`).
    public Dictionary<string, WhenOptions> Conditions { get; set; } = [];

    // Variables every lane sees beside the harness's own; a worker adds its own (WorkerOptions.Variables).
    // A value is literal or a reference: `${vars:NAME}` (the default repository's variable),
    // `${env:NAME}` (this process's environment), `${file:PATH}` (a file's text).
    public Dictionary<string, string> Variables { get; set; } = [];

    // Run in a fresh checkout before a seat starts there (a doctrine checkout, a tool restore).
    public List<CommandOptions> Prepare { get; set; } = [];

    // What this machine runs at once, per resource; a worker's Max caps its own jobs below them.
    public int Agents { get; set; } = 5;
    public int Builds { get; set; } = 3;
    public int Browsers { get; set; } = 1;

    // The slots a workspace keeps: every lane that may run at once.
    public int MostLanes => Agents + Builds;

    // The MSBuild nodes (and test hosts at once) one build may start: the turns bound how many
    // builds run, this how wide each one runs.
    public int BuildNodes { get; set; } = 4;

    // With every lane idle and nothing to take, the board is read again this often;
    // with lanes busy, a finished lane is what wakes the next read, and this is the floor.
    public int IdlePollSeconds { get; set; } = 30;
    public int BusyFloorSeconds { get; set; } = 300;

    // Whether new stories leave the queue (p1, epic, rest); false only finishes what is started.
    public bool TakeNew { get; set; } = true;

    // Stories under way at once before a new one starts (Planner's WipLimit); unset is the agent count, 0 none.
    public int? WipLimit { get; set; }

    // A story In-Progress a keeper with no open PR (`Pr: none`) would move must have been quiet this
    // long (Planner's OrphanGrace): a job's ending and its PR's opening land inside it.
    public int OrphanGraceMinutes { get; set; } = 10;

    // Drivers a story's label may ask for (`{driver}` or `{driver}:{model}`) that this machine cannot run: they hold it.
    public List<string> UnrunnableDrivers { get; set; } = [];

    // Slots, logs and state live here; a fast disk, excluded from antivirus scanning.
    public string WorkRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "harness");

    // Days a log under WorkRoot is kept, a job's or the harness's own.
    public int LogDays { get; set; } = 14;

    // The interpreters a command's file runs under, by its extension (.sh, .ps1).
    public string Bash { get; set; } = OperatingSystem.IsWindows() ? @"C:\Program Files\Git\bin\bash.exe" : "bash";
    public string PowerShell { get; set; } = "pwsh";

    // The identity of the commits the harness itself makes (the gate's merge with main).
    public string CommitName { get; set; } = "Harness";
    public string CommitEmail { get; set; } = "harness@localhost";

    // A Linux account a step that runs a PR's or a branch's code (a Gate, main Gate or Release step)
    // starts as, through `sudo -n -H -u`, so that code reads none of the harness user's files or
    // environment; unset, it runs as the harness user. The Harness README says what the account needs.
    public string? RunCodeAs { get; set; }

    // Where a lane reaches this harness (its build-turn door); its own listen address by default.
    public string PublicUrl { get; set; } = "http://127.0.0.1:6190";

    // The products a QA walk is walked on (ProductOptions).
    public Dictionary<string, ProductOptions> Products { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // The labels a story the harness files for a red main or a red suite is born with (the planner
    // already puts it first by its title). Lists start empty: configuration appends to a default.
    public List<string> RedLabels { get; set; } = [];

    // Paths a builder's commits may not touch: CI runs them with the repository's secrets before any
    // review. Globs (`*` within a folder, `**` across); AllowProtectedPaths lifts the screen, the
    // Labels.AllowProtected label lifts it for one story and releases the hold the screen set on it.
    public List<string> ProtectedPaths { get; set; } = [".github/workflows/**"];
    public bool AllowProtectedPaths { get; set; }

    // The marker a reviewer's verdict carries on a PR (`<marker>:<clean|findings>:<sha>`). Another
    // repository's own reviewer writes the same one.
    public string ReviewMarker { get; set; } = "harness:review";

    // Who may drive this harness (ControlOptions).
    public ControlOptions Control { get; set; } = new();

    // The logins whose gate and review markers on a PR count beside this harness's own: every
    // harness of the fleet, so one reads another's verdicts. The same list on every harness.
    public List<string> MarkerAuthors { get; set; } = [];

    // The logins whose comments reach a seat as the crew's words, beside this harness's own and the
    // MarkerAuthors: the owners and the crew. Anyone else's text on a story or a PR is laid in the
    // dossier fenced and labelled untrusted.
    public List<string> TrustedAuthors { get; set; } = [];

    // The Ops this Harness reports to (OpsOptions).
    public OpsOptions Ops { get; set; } = new();

    public HarnessTexts Texts { get; set; } = new();

    public RepositoryOptions DefaultRepository => Repositories.Count(r => r.Label is null) == 1 ? Repositories.Single(r => r.Label is null)
        : throw new InvalidOperationException("Harness:Repositories needs exactly one repository without a Label (the default)");

    // Whether a story PR is judged by the harness's own build (a Verify worker) instead of its
    // code host's CI.
    public bool Gate => Workers.Values.Any(w => w.Enabled && string.Equals(w.Type, "Verify", StringComparison.OrdinalIgnoreCase) && !string.Equals(w.Target, "main", StringComparison.OrdinalIgnoreCase));

    // The release workers, one per product.
    public IEnumerable<WorkerOptions> Releases => Workers.Values.Where(w => w.Enabled && string.Equals(w.Type, "Release", StringComparison.OrdinalIgnoreCase));

    // How a story is named outside its board; the default repository's name unless the board names one.
    public string Key => string.IsNullOrWhiteSpace(Board.Key) ? DefaultRepository.Name.Split('/').Last() : Board.Key;

    public StoryNames Names()
    {
        return new StoryNames(Key, Repositories.Select(r => r.BranchPrefix).Distinct().ToList());
    }

    // A configuration that cannot run fails the start, not the first job.
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Board.Provider)) throw new InvalidOperationException("Harness:Board needs a Provider");
        _ = DefaultRepository;
        foreach (var r in Repositories.Where(r => string.IsNullOrWhiteSpace(r.Name)))
        {
            throw new InvalidOperationException("Harness:Repositories has an entry without a Name");
        }
        if (Workers.Count == 0) throw new InvalidOperationException("Harness:Workers names no worker: the harness would take nothing");
        if (Control.ResolveToken() is { } control && control == Control.ResolveLeaseToken())
        {
            throw new InvalidOperationException("Harness:Control:Token and LeaseToken are the same: a seat holds the lease token, so it would hold the control one");
        }
        _ = Harness.Workers.Worker.Compile(this);
    }
}

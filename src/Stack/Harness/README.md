# NSail Harness

A local dispatcher for an AI crew: it reads a board (stories, PRs, CI, what is in flight), plans
the next work in one fixed order — finishing before starting — and starts what fits on this
machine: an analyst shapes a story, a developer builds it to a PR, the harness's own gate builds
and tests the PR, a reviewer reads it cold and lands it, a release cuts what landed — on a
release branch: a red suite files a story the crew fixes on main, and its fix is cherry-picked
onto the branch, where only what failed runs again before the cut; once the cut is published, its
stories go to Released — and a QA walker checks it live. The seats hold no token: the harness hands each one its story on disk and acts on
the files it leaves.

| Project | |
|---|---|
| `NSail.Harness.Sdk` | the API's contract: its messages (`Api/`, NSail Stack messaging with `[Http]` routes) and models, and a generated typed client (`AddHarnessHttpClients`) for whoever drives the harness from elsewhere |
| `NSail.Harness` | the core: planner, loop, jobs, the API's handlers, and the seams every outside system plugs into — `IBoard`, `IRepository`, `IStoryLock`, `IAgent`, `ILaneEnvironment`, `ILogSource` |
| `NSail.Harness.GitHub` | the `GitHub` provider: a board (issues, each in a column of a project), the code host and the story lock (`AddGitHub`) |
| `NSail.Harness.Claude` | Claude Code as the seats' agent (`AddClaude`) |
| `NSail.Harness.OpenObserve` | OpenObserve as the `OpenObserve` log source a Watch worker reads (`AddOpenObserve`; `OpenObserve:Endpoint`, `Organization`, `Stream`, and a reader's `Username`/`Password`, each also as a `...File`) |
| `NSail.Harness.Hosting` | what every host is, added to a plain `WebApplication`: `builder.AddHarness()` and `app.MapHarness()` (service or daemon, machine-local settings, the log file, the API and its reference, the status page) and the build-turn shim |

- **The order** is the workers' (`Workers/WorkerPlanner.cs`, one pure function) with its tests over
  hand-written boards (`test/Stack/Harness`), planned with a whole crew's sample configuration
  (`harness-settings.json` there). A rule changes there and in
  its test together.
- **Everything a project names lives in configuration** (`Harness` section, `HarnessOptions`); the
  code's defaults are neutral, a crew's values are its host's `appsettings.json`:
  - `Board` and `Repositories`, each naming its `Provider`. One harness runs one board; two boards
    are two harnesses (their limits split by hand on a shared machine). The repository
    without a `Label` is the default; a story whose only area label is another's is built there.
    A story with that label beside other areas has two halves: it builds in the labelled repository
    until a PR of it merged there, then in the default one.
    A board that names its columns or labels otherwise maps them (`Stages`, `Labels`); a repository
    says its `DefaultBranch`, and its `Build` (`Kind` DotNet: the solution, the `Roots` whose changes
    seed projects, the `Everything` files that build it all, the `Tests` and their `TestFilter`): the
    gate builds and tests what a change affects through the solution's ProjectReference graph (a PR
    red only where main's own gate is red waits for main to move: no round owes it), and a
    Release's `Suite` (a test project, `{Product}` its name) runs there — no script. Another repository
    says its `Pin` when the default one builds against a version of it (a file, a pattern, the ref):
    a release holds that repository's merges when the version its tag pins does.
  - `Workers` (by name, ordered by `Priority`): everything the harness takes.
    Each has a `Type` (Analyze, Develop, Review, Verify, Test, Release, Watch, UpdateBoard) whose defaults
    (`Workers/WorkerTypes.cs`: filter, events, what each event does on the board, minutes) it changes only
    where it says: `Filter` (status, labels `x`/`!x`/`a|b`, PR and its checks, sub-issues,
    blockers, title), `When` (schedule, CPU, memory, quota, reachable endpoints, named
    `Conditions`), `Max`, `Seat` (from `Seats`), a prompt layered after the seat's, `Command`,
    `Minutes`, `Variables` (a Release also its `Product`, `Workflow` and `Paths`, and a
    `When.Schedule` with no End: its day's cut fires there), and `On` — per event, a list of actions (`MoveTo`, `AddLabel`,
    `RemoveLabel`, `Comment` — `{report}` quotes what the job wrote —, `Close`) that replaces the type's; a
    comment signs as the worker's `Seat`. A Release also walks each release of its product once
    published: every story its commits name ships in its version (a milestone), every Merged story
    whose PRs the tag holds moves to Released, and the closed version says it was walked. A tag never
    published is no release, so a cut that died never reads as the last one. A Test walks one story with its seat in a fresh browser
    (its `Mcp` servers; `Tools` adds what the walker may call): the harness lays down the story, its
    PRs and what QA already asked, and which build each of `Products` runs (the newest `Live.Tag`, or
    a tenant's `Live.Header`) — a story its tenant does not run yet is not walked, and is asked again
    after `QaJob.NotLiveRecheck` rather than at the next cut, because a deploy lands after its cut — and attaches
    the walk's shots to the board (on GitHub, the attachments a pasted file becomes). A story waiting
    A Watch reads its `Source` (a log source by name) every `Every` minutes, from where it last read
    (`Lookback` hours the first time), groups the error lines by service, logger, message template and
    exception type, and files a story per new group in Analyze (`telemetry`, the service's area — a
    product of that name, or its `Areas` entry — and `bug` when a cell outside `NotProd` logged it), at
    most `MaxStories` a pass; an open one gets the count, once a day; one closed and logged again after
    is filed anew naming it (it broke twice); one closed as not planned mutes its error.
    on its ask is asked once, and walked again only when someone wrote on it or after `Recheck` minutes. The walker writes
    `VERDICT: ok|n/a|bug|asked|waiting|not-live|doubt` and its report: the verdict is the event, and
    the board is the harness's. Every verdict leaves the story where a walk reads it again: Released,
    or QA wearing `qa-asked` (a doubt is an ask) — a story in QA with neither is a planner note. A seat's `PromptFile` beside the harness's configuration (`prompts/`)
    is the harness's, for every repository; any other is the repository's. A pass: the UpdateBoard keepers,
    every story worker over the `bug` stories (broken where a customer runs: before a release too, and
    past the WIP limit), the workers on their own, then every story worker over the p1 stories, then
    over the rest; one job per story per pass. A story whose body or refinery comment names a blocker
    in words (`blocked by #N`, `waits on #N`, a `Waits:` line) gets the board's link and `blocked` at
    its analysis and before its first voyage (`Board/WrittenBlockers.cs`); a `Waits` line that answers
    `nothing`, `none`, `nada`, `ninguna` or `ninguno` names no blocker, whatever else it mentions. A
    machine's Local changes or disables (`Enabled: false`) one by name.
    A reading not taken yet (no CPU samples, no quota read since its reset) stops no worker.
  - `Seats` (signature, model, `Prompt` or `PromptFile`) and `Conditions` (named Whens: a crew's
    `db-up` the test database answering, `quota-5h`/`quota-week` the agent's quota walls).
  - `Variables` for every lane, and a worker's own: literal, or `${vars:NAME}`, `${env:NAME}`, `${file:PATH}`.
  - What a process the harness starts sees: the machine's own names (PATH, home, temp, locale; `Shell.Inherited`) plus
    the lane's `Variables` (a proxy or CA name — `HTTPS_PROXY`, `SSL_CERT_FILE` — is declared there too) — never the harness's credentials. A Gate (PR or main) or Release step, which runs a PR's or a branch's
    code, gets `GH_TOKEN` only where its `Variables` list it; any other lane step keeps it. A seat gets none. The
    harness's own git authenticates through `Shell.GitEnvironment`, not the process environment.
  - How a story is named (`{key}#{n}`, `{prefix}/{key}-{n}`) and the words the harness posts (`HarnessTexts`).
  - A provider's own settings sit in its section (`GitHub`, `Claude`: the seats' plugins live there, and
    `SettingsFile`, the Claude Code settings every seat runs with, whatever the repository).
- **Capacity is per resource, per machine**: agents, builds, browsers (`Agents`, `Builds`,
  `Browsers`). The plan is walked in order and every item whose resources are free is taken; a
  worker's `Max` caps its own jobs. Past the agent's quota wall (a refused run) only the agents stop.
- **No state of its own.** A verdict lives on what it judged: the gate, a product's suite, the day's
  cut and the QA drain are commit statuses on main (`harness/gate`, `harness/suite/{product}`, ...);
  a dead voyage, an analysis without a verdict and a broken job are comments with a marker on the
  story. Every harness on the board reads the same. What stays on the machine is its job history
  and its count of them.
- **The count** (`Lanes/JobStats.cs`, `stats.json` beside the history): each job that ends adds to its
  role's tally in its hour (kept two days) and its day (ninety, on the machine's clock), its time and
  its wait in a histogram, so the status page and `GET /api/harness/stats?range=24h|7d|30d` read
  small sums and never walk the jobs. A job that found nothing to do or met the quota wall is not
  counted; a PR the reviewer lands closes its story's lead time (first job to merged).
- **Locks** are the board's labels plus a story lock (a git ref per story on GitHub), created
  atomically, so any number of harnesses can share one board.
- **A job's processes die with the harness** (`Lanes/ProcessGroup.cs`): a restart never leaves a
  build writing in a slot the next job is handed.
- **Modes**: `Debug` reads and plans and starts nothing; `Live` acts.

## A host

A host is a plain `WebApplication` that picks its providers and carries its configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddHarness();
builder.Services.AddGitHub(builder.Configuration);
builder.Services.AddClaude(builder.Configuration);
builder.Services.AddOpenObserve(builder.Configuration);
var app = builder.Build();
app.MapHarness();
app.Run();
```

It listens on `127.0.0.1:6190` with no authentication, so it never binds beyond the loopback. The
GitHub token is `GitHub:Token`, `GitHub:TokenFile` or `GH_TOKEN`, the workstation's `gh` login
otherwise. Machine-local settings go in `appsettings.Local.json` beside the binary (ignored); it
is read last, so it outranks the command line.

The API is the Sdk's messages, under `/api/harness/` — browsable at `/scalar`, described at
`/openapi/v1.json`: the status and last pass, the jobs and build turns, pause/resume/wake,
new stories on or off, a job sent by hand (`POST runs {"kind":"Dev","story":N}`), the build
turns the lanes' shim takes and gives back, and the crew's count (`stats?range=`). For a browser:
`GET /` is the status page (`?range=24h|7d|30d`: the crew's jobs by role as plain SVG, their
outcomes and times, the agent's quota and the rework, then what runs, ten rows of the plan and of
what it holds, and the last ten jobs; a list that can grow shows a few rows and a link to its own
page), `GET /plan` the whole plan and every held story (searched, filtered by tier, 25 a page each),
`GET /jobs` every job this machine remembers (searched, filtered by role, result and story, 25 a page),
`GET /jobs/{id}` one job and its log, and `GET /jobs/{id}/log?tail=` its log as plain text.

A running job (`RunningJob`, in the status, the jobs and the report to Ops) says what it is doing
and when its log last moved, read off the log at each look (`JobActivity`): an agent's job (dev,
analyze, review, QA by seat or by script) its last words — the last text it said itself in its
stream, never a command or a tool's input or output, its first line cut to `JobActivity.LineCap`
with «…» — and a gate its step (`RunningJob.WaitingForTurn`, `Building`, `Testing`). `Doing` is the
agent's own word, not a verified state. `LastWrote` is an instant, not an age, beside
`SilenceLimit`, the limit the job already runs under: an agent's longest foreground command
(`IAgent.LongestCommand`), a QA script's step minutes (the script runs outside that ceiling), a
gate's build timeout once it holds its turn, any other job's step minutes; a gate waiting for its
turn has none and is never stale. A reader measures `StaleAt` against its own moment. A log that
cannot be read leaves `Doing` and `LastWrote` empty; the Running table draws a dash.

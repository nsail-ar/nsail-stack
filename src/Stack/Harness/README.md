# NSail Harness

A local dispatcher for an AI crew: it reads a board (stories, PRs, CI, what is in flight), plans
the next work in one fixed order — finishing before starting — and starts what fits on this
machine: an analyst shapes a story, a developer builds it to a PR, the harness's own gate builds
and tests the PR, a reviewer reads it cold and lands it, a release cuts what landed — on a
release branch: a red suite files a story the crew fixes on main, and its fix is cherry-picked
onto the branch, where only what failed runs again before the cut; once the cut is published, its
stories go to Released — and a QA walker checks it live. The seats hold no board or code-host token: the harness hands each one its story on disk and acts on
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
    `Authors` lists the logins whose PRs are the crew's there (none: any author; when listed, it
    includes the login of the token that opens the harness's own PRs). A PR whose head lives in
    another repository (a fork) is never matched, gated, reviewed or merged.
    `Board:RetryDelay` (default one second) spaces the three attempts at a column write, which together end within a fixed ceiling (a wait the provider asks inside it counts): when they
    all fail, the project item that call added is removed, so no story reads as columnless.
    A story the harness files is born with its labels and found again by its title among the open
    issues (the list API, never the search index), so labels stay free to edit.
    A board that names its columns or labels otherwise maps them (`Stages`, `Labels`); a repository
    says its `DefaultBranch`, and its `Build` (`Kind` DotNet: the solution, the `Roots` whose changes
    seed projects (a non-Markdown change outside them builds it all), the `Everything` files that build it all (by default solutions, `global.json`, props, targets, `nuget.config`, `.editorconfig`, `Directory.Build.rsp`, `*.globalconfig`), the `Tests` and their `TestFilter`): the
    gate builds and tests what a change affects through the solution's ProjectReference graph (the
    `Solution` itself, when its change only adds projects — their entries, configurations, nesting and new solution
    folders, `DotNetBuilder.AddedOnly` — seeds those projects instead of building it all, so a new kit builds itself and
    what references it; a PR
    red only where main's own gate is red waits for main to move: no round owes it; a head carries one verdict, whichever
    harness gates it: a gate re-reads the PR's thread when it starts and again before it writes, and when another harness
    judged the head meanwhile (green or red; waiting on main is none) it stands down with no comment, no status and no
    event, a line in its log alone, and a green at a head outlasts any later red there (`Markers.GateAt`); a gate that broke on infrastructure — git, the provider, the disk — writes
    no verdict and no status, only an unmarked comment, and the head is gated again after the backoff,
    and so does the first gate at a head that runs out its clock while the machine is saturated (its
    comment carries `harness:gate:saturated:<sha>`, which is no verdict); any other run that passed its `Minutes` with
    nothing failed is red by time alone: its verdict names what was green (the build, each test project that passed)
    and fires the Verify event `OutOfTime`, whose default adds `hand` instead of sending a round no builder can do; a malformed project or a
    missing solution in the PR is its red), and a
    Release's `Suite` (a test project, `{Product}` its name) runs there — no script. Another repository
    says its `Pin` when the default one builds against a version of it (a file, a pattern, the ref):
    a release holds that repository's merges when the version its tag pins does, and a story merged
    there has landed on the default branch at the oldest commit whose pin names a version holding
    that merge (`RepositorySet.LandingOf`): until then a red fix is not cut, QA does not walk it,
    main's gate has not judged it. A story it blocks is held only until a release of that repository
    holds the merge (the newest tag named like the pin's ref, `SnapshotReader.ReadAwaitingRelease`),
    not until the pin does: the dependent is often the adaptation the pin cannot move without, so its
    PR moves the pin.
    A story's merged PRs are read off the repository's own list of merged PRs, whole once and then
    only what changed since (never the search index, whose budget every job on the token shares).
    A PR no gate judges (another repository's, or any with no Verify worker) whose red CI failed only
    tests or build errors that main's newest completed CI run failed too waits for main the same way,
    read again each pass; nothing re-runs it.
  - `ProtectedPaths` (default `.github/workflows/**`): a builder whose commits touch one is not pushed,
    since CI would run the changed workflow with the repository's secrets before review. A file counts when the voyage's
    commits touch it and it differs from the default branch since their merge base, as a PR's files do: one merged in
    from main as main holds it runs nothing main does not. Both ends are out of the seat's reach: the voyage's start is
    the commit resolved at checkout, main is read from the remote, never from a ref the seat can move. `AllowProtectedPaths`
    lifts it for the harness, the `allow-protected` label for one story. A refused story waits where it stands wearing `ruling` and
    `screened` and is not built again; its commits and its ending stay on the machine that built it (a ref and a folder under
    `harness-kept` in its git directory), never on the remote. The refusal is no dead voyage. Adding `allow-protected` releases
    it: the planner lifts the `ruling` the screen set (one from any other question stays), and the next voyage on that machine
    pushes the kept work and opens or updates the PR with its builder's words, with no seat, while the story's remote branch is
    still at the head the work was built from (still absent for a new one) and no PR of the story merged since; otherwise it
    builds as any voyage. An analyst whose story's change must touch a protected path lists it in its `protected.txt` (its prompt's `__PROTECTED__` slot names the
    list): a Ready verdict then waits in Ready under the same hold before any voyage. Extend the list when a workflow runs
    scripts taken from the PR checkout; configuration appends to the default, it does not replace it.
  - Commits a cancelled job or a refused push would lose go to `rescue/<key>-<n>` (a numeric suffix if it exists), under the same
    protected-paths screen (a held rescue logs a warning naming the label that lifts it); the refs accumulate and a hand
    reconciles them.
  - A seat's `close-pr.md` closes only a PR of its own story, and an analyst's `labels.txt` sets only the
    board's `area:` and driver labels and `no-walk`. A `ready`'s `labels.txt` is the story's whole routing: a routing
    label (a repository's `Label`) the story wears and the file does not carry comes off, so `RepositorySet.RoutedBy`
    stops sending its voyages there; a `labels.txt` with no label the board knows removes nothing.
  - An analyst ends a story whose work the default branch already holds with `VERDICT: done`. When no PR of
    the story did that work (another story's merge fixed it), its `fix.txt` names the commits that did, one sha
    per line: the harness checks the default branch holds each and writes them on the story, and from there they
    stand for the story's merge wherever one is read — a release red's fix is carried to the release branch and
    cut, the story ships with the release that holds them. A release red with no merge of its own must name at
    least one; a `done` that names none there, or a commit the default branch does not hold, counts as an
    analysis with no verdict.
  - An analyst ends a story that is several which must land apart (a change in one repository and its use in
    another) with `VERDICT: split`: its `comment.md` says why, and `parts/<n>/` (numbered from 1, between
    `StoryParts.Fewest` and `StoryParts.Most`) each hold a `title.txt`, a `body.md` and, where the part has them, `labels.txt`,
    `comment.md` (its refinery comment) and `protected.txt`. A part names an earlier one as `__PART_<n>__`, which
    becomes that story's reference: the seat never knows a number the board has not given. The harness files the
    parts in order as sub-issues of the story, with its area and priority: a part that waits on another story goes to Backlog
    wearing `blocked` and is analyzed once its blocker has landed, one with its refinery comment goes to Ready (under the
    protected-paths hold, if it lists one), any other to Analyze. The story itself takes the `Split` event (Backlog by
    default) and is never analyzed or built again: it is an epic. A release red and a `stalled` story are never split,
    and a split the harness cannot file whole (a part missing its title or body, one naming itself or a later part)
    counts as an analysis with no verdict. One the board cut short after its first part waits `stalled` and
    `ruling` for a hand, since a second analysis would file the first parts twice.
  - An analyst whose story needs work in another repository first ends it with `VERDICT: elsewhere`. Its
    `repositories.txt` names each repository a label routes to (`Repositories[].Label`, a line `label name`);
    only the analyst, reading the code, knows a part belongs there. `body.md` is the story's own half, its
    `labels.txt` the story's, `comment.md` says why, and `parts/<n>/` (from 1, as a split's) are the other
    repositories' halves, each wearing exactly one of those labels in its `labels.txt` — any other is no
    verdict. The parts are filed beside the story, not under it, with its priority and not its areas, each
    where a split's part goes; the story drops every routing label (its half is the default repository's),
    waits `blocked` on the parts and takes the `Elsewhere` event (Backlog by default), so the keeper that
    lifts `blocked` (`unblock-backlog` in NSail's configuration) sends it back to Analyze once they land —
    a pinned repository's once the pin holds them — and it is analyzed against that tree. A story sends its
    parts once: a second `elsewhere` since a hand last lifted a hold counts as a death. Never on a release
    red or a `stalled` story.
  - An analyst whose story cannot be proven by a walk, and that nothing it could add to the story would make
    walkable, ends it with `VERDICT: unwalkable` and says in `comment.md` what is missing: the story takes
    `unwalkable` where it stands (the `Unwalkable` event) and waits for a hand, as a walk that found the same
    does. Never on a release red.
  - `Workers` (by name, ordered by `Priority`): everything the harness takes.
    Each has a `Type` (Analyze, Develop, Review, Verify, Test, Release, Watch, UpdateBoard) whose defaults
    (`Workers/WorkerTypes.cs`: filter, events, what each event does on the board, minutes) it changes only
    where it says: `Filter` (status, labels `x`/`!x`/`a|b`, PR and its checks, sub-issues,
    blockers, title, an epic started or with every child closed), `When` (schedule, CPU, memory, quota, reachable endpoints, named
    `Conditions`), `Max`, `Seat` (from `Seats`), a prompt layered after the seat's, `Command`,
    `Minutes`, `DailyCap`, `Setup`, `Variables` (a Release also its `Product`, `Workflow` and `Paths`, and a
    `When.Schedule` with no End: its day's cut fires there), and `On` — per event, a list of actions (`MoveTo`, `AddLabel`,
    `RemoveLabel`, `Comment` — `{report}` quotes what the job wrote —, `Close`) that replaces the type's; a
    comment signs as the worker's `Seat`. A story worker's job moves the board only through its
    events, at its end or while it runs (a Develop's `Started`, `Unfinished`, `Stalled`, `Landed`; a
    Review's `MergeRefused`, `Stalled`): an event whose actions name no `MoveTo` leaves the story where
    it stands, so a hand moves it (`"On": { "Ready": [] }`). An ending that moved nothing where its type
    would have leaves the story settled for that worker: not taken again until somebody touches it
    after (a comment, a label, a move — a minute past the ending, the board's clock against the
    machine's), kept across restarts in `state.json`; a board that does not say when a story was
    touched never holds one settled. A Review with `Merge: false` merges nothing: a clean read marks
    the head `harness/review` success (a check a branch rule can require), its word on the PR is
    `approved` — the `Checks` state no reviewer and no round takes — and it ends `Approved`; the story's
    move once a hand merges is a keeper's (`Pr: none`, `PrMerged: true`). A Release also walks each release of its product once
    published: every story its commits name ships in its version (a milestone), every Merged story
    whose PRs the tag holds (or, with no PR of its own, the commits its analysis named) moves to Released — but for another
    product's red-suite story, which ships with its own product's cut — and the closed version says it was walked. A tag never
    published is no release, so a cut that died never reads as the last one. A Test walks one story with its seat in a fresh browser
    (its `Mcp` servers; `Tools` adds what the walker may call): the harness lays down the story, its
    PRs, and which build each of `Products` runs (the newest `Live.Tag`, or
    a tenant's `Live.Header`) — a story its tenant does not run yet is not walked, and is asked again
    after `QaJob.NotLiveRecheck` rather than at the next cut, because a deploy lands after its cut — and attaches
    the walk's shots to the board (on GitHub, the attachments a pasted file becomes). A walk that could
    not be made for a reason outside the story (a doubt: the tenant is down, the tools failed) puts its
    report on the story, moves nothing and is tried once more after `QaJob.DoubtRecheck`; a second doubt
    since a hand last lifted a hold puts `stalled` on the story where it stands, since a wall that did
    not go away by itself is a hand's to read and another walk would only spend a walker on it. Both
    clocks — walked, wait for the next cut; left to be tried again, and until when — are read back from
    the machine's own job record when the harness starts (`QaJob.Remembered`), so a restart walks
    nothing twice.
    A story wearing `no-walk` (the analyst's word that nothing of it shows on a screen; a hand takes
    it off to have it walked) closes as `n/a` with no walker and no browser: its gate and tests are its proof.
    The walker writes `VERDICT: ok|n/a|bug|unwalkable|not-live|doubt` and its report: the verdict is
    the event, and the board is the harness's. An `unwalkable` is a story that cannot be walked as it
    stands (it needs an account, an environment, a person, a payment nobody provided): it stays
    Released wearing `unwalkable` with the report, a hold every worker leaves alone, and it is walked again once a
    hand takes the label off. It is not `ruling`: that is a question for whoever rules, and a story that cannot be
    walked asks nobody one — a hand provides what is missing, proves it by hand or closes it. The QA column is what is being walked: a story enters it when its walk
    starts (after the check that every product it names runs it) and leaves it by its verdict; a walk that gave none (a
    doubt, an unwalkable, a dead walker, a tenant the walker found behind, the quota wall) puts it back in Released, where what waits its
    turn or its cut stands. A story a dead harness left in QA is walked again. A walker that found its tenant behind spent
    a walk, so it is tried again on the doubt's clock, not the check's. A Watch reads its `Source` (a log source by name) every `Every` minutes, from where it
    last read (`Lookback` hours the first time) up to `IngestLag` minutes (default five) short of now, for a
    line the source ingests late. A pass reads at most 5000 lines, oldest first, and the next takes up after the
    last one read. A Watch groups the error lines by service, logger, message
    template and exception type, and files a story per new group in Analyze (`telemetry`, the service's
    area — a product of that name, or its `Areas` entry — and `bug` when a cell outside `NotProd` logged
    it), at most `MaxStories` a pass; an open one gets the count, once a day, with the newest stack
    when its body does not show that one (a stack opens with the cause chain,
    `exception.inner.stacktrace`, ahead of the outer frames, so a cut keeps the cause); one closed
    and logged again after is filed anew naming it (it broke twice); one closed as not planned mutes
    its error. A
    seat's `PromptFile` beside the harness's configuration (`prompts/`) is the harness's, for every
    repository; any other is the repository's. A pass: the UpdateBoard keepers, every story worker over
    the `bug` stories (broken where a customer runs: before a release too, and past the WIP limit), the
    workers on their own, then every story worker over the p1 stories and a red main's or suite's
    (the red first, and past the WIP limit too), then over the rest; at the limit (`WipLimit`, none
    unless a machine sets one: it counts the whole board, not that machine's stories), its note names
    the first story it holds; one job per story per pass. A keeper's (`On.Executed`) `MoveTo` is written
    only while the story's column is still the one the pass read: one a hand set since stays, the pass
    logs the dropped move, and the story gets no other chore that pass. A keeper whose filter asks for
    no open PR (`Pr: none`) reads an absence, so it takes a story In-Progress only when no job on any
    harness of the fleet holds the story's lock and nothing touched it (a comment, a label, a column
    move) for `OrphanGraceMinutes` (default 10, as long as a lock with no job behind it waits): a
    job's ending takes its label off before its PR opens. Right before its move is written, the open
    PRs are read again in every repository; one of the story drops the move and its comment, and the
    pass logs the PR it found. A lock label with no job behind it past `PlannerOptions.StaleLock` comes off
    and the pass logs it, read again right before: one another machine already took off is neither removed nor logged.
    A round of a PR starts only while the PR's head is still the one the pass planned it on, read once the
    story's lock is taken: one that moved since waits for the gate on the new head, its lock released,
    nothing said on the story, and the pass logs it.
    Every other keeper moves on the pass that sees its story. A story worker's `On.Starting` `MoveTo` goes with its job and
    is written as planned. A failed pass logs the call it was making, the provider's status and the
    rate limit it had left (numbers and an enum, so they reach the log store; the provider's body stays in the
    console line), counts `harness.passes{outcome=failed}` with that `status`, and the story filed for
    it names all three. A GraphQL read answered 2xx that failed adds, outside the message template so the
    watch's grouping does not move, `GraphQlFailure` (`NotAnAnswer` or `Errors`) and, for `Errors`, the first
    error's `GraphQlErrorType` (GitHub's `type`; `None` when it sent none, `Unknown` for one the harness does
    not list) and `GraphQlMessage` (`Timeout` for a message GitHub sends when it cut the query on time,
    else `Unrecognized`): enums, as none of GitHub's text reaches the store. A GitHub call refused for
    its rate limit (a 403 or 429 naming `Retry-After`, or `x-ratelimit-reset` with no calls left) waits
    that out once, when it is at most a minute away, and is sent again; the limit is shared by every job and lane on the token, so a second refusal, one that names
    no wait or a farther one, throws. A call answered 401, a write or a mutation too, is sent once more after a
    second: GitHub answers "Bad credentials", now and then, to the token it serves the calls around it with,
    and refuses an unauthenticated request before acting on it. A second 401 throws, its message naming
    where the token was resolved from (`GitHub:Token`, `GitHub:TokenFile` and its path, `GH_TOKEN`, the `gh`
    login), never the token. A read that gets a 5xx is sent again up to twice, and so is a GraphQL
    query answered 2xx with a body that is not a GraphQL answer (empty, not a JSON object, or `errors` that
    are not a list of objects with a `message`) or with `errors` whose first message is one of GitHub's for a
    query it cut on time (its 502 in a 200), or a read whose connection is lost before its answer
    arrives whole; one still failing throws with its status, any other `errors` answer throws at once, and a write or a mutation is never sent again on
    any of those. Any other refusal throws at once, and an upload is sent once whatever it is answered. A limit, on a pass or at a job's end, holds every pass after it for the wait it asked,
    at least `MinLimitBackoff` and up to `MaxLimitBackoff`, and a wake (a job's end, the API) does not end the
    hold; outside one, a wake runs the next pass at once. Every GitHub REST read is sent with the ETag of its last answer: one that has not
    moved comes back 304, which GitHub does not count against the token's rate limit, so a pass over an
    unchanged board bills no REST call (`PassBudgetTests`). A full page kept with no next link is read again
    without its ETag, as items appended behind it leave its answer unchanged. A call nothing answers
    within the client's timeout throws `ProviderTimeoutException`, a `ProviderException` with no status,
    never a cancellation: only the caller's own token is a stop. A call whose connection is lost on its
    last attempt (a write's only one, an upload's) throws `ProviderTransportException`, likewise with no
    status. A pass that fails on either logs the bare `pass failed — nothing started`. A job that ends on
    the provider's limit (a `ProviderException` with its `RetryAfter`), on a 5xx or on a timed-out or
    lost call is not a break: nothing
    is said on its story and nothing counts toward `stalled`; its lock label and lock come off and a Dev
    with no PR goes back to Ready, as a break's do. A limit holds the item for the same wait as the
    passes; a 5xx, a timeout or a lost connection backs the item off as a break. Its run's result is `provider` (`/jobs`,
    the pill, `harness.jobs{outcome=provider}`), its outcome `rate limited — <call>, retry in <wait>`,
    `provider failed — <status> <call>`, `provider failed — timed out <call>` or
    `provider failed — connection lost <call>`. A bare 403 or a 404 is
    an answer, and breaks. A job the host's stop cancels is not a break either: its result is `stopped`
    (`harness.jobs{outcome=stopped}`), nothing is said on its story, and the story goes back as a
    break's does. Nor is a job whose workspace this machine cannot prepare (`Workspace.Prepare`: the
    shared clone's branch sweep or `worktree add`, the slot's checkout, reset or clean, and the reset of
    a job's dossier, out folder or run folder, `reset <folder>`; the mirror's
    clone, config and fetch pause the harness instead, below), as the fault is the machine's and breaks every story its worker takes: nothing is said on
    any story or PR, the story goes back as a break's does, and that worker starts no job on this
    machine for the break's back-off, whatever the story; other workers, and the worker on other
    machines, go on. The machine's log says it once at error level, naming the worker, the machine and
    the step. Its result is `machine` (`harness.jobs{outcome=machine}`), its outcome `machine fault —
    <step>`, and it adds nothing to the crew's job stats. A fetch a job runs inside its own work (the
    tag fetches of a QA walk, a ship or a release) still breaks. Those fetches write the tag store every
    slot shares, as the mirror's fetch does when it follows a new tag, so each waits for the mirror's
    turn (`Workspace.Fetch`): two writers of one tag race for its lock, and git fails the one that finds
    it taken. Repairing the clone is a hand's work
    on the machine. A story whose body or refinery comment names a blocker
    in words (`blocked by #N`, `waits on #N`, a `Waits:` line) gets the board's link and `blocked` at
    its analysis and before its first voyage (`Board/WrittenBlockers.cs`); a `Waits` line that answers
    `nothing`, `none`, `nada`, `ninguna` or `ninguno` names no blocker, whatever else it mentions. A
    blocker merged in a pinned repository holds until the pin holds its merge. A
    machine's Local changes or disables (`Enabled: false`) one by name.
    A worker's `DailyCap` is how many jobs that ran an agent one story takes from this machine in a
    day, counted by kind of work since a hand last lifted a hold (`Labels.Lifts`; `HarnessLoop.Capped`):
    the next is not started, and the story takes `ruling` and one comment (Texts `DailyCap`). A story
    sent back that many times is not converging, and each lap costs an agent; a job that ended before
    its seat (a quiet ask, a tenant not live) counts for nothing. A red's story and a job sent by hand
    are never capped.
    A worker's `Setup` is a command run once at start, before the first pass, in the work root: what
    its jobs need on the machine and no checkout carries (a Test's browser, which the machine's own
    cache cleaning may collect). Workers naming the same command share one run. One that fails or
    outlasts `HarnessLoop.SetupTimeout` logs an error and its workers are planned as workers whose
    `When` does not hold, until the next start; every other worker goes on.
    A reading not taken yet (no CPU samples, no quota read since its reset) stops no worker.
  - `Seats` (signature, model, `Prompt` or `PromptFile`) and `Conditions` (named Whens: a crew's
    `db-up` the test database answering, `quota-5h`/`quota-week` the agent's quota walls).
  - `Variables` for every lane, and a worker's own: literal, or `${vars:NAME}`, `${env:NAME}`, `${file:PATH}`.
  - What a process the harness starts sees: the machine's own names (PATH, home, temp, locale; `Shell.Inherited`) plus
    the lane's `Variables` (a proxy or CA name — `HTTPS_PROXY`, `SSL_CERT_FILE` — is declared there too) — never the harness's credentials. A Gate (PR or main) or Release step, which runs a PR's or a branch's
    code, gets `GH_TOKEN` only where its `Variables` list it; any other lane step keeps it. With `RunCodeAs` set (a Linux
    account), that step — its `Command` or the builder's own `dotnet` — starts as the account through `sudo -n -H -u`,
    so the code reads neither the harness user's files nor the harness process's environment; it needs what `RunAs` below
    needs (the same sudoers shape, `setsid`, `pkill`, and write access to the slots and `gate/` under the work root).
    Unset, the step runs as the harness user. Windows ignores it. `RunCodeAs` and `Claude:RunAs` name the same account,
    or neither names one: the seats and these steps build in the same slots, and a build sets the time of a file only if it
    owns it (MSBuild's `Touch`, error MSB3374), which no ACL grants, so two accounts there fail the start. What the account
    wrote in a slot (`.voyage`, a job's out folder, a walk's run folder, a suite's `bin` and `obj`) the harness removes
    through the account (`Shell.Remove`): a temp folder it made for itself is 0700 and lists for nobody else, and an
    entry it left read-only is made writable to its owner and removed. A seat gets no token. The
    harness's own git authenticates through `Shell.GitEnvironment`, not the process environment. It reads no global git
    configuration and no global attributes file (a seat sharing the harness user writes both) and removes the shared
    repository's `info/attributes` before it checks a slot out: a `safe.directory` the host needs goes in the system
    configuration (`git config --system`).
  - How a story is named (`{key}#{n}`, `{prefix}/{key}-{n}`) and the words the harness posts (`HarnessTexts`).
  - A provider's own settings sit in its section (`GitHub`, `Claude`: the seats' plugins live there, and
    `SettingsFile`, the Claude Code settings every seat runs with, whatever the repository; `RunAs`, a Linux
    account the seats run as through `sudo -n -H -u`, so a seat reads none of the harness user's `~/.ssh`, secrets file or docker
    group. The account owns none of them and each side must write what the other creates, which the Stack does not chmod:
    default ACLs on the work root (slots and the bare `repo.git`) and `configRoot` for both users (`setfacl -d -m u:<account>:rwx`
    and `u:<harness user>:rwx`), or a shared group with setgid folders and umask 002 for both (`umask=0002` in the sudoers rule).
    The harness user's sudoers rule for it carries the `SETENV` tag, `Defaults!<each command> !secure_path` (the seat keeps the
    machine's PATH), and allows `setsid` and `pkill` (a timeout kills the seat's session through sudo),
    and `Claude:Command` is best given as an absolute path. `RunAs` covers the agent's runs and plugin installs, not a
    worker's own `Command` or `Setup` (a step that runs a PR's code has `RunCodeAs`, above): those run as the harness user, so what a `Setup` installs for a seat
    goes where both accounts read it. Windows ignores `RunAs`. A seat's run loads only the built-in tools its access and
    its worker's `Tools` name (`ClaudeAgent.Loaded`): every other one is context paid on each turn and never called.
    An MCP server's tools come with the server.)
- **Capacity is per resource, per machine**: agents, builds, browsers (`Agents`, `Builds`,
  `Browsers`). The plan is walked in order and every item whose resources are free is taken; a
  worker's `Max` caps its own jobs at once, its `DailyCap` one story's in a day. Past the agent's quota wall (a refused run) only the agents stop.
  A release's suite (a cut, a red fix's re-run) holds its build turn alone against the gates
  (`BuildTurn`): both draw on the machine's one Postgres, and a gate's clock starts at its turn. No
  gate is granted while a suite holds or awaits a turn, a suite waits only for the turns gates
  already hold, and a lane's turn is never held back for either.
  A build turn whose holder died returns to the line once older than the longest step the machine's
  enabled workers run (a main gate counts twice its minutes) plus ten.
  A lane waits for a build turn at every `dotnet` that compiles (`build`, `test`, `publish`,
  `restore`, `pack`, `msbuild`, `ef`, `run`, `watch`): the `dotnet` shim first on its PATH asks for
  it, so `Builds` alone bounds the builds on a machine. A `run` or a `watch` builds under the turn and
  gives it back before its app starts; a `dotnet` that compiles nothing goes straight through, and
  one started under a held turn (an E2E fixture publishing its host) rides it. `BuildNodes` (default
  4) is how wide each build runs.
  While the kernel reports the machine saturated (`/proc/pressure` over the last minute: memory
  `full` at or over `Pressure:MemoryFull`, default 10, or CPU `some` at or over `Pressure:CpuSome`,
  default 80) no new job of any kind starts, the jobs running go on, and the plan's notes give the
  reading (`machine saturated (memory full avg60 23.4 ≥ 10) — no new job starts`). A machine with no
  `/proc/pressure` (Windows, a kernel without PSI) holds nothing. `/status` carries the reading
  (`Readings.Pressure`).
- **The release andon** (`Planning/ReleaseAndon.cs`). A product's failed cut leaves its one open red
  story (`Regression red on main ({product})`, Texts `SuiteRedTitle`); while that story is out of
  Released, QA and Closed, the release takes the whole harness. The plan holds only: that story's
  analysis, build, PR rounds, gate, review and merge (and a red main's story, which may be what keeps
  the suite red); main's gate; the product's own cut (the day's, or the red fix's on its release
  branch) and the walk of its published release. No other story, PR, QA walk, Watch or cut starts, a
  `bug` included, and the WIP limit does not hold the release's stories. Jobs already running finish.
  Its items carry the `andon` tier, above `bug`: a customer's bug still waits behind the release
  because the release is the line's one way back to a shipped day. `take-new` off still holds the
  start of a story the andon would take — it is the operator's switch. The status page shows the
  product, since when and what it waits for (`/status` carries it as `Last.Plan.Andon`). A lit andon
  with none of the release's work planned or running waits on something outside the harness (a hand,
  a publication), and holding the line then buys the release nothing: past `HarnessLoop.AndonStill` the
  line runs behind it — every worker is planned again, the release's work still the most urgent turn the
  moment there is any — and it says so once: a warning in the log (it is a call to a person, and nothing of the harness fails), the status page (`AndonStillSince` on
  `/status`) and a comment on the red story (`HarnessTexts.AndonStill`), which is left unsaid while that
  product's cut runs on the code host. Main's gate, the release's while an andon is lit, is no sign that the
  release moves: every merge of the line moves main. The andon stays lit; nothing unsticks it but what it waits for. It lifts when
  the red story ships (its walk moves it to Released, or its product published a release after the fix merged) or a hand closes it; a story a hand holds
  (`ruling`, `hand`, `blocked`, `stalled`, `unwalkable`) keeps it lit and says so. A red story (a suite's or a red
  main's, `HarnessTexts.IsRed`) never asks a hand: a builder's question sends it to Analyze with no
  `ruling` (event `Reanalyze`; with a PR open, the next round decides it), a reviewer's crack goes
  back to its builder as `Findings`, and an analysis that ends `ruling` is refused and counted as a
  death (`RedQuestion`, `RedRulingRefused`), as is one that ends `done` naming no fix the release can carry
  (`DoneNamesNoFix`, `DoneFixNotThere`). A builder's `wall.md` (something outside the story must change first) ends the round as `Walled`, an event configurable under `On` like the others: the story goes to Backlog wearing `blocked`, and a blocker the wall names (`blocked by #N`) is linked so the rule that lifts `blocked` on its blockers lifts it; a wall that names none waits for a hand. A red story's wall goes the way of its question, with no hold. An `elsewhere.md` (the change has to land in another repository first) ends as `Reanalyze`: the story goes to the analyst with no hold, which splits it into that repository's half and blocks the rest, so no hand rules it. With a PR of the story open the next round is planned from the PR, not by the analyst, so an `elsewhere.md` that pushed nothing there counts as a voyage, and a second in a row is `stalled`. A `handoff.md` with work pushed ends as `HandedOff` and the next voyage takes it. One with nothing pushed moved nothing: if it names a blocker it ends as `Walled`, and otherwise it counts as a voyage, so a second in a row goes to the analyst instead of a third builder reading the same note. A `pr.md` with nothing pushed on an open PR counts as one too (`HarnessTexts.NothingPushed`). None of these, nor a voyage that died, counts when it pushed nothing on an open PR whose head on the code host is neither the one the round was planned on nor the one it checked out: another hand moved it, so the round says so with no voyage marker (`HarnessTexts.HeadMoved`, after a hand-off's own words) and the gate judges the new head.
- **No state of its own.** A verdict lives on what it judged: the gate, a product's suite, the day's
  cut and the QA drain are commit statuses on main (`harness/gate`, `harness/suite/{product}`, `harness/cut/{product}`, ...); a cut at work is held by `harness/cut/{product}/held`, renewed while it runs and let go with its verdict (or when it throws), and it lapses when a dead harness stops renewing it (`Markers.ClaimLapse`);
  a dead voyage, an analysis without a verdict and a broken job are comments with a marker on the
  story. A gate or review marker on a PR, and a voyage, analysis-died, broke or QA-bug marker on a story, counts only as a whole HTML comment on its own line, written
  by the login the harness runs as or by one listed in `Harness:MarkerAuthors` (the fleet's logins,
  the same list on every harness, empty by default); the builder's `pr.md` is posted with any marker stripped.
  The dossier a seat reads lays a comment or PR note by any other login fenced and labelled untrusted; the
  logins whose words reach it as the crew's are the harness's own, `Harness:MarkerAuthors` and `Harness:TrustedAuthors`
  (the owners and the crew, empty by default). Under an open PR it says why its head is not green where no
  gate verdict does: the head does not merge with the default branch, or the CI that runs outside the harness is
  red there — its failed jobs and steps and failed tests (else build errors), the first 30 of each, and the last 40
  non-empty lines of the first three failed jobs' logs, each cut where its failed step closes, fenced as data. A Watch story (its `telemetry` label, or the fingerprint its body carries) is laid in the dossier with its title fenced as data and its body under a line naming the log text it quotes as data (its criteria stay the seat's work; the analyst's `original.md` fences whole a body the watch filed unfenced), and the
  watch files it in Analyze, never Ready.
  Every harness on the board reads the same: main's `harness/gate` mark written `pending` holds that head from
  a second gate on any harness for twice the longest `Minutes` among that harness's own Main
  workers plus an hour (a fleet whose harnesses set different `Minutes` holds as long as the
  shortest of them); past that (a harness
  that died leaves it pending) another harness judges the head again. What stays on the machine is its job history
  (`jobs.json`, each job for `JobHistory.Keep`: what a day's cap and a week's reading need), the logs under the work
  root (a job's and the harness's own, swept on the first line of each day once older than `LogDays`),
  its count of them, the work the protected-paths screen refused (`ProtectedPaths`), and what a hand or
  the provider set (`state.json`: the pause, take-new when a hand moved it, an unexpired quota wall),
  which a restart keeps. A state file that is zero-length or torn is
  moved aside as `<name>.bad-<time>` with a log line and the state starts empty; every write goes to a
  temp file moved over the original. At the first board read the log names each In-Progress story no job holds.
- **main's red** files, or comments on, the one open red-main story of its own: titled
  `HarnessTexts.MainRedTitle` then `: `, filed by the harness's login or one of `MarkerAuthors`, and
  not routed by its area labels to another repository. A story merely named like one (a hand's, or
  another repository's) is never commented, moved or closed by the gate; with only such a story open,
  a red files the gate's own in Ready. The merge of its fix on the
  judged head is the only evidence of the fix: a red at a head that does not hold the story's newest
  merge (a gate that started before it) leaves a landed story in its column, and its comment says
  so (`HarnessTexts.MainRedWithoutFix`); while no gate verdict sits on a head that holds it Develop
  does not take it. The first green gate of such a head closes the story: its comment
  (`HarnessTexts.MainRedFixed`), the `Closed` column, the issue closed. A close that breaks partway fails the
  gate's job (`could not close #N`) and the next green gate finishes it, without saying the comment a second time for the same merge; a voyage on a story closed
  under it pushes nothing and says nothing, and keeps the quota wall its seat met.
- **The count** (`Lanes/JobStats.cs`, `stats.json` beside the history): each job that ends adds to its
  role's tally in its hour (kept two days) and its day (ninety, on the machine's clock), its time and
  its wait in a histogram, so the status page and `GET /api/harness/stats?range=24h|7d|30d` read
  small sums and never walk the jobs. A job that found nothing to do, met the quota wall or ended on the
  provider (its limit or a 5xx), or on a failed shared step, is not counted; a PR the reviewer lands closes its story's lead time (first job to merged).
- **Locks** are the board's labels plus a story lock (a git ref per story on GitHub), created
  atomically, so any number of harnesses can share one board.
- **A job's processes die with the harness** (`Lanes/ProcessGroup.cs`): a restart never leaves a
  build writing in a slot the next job is handed. On Windows that is a job object; off Windows
  there is none, and the guarantee is the service unit's `KillMode=control-group` (systemd's
  default), set in the unit file that runs the harness: the unit's stop kills every process in
  its cgroup. A child whose group join fails is killed at once.
- **A process's output** goes to the job log line by line as it comes; the caller gets the last
  `Shell.TailChars` characters, preceded by the cut-off lines a caller's `keep` accepts (the gate's
  failure lines, the agent's quota signals). Its stdin is written inside the same timeout window, so a child
  that never reads it is killed with its tree.
- **What pauses the harness and what stalls a story**: a step every job shares (the mirror's clone, config and
  fetch, `Workspace.PrepareAt`) fails the same for every job, so its failure is the harness's: the job ends
  as a `SharedStepException`, the harness pauses itself with `<step> failed: <error>` as the status's
  reason, and nothing is said on any story and the stats do not count the job. Its run's result is `shared` (`/jobs`, the pill, `harness.jobs{outcome=shared}`);
  its lock label and lock come off and a Dev with no PR goes back to Ready. A job that meets it while the host
  stops ends as `stopped`, and pauses nothing. The pause is no hand's: every pass starts by running the step again and the first
  that succeeds lifts it, with nothing to clean; a hand's pause (`POST pause`) is never lifted that way,
  a restart forgets the pause, and one draining for a restart is not lifted. A restart once asked stands: a resume
  lifts a hand's pause, the shared step's and the quota wall, and the harness still stops when the last job ends. A job's own failure (any other exception) still says `harness:broke`
  on its story and the second in a row stalls it.
- **By hand under the work root** (`Harness:WorkRoot`): anything that touches the mirror or a slot runs as the harness's user,
  never as root: a push from a slot writes the mirror's remote-tracking
  refs and reflogs, and root-owned files there make every fetch fail.
- **Modes**: `Debug` reads and plans and starts nothing; `Live` acts. A forced job (`POST runs`) refuses in Debug, while paused and, for one that needs an agent, inside the quota wall.

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

It listens on `127.0.0.1:6190`. Loopback is no boundary (a seat's process reaches it), so
`Api/ControlGate.cs` guards it. A request whose `Host` is not loopback, the host of `PublicUrl` or one of
`Harness:Control:Hosts` is refused (421; the port is not looked at). The routes that drive the harness (`restart`, `pause`, `resume`,
`wake`, `take-new`, `runs`) and a job's transcript (`/jobs/{id}`, `/jobs/{id}/log`) need
`Harness:Control:Token` or `TokenFile` (as `Authorization: Bearer` or `X-Harness-Token`; a browser signs in once at `/login`,
which keeps it in a cookie; the token never travels in a URL, which the log would record); with none configured they answer 401. A message route
needs it unless its message carries `[HarnessAccess]` (the Sdk marks the build-turn routes `Lease` and the
reads `Open`), and a page needs it only if it is mapped `ControlOnly`. The token and the lease token must
differ (the start fails otherwise). Like `GH_TOKEN`, a token passed as an environment variable is readable
by a seat's process (`/proc/<pid>/environ`): a deployment uses `TokenFile`, which is worth its permissions. The build-turn routes (`POST leases`, `DELETE leases/{id}`) take the lease
token (`Harness:Control:LeaseToken`, random per process when unset), which opens nothing else and is the
only credential a lane's environment holds (`HARNESS_LEASE_TOKEN`, sent by the `dotnet` shim). The status
pages and the `status`, `jobs` and `stats` reads stay open. The
GitHub token is `GitHub:Token`, `GitHub:TokenFile` or `GH_TOKEN`, the workstation's `gh` login
otherwise; a child never inherits it (see above), but a variable
exported to the harness process stays in that process's initial environment, which a PR's code
running as the same user can read (`/proc/<pid>/environ`). `GH_TOKEN` is a workstation convenience;
a deployment passes `GitHub:TokenFile`. A step's `GH_TOKEN` in `Variables` is a switch, not a value: it always
gets the harness's token. Machine-local settings go in `appsettings.Local.json` beside the binary (ignored); it
is read last, so it outranks the command line.

The API is the Sdk's messages, under `/api/harness/` — browsable at `/scalar`, described at
`/openapi/v1.json`: the status and last pass, the jobs and build turns, pause/resume/wake,
new stories on or off (`status` also says what runs: `Version`, the harness's build, and `HostVersion`, its host's), a job sent by hand (`POST runs {"kind":"Dev","story":N}`), the build
turns the lanes' shim takes and gives back, and the crew's count (`stats?range=`). For a browser:
`GET /` is the status page (`?range=24h|7d|30d`: the crew's jobs by role as plain SVG, their
outcomes and times, the agent's quota and the rework, then what runs, ten rows of the plan and of
what it holds, and the last ten jobs; a list that can grow shows a few rows and a link to its own
page), `GET /plan` the whole plan and every held story (searched, filtered by tier, 25 a page each),
`GET /jobs` every job this machine remembers (searched, filtered by role, result and story, 25 a page),
`GET /jobs/{id}` one job and its log, and `GET /jobs/{id}/log?tail=` its log as plain text (both need the control token: a header, or the `/login` cookie).

The report to Ops (`OpsReporter`, `Harness:Ops`: `Url`, `Token` or `TokenFile`, every `Minutes`): a
`HarnessReport` POSTed to `api/ops/node/harness` — the machine, its version, its status and its stats,
and the ended jobs Ops has not accepted yet (`EndedJob`: id, work, story, started, ended, outcome and
result, the outcome cut to `OpsReporter.OutcomeChars`; never the log nor its path; a job is its
machine, id and start). Which ones Ops accepted lives
in `ops.json` beside `jobs.json`, moved only by a 2xx, so a refused post or a restart sends them again
and Ops keeps each once. Never accepted, every ended job the list holds is owed; one trimmed off the
list no longer is. The jobs go oldest first, as many as keep the body under `MaxBytes` (its default in
`OpsOptions`, under Ops' own ceiling, never below `OpsReporter.MinBytes`); the rest go with the next
reports, and a report that owes jobs and fits none costs a warning. The token travels only in the header. Ops
down or refusing costs one warning per report and never touches a pass.

The last pass (`Pass`, in the status and the report to Ops) carries the board it planned from
(`Pass.Board`, a `BoardView`, from the pass's own read, never a second one): the repository, when it
was read, the open stories in each stage of `Stages.Open` (a stage with none reads 0), and the held
ones, an entry per hold — `ruling`, `stalled`, `unwalkable`, or `gate` when the PR's gate is red at its
head. A pass whose read failed carries none, and neither does a status before the first pass.

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

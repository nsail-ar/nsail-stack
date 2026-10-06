# Testing

What a test proves in NSail, and which layer owes it.

---

## The layer map

Each layer proves one kind of truth. A test in the wrong layer proves nothing twice.

| Layer | Proves | Lives in |
|---|---|---|
| `Architecture.Tests` | **form** — doctrine made mechanical: a rule over the whole tree (no `Mud*` in pages, migrations drift, the gate answers first on every endpoint) | `test/Architecture` |
| Stack unit tests | a Stack module's own behavior, offline (localization merge, problem shapes, the JWS codec against a fake JWKS) | `test/Stack/...` |
| Kit handler harnesses | **handler doctrine against real Postgres** — round trips, Problem contracts (AlreadyExists names the typed value, dangling refs answer NotFound), invariants (double entry balances), `@me` denial through the real evaluator | `test/Kits/.../WebApi.Tests` on `NSail.Data.Testing` |
| Shared/bUnit | screen behavior through real DOM events (dirty tracking, validation speaking first, vocabulary gating) | `test/Kits/.../Shared.Tests`, `test/Stack/Components` |
| App tests | **composition seams** — where a product wires kits: contributors, outlets, starter packs, the screens' own message payloads | `test/Apps/...` |
| Browser e2e | **what only a real browser sees** — WASM-vs-server divergence, surface geometry, and the golden paths end to end. A THIN layer on purpose: the bulk of a flow is proven at message level by the scenarios, and a screen's contract by bUnit | `test/Apps/.../E2E` on Playwright |

The kits are covered. **The Apps layer is where defects pool** — a screen sending
`Guid.Empty`, a pack missing a verb — because a handler test cannot see what a screen
sends. When an app story touches a seam, the seam's test rides the story.

**No coverage target, deliberately.** A target breeds filler in the layers already covered;
the question is *which layer owes the proof*, not how much. The honest-count rule lives in
commits.md: the suite total is the tree's, verified against
`find test -name '*.Tests.csproj' | wc -l`, never a number carried from memory.

## Rulings

Numbered; code and docs cite them by number ("testing.md, ruling 8").

1. **This page routes.** What is HERE: which layer owes which proof, the harness lore below,
   and the rulings. What stays in principles.md/CLAUDE.md: that verification is part of the
   work at all.
2. **Repeatable verification lands as a test; prose is for what only the eye judges.**
   A close that says "proven live" for something a harness could replay owes the replay.
   Aesthetic calls, third-party round trips needing real credentials, and one-shot
   migrations stay prose — named honestly as such. A queue issue claiming "unproven"
   names the test that would close it.
3. **Form rules go to `Architecture.Tests`.** A doctrine sentence quantified over the
   tree ("every generated endpoint denies before binding") is an architecture test with an
   explicit, documented exemption list — never a recurring manual audit. The exemption list
   is part of the test, and shrinking it is progress a diff can show.
4. **A documented wire format obliges a test per documented shape.** If a doc shows three
   JSON forms, three parse tests exist; a shape the parser refuses is either a doc lie or
   a missing case, and the test decides which. Doctrine that cannot be executed is prose
   fiction.
5. **Every new Publish/Subscribe is born with its cross-surface pin**: publish from an aside →
   the page underneath re-renders, pinned with a real Mediator over a Main+Aside tree
   (`SubscribeRerenderTests` is the pattern).
6. **Every found bug becomes a scenario before its fix, and every story adds its
   scenario** (bug-becomes-scenario). The scenario catalog (`NSail.Optical.Scenarios.Tests`
   against `src/Apps/Optical/docs/test-scenarios.md`) is the cross-kit net: a bug that
   reached a user proves a flow nobody pinned, so the pin lands first — red — and the fix
   turns it green. A feature story ships its scenario in the same close.
7. **A test per contract, never a test per anecdote.** This bounds ruling 6, it does not
   soften it. A fix earns a test when the defect **exposed a promise nobody was watching** —
   not because somebody erred. The question at every close is *what promise was unwatched?*:
   if the answer names a promise, the pin lands; if it is "we were careless here once", the
   fix ships alone and the story says so. A bug that reached a USER proves a flow nobody
   pinned, and a flow is a promise. When the same shape lands three times in a week, it is a
   class that earns a net, not three regression tests (baseservices.md, "The pipeline answers
   identically in Development and Production"). Suites die of accumulated anecdotes, one
   reasonable test at a time.
8. **An act is waited for before what it rendered is read.** A bUnit test that acts and reads
   on the next line passes when the continuation happens to run first and goes red when it
   does not — on a loaded runner, on a different test each run, bouncing stories that never
   touched it. The defect has two shapes: a `cut.InvokeAsync(…)` whose Task is dropped, and a
   bare `.Click()` / `.Change()` / `.Input()` / `.Submit()` / `.Blur()` / `.KeyDown()` read
   afterwards. **An
   `async Task` signature proves nothing** — the act is what has to be waited for.
   - `await cut.InvokeAsync(() => cut.Find("form").Submit())` is the default: deterministic,
     no polling, and it covers everything the act's own Task carries.
   - `cut.WaitForAssertion(() => …)` is for when the act STARTS work it does not itself
     await — a downstream re-fetch (`NsTable.LoadPage` through MudTable's `ServerData`), so
     awaiting the dispatch is not enough. It carries its reason in the code.
   - **Never both at one site, and never a bare `Task.Delay`.** A site already read through
     a `WaitForAssertion` is cured; adding the await on top is the opposite mistake.
   - The bar is **`AwaitedActTests`**, not a grep and not a count: greps over-report so badly
     (hundreds of act sites, a handful of defects) that judging hits by hand is the recurring
     audit ruling 3 refuses. The test reads statements, so the awaited form spread over three
     lines is not a hit. A site that is neither dispatched nor polled says why with
     `// Unwaited: <reason>`, and **`_ =` is a dropped Task, never a capture** — where the
     act's own Task cannot be awaited at all (`DialogManager.Open` completes when the dialog
     CLOSES), the discard is right and the poll below it stands in for the await.
   - **A `void` handler does not make a bare act safe.** The race is in the dispatch: the
     renderer's dispatcher runs it inline only while it is free, and the sync extensions drop
     the Task, so with the dispatcher busy the act returns having rendered nothing.
   - **The wrap is not armor against a moving handler id.** An act that dies with
     `UnknownEventHandlerIdException` was dispatched at an id the component re-mints every
     render; no shape on the test's side reaches that — the cure is the component's (harness
     lore, per-render delegates).
9. **A ruling lands as a scenario, never as a comment.** A decision Leonardo makes — in
   refinement, in a story's body, in chat — is a promise like any other, and ruling 7's
   question applies the moment it is made. A comment is read only by whoever opens that file
   and defends nothing: `Seed.Vouchers.cs` carried *"C and E carry no VAT to discriminate"*
   three lines above the table implementing it, and a whole story and PR were written against
   it unnoticed. The same sentence as a scenario (Escenario 43) refuses the PR in CI. **The
   comment still belongs there** — it says WHY, which a test cannot — but the scenario holds
   the line.
10. **A money scenario asserts which accounts moved, never the net.** A net nets: a credit in
    the right account and a debit in the wrong one come to the zero the test wanted
    (`Escenario15_OtCanceladaConSenaTests` passed while every refund debited `Proveedores`,
    because `Ledger.PartyBalance` sums across accounts). Name the accounts the act may touch
    and fail on any other.
    **The tool is `Ledger.PartyMovedOnly` / `Ledger.EntryMovedOnly`**
    (`NSail.Optical.Scenarios.Tests/Ledger.cs`): party-anchored for what the scenario's own
    Party touched, entry-anchored for one asiento — including the legs that name nobody,
    which is where a VAT line hides. A scenario that moves money calls one of them; a
    scenario where money is REFUSED calls the party one with an empty list. **The allow-list
    is written from what the narrative says should move, and then run** — an account that
    turns up unexpected is investigated and becomes its own story. Appending it to reach green
    is the rubber stamp this ruling exists against: a suite that asserts whatever the code
    happens to do proves nothing and reads like proof.
11. **A number or a code on a message declares its bound, and the gate refuses the silence.**
    No test finds a rule that does not exist (a sphere of +45, a negative price, a CAE of any
    text all passed), so the answer is not coverage — it is ruling 3's shape over field
    bounds. `DeclaredBoundTests` walks exactly `MessageValidator`'s reach and fails the build
    on a numeric or `string` member that declares nothing (messaging.md has what satisfies it,
    and the identifier escalation). **The exemption list is `[Unbounded("why")]`, per member,
    so it is read where the decision matters** rather than in the test.
    - **The bound is written from what the domain says, then run.** A number picked to reach
      green is ruling 10's rubber stamp in another suit. The tree usually already says it
      somewhere — a column length, a sibling message, a handler's own `if` — and the
      declaration is where that sentence belongs; a rule enforced only in a handler is a rule
      the screen cannot draw and the wire refuses twice.
    - **Where the domain names no bound, the opt-out is the honest answer and its reason IS
      the record.** A reason that says "no bound" says nothing. `uint Version` is the
      exemplar: an opaque concurrency token the provider mints and the caller hands back
      unread, so the only wrong value is a stale one and the save refuses it.
12. **A change to what's true has more than one place agreeing with it — a review names them
    all, not just the one the story's own criteria walk.** Examples of the shape: a status
    guard (`SaleStatus.Cancelled` blocking billing) landed on `SalePage.razor` but not on
    `SalesPage.razor` nor on either `SaleHandler` mutation beside it; a new validator ran
    ahead of an existing guard and silently overrode it for the terminal states that guard
    protected; an E2E fixture stayed built for an arrangement a later story made
    unreachable. None is a coding mistake; each is a place that agreed with the old truth
    and was never asked whether it still does. messaging.md's "a mutation review checks both
    ends of the event" is the pub/sub instance. **When a story changes a rule, a status, an
    approval or a transition: name every surface that reads or assumes it — sibling screens,
    sibling handler branches, a validator running near an existing guard, and any test
    arrangement (E2E included) built against the old shape — before calling the story
    done.**
13. **Every happy path has its E2E and its entry in the manual**: a feature is not done until
    every expected outcome it ships has a test in the product's browser suite and a paragraph
    in the guide. **A happy path is every expected outcome, not the one the story was named
    after** — *cerrar caja* and *cerrar caja con diferencia* are two paths, two tests, two
    paragraphs. **The test's name, the path's name and the `##` title of its guide paragraph
    are the same words**, so "which E2E does this change touch" is a grep and not a
    judgement. The suites are excluded from CI on purpose (`ci.yml`'s
    `FullyQualifiedName!~.E2E.`) and run at the nightly regression and at a release, so a
    path's proof is the builder's own run before the push. This bounds nothing in ruling 7:
    an outcome the product promises is a contract, and the anecdote it refuses is a second
    test of the same outcome. What it costs a story: story-template.md (nsail: `docs/agents/story-template.md`);
    the guide half: [ui/guide.md](ui/guide.md).

## Harness lore (paid for, do not re-buy)

### Every harness

- **A phone number is the one unique value a Guid cannot spell, so it is drawn —
  `Handsets.Draw()` (`test/Shared/Directory/Handsets.cs`), never an expression that writes
  the plan's admitted band itself.** Which digits a numbering plan admits is not derivable
  from their shape: Google's metadata lists ALLOCATED ranges, and behind this region's
  mobile marker the area code 11 refuses a subscriber number that itself starts with a 9.
  The drawer draws and then asks `E164`, repeating until the plan admits it, so an
  arrangement cannot answer 400 `PhoneNumberUnreadable` on one run in nine — in whichever
  path drew unluckily that night. One drawer for every layer that arranges a road: handler
  collections, bUnit screens and the browser suites all link it per project like
  `FakeSmtpTransport`, and `DrawnHandsetTests` holds it. A fixed literal written once and
  read in the diff is fine, a generated one is the drawer's.

### Handler harnesses

- `HandlerHost.Start(prefix, contextFactory, compose)` — ephemeral database per
  collection on the real local Postgres; the design-time factory migrates it with the
  composing app's chain (a hand-typed setup list rots the day a kit joins one app only).
- Every unique value embeds a fresh Guid: the collection shares one database, so a
  literal reused across tests collides with a unique index.
- The composition registers a **flat wide policy** ("any authenticated session, so the
  handler is what answers") — gate semantics belong to `NSail.Security.Tests`. The
  exception is a gate-proof collection (the `PolicyGate` precedent): it registers NO
  policies, so a denial can only come from the stored constraint under test.
- `RetryingLazy` keeps a faulted sweep from poisoning an assembly; a whole assembly
  failing instantly on connects is the shared Postgres under cross-slot load — rerun
  serial before suspecting code.
- **One form sweep needs a real database.** `Architecture.Tests` reads the tree and the
  composed models offline, except `MigrationScriptTests`: it applies each app's migration
  script (what `dotnet ef migrations script` writes) to an ephemeral empty database
  through `TestDatabase`, because only applying it shows whether the chain's raw SQL
  terminates its own statements (data.md).

### bUnit

- bUnit drives REAL DOM events; `form_input`-style value-writing without events is the
  documented automation trap and proves nothing about bindings.
- **`AddAuthorization()` cannot test a state CHANGE.** Its doubles answer from a context the
  test sets, not from an `AuthenticationStateProvider`, so `SetNotAuthorized()` after a render
  never reaches `AuthorizeRouteView` — a flip needs a real provider under a real
  `CascadingAuthenticationState` (`AnonymousRedirectTests`). bUnit registers its placeholders
  before the test's constructor while `AddAuthorizationCore()` only ever `TryAdd`s, so the
  real services need a `RemoveAll` first or the placeholder wins and throws
  `MissingBunitAuthorizationException`.
- **The bUnit suites flake on shared CI runners, a different test each run.** A family, not
  one test — a single red bUnit case on a CI run is a rerun, not a bug hunt. The ceiling is
  already widened (`test/Shared/BunitWaitTimeout.cs` sets `BunitContext.DefaultWaitTimeout`
  to 30s under `CI=true`, and `Directory.Build.targets` links it into every
  `bunit`-referencing project automatically, so there is no per-project opt-in gap). A
  contention repro — thousands of renders under oversubscribed CPU and a 2-core-pinned host —
  never landed red: raising the ceiling again or adding a load harness buys nothing, and a
  CPU-saturating guard test would create the contention it guards against. A render (model
  swap → `StateHasChanged` → parameter propagation → the field's post-swap value comparison
  in `NsFieldBase.SetValue`) stays serialized on one dispatcher, so scheduling noise changes
  *when* the comparison runs, never *what* it sees. The exception: a bUnit test that fails
  **fast** (seconds, not a wait timeout) is a concrete race, not this class, and earns a hunt
  — see the next bullet.
- **A gesture aimed at a per-render delegate can miss, and under load it does.** Blazor keeps
  an element's event handler id across a render only while the old and new delegates compare
  **equal**, and a closure over a per-render value never does — so a click wired as
  `@onclick="@(() => context.DoAsync())"` gets a fresh id every render and the previous one
  is retired. Inside a popover, where the vendor re-renders many times a second with a list
  open, the id the test dispatches against is already gone: `UnknownEventHandlerIdException`
  in seconds, never a wait timeout (`NsMenu`'s `Content` face was the case, caught by
  `AvailableProductSupplierMenuTests`; making the click a method of the component cured it).
  What the hunt taught:
  - **The load that reproduces it is concurrent test HOSTS, not a busy CPU** — six copies of
    the same assembly at once on 16 cores lands it red, while 32 spinning burners leave it
    green. Reach for `dotnet test` ×6 in parallel before believing a race is
    unreproducible.
  - **bUnit's own advice in that exception's text is not enough.** Wrapping the `Find` and the
    click in one `InvokeAsync` does not help: the render that retires the id runs inside that
    same dispatch. A race whose cure is not available to the test belongs to the component.
  - **The component's own method, never a lambda over what the render built**, is the shape —
    every other `@onclick` in the Stack has it.

### Browser suites (E2E)

- **A product's browser suite is its own, and its release's gate.** Every app owns
  `test/Apps/{Product}/NSail.{Product}.E2E`; `e2e.yml` takes a `product` and each
  release passes its own, so a tag is gated by the screens it is about to ship. The nightly
  names none and runs them all, one after the other on the one machine.
- **A browser suite is cut into lanes by measured minutes, not by area.** xunit serializes
  within a collection and parallelizes across them, so a suite's wall time is its LONGEST
  lane — never its total — and its floor is how many fixtures may live at once
  (`TestDatabase`'s permits: its own default, unless the run declares
  `NSAIL_TESTDB_CONCURRENCY`, which e2e.yml's E2E step sets). Each lane is a fixture of
  its own — own host, own database, own Chromium — so no two share mutable state and a test
  arranges what it needs inside its own class. The lanes are the suite's collection
  definitions (Optical's close `OpticalE2EFixture.cs`): **a new class joins its shape's lane
  carrying the fewest minutes**, and when the longest approaches the guard the
  lanes are re-cut from the nightly's own per-test times rather than by adding names. A
  lane's host has three minutes to answer, not one: its first act is a whole migration chain,
  while every other lane applies its own to the same server.
- **The browser suite builds the host it spawns.** `{Product}HostProcess` runs the Release
  build of the app (the `#if DEBUG` sign-in shortcuts are compiled out of it, the only way
  the sign-in path is the real one), and no slot ever produces that output —
  `dotnet build`/`dotnet test` are Debug. The harness runs the Release build itself every
  time, not only when the file is missing: otherwise a Stack change rebuilt in Debug leaves
  a stale Release host on disk and the golden paths silently exercise last week's app.
- **Arrange over HTTP, act through the UI.** `Api.Send` posts a message to its own `[Http]`
  route over the signed-in context's cookie jar, so reference rows cost one request instead
  of a WASM boot and a form — what the path proves is the screen it is named after.
- **A browser test wraps its act in `Evidence.Around`; `Evidence.Shot` is for the picture a
  pass keeps.** `Around` is the only call that catches a throw — a step left bare loses the
  screenshot, the DOM and the host's tail the moment it fails, which is exactly when they are
  needed (`Fixtures/Evidence.cs`).
- **The suite declares the language it reads in** — `{Product}E2EFixture.NewContext` writes the
  culture cookie *and* the browser locale, plus a desktop viewport wide enough that the
  container-query columns are present. Both installs default to `es`, so Optical's fixtures say
  it with the cookie alone while **Therapy's start their host `--Language:Offered:0 en`**: its
  screens under test are English, and a culture cookie no saved row backs does not survive the
  first authenticated read, which mirrors the signed-in user's row onto the device. A
  language a suite reads in is declared to the install, never assumed from it. The **one**
  fixture that declares nothing is `InstallLanguageFixture`: it starts the host at
  `Language:Default=es` with no offer and hands out a browser in English with no cookie,
  because that pair reproduces the Accept-Language/`Offered` bug (an install serving English
  beside a Spanish menu — baseservices.md, Request localization) that everything the other
  fixtures pin hides.
- **A text-shaped box binds on `input`, so a test types rather than changes.** bUnit's
  `Change()` raises the event the vendor no longer wires — the fields pass `Immediate` to it,
  which swaps `onchange` for `oninput` — and bUnit refuses an event its element has no handler
  for, so a suite driving a box with `Change()` fails with `MissingEventHandlerException`
  naming `onchange`. `Input()` is the act, in every notation and for every box in the family
  (ui/fields.md, *What the box holds is what the form submits*). **The pickers are the exception
  and still commit on leaving** — `NsDateField`, `NsDateTimeField`, `NsColorField`: the vendor's
  picker takes its typed text on change and `MudPicker.ImmediateText` is deliberately off — so
  those keep `Change()`, and an E2E date is still typed with `Ui.TypeDate`.
- **A take or a save is proven through the END of the entry, not through the typing.** A box's
  `OnCommit` — Enter, or the box being left — is where a handler that takes the value somewhere
  else or sends it hangs (ui/fields.md, *`ValueChanged` answers what the value IS*), so the act
  is `Input(…)` followed by `Blur()` or `KeyDown(Key.Enter)`. **A single `Input()` of the
  finished text is a PASTE and not a typing**, which is how three per-keystroke defects sat
  under eight green acts: a suite about the keystroke types the prefixes one at a time
  (`TypedValueCommitTests`), and the whole box arrives on each one because that is what a
  browser sends. `AwaitedActTests` gates `Blur` and `KeyDown` like the other four acts.
- **A figure is typed in the notation the suite declared it reads in.** Every numeric box
  parses in `RegionCulture.For(Language)`, so the same literal is two numbers: Optical reads
  `es` and takes `"-1,25"`, Therapy reads `en` and takes `"-1.25"` — and the wrong one is not a
  parse error but a SILENT factor of a hundred, the separator read as a group. Symptom: a
  `-1,25` sphere typed into an `en` box arrives `-125` (`LookupCreateRoundTripTests`),
  `PrescriptionRanges` refuses it, the
  aside stays open and the path times out on its own wait — a nightly red that reads nothing
  like a notation.
- **A browser suite's navigation budget is a property of the box, not of a screen.** A slow
  navigation under CI is the shared machine (parallel lanes on one box), not a slow screen, so
  per-site `PageWaitForURLOptions { Timeout = N }` literals on `WaitForURLAsync` are out.
  `Fixtures/NavigationBudget.cs` widens `IBrowserContext.SetDefaultNavigationTimeout` once,
  CI-gated like `BunitWaitTimeout`: at a developer's desk the vendor default (30s) answers
  every site, and a genuinely broken navigation still fails inside it. A per-site literal
  survives only where a comment says why that step is slower than the rest
  (`OnboardingWalk.cs`'s 60s wait for the wizard's cold WASM boot). A wait ceiling is only
  ever a CEILING, never a sleep, so widening one costs a green run nothing.
- **A locator wait's budget splits across two vendor mechanisms, because Playwright's does.**
  Web-first assertions (`Assertions.Expect(...).ToBeVisibleAsync` and siblings) are governed
  by `Assertions.SetDefaultExpectTimeout` — a PROCESS-GLOBAL static, unlike
  `IBrowserContext`'s setters — whose vendor default is 5s, far under navigation's 30s; a
  literal stripped without a setter in place takes a 20s wait down to 5s and reds the suite
  even at a desk, so `Fixtures/LocatorBudget.cs` widens it unconditionally (30s at a desk,
  45s CI-gated). `Locator.WaitForAsync`/`Page.WaitForSelectorAsync` read
  `IBrowserContext.SetDefaultTimeout` instead, which `SetDefaultExpectTimeout` does not
  reach; its vendor default (30s) already answers every site at a desk, so that half is
  CI-gated only, applied at the same four sites `NavigationBudget.Apply` is. One literal
  survives, the same shape as `OnboardingWalk.cs`'s exception: `Splash.Dismiss`'s 60s wait
  on a cold WASM boot.

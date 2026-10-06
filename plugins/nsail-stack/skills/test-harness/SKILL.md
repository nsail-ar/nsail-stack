---
name: test-harness
description: Write or run tests against the NSail harnesses — which layer owes the proof (Architecture, Stack, kit WebApi.Tests on real Postgres, bUnit Shared.Tests, Apps, browser E2E), HandlerHost, the Release-build E2E host, Api.Send arrangement, culture and viewport. Use when writing or fixing a kit test, bUnit test or E2E test, deciding whether a fix earns a regression test, diagnosing a flaky or instantly-failing suite, or reporting test totals.
---

# Test harness

The layer map and rulings are doctrine: read `${CLAUDE_PLUGIN_ROOT}/doctrine/testing.md` first —
each layer proves one kind of truth, and a test in the wrong layer proves nothing twice.
This skill surfaces the lore that has been paid for. When they disagree, the doctrine wins.

## Which layer owes the proof

| Layer | Proves |
|---|---|
| `test/Architecture` | form — a doctrine rule quantified over the whole tree |
| `test/Stack/...` | a Stack module's own behavior, offline |
| `test/Kits/.../WebApi.Tests` | handler doctrine against real Postgres (`NSail.Data.Testing`) |
| `test/Kits/.../Shared.Tests`, `test/Stack/Components` | screen behavior through real DOM events (bUnit) |
| `test/Apps/...` | composition seams — what a screen actually sends |
| `test/Apps/.../E2E` | only what a real browser sees; a THIN layer on purpose |

The kits are covered; **the Apps layer is where defects pool**. When an app story touches
a seam, the seam's test rides the story.

## Kit harness recipe

- `HandlerHost.Start(prefix, contextFactory, compose)` — ephemeral database per collection
  on the real local Postgres; the design-time factory migrates it with the composing app's
  chain. Never hand-type a setup list — it rots the day a kit joins one app only.
- **Every unique value embeds a fresh Guid**: the collection shares one database, so a
  reused literal collides with a unique index. **Except a phone number**: a Guid cannot
  spell one the numbering plan admits, so it is drawn with `Handsets.Draw()`
  (`test/Shared/Directory/Handsets.cs`) — why in testing.md, "Every harness".
- The composition registers a **flat wide policy** (any authenticated session), so the
  handler is what answers — gate semantics belong to `NSail.Security.Tests`. The exception
  is a gate-proof collection (the `PolicyGate` precedent): it registers NO policies, so a
  denial can only come from the stored constraint under test.

## Browser e2e recipe

- **The suite builds the Release host it spawns, every run.** `OpticalHostProcess` runs the
  Release build (the `#if DEBUG` sign-in shortcuts are compiled out — the only way the
  sign-in path is the real one), and no slot produces that output: `dotnet build`/`dotnet
  test` are Debug. Rebuilding only when the file is missing leaves a stale Release host
  exercising last week's app.
- **Arrange over HTTP, act through the UI.** `Api.Send` posts a message to its own `[Http]`
  route over the signed-in context's cookie jar — reference rows cost one request, and what
  the path proves is the screen it is named after.
- **Declare the language and the viewport**: `OpticalE2EFixture.NewContext` writes the
  culture cookie and a desktop viewport wide enough that the container-query columns are
  present — an install-default language change breaks tests that assume one.
- bUnit drives REAL DOM events; value-writing without events is the documented automation
  trap and proves nothing about bindings.

## Flaky suites

- A whole assembly failing instantly on connects is the shared Postgres under cross-slot
  load — **rerun serial before suspecting code**. `RetryingLazy` keeps a faulted sweep
  from poisoning an assembly.

## When a fix earns a test (testing.md, ruling 7 bounding ruling 6)

**A test per contract, never a test per anecdote.** A fix earns a test when the defect
exposed a promise nobody was watching. The question at every close is *what promise was
unwatched?* — if the answer names a promise, the pin lands (red first, the fix turns it
green); if the answer is "we were careless here once", the fix ships alone and the story
says so. A bug that reached a USER proves a flow nobody pinned, and a flow is a promise —
its scenario lands in the catalog (`NSail.Optical.Scenarios.Tests` against
`src/Apps/Optical/docs/test-scenarios.md`) before the fix. The same shape landing three
times in a week is a class that earns a net, not three regression tests.

Repeatable verification lands as a test; prose is for what only the eye judges. Form
rules quantified over the tree go to `Architecture.Tests` with an explicit exemption
list, never a recurring manual audit.

## Reporting

The suite total is the tree's, verified against `find test -name '*.Tests.csproj' | wc -l`,
never a number carried from memory. No coverage target, deliberately.

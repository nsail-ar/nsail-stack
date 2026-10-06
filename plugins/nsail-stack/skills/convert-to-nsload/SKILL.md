---
name: convert-to-nsload
description: Convert a screen's data read into an NsLoad region (OnLoad, Reload, Retry, NsPartial.Handoff for dashboard cards). Use whenever you touch a Blazor screen or card that still loads in OnCreatedAsync/OnParametersSetAsync/OnInitializedAsync — screens still read outside NsLoad and a screen touched for any reason converts — or when a load failure shows as a toast, a card reads twice, or an empty state lies about a failed read.
---

# Convert to NsLoad

`NsLoad` is the region around content that had to be READ — a list, a card's figures, a
counter. The contract is `${CLAUDE_PLUGIN_ROOT}/doctrine/ui/hosts.md` (NsLoad, NsDashboard); the
handoff record is `${CLAUDE_PLUGIN_ROOT}/doctrine/intentional-ui-cases.md`, "One arrival, one
loading". This skill is the conversion recipe.

**No story sweeps the rest: a screen being touched for ANY reason converts.** Screens and
cards still load from `OnCreatedAsync`/`OnParametersSetAsync`/`OnInitializedAsync` with no
`<NsLoad>` and get the floor `NsPage` provides, not a surface of their own — grep for a
`Send`/`Handoff` in those methods in a `.razor` with no `<NsLoad`. A list page and a
dashboard card are the two shapes that earn it first.

## The contract

Three states and no fourth: **not read yet** (nothing), **read** (`ChildContent`), **could
not be read** (the reason and a Retry, in the content's own place). Content inside the
region exists only when a read produced it — which is what keeps "no records" an answer
about the data instead of an answer about the network.

## Recipe

1. Move the read out of `OnCreatedAsync`/`OnParametersSetAsync` into the region's
   **`OnLoad`** — the region runs it on a `Runner` belonging to that one read, which turns
   a `BusinessException` into a `Problem` and everything else into
   `SystemProblem.Unhandled()`. The failure is reported handled there: **a load failure is
   never a toast** (it fades, carries no retry) and never the app-wide splash.
2. **Pass `args.CancellationToken` to whatever the read sends** (`ReadEventArgs`). A read
   the region has superseded — a filter moved, Retry pressed on a slow one — is cancelled,
   and the token is the only thing that stops it costing a round trip.
3. Screen-side re-reads go through **`Reload()`** on an `@ref` — a filter moved, a `*Saved`
   event arrived, a row was deleted — so the screen's re-reads and the user's Retry are
   one path with one failure surface. The Retry re-runs `OnLoad` alone; nothing around
   the region reloads and the page is not re-created.
4. Keep the empty states honest:
   - **Nothing to read is not a failed read** — a search nobody matched returns normally
     and the content renders its own empty state. Only a throw is a failure.
   - **A read that never happened is not one that came back empty** — a load that returned
     early (no active organization on a card scoped to one) produced no figures, so the
     content may not print any; it says which of the two it is (`DailySalesCard`).
5. The failure's words are the sender's, resolved through `ProblemManager.GetMessage` — a
   reason that reads badly is fixed in its own kit's strings, never in the Stack.

A failed read **replaces** the content rather than sitting above it: a filter that could
not be applied leaving the previous filter's rows on screen is its own lie.

## The card half (dashboard cards)

- **A card reads through `NsPartial.Handoff`, never `Send`** — the prerender resolves the
  read and persists the answer, the WASM client adopts it: one query per card per page
  load instead of two. A subject card passes its subject as the scope.
- Inside `OnLoad` it is STILL a `Handoff` — the region owns the three states and the
  retry, the handoff owns which side asks; the two answer different halves. The gotcha:
  **pass the token by name** —
  `Handoff(message, cancellationToken: args.CancellationToken)` — because the second
  *positional* parameter is the scope.
- **The handoff is take-once, so WHERE the card reads matters**: a card with no subject
  reads once on mount — inside `OnLoad` where it has a region, `OnInitializedAsync` where
  it does not; a subject card keeps `OnParametersSetAsync` but **guards on the subject a
  read has STARTED for, claimed before the await** (the null subject answers in
  `OnParametersSetAsync`, so the guard is reachable for the whole round trip). Both hosts
  rebuild the parameter dictionary on every render, so anything else asks twice.
- **Retry re-reads for real**: the prerendered answer was taken on the first pass and
  there is nothing left to adopt — which is what a retry should mean.
- **A card shows nothing until its read has answered** — a zero or an "empty" painted
  while the read is in flight is a false claim. Pinned by `CardHandoffTests`; its
  exemption list is one card that defers deliberately.

## Check

`NsLoad` reads once, from its own `OnInitializedAsync`. If the screen still has a read in
`OnCreatedAsync`/`OnParametersSetAsync` that the region now owns, delete it — two owners
is the double-loading the handoff exists to kill.

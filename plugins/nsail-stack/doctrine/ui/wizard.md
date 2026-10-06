# The wizard

`NsWizard`, the steps it hosts, and the router-level gate that decides an install has no
screens yet.

Read this page when you are building or changing a wizard step. The record behind these rules
is in [intentional-ui-cases-shell.md](../intentional-ui-cases-shell.md) under *The wizard*; a rule marked
*(cases)* has its story there.

---

## Steps

`NsWizard` hosts a sequence of contributed steps — today the ones `IOnboardingContributor`
registers — ordered by `Weight` and opened on the first that did not report itself done
(`OnboardingStep.Collect` / `FirstPending`). **The step map is the app's knowledge, never the
Stack's.**

**The chrome owns the way forward.** `NsWizard` renders Next and Back itself (`Next()` and
`Back()` are what the chrome calls); **a step never draws its own** (cases). **`WizardContext`
is the whole API**, cascaded to the step on screen, and a step says at most two things to it:

- **`Participate(onNext)`** — an async hook run on the way out. It may do work and it may
  refuse: `false` holds the step on screen with everything it had drawn. A **form step** submits
  its `NsForm` from here — `return _form is not { } form || await form.Submit();` — so success
  advances and a `Problem` vetoes and stays rendered under its own field. `NsForm.Submit()` runs
  the form's whole pipeline from outside its chrome and answers whether it took, which is why a
  step needs no submit of its own. A step that registers nothing just advances, which is what
  makes an **informational step born complete**.
- **`SetCanAdvance(bool)`** — Next is disabled until the current step reports it may fire. **The
  default is permissive**: a step with nothing to gate must not ask for its own button.

**While a Next is in flight, neither Back nor Next is offered** (`WizardContext.IsAdvancing`).
The hook is usually a form's `Submit()`, so a second Next re-enters that form's `Runner` for its
refusal and a Back unmounts the step mid-save. The sequence owns those two buttons, so the
sequence refuses — the footer stands outside the step's form and no cascade of it reaches here,
which is why `NsButton Submits` is no use ([actions.md](actions.md)). Both come back when the
hook lands, whichever way it ended.

**Back reloads the step from the server** — only the active step renders, so arriving at one
runs its own load and the wizard knows nothing about refetching anybody's data.

**Every step handler is an idempotent upsert**, because Next is pressed twice whenever Back was
pressed once — Next re-submits without fear. A step keyed on nothing the server can match says
what it is keyed on in its message's own comment.

**Steps are kit-pure**: a step belongs to one kit and reaches every other kit **by message**.
The exception is a write that must not half-land — a hire is one party, one user, one login and
one membership, so it travels as a single message whose handler does all four in-process
(`HireUser`).

---

## `IRouteGate`

**`IRouteGate` is the router-level veto.** Registered with `AddRouteGate<TGate>()` and asked by
`NsRouter` for every route **before** `AuthorizeRouteView` constructs anything, it answers a
destination page Type, or null to let the route through — which makes "an install with no setup
has no screens" true rather than merely invisible, and means **no page has to guard itself**.
Two things the gate owns, not the Stack: **which pages stay open while it is closed** (the
sign-in page above all — a lockdown that swallows the door locks its own key inside), and what
"done" means.

**The first gate that answers is the one the visitor obeys, and the order is `Weight`** —
ascending, ties keeping registration order, the same sort every other contributed sequence
uses. **A gate answering the page it was asked about CLAIMS that route**: the visitor stays and
nothing heavier is asked. A gate with one screen answers its own page that way while it is
closed, which is what makes its destination a place the visitor can stand; without it, two such
gates (the install's wizard and Iam's `SignUpGate`, per-user and closed on exactly the same
routes) would each send the visitor to the other's destination forever.

**The claim is a fixed point, not a veto over the whole set.** A gate that must be cleared
before another says so with a lighter `Weight` and is asked first, so a claim can never silence
a gate that outranks it — `SignUpGate` weighs `-10` because a wizard walked by a provisional
visitor writes an organization attributed to somebody nobody has met. Which gate decides is a
number on the gate, never the order a composition root happened to register in.

---

## The frame

**The frame is `NsFullLayout`, generalized rather than forked.** Its `Menu` slot is the drawer
where `MainLayout` puts the nav menu, so the **progress rail collapses on mobile exactly like
the nav does**; a caller passing none keeps the bare centered frame sign-in uses. It draws no
app bar of its own: the wizard names itself in its panel's own header (`NsPageHeader`, the same
component every document page uses), so the title sits flush with the panel below it and the
hamburger rides the title row, shown only below the drawer's breakpoint — exactly how every
other screen behaves.

**The rail is compact by construction**: one line per step, a check on completion, nothing that
scrolls — a sequence long enough to need a scrollbar has stopped reading as progress. An app
pairs the wizard with a chromeless layout of its own, keeping the nav drawer and its menu off an
install that has neither yet.

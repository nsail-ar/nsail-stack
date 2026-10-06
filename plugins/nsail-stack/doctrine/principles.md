# Principles

What NSail optimizes for. Every other document in `framework/` is a consequence of these; when
two documents seem to disagree, or a case is covered nowhere, decide here.

---

## 1. Total reusability

A capability is written once and serves every kit, app and surface that needs it. Reuse is
not "a shared folder" — the capability is expressed at the level where it is actually
general:

- **Behavior belongs to the Stack, meaning belongs to the kit.** `NsTable` knows nothing about
  parties; `PartiesPage` knows nothing about pagination.
- **A second caller is not a copy, it is a parameter.** When a screen needs what another screen
  already does, the answer is a parameter or a slot on the existing component — not a variant
  of it.
- **The general case is the only case.** `NsPageHeader` has no "parties mode": it has a `Search`
  slot that any list fills. A component with a per-caller branch is not reusable, it is shared.

---

## 2. Minimum coupling

A part knows the least it can about the parts around it.

- **Depend on contracts, not implementations.** Kits talk through messages; the Mediator is the
  only thing both sides know. A kit never references another kit's handlers.
- **Dependencies point down, never sideways or up.** `Types` sits at the bottom and reaches only
  for `Metadata`, which reaches for nothing. A component never reaches for the page that hosts it.
- **The vendor stops at the boundary.** MudBlazor lives behind `Ns*`. No page ever names a
  `Mud*` type, so replacing the vendor is a job inside one project.
- **Derived beats configured.** A value the framework can derive (`{Area}.{Type}.{Member}`,
  a URL from the route table) is a value nobody has to keep in sync, so it cannot drift.

---

## 3. Minimum code for maximum features

The measure is features per line owned, not lines produced.

**This holds even though an AI can emit a hundred thousand lines a minute.** Generation made
writing code cheap; it did nothing to the cost of *owning* it. Every line still has to be read,
kept true, and changed when the world changes.

- The framework absorbs repetition (generation, conventions, shared infrastructure); the
  application expresses intent. Prefer resolving over writing — [lesscode.md](lesscode.md) has
  the order to try.
- A feature that costs a lot of code is a design that has not been found yet.

---

## 4. Minimum surface for error

Prefer a design where the mistake cannot be made over one where it is caught, documented, or
remembered.

- **Make invalid states unrepresentable.** `NsPageLink` has no `Href` and `NsAction` has no
  `OnClick`, so there is no illegal combination left to validate at runtime — a rule enforced
  by a `throw` is a rule that already failed.
- **Silence is the worst failure.** An error that vanishes costs more than one that crashes:
  actions run through the `Runner` precisely so a rejected rule surfaces instead of doing
  nothing.
- **Remove the possibility, not the instance.** When something breaks from two things drifting
  apart, delete the second thing rather than syncing it better.

---

## When they pull against each other

- **Reusability vs. coupling.** Every reuse is a dependency. *Coupling wins across boundaries,
  reuse wins inside one.* Duplicating a small thing to keep two kits independent is correct;
  duplicating it twice inside one kit is not.
- **Minimum code vs. minimum error surface.** The shortest code is rarely the safest.
  *Error surface wins.* Lines are cheap now; a class of bug that can be designed out is gone
  forever.
- **Reusability vs. minimum code.** The abstraction that serves every future case costs more,
  today, than the two concrete ones it replaces. *Evidence wins over anticipation:* extract
  when the pattern has repeated, not when it looks like it will. Repeated manual code is the
  smell (lesscode.md) — the corollary is that unrepeated code is not one.
- **When two solutions exist and neither is perfect: build the seam that carries both** (the
  exemplar: the tenancy seam, `Tenancy:Mode` presets over two switches — data.md, Tenancy).
  Relegating one side of an imperfect fork buys today's simplicity with tomorrow's rewrite;
  fusing them into one branchy implementation buys neither. Find the narrow seam where the two differ, make each side a preset of it, and let
  configuration choose — one side may ship first, but the seam admits the other from day one.
  This applies at FORKS, where both sides have a real case; where one side is simply better,
  decide.

---

## Summary

Write it once. Let it know as little as possible. Own as few lines as the feature allows. Leave
no way to get it wrong.

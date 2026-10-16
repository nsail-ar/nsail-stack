# Principles

What the Stack optimizes for. Every rule in the doctrine is a consequence of these four; when two
rules seem to disagree, or a case is covered nowhere, they decide.

## 1. Total reusability

A capability is written once and serves every module, app and screen that needs it.

- **Behavior belongs to the Stack, meaning belongs to the module.** A table knows nothing about
  customers; a customers page knows nothing about pagination.
- **A second caller is a parameter, not a copy.** When a screen needs what another already does,
  the answer is a slot on the existing component, never a variant of it.
- **The general case is the only case.** A component with a branch per caller is shared, not
  reusable.

## 2. Minimum coupling

A part knows the least it can about the parts around it.

- **Depend on contracts.** Modules talk through messages; the mediator is the only thing both
  sides know.
- **Dependencies point down.** Never sideways, never up.
- **The vendor stops at the boundary.** The UI library lives behind `Ns*` components; no page
  names a vendor type, so replacing the vendor is a job inside one project.
- **Derived beats configured.** A value the framework can derive — a key from a namespace, a URL
  from the route table — is one nobody has to keep in sync.

## 3. Minimum code for maximum features

The measure is features per line *owned*, not lines produced. Generation made writing code cheap
— an AI can emit a hundred thousand lines a minute — and did nothing to the cost of owning it:
every line still has to be read, kept true, and changed when the world changes.

When code repeats, try in this order: reuse a generic solution; resolve it by convention or
metadata; generate it with Roslyn; emit it at runtime; reflect; and only then write it by hand.
Hand-written code is fine when the case is unique or the pattern is not stable yet — **repeated**
hand-written code is the smell. And generated code must read like hand-written code: less code,
never magic.

A feature that costs a lot of code is a design that has not been found yet.

## 4. Minimum surface for error

Prefer the design where the mistake cannot be made over the one where it is caught, documented or
remembered.

- **Make invalid states unrepresentable.** A link component that takes a page type and no `Href`
  cannot point at a missing page. An `UpdatePrescription` without `PatientId` cannot move a
  prescription.
- **Silence is the worst failure.** An error that vanishes costs more than one that crashes.
- **Remove the possibility, not the instance.** When two things drift apart, delete one of them
  instead of syncing them better.

## When they pull against each other

- **Reuse vs. coupling** — coupling wins across module boundaries, reuse wins inside one.
  Duplicating a small thing to keep two modules independent is correct.
- **Less code vs. less error surface** — error surface wins. Lines are cheap; a class of bug
  designed out is gone forever.
- **Reuse vs. less code** — evidence beats anticipation: extract when the pattern has repeated,
  not when it looks like it will.
- **Two imperfect solutions** — build the narrow seam that carries both, make each a preset of
  it, and let configuration choose. Where one side is simply better, decide.

Write it once. Let it know as little as possible. Own as few lines as the feature allows. Leave no
way to get it wrong.

The doctrine's own page: [principles.md](../plugins/nsail-stack/doctrine/principles.md).

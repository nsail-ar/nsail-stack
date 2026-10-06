# Structure

How NSail code is organized: physical layout (`src/Stack`, `src/Kits`, `src/Apps`), project
names, namespaces, modules, layers, folders, where a project's composition entry point
lives, and the comments policy. Type names are [naming.md](naming.md).

**Core principle:** organize around meaningful business or platform boundaries, not generic
technical categories. Names describe what something is — telegraphic (short, direct,
low-noise: `Users`, `Setup`, `SignIn`, `Prescriptions`) and platonic (the essence:
`Authentication`, `Sdk`, `Components`) — never accidental implementation details. The goal
is a structure that is stable, easy to navigate, and compatible with modular-monolith
thinking.

---

## Physical layout

```
src/
  Stack/                    # Framework — no business domain
    BaseServices/
    Components/
    Messaging/
    Types/
    …                       # one folder per Stack area
    Sample/                 # The Stack's example app: the one domain here, written as an app
  Kits/
    Iam/                    # Reusable domain kit
    Directory/
    …                       # one folder per kit
  Apps/
    Optical/                # Final application
    Therapy/
test/
  Stack/                    # mirrors src/ level by level
  Kits/
  Apps/
  …
```

The tree teaches the levels, not the inventory — the current list of projects and their
state is inventory.md (nsail: `docs/agents/inventory.md`).

Stack must not depend on Kits or Apps.

---

## Project names

Canonical shape: `Company.Area.(Project).Module.(Layer)`. In practice many projects stop at
`Company.Area.(Project)` and use folders for modules.

- **Company** — top-level organization: `NSail`
- **Area** — kit name or app name (the concept metadata.md defines): `Iam`, `Optical`, `Components` (in the Stack)
- **Project** — assembly-level split for platform, packaging, or capability: `Sdk`, `WebApi`, `Web`, `Wasm`, `Shared`, `Data`, `Runtime`, `Annotations`
- **Module** — meaningful functional boundary inside a kit or app: `Authentication`, `Prescriptions`
- **Layer** — optional internal subdivision: `Models`, `Entities`

Examples — Stack: `NSail.Messaging`, `NSail.Messaging.Runtime`, `NSail.Messaging.Annotations`,
`NSail.Components`, `NSail.Components.Mud`, `NSail.SourceGenerator`,
`NSail.BaseServices.WebApi`, `NSail.Data`. Kit: `NSail.Iam.Sdk`, `NSail.Iam.WebApi`,
`NSail.Iam.Shared`, `NSail.Iam.Data`. App: `NSail.Optical.Sdk`, `NSail.Optical.Web`,
`NSail.Optical.Wasm`, `NSail.Optical.Shared`.

| Suffix | Purpose |
|---|---|
| `Sdk` | Messages, models, HTTP client generation (the contract assembly) |
| `WebApi` | Server handlers, endpoint generation, API host wiring |
| `Web` | App ASP.NET host (`Program.cs`) |
| `Wasm` | Blazor WebAssembly client host |
| `Shared` | Razor UI shared between Web and Wasm |
| `Data` | Persistence and data access for a kit |
| `Annotations` | Marker attributes for generators or runtime |
| `Runtime` | Runtime implementation of an abstraction |
| `Http` | HTTP transport adapters |

`Shared` is acceptable only when the assembly contains Razor UI reused across hosts. Avoid
vague suffixes: `Common`, `CoreStuff`, `Helpers`.

Before creating a new project, ask: does the platform differ? Does the dependency boundary
differ? Does packaging/reuse justify a separate assembly? Is the name concrete and
meaningful? If not, keep the structure flatter — don't split projects early.

---

## Namespace shape

Canonical shape: `Company.Area.Module.(Layer)`. The namespace does **not** include the
project segment — the project name is an assembly concern; the namespace is a code model
concern. The root namespace is `Company.Area` (`NSail.Iam`, `NSail.Optical`).

```
NSail.Iam.Authentication          # good — in NSail.Iam.Sdk
NSail.Optical.Prescriptions       # good — in NSail.Optical.Sdk
NSail.Iam.Sdk.Authentication      # bad
NSail.Optical.WebApi.Prescriptions  # bad
```

Stack projects may include the capability in the namespace when it reflects a real
boundary — intentional for infrastructure:

```
NSail.Messaging
NSail.Messaging.Runtime
NSail.Messaging.Http
NSail.Problems          # from NSail.Types assembly
NSail.Components        # all UI projects share this root via RootNamespace
```

---

## Module, use case, layer

A **module** is a meaningful functional boundary: usually a business capability, a stable
mental boundary, large enough to contain multiple use cases, small enough to stay
understandable. Examples: `Authentication` (Iam), `Prescriptions` (Optical), `Entities`
(Iam.Data). Before creating one: is it a stable functional boundary, larger than a single use
case, home to several related use cases? If not, keep the structure flatter.

A **use case is a type, not a namespace**: `NSail.Iam.Authentication.SignIn`,
`NSail.Optical.Prescriptions.CreatePrescription` — never
`NSail.Optical.Prescriptions.CreatePrescription.Messages`. Use cases remain classes, records,
or handlers. The `Messages` layer folder is optional; messages often sit directly in the
module folder.

A **layer** is optional and must earn its place. Valid: `Models`, `Entities`, `Components`
(UI within a module). Avoid: `Helpers`, `Managers`, `Utils`, `CommonStuff`, and layers
created just because other codebases have them.

Folders reflect modules first:

```
Prescriptions/
  CreatePrescription.cs
  GetPrescription.cs
  PrescriptionHandler.cs
  Models/
    Prescription.cs
```

No horizontal top-level folders (`Controllers/`, `Services/`, `Repositories/`) by default.
NSail uses messages and handlers, not controllers.

### A contributor lives with the feature that contributes it

`IActionContributor<T>`, `INavMenuContributor` and their siblings are how a feature reaches
into something it does not own — a Prescriptions action appearing on a Party row. The file
goes in the **contributing** feature's folder, never beside the type it decorates and never
at the project root:

```
Prescriptions/Actions.cs        # NSail.Optical.Prescriptions
```

Filing it by target would invert the dependency the pattern exists to avoid — Party would
accumulate a file naming every feature that decorates it. The project root is for what
belongs to no feature: icons, routes, the setup — and the contributions the *project as a
whole* makes (`Menu.cs`, a root `Cards.cs`, `Onboarding.cs`). How the class is named
(`Actions`, `Cards`, `Contributed`): [naming.md](naming.md#contributor-classes-are-named-by-the-contribution-alone).

---

## Setup

A kit or app project's local composition entry point is named by **the capability the project contributes**, never by its segment plus `Setup` — the file names and the shadowing rule that constrains them are the table in [naming.md](naming.md), which this file does not repeat. Stack projects have a single setup each and no ambiguity to resolve, so they stay at plain `Setup.cs`.

The class matches the file and the method keeps naming the kit: `public static class Persistence` in `Persistence.cs`, holding `AddIamData`. `Configuration`, `ModuleConfiguration` and `ServiceRegistration` are the mechanism words the rule exists to keep out.

A setup file usually contains DI registration, module wiring, and calls to generated
partial methods ([generation.md](generation.md)).

---

## Anti-patterns

- putting use cases in namespaces
- reflecting packaging/platform segments in namespaces
- using `Shared` without a clear reason (UI reuse is a valid reason)
- creating layers just because other codebases do
- splitting projects too early
- using `Identity` when the kit is named `Iam`

---

## Comments policy

Three rules, no exceptions:

1. **Never write narrative or temporal comments** — nothing that references how the code used to be, what a change did, or a session decision ("no card here", "was previously X", "moved from Y"). That context belongs in commit messages and `docs/agents`, never in code. The ban is on **the code's own past**, not on a promise to callers or consumers that still exist: that promise is a rule 3 invariant and stays, written in the present. Banned: "what all of them meant before this field existed". Kept: "every caller that omits it means the whole rule".
2. **XML `<summary>` only on the Stack's public API, and only where the name is not enough** — contracts consumed by kits/apps, surprising behavior, semantics a signature can't carry. Kits and Apps carry **no summaries**: names plus `docs/agents` cover them. Never restate the name ("Gets or sets the name" is banned).
3. **Inline comments only for invisible constraints** — why the obvious option is NOT used, invariants the code cannot express. If it can be expressed by renaming, rename instead of commenting.

Good (rule 3 — protects against an "obvious fix" that breaks the aside):

```csharp
// Responsive is deliberately not used: it closes itself below the breakpoint,
// which would desync the URL-driven surface. The variant is picked here instead.
```

Banned (rule 1 — talks to the commit reviewer, noise a week later):

```razor
@* The surface provides the chrome — no card here. *@
```

# Permissions

Authorization constrains **message inputs, not query outputs** (there is no row-filtering
authorization model). What is implemented vs. deferred: [Implementation status](#implementation-status).

---

## Core doctrine

- **The message carries all filter capabilities; the policy says whether you may use them.** `GetPrescriptions { Doctor, Patient }` can filter by doctor or patient; a policy constrains what values each caller may pass (`Doctor: @me` for doctors, `Doctor: *` for admins). The handler filters by the fields as it naturally would — authorization never injects WHEREs.
- **Create and Update are specific messages — no Save.** An Update that doesn't *have* the immutable subject fields (PatientId) can't lie about them: invalid becomes unrepresentable, which beats forbidden-by-doctrine. Existing `Save*` messages migrate opportunistically.
- **Multi-organization ≠ multi-tenant.** The org axis models ONE company's internal hierarchy (branches, wings) — that's why `@memberOfOrDescendant` is a first-class case. Isolation between customer companies is the **tenant wall** ([data-tenancy.md](data-tenancy.md)) — a connection or a column below the policies, never a policy — and no grant crosses it. Don't stretch the org axis to separate companies.
- **The permission travels down, the role does not.** A `Membership` says where you may stand and with what role, and the selector offers **its own organization and nothing below it**: a member of the hospital is not the head of cardiología; acting there needs a membership there, which may carry a different role. Looking down goes through the other carriage: a policy audience's `Cascade` and the `@memberOfOrDescendant` field symbol hand a *permission* over the descendants without handing the role. The selector governs **acting as**, policies govern **doing**; the two must not be collapsed, and a per-membership cascade flag is exactly that collapse.
- **A role holds only at its membership's organization and below, and only where the session stands.** Standing somewhere, what holds is the membership there plus every membership at an organization above it; one below or beside grants nothing — not through `hasRole`, not through `memberOf`, not through `Cascade`. Somebody who is Empleado at a sucursal and Cliente at the root holds both at the sucursal and Cliente alone at the root. `Session.Standing()` (Stack) is the one computation: the evaluator's membership audience walks only it, `@current` matches a membership held above the current organization, and `Session.Roles` is derived from it at the edge (`SessionAdapter`) rather than read off a claim, so `SwitchOrganization`, which rewrites only the organization claim, re-derives the roles with it. The ticket still carries every membership — the selector offers them all. **A membership always has a role**: `Membership.RoleId` is a required foreign key, every door that mints one names the role, and a claim naming none is read as no membership.
- **How wide a grant reads is the grant's own word.** `everyOrganization` on the row says that what it hands over belongs to the company and not to one of its counters, so the org filter takes every branch instead of the one the caller stands at (`Policy.EveryOrganization` → `AuthorizationResult` → `AmbientAuthorization` → `OrgScopeEntry`; [data-tenancy.md](data-tenancy.md), The org filter). It is authored, never derived: not from the role, not from where the session stands, not from which fields are pinned — **there is no classification of roles in the model**, and "where does this session stand" answers only where somebody operates. Each screen is answered by its own policy and somebody holding two reads both, unmerged: Optical's Cliente grant is pinned `PartyId == @me` and crosses the branches, the Empleado grant beside it is filtered by org, and a seller who bought a pair of glasses reads the counter's list for their own sucursal and their own purchases from every one of them. A row that crosses the branches while pinning nothing is a blanket cross-branch grant — the author's call, the same way hanging an admin preset on a portal role is.
- **Organization ids travel in the message, never ambient from the token.** The multi-tenant orthodoxy (CompanyId from the signed claim) kills contract versatility (multi-org queries become inexpressible) and *bypasses* the permission system (an ambient filter is invisible plumbing, not a policy). Here the field is explicit, the UI fills it (resolved `@current`, or the orgs the user picked), and the policy IS the validation — one mechanism for mono- and multi-org. The token only feeds the Session. **The row filter follows the field**: the org filter reads the subtree of the organization the message names — the session's only when the message names none — so a caller a policy let ask about branch B reads B (data-tenancy.md, The org filter). That filter is not a second policy and never widens one: it runs behind the gate and follows the field only where the gate read it — a send allowed by an implication alone, whose fields `[Requires]` ignores by design, keeps the caller's own subtree.
- **Authorization applies to the input; the handler owns the output.** A policy answers *"may this principal send this message with these values?"*. The message decides what it returns.
- **The message is the permission unit.** No verb system: access levels are emergent from which messages (with which constraints) a role can send. Lookup = a dedicated message (`LookupParties` returns id+name — the response shape enforces exposure, not frontend goodwill).
- **Scoped messages for aggregates**: input constraints cover parameterized navigation (pick a patient → see their prescriptions). The aggregate view ("all my patients' prescriptions in one grid") has no field to constrain — it gets its own message (`GetOwnPatientPrescriptions`) whose handler computes the set as part of its contract. Rules of thumb:
  - A constraint that needs to look at rows the message will return → scoped message.
  - A message per customer/value ("GetPrescriptionsForHospitalX") → a field constraint.
- **Satellites never join the permission vocabulary — the organization is the only spatial axis** (the satellite rule). A thin entity that leans on an Organization (`Store`, `PointOfSale`) is never itself a policy field family: granting by store *and* by sell point *and* by whatever comes third is the field explosion this model exists to avoid. Instead, **a message about a satellite carries `OrganizationId` alongside the satellite id**, the org-axis constraint binds the org field, and the handler validates the pair (a mismatch is a validation error, not a policy question, per the discriminator below). The duplication is the price of an I/O-free evaluator, and it is wire-only: at rest the satellite keeps its single link. Resolving satellite→org during evaluation is rejected — it would grow per-satellite symbols and evaluation-time joins, the same explosion one level down.
- **Deny by default.** No explicit denies (absence = deny; exceptions are modeled with finer messages). The pre-auth boot path (SignIn, GetSession, system-scope GetSettings) is covered by **built-in anonymous policies** (audience absent) — no attribute (see below).
- **Join/projection exposure is the message contract's responsibility**: a list joining Party for display names exposes id+name because the response model only carries that.

## Policy schema (v2)

Stored as data (JSON document per row — Settings precedent). App policies are authored in C# via a typed builder that *produces documents* (never delegates), seeded; customer policies are edited in the admin UI. Same evaluator for both.

```
policy {
  audience {                            // who this applies to (AND within)
    is: {partyId}                       // one person
    memberOf: {orgId|@current} as {roleId}   // membership with role; cascade: bool (descendants)
    hasRole: {roleId}                   // the role holds where the session stands (Session.Standing)
    authenticated: true                 // any session, no role required (self-service: ChangePassword)
  }
  // audience absent/empty = ANONYMOUS (matches without a session) — allowed, editor warns loudly
  messages: [key, ...] | wildcard       // ["...CreatePrescription", "...ListPrescriptions"] — one row per use case, or "Directory.Parties.*" / "*"
  validFrom / validTo (optional)        // validity period — cheap matcher check, covers temporary grants/substitutions
  everyOrganization: bool               // the reads this grant allows are not narrowed to the branch the caller stands at
  fields {                              // constraints on message fields (all optional, AND)
    {FieldName}: * | {literal} | true | false | @me | @current | related as {roleId}
  }
}
```

- Symbols, party axis: `@me` (must equal actor's party), `related as X` (Relationships edge with actor's party as role X), literal. Org axis: `@current` (the active organization), `@memberOf` (any org the actor belongs to), `@memberOfOrDescendant` (explicit symbol — replaces a cascade flag), literal. `*` = any value, including null.
- Constraint value shapes (self-discriminating in JSON): symbol string (`"@me"`), parameterized object (`{"relatedAs": "medico"}`), literal, **array of literals** (contains — one policy pins several orgs), **`true`/`false`** (a flag field, pinned to that value — canonically `{"kind":"Literal","flag":false}`, and a values array beside it is refused at load). Arrays hold literals only — symbols don't mix into lists; "symbol OR literal" = two policies (policy-level OR already exists). Field absent from `fields` = free; `fields` absent = flat policy (full capability).
- **Policy vs validation discriminator**: a policy constraint always relates the value to the ACTOR. A rule that is true regardless of who asks (`Date > today`) is validation — it lives in the contract, is not editable, and must not creep into the policy vocabulary.
- **Null semantics**: a constrained field rejects null (you must pass the qualifying value); `*` allows null (= no voluntary filter). Message fields are voluntary filters; constraints make them mandatory. **One constraint reads an omitted claim differently, and only because a second door answers for it: the ORG axis pinned to `@memberOfOrDescendant` allows null**, because a message naming no branch is scoped by the row filter to the seat's own subtree (data-tenancy.md, The org filter — "every by-id read and every transition"), which is inside what that symbol already permits. Refusing it would refuse every by-id site while widening nothing. **`@current` and `@memberOf` do NOT get it**, and the difference is exactly the subtree: they permit the seat alone and the seat's memberships without descending, so an omission would hand back branches that naming them would have refused — widening by omission. A **literal** branch list rejects null from the other end: there the author named branches and the seat is not one of them (`PolicyHandler.Omitted`).
- **Wildcard rule**: a wildcard (`Feature.*`) may carry field constraints. It is expanded against the boot-computed registry BEFORE anything is bound, so the constraint is checked against the keys it covers, and a field none of them declares is still a loud typo. What a pattern cannot promise is the message that joins it tomorrow — and that direction is safe, because a constraint only ever narrows: the new message arrives constrained, never freed. That is what lets a feature grant arm the org axis (`Products.*` with `OrganizationId: @memberOfOrDescendant`) instead of an enumerated list that is one message out of date by the next release. Every policy, single, list or wildcard, evaluates through per-type generated handlers — one evaluation path, no special case (no `KeyPatternPolicyHandler`).
- **Combination**: a send is allowed if **at least one matching policy is fully satisfied** — OR at policy level, AND within. No field-level merging (doctor+admin sending Doctor=null: the doctor policy fails, the admin policy passes → passes).
- Field names in policies are wire-contract names (JSON binding) — no more fragile than the HTTP contract itself.

### Policies are multi-message

One row covers a use case's verb family: "doctors manage their own patients' prescriptions" is
ONE policy listing Create/List/Get, one audience, one `fields` block — not four rows drifting
apart. At load, a policy **expands** into one hydrated instance per member message, each
binding the shared `fields` block against its own typed accessors by wire name. The editor
shows the **union** of the members' `[PolicyField]` fields, marking which members each
constraint binds to.

Field semantics across members are **lenient by design**:

- A member message that *lacks* a constrained field is fine — the constraint applies where
  the field exists. `UpdatePrescription` lacks `PatientId` *on purpose* (immutable subject),
  and by-id operations are covered by handler doctrine (the loaded-value check). Strict
  all-members validation would forbid exactly the verb family the feature exists for.
- A constrained field that exists in **no** member is a save error (and a loud hydration
  error) — a typo or a dead constraint, not an intent. Exception (below): once hydration has
  dropped a member that no longer exists, **every** unbound field in that row is excused and
  reported.
- Same field name within a policy's members = same meaning. This is a rule, because
  multi-message makes it load-bearing.
- Drift is asymmetric: a member *gaining* a named field starts being constrained (tightens —
  safe); a member *losing* it stops (widens — the residual risk, mitigated by the no-member
  save error and the by-id handler doctrine).

### A member message that no longer exists costs its own grant and nothing else

A stored row is data written against the registry of the day it was saved; a message deleted
afterwards leaves a key nothing resolves. That key is **dropped** at hydration, the surviving
members hydrate and grant, and the row is **not quarantined** — the editor still opens it.
Nothing is loosened: a key no handler answers granted nothing, so deny-by-default holds, and
the failure is the size of the mistake instead of the size of a role. Bounds:

- **A built-in still fails hard.** Its keys are code, so an unknown one is a deploy-time bug,
  not a stale row.
- **The drop excuses the unbound-constraint error, and nothing else.** A row failing for any
  other reason — an off-axis constraint, an unbound field with no key dropped — quarantines as
  before.
- **The excuse is the row's, not the dropped field's, and it is reported.** The check is "did
  this row drop anything", not "did the dropped key declare this field": the deleted message
  took its `[PolicyField]` declarations with it, so that question has no answer, and "does a
  *live* member declare it" would quarantine precisely the row the drop exists to save. So an
  unrelated authoring typo rides out on the first dead key its row carries — deliberate and
  cheap: under lenient fields a constraint no surviving member declares binds nothing, so the
  grant is unchanged and only the signal was at stake. The signal is kept: each excused field
  is named in the drop report.
- **The report is once per store load, not once per hydration.** The stored set re-hydrates on
  every request, so an unconditional log would print the same stale row dozens of times a
  second. The drop and its amnesty are one report, throttled together.

Going forward nothing changes: the save door refuses an unknown message (`UnknownMessage`), so
a row only acquires a dead key by outliving the message it named — and `UpdatePolicyPage`
drops that key when it loads the row into the form, since the editor's tree has no node to
show it on.

## Examples

```json
{
  "name": "Doctors create prescriptions for their own patients",
  "messages": ["Optical.Prescriptions.CreatePrescription"],
  "audience": { "memberOf": { "organization": "@current", "role": "medico" } },
  "fields": {
    "PatientId": { "relatedAs": "medico" },
    "DoctorId": "@me"
  }
}
```

```json
{
  "name": "Admins create any prescription",
  "messages": ["Optical.Prescriptions.CreatePrescription"],
  "audience": { "memberOf": { "organization": "@current", "role": "admin" } }
}
```

```json
{
  "name": "System Administrator",
  "messages": ["*"],
  "audience": { "hasRole": "admin" }
}
```

What the examples encode:
- **`messages` is always a list** — one key, one shape, whether it names one message, several,
  or `"*"`. A singular `message` does not parse: the row quarantines and the grant does
  nothing. Every example on this page is a parse test in `NSail.Security.Tests`, which keeps
  the page executable.
- The doctor row: `DoctorId: @me` prevents prescribing in another doctor's name; a future "assistants register for their doctor" is another row (`DoctorId: {"relatedAs": "asistente-de"}`) — new cases = new rows, zero code.
- The admin row: no `fields` = full capability. "DoctorId must be an actual doctor" is NOT policy (true for every caller → validation, contract/handler).
- The root row is the **bootstrap seed** (deny-by-default would lock you out of your own system on day one). `hasRole` here is deliberate and different from `memberOf @current`: it names no organization, so an admin whose membership is at the root is the admin of every organization they stand in — the one spot where the distinction has big consequences (and the argument for keeping `hasRole` in the vocabulary). It does not reach up: an admin held at a sucursal is nobody's admin standing at the root (the ruling above).
- The **Name is documentation, not semantics** — a row named "Doctors see their own prescriptions" with an admin audience works fine and lies to the auditor. Meaningful names are a review concern.
- **A stored row's name is the shop's own words; a built-in's is a localization key.** A shop writes and rewrites its own rows, so nothing translates them. A built-in is product text a kit ships, so its `Name` is an `{Area}.Policies.{Name}` key whose English lives in the registering project's `strings.json` with translations beside it. `ListPolicies` resolves the key, so the grid's search and ordering run over the text the reader sees; `PolicyNameStringTests` fails on a built-in whose key has no text in either language, or whose name is prose.

### Built-in policies (the pre-auth path)

No `[Anonymous]` attribute; one mechanism for everything. The document below is the shape a parser sees, so its `name` is written out; a real registration carries `Iam.Policies.SignIn` there, which the catalog renders as this sentence:

```json
{ "name": "Anyone can sign in", "messages": ["Iam.Authentication.SignIn"] }
```

No `audience` = anonymous. The evaluator has two policy sources: the DB (admin-editable) and **code-injected built-ins** (same builder, registered in memory, never stored) — same document shape, same evaluator, shown read-only in the editor. **Being built-in is the source, never a key in the document**: a stored row claiming it would be a document asserting its own indestructibility. The reader refuses any key outside the schema for the same reason — one letter out of `fields` would otherwise leave the full-capability shape. Rules:
- Without a session, only audience-less policies match (anonymous mode).
- An anonymous policy cannot carry session symbols in fields (`@me` without a session is meaningless) — rejected at construction.
- The editor allows saving an audience-less policy but warns loudly (it is one step from an unauthenticated API).
- Being code, built-ins are immune to administration: no misclick can open an unauthenticated API, and no deleted row can lock everyone out of sign-in (you can't SSH into every customer install). The real pre-auth list: SignIn, GetSession, system-scope GetSettings (the sign-in page renders localized *before* login).
- Apps may also inject their bootstrap row (System Administrator) as a built-in — indestructible root access, app's choice.
- **A built-in and a seeded row may carry the same name, and a message added to that use case joins BOTH.** The built-in is the **floor** — what an install answers with the `Policies` table empty, and what no deleted or narrowed row can take away — and the app's seeded row is that install's **editable copy**, widened by a migration. Widen one and not the other and the flow works in the apps that seeded it and nowhere else. The tell: a `Named(...)` in an app's `Seed.Policies.cs` whose sentence is what a kit's `AddBuiltInPolicy` key renders as — the live example is the self-service password change (`Iam.Policies.ChangeOwnPassword` in the catalog, "Los usuarios autenticados pueden cambiar su propia contraseña" in both apps' seeds), which is why a two-message flow has to check both. The two read the same on screen; only the Origen column tells them apart.

### Anonymous inside the message system: the screen a person opens

A surface a PERSON reaches with a browser is not a capability endpoint — it is an
`[AllowAnonymous]` `NsPage` on `SignInLayout` talking through ordinary messages, each let past
`SecurityInterceptor` by a built-in audience-less policy. Precedents: `RecoverPasswordPage`,
`Channels.Replies.AnswerPage`, `Scheduling.Invitations.BookingPage` — a one-time token in the
URL opens a screen to somebody who holds no session and never will. The shape is doctrine:

- **Two messages, because a GET must not spend.** Outlook Safe Links and Gmail's scanners open
  a link unattended, so the query resolves and renders and a POST carries the effect.
- **One refusal for every fact, RETURNED and never thrown.** Unknown, expired, spent and never
  real are one `false`, so the door never reveals whether a token was ever minted — and a
  refusal travelling as an exception would unwind the ambient transaction and take the burn
  with it (`SignInLinkManager.Redeem`, `VerificationManager.Redeem`).
- **Single use is the DATABASE's answer**: the consumed stamp is the model's concurrency token,
  so two submits produce an UPDATE that matches no row rather than two effects.
- **The token is the whole credential and it identifies the actor**, so the screen asks nothing
  about who they are; the grant is built-in for the same reason the password's is — a narrowed
  or deleted row would close a link already on somebody's phone.
- **The public surface declares its ceiling** (`[Throttled]`, messaging.md).
- **A send the handler makes is the CALLER's send.** `SecurityInterceptor` runs on an in-process
  `Mediator.Send` exactly as on a request, so a public handler asking another kit a question
  asks with no session and the whole door answers 401 — and no test catches it in a host that
  composes the other kit out. Where the answer is the system's own business and never reaches
  the caller, ask on a scope of its own with `Session.System()`, the seam `BackgroundJobRunner`
  rides (`FreeHours.BehindABookingLink` subtracts a professional's external busy time that
  way). Both shortcuts are wrong: granting the inner message to the audience-less policy
  publishes it to the whole internet, and `[Requires]` does the same with a flat implication
  nothing can narrow. The fixture that catches this composes the OTHER kit
  (`SchedulingWithACalendar`); a fixture without it shipped green and answered 401 in the app.
- **What the door reaches is the shop's own opt-in, never a guess.** The token says who is
  asking; which rows it may touch is a field somebody set on purpose — `Agenda.PublicOfferingId`
  for the booking door, null in every fresh install. A door that picked "the only agenda" or
  "the first offering" would invent a decision the shop never made, and start lying the day
  there were two.

### Anonymous outside the message system: the capability endpoint

A surface consumed by software that will never hold a session (a subscribed calendar feed) is
not a message and must not become one: it is a hand-mapped endpoint (the Assets precedent),
marked `AllowAnonymous` at the map site. The credential is a capability token in the URL,
compared `[CaseSensitive]`, unguessable by construction; the handler authenticates by looking
it up and answers **404 for missing, invalid or revoked — never 401/403** (a feed that does not
exist does not say it exists). The handler reads the database directly and sends no messages (a
send would meet the `SecurityInterceptor` without a session). What it serves is already
privacy-shaped upstream, so a leaked URL exposes times, never identities.

## Message dependencies: [Requires]

`CreatePrescription`'s UI needs a patients dropdown → the use case depends on `LookupParties`. Modeled in the **contract**, not in policies:

```csharp
[Requires(typeof(LookupParties))]
public class CreatePrescription : IMessage { ... }
```

Evaluator: a message is allowed if a policy allows it, **or** if some allowed message declares it in `[Requires]`. The admin never sees or maintains the dependency (grants "create prescriptions", the dropdown works); audits answer to the attribute. Guardrails:

1. **One hop, no transitivity** — chains are a smell of a badly factored contract.
2. **The implication is flat** (no field constraints inherited) → only lookup-shaped messages (safe-by-response: id+name) belong in `[Requires]`; requiring a fat message is abuse, caught in review. **Safe-by-response is the rule and `List<{Entity}Ref>` is only its usual shape**: a read whose whole answer is a picker's own offer — identity and the text it is drawn with, no row of any entity — qualifies whatever container it answers in, and is named one by one in `RequiresGuardrailTests` with the reason it carries less than the lookup beside it. A `{Entity}Model` never qualifies.
3. **OR composes**: an explicit narrowed policy on the required message cannot restrict below the implication — the required message is unrestricted for whoever holds the primary. Assumed and documented; again: lookups only.
4. **The row filter does not follow the field of an implied send.** Because the implication is flat, an organization field on a message it alone allowed is a value no policy read — so the org filter reads the *caller's own* subtree for it, not the branch the message names (data-tenancy.md, The org filter). Without this, guardrail 2's "flat" would hand the holder of one `[Requires]` every branch's lookup, and guardrail 3 means no policy could narrow it back. A send that a stored or built-in policy also allows is vetted and keeps the message's branch: the implication only ever widens the *key*, never the rows.

Seeds may *also* grant lookups broadly — `[Requires]` guarantees UI correctness; broad seeds serve users who hold no primary message. They reinforce, not compete. The editor can display the implication (grey "includes: Look up parties" under the primary's checkbox — the aggregated dependency handlers are already that list).

**Mechanics.**
- **The declaration rides the generated registration**: `PolicyHandlerFactory.Requires`
  travels beside `Fields`, emitted from the same walk over the Sdk's messages. At startup
  every declaration becomes a **dependency handler** (`LookupParties ← CreatePrescription`),
  read off the registration rather than by reflecting over attributes (which a trimmed client
  could not do); the client derives the same set from the same registrations.
- **`SecurityManager` aggregates the sources through one list** (built-in, stored,
  dependency), so `Authorize`, `PreAuthorize`, `GetEffectivePolicies` and `RequiresMe` all see
  the implication. The effective-policies response includes the **closure** of the matched
  policies' requires, so client visibility and dropdowns work without the client knowing
  implications exist.
- **The guardrails are structural.** A `DependencyPolicyHandler` asks its satisfiability
  question — does the requiring message have a policy whose *audience* matches? — of the
  stored and built-in handlers ALONE (one hop), its fields verdict is unconditional (flat),
  and it is asked **alongside** the direct policies, never only when they fail (guardrail 3).
  Guardrail 2's review half — only lookup-shaped messages in the attribute — stays in the
  `NSail.Architecture.Tests` sweep, never the evaluator.
- **A match names its source**: `Origin: Dependency` plus the primary's key, so "why do I have
  access" answers "because you may send `Optical.WorkOrders.CreateWorkOrder`".
- Optical's Vendedor preset relies on it for `LookupParties`, `LookupPrescriptions` and
  `LookupFiscalPeriods` (declared by the work-order, counter and settlement messages that grant
  them). `LookupFittingMeasures` stays named in the preset: its ref carries the graduation, so
  guardrail 2 keeps it off the attribute and the row is its only grant. Encargado keeps its
  Directory enumeration: a broad seed reinforces.

Proof: `DependencyPolicyTests` (`NSail.Security.Tests`) for the evaluator, `PolicyHandlersTests`
(`NSail.SourceGenerator.Tests`) for the emission, `VendedorLookupImplicationTests`
(`NSail.Optical.Scenarios.Tests`) for the shipped pack — the seller's lookups answer and a
lookup nothing declares is still 403.

`requires` must NOT be a policy field: as data it would be editable, and an editable implication list is a **privilege-escalation vector** — any policy row could smuggle `requires: ["…DeletePrescription"]` as a camouflaged grant, and no editor validation can know what is "lookup-shaped". In the contract, the implication set is fixed at deploy and review-gated — the same asymmetry that puts built-in policies in code. (A per-feature `PolicyDefinition` registry is also rejected — a "giant module".)

## Metamodel: Policy (data) + PolicyHandler (behavior) + SecurityManager

Security mirrors the messaging architecture: **`Policy` is data** (entity at rest, JSON on the wire), **`PolicyHandler` is behavior** — the Message/Handler pair repeated. The generated default and the hand-written escape hatch are the same concept: a PolicyHandler, different authorship.

- **Contract is non-generic**: `Authorize(object message, Session session)` (+ `Fields`, `Name`, `Origin`). No generic interface (`PolicyHandler<T>` rejected — no utility): an object contract lets one handler cover *all* messages (the Admin wildcard) or a whole feature. Generated handlers type-test and cast once.
- **Home: `NSail.Security`** — Session, PolicyHandler, SecurityManager, SecurityInterceptor, RelationProvider, plus `NSail.Security.Annotations` for the Sdk-facing attributes. Iam implements: the DB-backed `DbRelationProvider`, the `SessionAdapter`/`HttpSessionProvider` edge, built-ins, the stored set (`PolicyStore`, `DbSecurityManager`) and the effective-policies endpoint.
- **`PolicyHandlerBase`** (NSail.Security, hand-written): owns ALL evaluation semantics — audience matching, symbol evaluation (`@me`, `relatedAs` graph query, org symbols). Generated code only dispatches to it (same split as the HttpClient generator): a symbol bug is fixed in one Stack file, never by regenerating.
- **Generated per message** (`[Generated(Policies.Handlers)]` holder per Sdk, scanning `IMessage`s in scope): `GetPartyPolicyHandler` with its `Fields` (name, kind, typed getter delegates — no per-send reflection, AOT-safe). `[Requires]` additionally yields an in-memory dependency handler, `Origin: Dependency`.
- **Flat/wildcard documents expand at load** against the boot-computed registry into per-type generated handlers, exactly like lists — one evaluation path.
- **Registry**: message key → generated handler type. Keys are **metadata keys computed at boot by MetadataProvider** (never baked literals, never `Type.FullName`: wildcards match on the `{Area}.{Feature}.*` rendering, the editor tree groups by it, and CLR refactors must not invalidate stored rows).
- **`Policy` entity** (Iam.Data, table Policies — noun without suffix, like User/Account/Party): Name, JSON document (which carries the `messages` list). No denormalized Area/Feature columns — a row can span features, and the editor loads the whole (small) set anyway, so the tree groups in memory. Hydration: document → handler instances (one per member). One noun, two states (at rest / hydrated); it also travels serialized to the frontend.
- **`SecurityManager`** (Manager family): loads, caches, invalidates, aggregates sources (Stored + BuiltIn + Dependency; custom `[Injectable]` handlers are deferred). Hydrated handlers live HERE, not in DI (data-born, set changes on admin edits). **Same class both sides** — it only needs `LoadPolicies(list)`: the server feeds it DB+built-ins (enforcement); the client feeds it the effective-policies response (satisfiability: menu, router, field states — `Authorize(page)` is the client asking the same object). Generated handlers live in the Sdk → WASM hydrates the same documents with the same code: zero client/server drift by construction. A `SecurityInterceptor` in the Mediator pipeline (the IInterceptor seam) applies the manager per send.
- **A caller that can await calls `EnsureLoaded` before any synchronous read** — `PreAuthorize`, `RequiresMe`, `Permits`, `GetEffectivePolicies` all read whatever is already hydrated, and `DbSecurityManager`'s per-tenant store loads lazily, so a tenant nobody has sent a message for yet answers from an empty set until something awaits the load. `Authorize`, the page gate and the endpoint gate all await it first; a new synchronous-read call site on a route neither gate reaches (a hand-mapped endpoint, a background job) owes the same call.
- **`Authorize` returns details, not a bool**: the result carries *which* policies allowed (Name, Origin) — "why do I have access" and audit logging fall out for free. `Origin: Stored | BuiltIn | Dependency` drives the editor's three displays (editable / read-only / grey implication row).
- **`Session`**: see [Session](#session). Lives in Stack (messaging consumes it).
- Validation runs on both sides: the editor validates documents on save (against `[PolicyField]`-marked fields via reflection); hydration validates again (unknown field/symbol → loud error, no silent skip). Two exceptions, both stored-row only: an unknown **message key** is dropped, and an unbound field in a row that dropped one is excused — neither silently, both reported once per load.

## Attributes: kind, not rule

- **Message fields** (Sdk): **`[PolicyField(RestrictAs.Party)]` / `[PolicyField(RestrictAs.Organization)]`** — the attribute names exactly what the field becomes: an entry in the metamodel's `Fields: IPolicyField[]`, and the enum supplies the security verb (restricted in its capacity as a Party). Declares the field's *nature* — the rule lives in the policy (data, admin-editable). Unmarked fields (Date, Rows) are invisible to security. Buys: evaluator type-safety, grants-UI discovery (which fields are constrainable, with which vocabulary), and validation on policy save (constraint on unmarked field = loud error). Strong value types (PartyId struct + converters) are rejected: the type marks every field of that type, the attribute marks only what you choose.
- **Naming rule: no enum may share a name with an NSail namespace segment or entity.** Inside `namespace NSail.*` the sibling namespace resolves before a using-imported enum, so names like `Party.Id`, `Iam.Party`, `Security.Party`, `Policy.Party` break by entity or namespace shadowing. The same rule names the generator target `[Generated(Policies.Handlers)]`, not `Security.Policies`.
- **Entities carry NO contracts** — no `[Owner]`/`IOwned`/`IOrganizationOwned`. `IOrgScoped` is not one: it classifies a row for the org filter ([data-tenancy.md](data-tenancy.md), The org filter), plumbing behind the gate that no policy reads. The generated handler compares decorated *message* fields against the policy's values (`message.PatientId == policy value`, `IsRelated`, …). By-id operations are handler doctrine, not framework machinery (see Evaluation). Attributes live in a shared annotations assembly.

### When `[PolicyField]` applies — the audit criterion

All four must hold, or the field stays unmarked:

1. **The values are `Guid`** — a `Guid`, a `Guid?`, or a collection of them. Decimals and
   strings are structurally unrepresentable as axes. A collection is a *shape*, not a kind:
   it wears any of the value kinds below and is quantified universally (below).
   **`RestrictAs.Flag` is the one exception, for that kind alone**: a `bool` field is
   representable because the policy pins it to `true`/`false` and nothing else can answer
   for it. Every other type stays out.
2. **It denotes a Party, an Organization, a catalog Reference or a Flag** — the four
   members `RestrictAs` has.
3. **The gate can read it off the wire without loading the row** — with one exact
   exception: when the Id *is* the axis (`GetOrganization.Id`, `GetParty.Id`).
4. **A rule would relate the value to the actor** — the policy-vs-validation
   discriminator above.

Derived sub-rules: a **Create never marks its own Id** (the row doesn't exist; no edge is
possible); an **Id on a `Lookup*` is never an axis** (it is rehydration filtering — marking
it breaks `[Requires]`); a **party/org reference that is descriptive payload of another
aggregate is not an axis** — the aggregate is (`CreateWorkshop.PartyId` no;
`CreateRelationship.PartyId`/`RelatedPartyId` yes, the pair *is* the identity);
**satellites stay out of the vocabulary** (Store/PointOfSale never; the `OrganizationId`
beside them is the axis).

Known limit, deliberate: constraining **inputs** can never hide **outputs** — response-field
visibility would be its own mechanism, not a `[PolicyField]` use case.

**So a grant is audited by its outputs.** What a role sees is decided by the response models
of the messages it is granted, so a preset is reviewed model by model — the `[Requires]`
closure included, since an implication is flat — and a value that must not travel is closed
with a read that does not carry it: a narrower response model, or no grant at all. Never by
hiding a column on screen, and never by a field constraint, which answers a different
question. A wildcard grant cannot be audited at all, because it also grants whatever is filed
under that feature next. Optical's Vendedor is the worked example and
`VendedorSeesNoMoneyTests` (`NSail.Architecture.Tests`) is that walk, run on every build.

### `RestrictAs.Reference`, `RestrictAs.Flag` and collections

- **`RestrictAs.Reference` is literal-only.** A catalog row — a role, a type, a code table
  entry — has no relation to the actor, so no symbol can answer for it: the policy carries
  the ids and nothing else. A denial nobody authored is the failure this vocabulary exists
  to prevent, so both doors refuse a symbol out loud: `ValidateDocument` answers
  `SymbolOnReference` in front of the author, hydration throws for a row that arrived some
  other way, and the editor offers a Reference field only `*` and a literal list.
  `CreateMembership.RoleId` and `CreateRelationship.RelatedRoleId` wear it: the message
  gate says who may enrol somebody; this says which role they may put in it.
- **`RestrictAs.Flag` (the Flag axis) is literal-only for the same reason, said of a value.**
  The policy carries `true` or `false` and both doors refuse anything else out loud:
  `ValidateDocument` answers `SymbolOnFlag` (or `ValuesOnFlag` for a list of ids),
  hydration throws for a row that arrived some other way, and the reverse mismatch — a flag
  pinned on an axis that names values — is `FlagOnValue`. Criterion 4 still decides which
  bools qualify: it gates an **override**, where the answer differs by caller, never a rule
  true whoever asks (`IsVirtual` is payload, not an axis).
  `CreateAppointment.BookedOutsidePolicy` and its siblings `Reschedule` and `MoveBookings`
  say who may book outside the declared schedule; the reason beside it stays the record,
  never the axis. A policy that omits the field leaves it free, so a grant written before the
  axis existed grants exactly what it granted. The client derives it like any literal — the
  control is not drawn (below) — and the evaluator answers it without the session or the
  graph, which is why the generator emits a call naming no axis for this kind and a
  type/axis mismatch is a compile error rather than a silent denial.
- **A collection is a shape the value kinds wear, quantified universally**; a flag, which
  names no values, stays scalar. Every element must satisfy the constraint, so one element
  outside it denies the whole message. `AttendeePartyIds` is constrainable as Party,
  symbols included — `@me` over the collection means every attendee is the actor.
  **An empty collection is refused like a null scalar**: quantified over nothing the
  constraint would pass vacuously — a constrained field widening by omission, the one thing
  the scalar's null rejection exists to stop. A caller narrowed on a collection has to fill
  it.
- The generator emits the axis beside the name in `PolicyHandlerFactory.Fields`, and the
  field itself as one of two calls: the Guid axes hand `Satisfies` the value and the axis
  — a `Guid` or `Guid?` binds the scalar overload, a collection the `IEnumerable` one —
  while `Flag` hands it the value alone, to the overload that takes a `bool`. The generator
  checks no type: the **pair** is enforced by overload resolution at that call site, so a
  field its axis cannot answer for fails to compile — a string or a decimal under any axis, a
  `bool` marked `Party`, a `Guid` marked `Flag`. That is where "structurally
  unrepresentable" is actually enforced.
- No axis of its own for passwords: `SetPassword { UserId, NewPassword }` is administering a
  user, gated by the message; `ChangePassword { CurrentPassword, NewPassword }` reads its
  actor from the Session so it cannot be pointed at anyone else, and refuses a wrong
  current password with a 401 — verifying it is authentication, not authorization.
- The Reference editor is a typed id, not a picker: the axis says the value is a catalog
  reference and never which catalog — a picker that guessed the wrong table would author a
  constraint that denies every send. A per-field catalog hint would turn it into a lookup,
  and no message needs one yet. The Flag editor is the two answers themselves: `*` or the
  value the policy pins, shown rather than implied.

Proof: `NSail.Security.Tests`, `NSail.SourceGenerator.Tests`, `NSail.Iam.WebApi.Tests`
(`PolicyGate`). The Flag axis end to end is `NSail.Therapy.Web.Tests`
(`AppointmentExceptionPolicyTests`, on that product's own `PolicyGate` — the axis's only
messages live in Scheduling, which the Iam fixture does not compose) and
`NSail.Scheduling.Shared.Tests` (`AppointmentExceptionGateTests`) for the control that is
not drawn — plus their twins on the app door that settles a clash outside the declared
hours, `ReplaceConsultingRoomExceptionGateTests` (`NSail.Therapy.Shared.Tests`).

**A Create's literal-only axis obliges every sibling that can write the field, and any other
message that hands the same value out.** A restricted create is worth nothing while a second
send reaches the same value. For role ids the mark rides `UpdateMembership.RoleId`,
`HireUser.RoleId`, and each sign-in provider's settings message —
`SaveGoogleSettings`/`SaveAppleSettings`/`SaveMetaSettings` carry it on `AffiliationRoleId`
(the role everyone a provider affiliates is born with) and on `PortalRoleId` (the role every
outsider is born with), alongside `RestrictAs.Organization` on `AffiliationOrganizationId`.
`Reference` is what a role wears. `Flag` answers alike: `Reschedule.BookedOutsidePolicy`
wears the mark `CreateAppointment` does, or a caller refused the exception at booking reaches
the same slot one send later.

**A value carried inside a nested collection is the same door and needs the same mark one
level up**: a policy binds a message's OWN fields, so `[PolicyField]` on a list item is an
annotation no evaluator reads — the axis rides the message the caller sends, the row keeps
the record of which entries used it, and the handler refuses a row claiming what its message
does not carry (`MoveBookings.BookedOutsidePolicy` over `BookingHour`'s, and Therapy's
`ReplaceConsultingRoom`, which is what the browser actually posts). `[Authorize<T>]` does not
cover this: it gates whether the message may be **sent**, never what values it may carry.

In the pack, `Encargado`'s constrained role set is the two roles the floor is staffed with and
never `admin` (the mechanism governs; the seed only picks the safe factory default); a
tenant's own admin widens or narrows the set through the policy editor. A message listed by
any satisfied row is unconstrained by that row, so `UpdateMembership` lives in the row that
already restricts the create rather than a second grant. Pinned against the shipped pack in
`NSail.Optical.Scenarios.Tests` (`EncargadoRoleAssignmentTests`, `PresetPolicyGate` — the one
Optical fixture with no overriding wildcard, so the preset itself answers).

## Evaluation

1. **Gate** — a filter in the Mediator pipeline. Full evaluation against actor + message fields: audience, message key, field constraints. Deny → a refusal Problem, and **which one depends on the caller** (`SecurityProblem.Denied`, see [The two refusals](#the-two-refusals)): 403 for a session, 401 for none. Covers queries-by-fields AND creates with zero DB loads beyond the graph edge checks. A constrained field rejects a null claim — the evaluator fails closed, so a nullable marked filter cannot widen a scoped caller's view by omission; the admin's free view arrives through an Any constraint, never through leaving the field out. `@memberOfOrDescendant` is the one exception, and the reason is that omission under that symbol alone is not a widening (Null semantics above).
2. **By-id operations are handler doctrine** (no framework machinery):
   - **Gets/Deletes by id**: the handler bakes the check into the query (scoped WHERE via graph helpers) and returns empty — hides existence (no 403 oracle). On the ORG axis the handler writes nothing wherever the row wears `IOrgScoped`: the model carries that WHERE (data-tenancy.md, The org filter). A hand-written scoped WHERE is what is left: a list of one person's EDGES, which no model-wide filter can reach without circularity, and a `Conversation`, whose organization is NULLABLE so `OrgFilter.Axes` has no column to join (`ConversationHandler.Read`) — and it compares against the operation's own `OrgScopeProvider`, never a second walk down the chart (org-map.md, The party axis).
   - **The by-id carve-out**: a verb whose success asserts a credential is dead (revoke, rotate) answers NotFound on missing and foreign alike instead of succeeding silently — both answers hide existence equally well, and a refused revocation must never read as success. The owner's retry still succeeds — a soft disable finds the disabled row and matches.
   - **Which by-id operation is answered by which of these is written down per message** (org-map.md, Messages — where each send stands): the filter alone, the filter plus a handler that refuses a stored organization differing from the claimed one, a WHERE the handler writes because the row is not `IOrgScoped`, or nothing on this axis because the id names a tenant-level row. The last is where guard reads go wrong — a delete whose in-use check reads an org-scoped table is narrowed by the filter and fails OPEN.
   - **Updates by id — the input-only residual risk**: the gate validates *claimed* fields; the stored row may differ. Primary defense is the **contract shape**: Create/Update specific messages — `UpdatePrescription` simply doesn't carry `PatientId`, so the lie is unrepresentable. Where a mutable field still needs a graph check, the handler verifies the *loaded* value via the provider before writing.
   - **A write that MOVES a row between branches carries both ends as marked fields.** The rule
     is about rows this axis protects — an `IOrgScoped` row, one the org filter reaches; a
     tenant-level row whose `OrganizationId` is a plain attribute is outside it and takes no
     source field (`UpdateRelationship` re-stamps one and is right to: an edge is the tenant's,
     org-map.md, The party axis). For the rest: an update that re-stamps `OrganizationId` over a
     loaded row puts only the DESTINATION in front of the gate, and the branch the row is leaving
     is not a field, so no policy can see it: whoever held one branch could pull another branch's
     row into it. So the message declares the source too — `UpdateStore.FromOrganizationId` and
     `UpdatePointOfSale.FromOrganizationId`, both `[PolicyField(RestrictAs.Organization)]`, both
     required — and three things follow from the axis alone, with no new machinery. The gate
     answers **403 for a caller who holds the destination and not the source**, including on a
     sideways move between siblings. The filter reads the **union** of the two subtrees
     (data-tenancy.md, The org filter), so the row is visible from either end — which is what
     makes that sideways move possible at all for a caller who does hold both, and what takes the
     filter out from under the DESTINATION: over the union a missing destination reaches the
     foreign key as a 500, so both handlers ask the destination exists
     (`EnsureOrganizationExists`) like every other dangling reference their kit checks. And the
     handler **refuses a stored organization that differs from the claimed source**, because the
     gate vetted the claim and only the row can confirm it (org-map.md's `filter + claimed=stored`,
     here as `StoreNotOfOrganization` / `PointOfSaleNotOfOrganization`). The destination keeps the
     name the axis wears everywhere, `OrganizationId`: renaming it `To…` would free the constraint
     on every stored row that had narrowed it. **The corollary for whoever narrows the axis: a
     policy granting a branch has to constrain BOTH fields** — a row that names the destination
     alone reopens the gap by data. Held by `MovingASatelliteBetweenBranchesTests`
     (`NSail.Optical.Scenarios.Tests` — one collection for both handlers, since the app is what
     composes the two kits beside the gate and the walk).
3. **A reaction is not a request: the gate stops at the publish.** `Mediator.Publish`, every
   subscriber it reaches and everything those subscribers send run without consulting the
   caller's grants. The send that decided the act was already authorized; the chain then
   crosses into kits the policy's author never heard of, so the only alternative is a seeded
   row naming event keys — which buys one frame and loses to the next `Send` behind it.
   - Mechanism: **`AmbientPublish`** (`NSail.Messaging.Runtime`), an `AsyncLocal`
     `PublishPipeline` enters *structurally*, so no `Add*` order and no composition root can
     drop it; `SecurityInterceptor` reads it and skips, both arities.
   - Not exempt: **a caller's own `Send`** (still 403) and anything crossing a **process
     boundary**, since an `HttpSender`'s message arrives at the other side as an ordinary
     send and is gated there.
   - **The boundary this rests on is DI registration**, as a background job's is: a domain
     event carries no `[Http]`, so nothing off the wire can publish. What may publish is what
     the app composed, and a new `IHandler<TEvent>` is reviewed as code — no stored policy
     narrows it.
   - The session is **not** elevated. The actor stays himself all the way down, so a reaction
     records who acted (`Cancellations.Close` takes `CancelledByPartyId` from the Session);
     `Session.System()` would have erased him to buy the same exemption.

   Proof: `ReactionGateTests` (`NSail.Security.Tests`) walks the chain — a caller granted one
   message, the event behind it and the reaction's own sends — and pins that the same
   ungranted message sent by the caller is still refused; `AmbientPublishTests`
   (`NSail.Messaging.Runtime.Tests`) pins the flow that carries the flag.

Graph questions go through a **provider** (`RelationProvider` — Provider family, lives in NSail.Security, covers both edges: `IsRelated(partyA, partyB, role)` over Relationships, `IsMember(party, org, role)` over Memberships, plus `GetAncestors`/`GetDescendants` over the chart — in-query always; post-query filtering is banned: paging, counts, existence leaks). Two implementations:
- **Optimistic default** (TryAdd, returns true) — serves the WASM client AND apps without Iam (the Null pattern). Client flow for `relatedAs`: handler passes optimistically → UI attempts → server 403 → Forbidden toast. Few cases, acceptable UX.
- **Iam registers the real DB-backed one** server-side.

Consequences: **`Authorize` is async** (the graph is DB, even with policies preloaded). And no `WasmSecurityManager`/`WebSecurityManager` split: ONE SecurityManager — the environment difference is which provider and policy source get registered (composition, not class proliferation).

## The two refusals

Deny-by-default answers a refused send with one of two facts a client must tell apart:

- **403 Forbidden** — *you may not*. The caller is somebody; no policy covers this send for
  them. Signing in again changes nothing.
- **401 Unauthorized** — *there is nobody here*. The caller carries no session, so there was
  no one for a policy to allow. Signing in fixes it.

`SecurityProblem.Denied(authenticated, action)` is the one place that chooses, and both gates
call it — `SecurityInterceptor` (both arities) and `EndpointGateMiddleware` — so they cannot
drift. An anonymous built-in still lets its message through; only a **denied** anonymous send
answers 401.

**Why it matters:** a client holds its `Session` in memory for the life of its tab while the
cookie behind it expires on the server; with one refusal for both facts, the tab could never
learn its credential had died.

**The client half** is `SessionInterceptor` (`NSail.Iam.Shared`), registered by `AddIamWasm`
only — a server host rebuilds its `Session` from the request's cookie every time. On a 401
reaching a client that believes it is signed in, it calls
`IamAuthenticationStateProvider.RenewSession()`; the answer comes back anonymous, the
authentication state changes, and `NsNotAuthorized` sends the user to sign in carrying the
address they were on. The refusal is **re-thrown, never swallowed**: the form still draws it
(intentional-ui.md, refusal placement). `RenewSession` is a no-op while a resolution is in
flight, because `GetSession` travels the same pipeline that reported the refusal.

Proof: `NSail.Security.Tests`, `NSail.Iam.Shared.Tests` (the renewal, the 403 that must not
trigger it, the anonymous client that must not loop, the re-entrancy bound), and the
`NSail.Architecture.Tests` endpoint sweep, which requires **401** from every mapped endpoint
for an anonymous probe.

## Session

Built once per request; the gate and helpers consume it (never `HttpContext.User` directly). The .NET-free identity; lives in Stack; `GetSession` returns `NSail.Security.Session` (the PartyId rides as a claim in the cookie).
- Who: UserId, PartyId, roles — the ones standing where the session stands (`Session.Standing()`, the Core doctrine). Where: **OrganizationId with a setter** (session-state semantics: the active org changes at runtime from the user menu, a tree of the user's memberships; `@current` resolves against it), and **Domain**, the bare host the request arrived on.
- **ClaimsPrincipal is adapted ONCE at the edge** (Iam.WebApi). Effective-policy caches key on **(user, active org)**; an org switch is a *security event*: caches invalidate, the client refetches effective policies, and the roles are re-derived.
- **Where a sign-in lands is operational, and decides nothing about what a grant reads**: the saved preference while it still names a membership, else `OrganizationHierarchy.PickDefault` — the shallowest membership, then by name. It says which sucursal somebody *operates* from (which one a sale is written in); there is no ordering of roles behind it and no classification of roles anywhere. What crosses the branches is the grant (Core doctrine).
- **`Domain` is read exactly once, in `SessionAdapter.FromRequest`** — the one place that touches `HttpContext.Request.Host`, so every door carries the same value and nothing below the edge reaches for HttpContext. It is a *fact*, never an authorization input: it feeds `RequestOrganizationProvider`, which places an ANONYMOUS request (the organization whose `Slug` is exactly the one label `Domain` carries under `TenantInfo.Domain`, the tenant's own base; else the single root organization; else `RequestOrganization.Ambiguous`, which every consumer treats as "no organization" and the sign-in page turns into an error screen). It cannot be resolved at Session build — the provider is async, the session provider a synchronous property — so the parse and the resolution are two steps.
- **`Tenant`** is the tenant this identity was minted for, as its slug — the one session fact
  the request is never asked for, because it is what the request is checked *against*:
  `TenantClaimMiddleware` compares it with the tenant the proxy header resolved and refuses a
  mismatch — signing the ticket out, then answering 401 as it would anyone with none. Null off
  a tenant-resolving install. Filled at the edge from the `tenant` claim, which sign-in stamps;
  the full law is in [data-tenancy.md](data-tenancy.md#the-credential-is-stamped-and-checked-on-every-request).
- **`IsProvisional`** says the actor's own Party was founded by an external sign-up out of a
  provider's claims alone (`Party.IsProvisional`, Directory's, because Directory's own list and
  lookups are what must not show it). It is **not an authorization input** — no policy names it
  and the gate it feeds is the router's, not the evaluator's (Iam's `SignUpGate` holds every
  route on the sign-up form). It rides the credential like `Tenant` does, written only when
  true, so a ticket without the claim and the one `CompleteSignUp` re-issues both read as
  complete.
- **A credential is re-read against its own records on every request, and that is one place.**
  The ticket is signed, so nothing in it can go wrong — and everything it NAMES can: a party
  merged away, a user disabled, a user deleted, a user repointed at another party. `CredentialProvider`
  (Stack, permissive floor; `DbCredentialProvider` in Iam) answers one lookup by primary key, and
  `CredentialMiddleware` ([baseservices.md](baseservices.md#webapi-bootstrap)) acts on it: a user
  **repointed** has its credential re-issued and the request carries the new party, a user **gone
  or disabled** is signed out and challenged. **No validation interval**, and that is the point —
  the next request after the records moved is the one that would write the dead id, so a window
  is a window of foreign-key refusals read as 500s. It is asked of everything the matched endpoint
  does not say is a static asset — a file off the manifest can write nothing and draw nothing, and
  a cold WebAssembly load is dozens of them; the skip is a deny-list and not an allow-list so a
  host's own new endpoint is walled by default. **A guard per handler is the wrong shape**: every
  write that takes `PartyId` off the Session is the same defect, and there is no counting them.
- **The refusal says why, on both arms, because the ticket is gone by the time the person reads
  it.** The browser's redirect carries `SignInRoutes.StaleParameter` and the sign-in page draws
  one sentence for all three ways (marked on the challenge as `SignInRoutes.StaleProperty`, turned
  into the query by Iam's `OnRedirectToLogin`). **The arm every product actually ships is the
  401**: both render `InteractiveWebAssembly`, so after boot there is no document navigation and
  somebody already inside meets the refusal on a send ([The two refusals](#the-two-refusals)).
  There the client is what carries the reason: `SessionInterceptor` renews on a 401 that met a
  live session, the state it rebuilds is anonymous *and marked* (`SignInRoutes.StaleClaim`), and
  `NsNotAuthorized` puts `StaleParameter` on the address it bounces to. Without that the person
  lands on a door that looks like it forgot them and no later document load can say otherwise —
  the cookie went out with the 401. Bound: an interactive circuit built its session from the
  document request that started it and does not pass the pipeline again, so it picks the refusal
  up on its next document load. Proof: `CredentialWallTests` (`NSail.Iam.WebApi.Tests`) on a real
  host behind the real pipeline — a ticket is what the subject is, so no handler send can hold it
  — plus `SessionExpiryTests` and `AnonymousRedirectTests` for the two halves of the client arm.
- `Session.System()` for background jobs; hand-built in tests; WASM builds its own from `GetSession` + effective policies.
- **A system session bypasses evaluation entirely** (`SecurityManager` short-circuits
  `IsSystem` before any policy runs), so for background work the security boundary is not
  runtime policy — it is **DI registration**: whatever `AddBackgroundJob<T>()` contributes
  runs with every send allowed. Jobs are code, not data, so the boundary is code review of
  those registrations: every job is born with total permissions, and no stored policy can
  narrow it.
- Named `Session` because the object `GetSession` returns IS the object the gate consumes — one identity concept end to end.

## The client consumes policies as UX data

The client fetches its **effective policies** once per session (twin of GetSession). From them it *derives* — never from role checks:

- Field states: `@me`/`@current`/literals → client-derivable (locked/prefilled/hidden, or restricted to the literal set); a pinned **flag** is the sharpest case — `SecurityManager.Permits(message, field, value, session)` answers whether any matching policy leaves that value possible, `NsPartial.CanSend<T>(field, value)` is the markup half, and a control the caller may not tick is **not drawn** rather than drawn and refused; `*` → free (admin view sends null); `relatedAs` → **not client-evaluable** (no graph client-side): the optimistic provider passes it, the field renders free, the server gate validates — wrong picks get a Forbidden toast; apps may ship scoped lookup messages ("my patients") so the dropdown only offers values that will pass. No satisfiable policy for the message → the view/menu/link doesn't exist for this user.
- "Doctor view" and "Admin view" are the same page with fields in different derived states — no `if (IsAdmin)` in pages, ever. The client asks "what may I send?", not "what am I?".
- Defense in depth: the client is best-effort; the gate validates regardless. Stale client policies degrade to refusals, never to exposure — and a stale client *session* degrades to 401s, which the client acts on rather than absorbs ([The two refusals](#the-two-refusals)).

The wiring, from the same code on both sides:

- `GetEffectivePolicies` (`api/iam/policies`) is the twin of `GetSession` and, like it, a **built-in policy** — learning what you may do cannot itself need a permission. It returns `SecurityManager.GetEffectivePolicies`: the policies whose **audience and validity** match, message key untouched. Filtering here is what keeps the client from ever evaluating an audience.
- `MessagePolicyProvider` / `MessageAuthorizationHandler` live in `NSail.Components`, next to the attribute whose names they read, and both hosts register them through `AddMessagePolicies` — the server from `AddBaseWebApp`, the client from `AddIamWasm`. A host without pages needs neither.
- `WasmAuthenticationStateProvider` is the client's edge, the twin of `HttpSessionProvider`: the `Session` the API just returned becomes the one the gate reads, and the effective policies are fetched in the same breath. It hangs off `OnSessionLoaded` on the shared provider — the server must not have its request-built session overwritten or its stored policies replaced.

Page/menu/link visibility — **`[Authorize<TMessage>]` on the page**: `[Authorize<ListParties>]` on PartiesPage. Menus and links point at routes → page type → its `[Authorize<T>]` → visibility (the page is the source of what OPENS, and for a link and a row action it is the only source). NsRouter checks it on navigation for direct URLs — that check is manners, not security (without it the page mounts and its OnInit sends collapse into 403s; the server enforces regardless).

**A nav entry may declare one permission of its own, ANDed with the page's and never instead of it** (`NavMenuItem.Permission`, [ui/navigation.md](ui/navigation.md)). The page stays the single source of what opens — the route is not closed by it, and a link or a row action never reads it — so what the field adds is strictly the DOOR, and it earns its keep where the page provably cannot answer: **two doors onto one page**, which is one drawer's reading of a screen against another's, and a **group**, which the page gate never asks about at all. It is a message type like any other gate, so visibility stays one mechanism; a role name tested in code is still out, and so is a marker message minted to carry nothing but the distinction — hang the door on a grant the audience already differs on, and the discrimination is then by GRANT: a hand-built role narrowed past that message loses the door, and widening the role is the way back.

`[Authorize<T>]` is a real ASP.NET `AuthorizeAttribute` (so `AuthorizeRouteView` reads it natively and it composes with plain `[Authorize]`/`[AllowAnonymous]`), and the generic argument is checked at compile time where a `typeof()` is not. The message type travels in `Policy` as `msg:{AssemblyQualifiedName}`; an `IAuthorizationPolicyProvider` fabricates the policy and answers it with `SecurityManager.PreAuthorize` — the same optimistic gate the menu and the action panels use, so a page, its menu entry and its row action agree.

**Two attributes, two different words** — they are not interchangeable:

| Where | Attribute | Means |
|---|---|---|
| Message | `[Requires(typeof(X))]` | **implication** — whoever may send me may send X |
| Page | `[Authorize<X>]` | **gate** — you must be able to send X to see me |

The entry message is the one the screen exists *for*: a list page is governed by its `List{Entity}s`, a create page by its `Create{Entity}`, an update page by its `Update{Entity}` — never by the `Get`/`Lookup` sends it makes along the way (those are covered by `[Requires]` on the primary). A screen with no message that clearly governs it declares nothing and stays visible; an invented gate is worse than a declared gap — unless the screen's material is not an entity at all (the Manual: `NsGuide` composes its chapters client-side and sends nothing), in which case the story may mint an empty `IMessage` for the sole purpose of carrying the permission, with a handler that does nothing (`ViewGuide`/`GuideHandler`, `Optical.StaffGuide`). That message governs nothing else and gains no `[Requires]` dependents.

The route table is a **public service** (`RouteTable`) — consumed by NsRouter, the nav menu and every link a page builds.

Visibility checks **satisfiability, not validity**: does *some* effective policy exist for the message key? Fields are irrelevant here (a `@me`-constrained policy still permits some send → the view exists; a doctor with zero patients sees the menu and gets empty lists — graceful false positive, false negatives impossible). And the client never evaluates audiences: the effective-policies message filters by audience **server-side**, so the client check collapses to a key-set lookup with wildcards — no evaluator logic duplicated in WASM, no drift.

## Admin UI

Policies are **multi-message** in the editor — the primary authoring shape is the use case: a message checklist per row (a fully-marked feature collapses to its wildcard), the field-constraint panel showing the union of the members' `[PolicyField]` fields — a pattern contributing the fields of what it covers today — with per-member binding indicators. Each policy carries a meaningful free-text **Name** — the policy table doubles as living documentation of the security system; auditors read names, not JSON. Tree: Area → Feature → message-disguised-as-permission, message display names via localization keys (missing key shows raw key, house policy). Per-field constraint editors enabled by attribute discovery (server-side reflection is fine here — the generator-emitted accessors are only for the evaluation hot path). Message list derived at runtime from registered `[Http]` messages — no maintained catalog.

Field constraints in the editor:

- **`PolicyConstraint` travels on the wire**, unchanged — `CreatePolicy`/`UpdatePolicy` and
  `PolicyModel` carry `Fields: Dictionary<string, PolicyConstraint>`, keyed by the message
  field's wire name. The self-discriminating converter is attached to the type, so the
  compact shape an author writes and the canonical shape a row holds are one vocabulary on
  both sides of the wire, and its rejection battery guards the HTTP door for free.
- **The update is authoritative**: an omitted constraint is a removal (absent-means-keep
  could not express "this field is free again").
- **`PolicyHandlerFactory.Fields` carries each field's `RestrictAs`**, and
  `SecurityManager.GetConstrainableFields(keys)` answers the union for a policy's members.
  The editor offers only what the axis can answer — `@me` on an organization field is
  unauthorable, a grant the evaluator could only ever deny.
- **`ValidateDocument` refuses the two half-authored shapes** the converter refuses at load —
  a literal list with no value, a relation with no role — in front of the person who can
  still fix them, instead of arriving later as a quarantined row.
- `relatedAs` stays the one shape no client decides: the panel renders a relationship-role
  picker, the optimistic provider passes it, and the server gate answers.

Proof (`NSail.Iam.WebApi.Tests`): the `PolicyGate` collection — the fixture that registers
NO policy at all, so a denial it observes is the stored constraint's and never an absent
grant (the `IamHandlers` collection opens the gate with a wildcard built-in, right for
handler doctrine and fatal for this).

## Case battery (studied)

Fits: receptionist same-org CRUD; global root; doctor filtering own prescriptions (`Doctor: @me`); doctor creating for own patients (`PatientId: related as Doctor`); tutor→minor; nurse by ward (org axis); **external auditor** (audience org Y ≠ literal field/org X — the reason audience and fields are separate blocks); feature wildcards, constrained or flat; break-glass wide grant (auditing is logging's concern); multi-org users (per-membership + `@current`).

Constant vs variable: `@current` is the parametric policy (one row covers every org, new orgs need no policy work); literal org ids are pinned exceptions (auditor, deliberate restriction). Not redundant — absolute vs relative paths.

Settled: empty audience = anonymous, allowed with a loud editor warning; validity period = yes (`validFrom`/`validTo`); identity object = `Session`; field attribute = `[PolicyField(RestrictAs.*)]`; generator target = `Policies.Handlers`; entity contracts (`IOwned`/`[Owner]`) = none; `PolicyHandler<T>` = rejected. `hasRole` stays: the System Administrator row shows where it meaningfully differs from `memberOf @current`.

**No deny.** The motivating case (freeze prescription 123 during a dispute) doesn't work as a deny in the input-only model: a deny on `GetPrescription.Id = 123` doesn't reach lists (the id isn't in their input) → half-protection. A record freeze is domain state — Locked flag with reason, checked by write handlers, visible in UI, auditable — which does more and doesn't hole the model.

Known-outs (deliberate, workaround named): no "everything except X" (no denies — grant what is allowed); no delegation chains (one relationship hop — workflow creates the direct relation, or explicit grant); no payload/business conditions ("discounts up to 10%" → handler, or PolicyHandler<T>); no environment conditions (time/IP → PolicyHandler<T> if ever); no per-row deny (VIP records = sensitivity flag + business filtering).

## Relation to features/metadata

Policy keys are canonical `{Area}.{Feature}.{Object}` — note `KeyFor` renders `{Area}.{Object}` (localization); permissions need the feature segment for wildcards → its own rendering on MetadataProvider (separate renderings; don't couple formats). The generated feature-constants list stays deferred until writing `"Directory.Parties"` strings by hand hurts.

## Implementation status

- **Stored policies are live**: the `Policies` table (`NSail.Iam.Entities.Policy`) is read per
  tenant by `PolicyStore` and fed to `DbSecurityManager`, which Iam registers in place of the
  plain manager; `StoredPolicyHandler` answers the admin UI's messages.
- **Admin UI**: `PoliciesPage`, `CreatePolicyPage`, `UpdatePolicyPage` (`NSail.Iam.Shared`,
  Authorization).
- **`[PolicyField]` marks** ride kit and app messages alike (`CreatePrescription.PatientId`,
  `CreateMembership.RoleId`, …); the audience vocabulary — `is`, `hasRole`, `memberOf` with
  `Cascade`, `authenticated` — is evaluated in full.
- **The client** loads the effective set `GetEffectivePolicies` returns — built-in and stored
  rows whose audience matched — through the same `LoadPolicies` seam.
- **Visibility wiring**: `[Authorize<T>]` on pages, read through the public `RouteTable` by the
  router and the menus; the org switch is `SwitchOrganization` from `SessionMenu`.
- **Deferred**: the custom `[Injectable]` PolicyHandler source (the manager aggregates
  built-in, stored and dependency handlers only), and the generated feature-constants list.

# Naming

How types are named in NSail. The stack fuses identity and payload in one type (a message
is both the operation's name and its data; a settings POCO is both the storage key and the
shape), so names are load-bearing: each family has a fixed grammatical form, and each form
has exactly one meaning.

---

## The registry

One suffix, one meaning. A form listed here is owned by its family across the whole stack.

| Form | Means | Examples |
|---|---|---|
| imperative verb | Mediator message / operation | `CreateParty`, `ListParties` |
| past participle | published event (something happened) — the home says who it is for: a UI event lives in the kit's Shared, a domain event another module handles lives in the Sdk, both without `[Http]` (messaging.md) | `PartySaved`, `AppointmentCompleted` |
| `ISaved` | a `*Saved` event carrying the id of the row it saved (`NSail.Messaging`) — lets a control subscribe to "my concept was created" without a per-entity accessor | `PartySaved : ISaved` |
| `Ns*` | Blazor component — Stack, or a domain-free infrastructure kit (Assets, Settings): an image is not a domain concept, a Party is, so `NsImage` may live in a kit and `PartyLookup` stays unprefixed | `NsButton`, `NsActionToolbar`, `NsImage` |
| `*Item` | contributed entry — POCO, no behavior | `NavMenuItem`, `SettingsItem`, `ActionItem` |
| `*Entry` | flat declaration — a row naming its own parent and its order — that resolves into a `*Item` tree; the shape a stored arrangement could later be read into | `NavMenuEntry` |
| `I*Contributor` | a module contributes entries to a point | `INavMenuContributor`, `IActionContributor<TOutlet>` |
| `Actions` | action contributor class, one per contributing feature folder — named by the contribution alone, never the target ([below](#contributor-classes-are-named-by-the-contribution-alone)) | `Prescriptions.Actions` |
| `Contributed` | the ONE class a feature keeps when its outbound contributions span more than one contributor interface — any mix of targets or kinds; it marks that these are things happening to other modules' screens. A single-interface feature keeps the kind's own name (`Actions`, `Cards`); growing a second interface renames to `Contributed`, and the rename is information, not churn | `Notes.Contributed` |
| `Cards` | dashboard-card contributor class — `IDashboardContributor<TSubject>`, same bare-name rule as `Actions`; lives in the contributing feature folder, or at the Shared root when the cards belong to the project as a whole. The subject-less app dashboard's contributor stays `Dashboard` | `Customers.Cards` |
| `Onboarding` | onboarding-step contributor — `IOnboardingContributor`, at the Shared project root beside `Menu.cs` | `Onboarding` |
| `Guide` | guide-chapter contributor — `IGuideContributor`, at the Shared project root beside `Menu.cs`, same bare-noun rule as `Onboarding` | `Guide` |
| `I*Source` | feeds a manager; sources merge in registration order | `IStringSource` |
| `IValidator<TMessage>` / `*Validator` | a save-time condition a module imposes on a message it does not own — returns the `Problem` that refuses the send, null to allow. The send pipeline runs them innermost: after every interceptor, before the handler (messaging.md) | `ProductTypeValidator : IValidator<UpdateProduct>` |
| `*Manager` | imperative instance service | `DialogManager`, `StringManager`, `SettingsManager` |
| `*Provider` | supplies a value; replaceable, async-friendly | `IBrandProvider`, `MetadataProvider` |
| `*Context` | stateful plumbing cascaded to components, or the ambient state of one operation | `SurfaceContext`, `SettingsContext`, `MessageContext` |
| `*Accessor` | reads an ambient the framework wrote, and nothing else — the `IHttpContextAccessor` shape: injected, virtual so a test hands over a fixture, with no way for user code to write what it answers | `MessageContextAccessor` |
| `*Settings` | settings POCO; storage key derives from the type | `ThemeSettings` |
| `*EventArgs` | component event payload | `QueryEventArgs`, `ProblemEventArgs` |
| `*Icons` | semantic icon catalog — the Stack's generic vocabulary, a kit's for what it speaks and no product owns (a shared domain concept, or a vendor's own mark), an app's for its own domain; one drawing has one entry across all of them (the `new-icon` skill) | `NsIcons`, `ProductsIcons`, `OpticalIcons` |
| `Glyph` | one entry of an icon catalog — the SVG markup a control draws | `NsIcons.Add` |
| `Surface` | one of the app's browsing contexts, named — a surface, a reserved link target, or a browser target | `Surfaces.Aside` |
| `*Job` | contributed unit of recurring background work — implements `IBackgroundJob`, registered with `AddBackgroundJob<T>()`; the runner runs it on its own `Interval` under `Session.System()`, once per tenant or once for the install as its `Tenancy` says | `HorizonJob` |
| `*Row` | Sdk listing projection — what fills a grid's `TRow` | `PartyRow` |
| `Packed*` | one row of a data document a kit ships and plants (`packs/`), read before any database holds it — neither an entity nor a projection, so it never takes the entity's name or a projection's suffix | `PackedCity` |
| `*Ref` | Sdk minimal reference — `Id` + display; what lookups return and other entities embed (presented via `Option<T>`, which is UI) | `PartyRef` |
| `IRef` | the `*Ref` family's identity half (`NSail.Types`), implemented by any Ref a generic control reads the key from. `Id` only — a Ref's display member is named for its own concept (`DisplayName`, `Name`, `Code` + `Name`), so identity is the one shape they all share | `PartyRef : IRef` |
| `*Model` | Sdk full projection — the form/detail shape | `PartyModel` |
| `*Lookup` | kit control that picks one entity from an unbounded set — search-first (`Lazy`), inherits `NsLookupBase` and wraps `NsAutocomplete`, contributing only the entity's message, saved event, create page and filters | `PartyLookup` |
| `*Select` | kit control that picks one entity from a bounded reference set — loads all options eagerly and may default itself (active period, settings currency); wraps `NsSelect` for tiny sets or non-`Lazy` `NsAutocomplete` when browsing wants a filter | `CurrencySelect`, `AccountSelect` |
| `*Outlet` | kit action outlet — the typed slot actions are contributed to; a component inheriting `NsActionOutlet<TSelf, TModel>`, founded by the projection's owner in its Shared. `IActionOutlet` is the marker that closes the contributor constraint (`where TOutlet : IActionOutlet`) — a bare marker is legitimate only where the type system forces it, as here: an open generic base cannot be a constraint | `PartyRowOutlet` |

`{Kit}Capabilities` — provider-kit composition options (`AppleCapabilities`, `GoogleCapabilities`); the capability word is doctrine (registering a capability grants a door).

`ISignUpProvider` — joins `I{Verb}Provider` beside `ISignInProvider`: the provisioning seam AuthenticationHandler consults when no Login row exists.

---

## The rules

1. **One suffix, one meaning.** Each form in the registry is owned exclusively by its
   family. A candidate that collides with an existing family loses, no matter how well it
   reads in isolation (`*Model` for anything but Sdk models, `*Context` for data POCOs,
   `Input`/`Focus`/`Frame` anywhere — they belong to the DOM).
2. **A new name joins an existing family first.** If the concept already has a form in the
   registry, use that form without debate.
3. **A genuinely new concept founds a family.** Add the row to the registry in the same
   change. Founding is expensive by design — it should be rare.
4. **Test the three positions before accepting.** The name must read well where it is
   declared, where it is consumed, and where it is constructed. A plural that reads as a
   collection when it isn't one fails position one.
5. **When nothing can be named, the thing is wrong.** Naming rounds that converge on
   nothing are a design smell, not a thesaurus problem — step back and question the
   concept.

### Wiring files are named by the capability, never by the mechanism

The files at a project's root that wire it into the app — composition entries and
`[Generated]` holders — are named by **what the project contributes**, one platonic noun,
never by how ("Setup", "Handlers" are mechanism words and are banned in these names):

| Project | Files |
|---|---|
| Sdk | `Clients.cs`, `Permissions.cs` |
| WebApi | `Api.cs` (composition entry), `Endpoints.cs`, `InProcessClients.cs`, `Dependencies.cs` |
| Shared | `Screens.cs` (composition entry), `Menu.cs`, `Onboarding.cs`, a root `Cards.cs` when the project contributes project-wide cards. `Onboarding` knowingly bends the namespace-shadowing rule below: areas keep an `Onboarding/` folder of step screens, and the root class collides with that namespace segment — resolved by the step pages declaring `@namespace` of the project root, so the folder stays physical-only (accepted cost of the name; copy that pattern when it recurs, do not rediscover the collision) |
| Data | `Persistence.cs` (composition entry), `Mappings.cs` |
| App Web host | `Web.cs` (composition entry) |
| App Wasm host | `Browser.cs` (composition entry) |

The class matches the file, and the extension method keeps the kit in its name
(`AddDirectoryPermissions`, `AddDirectoryData` — method names don't change with the file).
The composition entry is named by **the capability the project contributes** — its API,
its screens, its persistence, its host — not by a uniform word: a uniform `Services.cs`
collides with the `Services` annotations enum that every `Dependencies.cs` names in
`[Generated(Services.Registration)]`, and creates homonym classes across a kit's projects,
where `typeof(X)` anchors silently resolve to the wrong assembly. Distinct names kill both
structurally. `DbContextSetup` is not a wiring file (it implements `IDbContextSetup`) and
keeps its name.

**A wiring class must not share a name with an NSail namespace segment or an annotations
type** — the enum/namespace shadowing rule (permissions.md) extended to these classes. It
is why the permissions holder is `Permissions` and not `Security` (shadows
`NSail.Security`) nor `Policies` (collides with the generator target it declares), why the
mapper holder is `Mappings` and not `Mapping` (shadows `NSail.Mapping`) nor `Mappers` (the
enum it declares), and why
the Wasm entry is `Browser` and not `Wasm` (`Wasm` is a segment of
`NSail.BaseServices.Wasm`). **One deliberate exception: the app hosts' `Security.cs` — the
class is only ever reached through extension-method syntax, so the shadow never bites. It
does not license a second one.**

**Assembly anchors point at a type that exists only there.** `AddStringsFromAssembly
(typeof(...).Assembly)` and friends must anchor on a type unique to the target assembly —
with per-capability wiring names this holds by construction, and it is the rule that makes
a wrong-assembly resolve impossible rather than quiet.

### Contributor classes are named by the contribution alone

`Actions`, `Cards`, `Menu`, `Onboarding` — never by the target. The namespace already names
the feature, and the contributor interface names the target
(`IActionContributor<PartyRowOutlet>` says "party rows" better than a prefix could).
`Prescriptions.Actions` and `Appointments.Actions` coexist; `PartyActions` repeats what the
interface declares, and `PrescriptionPartyActions` also stutters against the namespace. A
feature whose contributions span **more than one** contributor interface keeps **one** class
implementing them all, named `Contributed` — the merge is the rule working, not a
workaround. Where the file lives: structure.md (A contributor lives with the feature that
contributes it).

### Plural is the catalog, singular is the component

A catalog of constants takes the plural (`NsIcons`, `OpticalIcons`); the singular is
reserved for the `Ns*` component that renders one of them (`<NsIcon Icon="@NsIcons.Add" />`).
The two cannot share a name — a static class and a component in the same namespace
collide — so the plural is what keeps the component name available. A catalog genuinely
*is* a collection, so the plural reads correctly in all three positions.

### What a catalog hands out is a type, never a string

`NsIcons` hands out a `Glyph`, `Surfaces` hands out a `Surface` — the entries are typed
values, not `const string`. This is not decoration: **Razor compiles a quoted attribute
value as C# for every parameter type except `string`, and as literal text when the
parameter *is* a `string`.** So `Icon="NsIcons.Add"` with the `@` forgotten binds the
literal characters `NsIcons.Add` on a `string` parameter — no error, no warning, wrong
only at runtime — while the same mistake against a typed parameter does not compile.

Hence: **a parameter whose value has a real type behind it (an icon, a surface, a page,
an id, an enum member) is declared as that type.** A parameter that carries genuinely
arbitrary text a human types — `Label`, `Placeholder`, `Helper`, `Title`, `Class`, `Alt`,
`Href` and other free URLs — is correctly `string` and stays one. When the value can be
*derived* rather than typed, derive it: `NsRouter` names a sign-in **page** and asks the
route table for the path, instead of taking one.

Where the catalog cannot reach the declaration — a `Glyph` on an Sdk projection crossing
HTTP — the type carries a converter that keeps the wire shape it replaced, so typing the
parameter costs no payload change.

### Query verbs pair with projections

The message verb set is closed — `Get` / `List` / `Lookup` / `Create` / `Update` /
`Delete` (no `Save`: create and update are specific contracts so an update can't carry
immutable fields — permissions.md). The one exception is a message that exists only to
carry a screen's permission (permissions.md, the empty `IMessage`): it takes the screen's
verb (`ViewGuide`) and has no projection. Each query verb owns its result shape, so
knowing the consumption names both types:

| Consumption | Message | Returns |
|---|---|---|
| grid / table | `List{Entity}s` | `{Entity}Row` (paged) |
| dropdown / autocomplete | `Lookup{Entity}s` | `{Entity}Ref` |
| form / detail | `Get{Entity}` | `{Entity}Model` |

`Create{Entity}`/`Update{Entity}` carry their own flat fields — duplicated on purpose,
never composed from a shared payload: the contracts must be free to differ, and
`[PolicyField]` marks flat fields. Search is a filter *inside* `List{Entity}s`, not a
verb. These projections are also what action outlets are founded on (`PartyRowOutlet` for
grid rows), and `IActionContributor<PartyRowOutlet>` is how a module reaches them — see
intentional-ui.md and its `ui/actions.md`.

### No `Async` suffix without a sync twin

The `Async` suffix exists to disambiguate two versions of the same method (`OnCreated` /
`OnCreatedAsync`). When no synchronous version exists — nor plausibly ever will — the
suffix is noise: `Load`, not `LoadAsync`. The return type already says it's awaitable.
The stack's own services set the precedent (`Mediator.Send`, `Runner.Run`,
`SettingsManager.Get`, `DialogManager.Confirm`). Framework overrides keep their inherited
names (`OnParametersSetAsync`, `InvokeAsync`).

Two lessons worth keeping: attributive nouns are singular even when the referent is plural
(`ActionItem`, `IActionContributor` — like `ActionResult`, not `ActionsItem`); and
sometimes the best name is no type at all — before founding a family for a new abstraction,
check whether an existing published type (an Sdk model) already is the contract.

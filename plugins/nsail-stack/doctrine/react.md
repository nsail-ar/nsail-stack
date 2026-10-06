# React clients

How an NSail host ships a React client instead of (or beside) a Blazor one, and keeps the
Stack's promise that the API is declared once. The example is the Stack's Sample
(sample.md (`src/Stack/Sample/docs/sample.md`, in the Stack's own repository)), whose host serves a Blazor and a React client over one API; this page is the mechanism.

---

## Why React

React is the Stack's client for **public sites**; Blazor stays the client for the
back office. A public page cannot open behind a loading bar — a visitor leaves before a
runtime downloads — and it needs what an ERP never asks for: showcase pieces (a carousel,
a hero, a gallery) beside forms that talk to the Sdk, and sometimes a cart. So the React
side is judged by what a public site needs, not by parity with `NSail.Components.Mud`:
HTML first, JavaScript only where a piece moves, the same message contract underneath. A
cart is domain — a kit's, never the Stack's; the Stack owns the pieces and the contract.

---

## The contract is still the Sdk

The browser speaks to the host through **`sdk.ts`, which the Sdk writes on every build of
its own**, through `NSail.TypeScriptGenerator` (`src/Stack/Generation/`). Nothing on the
TypeScript side declares a route, a binding, a bound or a label key: a route string, a
query name or a `maxLength` typed in a `.tsx` is a second copy of the message, and the
generator exists so there is none.

For every `[Http]` message the file carries two declarations under one name — TypeScript
keeps types and values apart, so they coexist:

```ts
export interface CreateContact { id: string; name: string; email?: string | null; … }
export const CreateContact = defineMessage<CreateContact, void>({ method: "POST", path: "api/sample/contacts", fields: { … } });

await mediator.send(CreateContact, { id, name });   // reads like the C# send
```

- **The interface is the request.** A member is mandatory only when the message cannot do
  without it — `required`, `[Required]`, or a route token; every other member is optional,
  because absent means "keep the message's own default" (generation.md, The message owns
  its defaults). A nullable member is `| null`.
- **The descriptor is what the runtime reads**: per member its wire kind, its localization
  key (`MetadataProvider.KeyFor`, so `Sample.CreateContact.Name` is the key Blazor would
  use), its binding (HttpModelFactory's rules, rule for rule) and the message's
  DataAnnotations (`required`, `maxLength`, `minLength`, `min`/`max`, `format`, `pattern`).
  An attribute is read by constructing it from the arguments the source wrote, so a Stack
  subclass that fixes its own bound (`[SearchTerm]`) answers that bound. A rule that names
  its own code (`ICodedValidation`), or one the tool cannot construct (a kit's own
  attribute), is listed in `serverRules` and left to the server: drawing it locally under
  the BCL rule it may subclass would show a sentence the server never answers.
- **Models and enums travel too.** Every type a message reaches is emitted: a model as an
  interface plus a `defineModel` descriptor (keys and kinds for a table header and a cell),
  a generic definition (`DataPage<T>`) as a generic interface, an enum as a union of its
  names (the wire's shape, generation.md) plus a `defineEnum` with its key.
- **One namespace.** Two reachable types sharing a simple name fail the build
  (`NSTS001`), as does a route token no member binds (NSG001's twin) — a generator that
  guessed would emit a client that compiles and calls the wrong thing.

### Where it comes from: the Sdk's own compile, read with the C# generator's model

The TypeScript client is the twin of the C# one. An Sdk that ships it imports the
generator's targets beside its `[Generated(Http.Clients)]` holder; after the Sdk compiles,
its own sources and references are handed to the tool, which rebuilds the compilation and
asks **`HttpModelFactory`** — the model the C# client is rendered from — which messages the
holder carries and where each member binds. So the two clients cannot disagree about a
message, a route or a binding; the tool adds only what C# never needed spelled out (wire
types, keys, client-side rules). No host is started and no assembly is loaded.

```xml
<PropertyGroup><TypeScriptOutput>../NSail.Sample.React/src/sdk.ts</TypeScriptOutput></PropertyGroup>
<Import Project="…/NSail.TypeScriptGenerator/NSail.TypeScriptGenerator.targets" />
```

- **A compile that does not resolve is refused** (`NSTS001`, naming the first errors),
  never read as an Sdk with no messages — an empty client that builds is a failure nobody
  sees until a page calls nothing. The one error ignored is `CS8795`: a `[Generated]`
  holder's body is the C# generators' output, which the sources handed over do not carry.
- **Keys follow `MetadataProvider`'s convention** (its own `Parse`/`Bind` over the declaring
  assembly's `[assembly: MetadataTemplate]`), so they match runtime keys exactly as long as
  no host overrides `MetadataProvider.Get` — the same compile-time asymmetry metadata.md
  accepts for `HttpClient` names.
- **The target is incremental** over the Sdk's sources, references, project file and the
  tool itself, and an unchanged `sdk.ts` is never rewritten, so a dev server is not
  reloaded by a build that changed no contract.

`sdk.ts` is gitignored, like every other generated file.

---

## The packages

One npm workspace at the repository root (`package.json` lists its members). The Stack's two
packages live in `src/Stack/React/`, split where the vendor starts — the line
`NSail.Components` / `NSail.Components.Mud` draws in C#:

| Package | Folder | Holds |
|---|---|---|
| `@nsail/stack` | `src/Stack/React/NSail.React.Stack` | Vendor-free: the descriptors, `Mediator.send`, `Problem`, the client half of `MessageValidator`, `Strings`, and the hooks a page reads with (`useMediator`, `useStrings`, `useCatalog`, `useLoad`, `useForm`) |
| `@nsail/ui` | `src/Stack/React/NSail.React.Ui` | The `Ns*` components and `ns.css` |

A kit or app package imports both; nothing below `@nsail/ui` names a component.

**`Mediator.send(message, request)`** builds the request exactly as `HttpSender` does
(route tokens, query members with an unset filter left out and a collection repeated, body
members as JSON, header members as headers), and refuses an invalid request **before**
anything leaves, with `MessageValidator`'s own codes, sources and arguments — so the
sentence under a field is the same whether the browser or the server refused it. A failure
is always a `BusinessError` carrying a `Problem`: the server's own when it sent one,
`RequestFailed` with `NetworkProblem`'s issue codes when it did not.

| Piece | Package | Does |
|---|---|---|
| `NsApp` | ui | `StackProvider` plus the catalog read before the first paint (`useCatalog`) and the one confirmation surface |
| `Strings` | stack | `StringManager`'s resolution: a miss renders the key; `Problems.{Code}.{Source}` → `Problems.{Code}` → the message; the `entity` argument is a key |
| `useLoad` + `NsLoad` | stack + ui | A region owns its read (convert-to-nsload's contract): a spinner the first time, the last answer dimmed while it re-reads, a failure in place with a retry — never a toast |
| `useForm` + `NsForm` + `NsField` | stack + ui | A form over one message: `NsField name="…"` picks its input from the member's kind, its label from the member's key, its bounds from the message; refusals are checked as the user types and the server's land under the field they name; anything no field can show is drawn above the fields; `onReload` is offered only for a `Conflict` |
| `NsTable`, `NsPager`, `NsSearch`, `NsEnumSelect` | ui | A projection's rows with headers from keys and cells formatted by kind; a `DataPage`'s pager; a debounced search bound by its member; an enum pick whose options come from the descriptor |
| `NsPage`, `NsButton`, `useConfirm` | ui | A titled page; a button whose `text` is a key, never a sentence; one native `<dialog>` for confirmations |

**The Stack's sentences are the Blazor catalog's.** `Strings` reads `Common.*`,
`Actions.*` and `Problems.*` from the very `strings*.json` that
`NSail.Components.Mud` embeds — imported, never copied, so one sentence lives in one file
whichever renderer draws it. The app's own catalog arrives from its host (the Sample's
`GetStrings`) and is merged over it, as an app's strings registered last win on the
server. Every label a component draws is a key; a literal sentence in a `.tsx` is the
same defect it is in Razor.

**Routes are a table.** The client declares each address once (`src/routes.ts`) and a link
asks it by name; a path literal in a screen is a defect, as in Razor.

---

## Building and serving

The Sdk's build writes `sdk.ts`; the host's build then — when the root `node_modules`
exists — runs the client's `npm run build` (`tsc --noEmit && vite build`) into a folder beside
the host, which is gitignored — never `wwwroot`, which a host that also serves Blazor gives to
that client's static assets. Without `node_modules` (a CI runner) the host still builds and its React address says
the client is missing; nothing else in the solution depends on the client. The host
serves the files under the client's own path and answers `index.html` for every other address under it, so the client
router owns the paths and an unknown message keeps its bare 404.

`npm test` and `npm run typecheck` at the root run every workspace's suite. Proof of the
generator: `NSail.TypeScriptGenerator.Tests`, which compiles fixture Sdks from source the
way the targets hand a real one over.

---

## Not yet

- **Sign-in.** The Sample composes no Iam; its messages are open through an anonymous
  built-in policy, with the gate on. A client of a host that composes Iam needs the
  session, the cookie and the 401 → sign-in turn on the TypeScript side first.
- **Publish.** The client is built beside the host by `Build`, and nothing copies it into a publish yet; a `dotnet publish` that
  should carry it has to run after that build on a machine with the workspace installed.
- **Coded rules** are judged by the server only (above).
- **Public-site pieces.** `@nsail/ui` mirrors the back office's `Ns*` family; the
  showcase pieces a public site needs (carousel, hero, gallery) and server-rendered pages
  are not there yet.
- **One SDK package per kit.** An Sdk writes one `sdk.ts` where it is told; a kit
  composed by several apps would publish `@nsail/{kit}-sdk` instead, and two such files
  each carrying a shared model (`DataPage<T>`) still declare it once apiece.

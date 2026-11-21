# React

A second client over the same messages, not a second API. The Sdk writes a TypeScript contract
from its C# messages on every build; two npm packages send those messages, draw their refusals
with the server's own sentences, and ship the `Ns*` building blocks of a React page, drawn with
MUI and themed by the install's `Brand`. Nothing on
the TypeScript side declares a route, a binding, a bound or a label key.

## Packages

Both are npm packages (not NuGet), released at the same version as the NuGet packages.

| Package | Folder | What it is | Peer dependencies |
|---|---|---|---|
| `@nsail/stack` | `NSail.React.Stack` | Vendor-free core: descriptors, `Mediator`, `Strings`, `Problem`/`BusinessError`, client-side validation, `Brand`, `brandScheme`, `brandMix` and `brandWeights`, and the hooks `useMediator`, `useStrings`, `useCatalog`, `useLoad`, `useForm`, `useSession` | `react >= 19`, `react-dom >= 19` |
| `@nsail/ui` | `NSail.React.Ui` | The components (`NsApp`, `NsLoad`, `NsSignedIn`, `NsForm`, `NsField`, `NsTable`, `NsNavMenu`, `NsCard`, ...), drawn with MUI; depends on `@nsail/stack`, `@mui/material` and Emotion | `react >= 19`, `react-dom >= 19` |

Only `@nsail/ui` imports `@mui/*` or `@emotion/*`: your screens import the `Ns*` pieces and never
the vendor, and the package ships no stylesheet to import.

The contract itself comes from the Sdk, through the build-time package `NSail.TypeScriptGenerator`
(`PrivateAssets="all"`; no `lib/`, no runtime dependency). Your client also needs `react`,
`react-dom` and a router (the Sample uses `react-router`).

## Use it

In the Sdk project, name where the contract is written; every build of the Sdk rewrites that file:

```xml
<PropertyGroup>
  <TypeScriptOutput>../Acme.Tasks.React/src/sdk.ts</TypeScriptOutput>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="NSail.TypeScriptGenerator" Version="..." PrivateAssets="all" />
</ItemGroup>
```

In the client, install `@nsail/stack`, `@nsail/ui` (same version as the NuGet pin), `react`,
`react-dom` and a router, then:

```tsx
import { Mediator } from "@nsail/stack";
import { NsApp } from "@nsail/ui";
import { GetStrings } from "./sdk";

const mediator = new Mediator();

createRoot(document.getElementById("root")!).render(
  <NsApp mediator={mediator} language="es" strings={(lang) => mediator.send(GetStrings, { language: lang })}>
    <RouterProvider router={router} />
  </NsApp>,
);
```

`GetStrings` is a message you declare in your Sdk whose handler answers the host's merged catalog
for a language (the Sample's `GetStrings` and `StringsHandler`). A page reads, sends and renders:

```tsx
const contacts = useLoad(ListContacts, filter); // re-reads when `filter` changes by value

<NsLoad load={contacts}>
  {(page) => <NsTable model={ContactRow} rows={page.items} rowKey="id" columns={["name", "email"]} />}
</NsLoad>

await mediator.send(DeleteContact, { id }); // validated locally first
```

## Configuration

**The contract.** For every `[Http]` message `sdk.ts` carries an interface (the request; a member
is mandatory only when the message cannot do without it) and a `defineMessage` descriptor with the
same name: method, path, and per member its wire kind, localization key, binding and the
DataAnnotations (`required`, `maxLength`, `minLength`, `min`/`max`, `format`, `pattern`).
Rules the generator cannot construct are listed in `serverRules` and left to the server. Models
become interfaces plus `defineModel`, enums a union of names plus `defineEnum` (an empty enum
`never`, a `[Flags]` enum `string`). Two reachable types with the same simple name fail the
build (`NSTS001`).
A `[RegularExpression]` using a .NET-only construct (`(?i)`, `\A`, atomic groups) warns `NSTS002`
and is left to the server; the client compiles each pattern once, with the `u` flag, retrying
without it only when the pattern has no `\p{}`, `\P{}` or `\u{}`.

**`Mediator`** (`new Mediator(options?)`):

| Option | Default | Meaning |
|---|---|---|
| `baseUrl` | `""` | Origin the API lives at; empty is the host that served the page. |
| `fetch` | global `fetch` | Replaceable transport. |
| `headers` | none | Function returning headers every send carries (language, tenant); read per send. |
| `signIn` | none | Sign-in address on the page's host (from your route table). A send answered 401 turns the page there once, carrying where the visitor was on `returnUrl`, and `staleCredential=1` when the 401 met a signed-in session (`reportSession`, which `NsApp` calls from its `session`); not read through the router's `basename`; absent, a 401 is a refusal like any other. A 403 never turns. |
| `location` | the page's `location` | Where the visitor is and how the turn is made; replaceable for tests. |

`send(message, request, { signal? })` builds the URL (route tokens, query members, repeated
collections), body and headers as the C# `HttpSender` does, refuses an invalid request before it
leaves, and throws a `BusinessError` carrying a `Problem` on any failure. Every send leaves
`credentials` at `same-origin`: the session is the host's HttpOnly cookie, never a token the client
holds.

**`NsApp`** props: `mediator`, `language`, `strings?` (loader of the app's catalog, merged over
the Stack's own sentences; the app's keys win), `session?` (the Sdk message that answers
`NSail.Security.Session`, such as Iam's `GetSession`), `brand?` (the install's `Brand`, below),
`dark?` (the scheme to draw; unset follows the device's `prefers-color-scheme`), `children`. With `session`, `useSession()`
answers who is signed in as a `Load`; call its `reload()` after the client signs in or out. A failed catalog read still renders,
with keys. `useCatalog` and `StackProvider` are the pieces underneath when you need your own root, but
only `NsApp` lays the `Brand`'s theme: a root of your own draws the `Ns*` pieces with the vendor's
palette.

**Components** (`@nsail/ui`):

| Component | Props worth knowing |
|---|---|
| `NsLoad` | `load` (from `useLoad`) and a render function over the data; spinner the first time, dimmed while re-reading, failure in place, with retry where a second try can pass |
| `NsSignedIn` | `anonymous?` (drawn when nobody is signed in); the content is drawn only once the session is read and signed in, and nothing from a failed read (wrap `useSession()` in `NsLoad` to draw the failure) |
| `NsForm` + `useForm` | `form`, `onSubmit`, `onReload?` (offered only for a `Conflict`); `NsSaved` shows the saved state, which `form.reset(value, { keepSaved: true })` keeps over the re-read of what was just sent |
| `NsField` | `name`, `multiline?`, `autoFocus?`, `label?` (a string key); input, label and bounds come from the descriptor |
| `NsTable` | `model`, `rows`, `columns`, `rowKey`, `onRowClick?` (rows also open from Enter and Space), `actions?`, `empty?` (a key), `ungrouped?` (columns shown without digit grouping, for numbers that are identifiers) |
| `NsPager` | `page` (`pageIndex`, `pageSize`, `totalCount`), `onPage` |
| `NsSearch` | `field`, `value`, `onSearch`, `delay?` (300 ms) |
| `NsEnumSelect` | `field`, `value`, `onChange`, `none?` (a key, default `Common.All`) |
| `NsPage` | `title` (a key), `actions?` |
| `NsButton` | `text` (a key, never a sentence), `variant?` (`default`, `primary`, `danger`, `quiet`), `busy?` |
| `useConfirm` | `confirm(text, danger?)` resolves to a boolean; one native `<dialog>` hosted by `NsApp` |
| `NsNavMenu` | `label` (a key naming the navigation), `entries` (`text` key, `href` from your route table, `icon?`), `current?` (the href the reader is on), `onNavigate?` (your router's navigate; a plain click is handed to it) |
| `NsCard` | `title?`, `subtitle?` (keys), `aside?`, `actions?`, `children` |
| `NsToolBar` | `children`: a row of controls that wraps |
| `NsList` + `NsListItem` | `primary`, `secondary?` (data, not sentences), `icon?`, `aside?`, `href?` or `onClick?` |
| `NsAlert` | `text` (a key), `args?` (named arguments), `severity?` (`info`, `success`, `warning`, `error`; the last two are read out), `action?` |
| `NsChip` | `text` (a key), `severity?`, `solid?` (the filled face) |
| `NsRadioGroup` | `field` (an enum member), `value`, `onChange`: a segmented choice, one value always chosen |
| `NsEmpty` | `text` (a key), `icon?`, `action?`; what `NsTable` draws for no rows |

**Strings.** `Strings` is `StringManager` for the browser: `translate`, `format` (named or
positional tokens), `count` (`.Plural`), `enumValue`, `issue`, `problem`. A missing key renders as
itself. The Stack's own sentences (`Common.*`, `Actions.*`, `Problems.*`) come from the same
`strings.json` / `strings.es.json` that `NSail.Components.Mud` embeds, so both renderers say one
sentence per key.

**Branding.** `brand` is `NSail.Components.Brand` as the wire carries it (camelCase): `typeface?`
(a key of `Typefaces`, set on the heading ranks; the font files are yours to serve), and `light?` /
`dark?`, each naming only the slots it changes (`accent`, `accentHover`, `accentPressed`,
`accentFocus`, `textOnAccent`, `chrome`, `textOnChrome`, `textOnChromeSecondary`, `rowHover`,
`error`, `warning`, `success`, `info` and their `textOn*`). A slot left out, or not a hex, is that
scheme's default; with no `brand`, both schemes are the Stack's own. Surfaces and inks derive from
`chrome` as on the Blazor side, and so do the accent's inks (`accentInk` on the content,
`accentOnChrome` on the navigation band), which a word in the brand's hue wears instead of the
accent. The defaults are in [brand.json](NSail.React.Stack/src/brand.json); `brandScheme(brand,
dark)` answers a scheme checked and derived, `brandMix` is `BrandTone.Mix`, and `brandWeights`
are `BrandTone`'s weights.

```tsx
<NsApp mediator={mediator} language="es" brand={{ light: { accent: "#0a7d4f" }, dark: { accent: "#5fd39c" } }}>
```

**Development.** Each package's `exports` answers an `@nsail/source` condition with its `src/`; add
it to Vite's `resolve.conditions` (as the Sample does) to run against sources inside this
workspace. Outside it, `dist/` comes from npm. `npm test` and `npm run typecheck` at the repository
root run every workspace.

## How it fits

- [Generation](../Generation/README.md) owns `NSail.TypeScriptGenerator`, which reads the same
  model as the C# client generator, so the two clients cannot disagree on a route or a binding.
- [Messaging](../Messaging/README.md) defines the messages, `[Http]` routes and `Problem`s this
  client sends and renders.
- [Localization](../Localization/README.md) and [Metadata](../Metadata/README.md) are the source
  of the keys and the catalog `Strings` reads.
- [Components](../Components/README.md) is the Blazor counterpart; `@nsail/ui` wraps MUI as it
  wraps MudBlazor, with the same `Ns*` names, the same keys and the same `Brand`.
- [Security](../Security/README.md) is the server half of sign-in: the cookie, `Session` and the
  401 a send without one answers. The Sample composes no Iam, so its messages are open through an
  anonymous policy.

## Going deeper

- Guide: [getting-started.md](../../../docs/getting-started.md), section "A React client, over the same messages"
- Doctrine: [react.md](../../../plugins/nsail-stack/doctrine/react.md)
- Sample: [NSail.Sample.React](../Sample/NSail.Sample.React) (`ContactsPage.tsx`, `ContactPage.tsx`), served by [NSail.Sample.Web](../Sample/NSail.Sample.Web)
- Tests: specs beside each source file (`*.test.ts`); generator tests in [test/Stack/Generation/NSail.TypeScriptGenerator.Tests](../../../test/Stack/Generation/NSail.TypeScriptGenerator.Tests)

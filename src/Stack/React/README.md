# React

A second client over the same messages, not a second API. The Sdk writes a TypeScript contract
from its C# messages on every build; two npm packages send those messages, draw their refusals
with the server's own sentences, and ship the `Ns*` building blocks of a React page. Nothing on
the TypeScript side declares a route, a binding, a bound or a label key.

## Packages

Both are npm packages (not NuGet), released at the same version as the NuGet packages.

| Package | Folder | What it is | Peer dependencies |
|---|---|---|---|
| `@nsail/stack` | `NSail.React.Stack` | Vendor-free core: descriptors, `Mediator`, `Strings`, `Problem`/`BusinessError`, client-side validation, and the hooks `useMediator`, `useStrings`, `useCatalog`, `useLoad`, `useForm` | `react >= 19` |
| `@nsail/ui` | `NSail.React.Ui` | The components (`NsApp`, `NsLoad`, `NsForm`, `NsField`, `NsTable`, ...) and the `@nsail/ui/ns.css` stylesheet; depends on `@nsail/stack` | `react >= 19`, `react-dom >= 19` |

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
import "@nsail/ui/ns.css";
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
become interfaces plus `defineModel`, enums a union of names plus `defineEnum`. Two reachable types
with the same simple name fail the build (`NSTS001`).

**`Mediator`** (`new Mediator(options?)`):

| Option | Default | Meaning |
|---|---|---|
| `baseUrl` | `""` | Origin the API lives at; empty is the host that served the page. |
| `fetch` | global `fetch` | Replaceable transport. |
| `headers` | none | Function returning headers every send carries (language, tenant); read per send. |

`send(message, request, { signal? })` builds the URL (route tokens, query members, repeated
collections), body and headers as the C# `HttpSender` does, refuses an invalid request before it
leaves, and throws a `BusinessError` carrying a `Problem` on any failure.

**`NsApp`** props: `mediator`, `language`, `strings?` (loader of the app's catalog, merged over
the Stack's own sentences; the app's keys win), `children`. A failed catalog read still renders,
with keys. `useCatalog` and `StackProvider` are the pieces underneath when you need your own root.

**Components** (`@nsail/ui`):

| Component | Props worth knowing |
|---|---|
| `NsLoad` | `load` (from `useLoad`) and a render function over the data; spinner the first time, dimmed while re-reading, failure in place with retry |
| `NsForm` + `useForm` | `form`, `onSubmit`, `onReload?` (offered only for a `Conflict`); `NsSaved` shows the saved state |
| `NsField` | `name`, `multiline?`, `autoFocus?`, `label?` (a string key); input, label and bounds come from the descriptor |
| `NsTable` | `model`, `rows`, `columns`, `rowKey`, `onRowClick?`, `actions?`, `empty?` (a key) |
| `NsPager` | `page` (`pageIndex`, `pageSize`, `totalCount`), `onPage` |
| `NsSearch` | `field`, `value`, `onSearch`, `delay?` (300 ms) |
| `NsEnumSelect` | `field`, `value`, `onChange`, `none?` (a key, default `Common.All`) |
| `NsPage` | `title` (a key), `actions?` |
| `NsButton` | `text` (a key, never a sentence), `variant?` (`default`, `primary`, `danger`, `quiet`), `busy?` |
| `useConfirm` | `confirm(text, danger?)` resolves to a boolean; one native `<dialog>` hosted by `NsApp` |

**Strings.** `Strings` is `StringManager` for the browser: `translate`, `format` (named or
positional tokens), `count` (`.Plural`), `enumValue`, `issue`, `problem`. A missing key renders as
itself. The Stack's own sentences (`Common.*`, `Actions.*`, `Problems.*`) come from the same
`strings.json` / `strings.es.json` that `NSail.Components.Mud` embeds, so both renderers say one
sentence per key.

**Styling.** `ns.css` is driven by CSS custom properties (`--ns-bg`, `--ns-surface`, `--ns-ink`,
`--ns-primary`, `--ns-danger`, `--ns-radius`, ...) with a `prefers-color-scheme: dark` set; override
them to retheme. The full list is in [ns.css](NSail.React.Ui/src/ns.css).

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
- [Components](../Components/README.md) is the Blazor counterpart; `@nsail/ui` mirrors its `Ns*`
  family, with the same keys, without Mud.
- Sign-in is not on the TypeScript side yet: the Sample's messages are open through an anonymous
  policy ([Security](../Security/README.md) for the server half).

## Going deeper

- Guide: [getting-started.md](../../../docs/getting-started.md), section "A React client, over the same messages"
- Doctrine: [react.md](../../../plugins/nsail-stack/doctrine/react.md)
- Sample: [NSail.Sample.React](../Sample/NSail.Sample.React) (`ContactsPage.tsx`, `ContactPage.tsx`), served by [NSail.Sample.Web](../Sample/NSail.Sample.Web)
- Tests: specs beside each source file (`*.test.ts`); generator tests in [test/Stack/Generation/NSail.TypeScriptGenerator.Tests](../../../test/Stack/Generation/NSail.TypeScriptGenerator.Tests)

# NSail Stack

A framework for modular monoliths on .NET 10: messaging, data, security, and the Blazor and
React clients, with Roslyn source generation deriving the endpoints, wiring and typed clients
from each message. Products build on it from the NuGet packages this repository publishes; the
Stack names no product and references none.

The architecture is strict and it is written down, in this repository's own Claude Code plugin:
[plugins/nsail-stack/doctrine/](plugins/nsail-stack/doctrine/). **Read the page for the area you
are about to touch before you edit anything** — the routing table is
[plugins/nsail-stack/skills/doctrine/SKILL.md](plugins/nsail-stack/skills/doctrine/SKILL.md),
and it is the same table a product's builder reads through the plugin. A change to a rule
changes its page here, in the same pull request as the code.

## The plugin

`plugins/nsail-stack/` is what a product installs to read this doctrine and use the Stack's
skills (`new-message`, `new-mapping`, `new-icon`, `test-harness`, `doctrine`);
[.claude-plugin/marketplace.json](.claude-plugin/marketplace.json) lists it. A product pins it at
the same `v*` tag as the packages it compiles against, so the pages it reads describe the code
it runs. Pages and skills name kit and app files as examples: those live in the products, not
here.

## Non-negotiables

- **Concision.** Comments and docs say what carries weight and stop.
- **Docs state the current rule and its why** — never who ruled it or when (git has that), and
  never a value that lives in config: name the file that holds it instead.
- **Comments.** Never narrative or temporal — nothing about how the code used to be, what a
  change did, or a session decision. XML `<summary>` only on the public API and only where the
  name is not enough. Inline comments only for invisible constraints. Full rule in
  [structure.md](plugins/nsail-stack/doctrine/structure.md).
- **Method bodies use braces, never `=>`.** Properties may keep `=>`.
- **No vendor types in a public NSail API.** A page never names a `Mud*` type.
- **Nothing is written that can be derived** — routes, localization keys and URLs come from the
  route table and `MetadataProvider`.
- **No product in the Stack.** Nothing here references a kit or an app, or names one in code.
- **New source files carry the license header** (`SPDX-License-Identifier: MIT` and the
  copyright line every file here opens with).

## Build, test, release

```
npm install
dotnet build NSail.sln
dotnet test NSail.sln
```

The data tests need PostgreSQL on `localhost`, user `postgres`, password in `PGPASSWORD`.
`src/Stack/Sample` is the example app ([sample.md](src/Stack/Sample/docs/sample.md)).

Every merge to `main` publishes every Stack project to nuget.org and tags the commit `v<version>`
([.github/workflows/ci.yml](.github/workflows/ci.yml)); a product takes a change by bumping its
pin. Pull requests build, test and pack, and publish nothing.

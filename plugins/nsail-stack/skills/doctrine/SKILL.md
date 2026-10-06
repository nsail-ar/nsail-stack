---
name: doctrine
description: The NSail Stack's written architecture — which page to read before touching a message, handler, entity, migration, tenancy, permission, screen, Ns* component, test, React client, localization key or project layout in an NSail codebase. Use before editing any code that builds on the Stack (kits, apps or the Stack itself), whenever a CLAUDE.md routes "the Stack's doctrine", or when asked how the Stack wants something done.
---

# The Stack's doctrine

The architecture is strict and it is written down. Read the page for the area you are about to
touch before you edit anything — you are not expected to read all of it, you are expected to
read your row. Every page is a file of this plugin, under `${CLAUDE_PLUGIN_ROOT}/doctrine/`.

| You are touching | Read first |
|---|---|
| Anything at all | `${CLAUDE_PLUGIN_ROOT}/doctrine/principles.md` |
| A `.razor`, an `Ns*` component, `ns-mud.css`, a screen | `${CLAUDE_PLUGIN_ROOT}/doctrine/intentional-ui.md` — the rules; it routes to `${CLAUDE_PLUGIN_ROOT}/doctrine/ui/README.md` for a component's own contract (a screen that still reads outside `NsLoad`: the `convert-to-nsload` skill) |
| A message, a handler, an endpoint, an HTTP client | `${CLAUDE_PLUGIN_ROOT}/doctrine/messaging.md` + `${CLAUDE_PLUGIN_ROOT}/doctrine/generation.md` (the `new-message` skill walks it) |
| An entity, `DbContextSetup`, a migration, a seed, the mapper | `${CLAUDE_PLUGIN_ROOT}/doctrine/data.md` (the `new-mapping` skill walks the mapper) |
| Tenancy, the org filter, tenant ids, background jobs | `${CLAUDE_PLUGIN_ROOT}/doctrine/data-tenancy.md` |
| Classifying an entity or a message against the org axis | the codebase's own org map — the law is `${CLAUDE_PLUGIN_ROOT}/doctrine/data-tenancy.md` (The org is the other axis); the map of a codebase's entities and messages lives with them (in nsail, the `nsail-kits` plugin's `org-map.md`) |
| A new type, project, namespace or folder | `${CLAUDE_PLUGIN_ROOT}/doctrine/structure.md` + `${CLAUDE_PLUGIN_ROOT}/doctrine/naming.md` |
| Authorization, policies, `Session` | `${CLAUDE_PLUGIN_ROOT}/doctrine/permissions.md` |
| Localization keys, Swagger tags, `{Area}.{Feature}` | `${CLAUDE_PLUGIN_ROOT}/doctrine/metadata.md` |
| `Program.cs`, `Setup.cs`, host bootstrap | `${CLAUDE_PLUGIN_ROOT}/doctrine/baseservices.md` |
| `Problem`, paging, shared primitives | `${CLAUDE_PLUGIN_ROOT}/doctrine/types.md` |
| A test, a harness, a `*.Tests` project | `${CLAUDE_PLUGIN_ROOT}/doctrine/testing.md` (the `test-harness` skill walks it) |
| A React client, an `@nsail/*` package, `sdk.ts` | `${CLAUDE_PLUGIN_ROOT}/doctrine/react.md` |
| A glyph a screen needs that no catalog carries | the `new-icon` skill |
| Deciding whether to write the code at all | `${CLAUDE_PLUGIN_ROOT}/doctrine/lesscode.md` |

The pages name files of the codebase they were written against: `src/Stack/...` and
`test/Stack/...` are this Stack's own repository; a path under `src/Kits` or `src/Apps` is an
example from a product built on it.

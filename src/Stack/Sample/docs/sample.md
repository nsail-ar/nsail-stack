# Sample

The Stack's own example app: one API and two clients over it, a **Blazor** one and a
**React** one, built from the Stack alone — no kit is referenced, so it travels with the
Stack wherever the Stack goes. The Sdk's `[Http]` messages are the only declaration of the
API: Blazor sends them as C# types, React as the TypeScript the generator writes from them
on every build of the host. Both clients read one string catalog.

The domain is deliberately plain — one `Contact` ABM — chosen to touch every wire kind a form
draws (text, email, phone, enum, date, integer with a range, boolean, multiline) and every
refusal path (client validation, server validation, not found, concurrency conflict).

It lives under `src/Stack` but is written the way a kit or an app is: it is the code a
stranger copies first, so the consumer-side rules (no vendor types, no doc comments, every
endpoint swept by the gate) are enforced over it too (`SourceTree.Sample` in
`NSail.Architecture.Tests`).

No Iam is composed: there is no sign-in. The gate is on all the same — the messages are open
through one anonymous built-in policy (`Policies.cs`), registered on both sides: the
server enforces it, the client draws by it. Pages keep their `[Authorize<TMessage>]`; what
changes is the default page policy, which asks for anyone instead of a signed-in user
(`Screens.cs`). No tenancy either: the install is one database.

## Layout

| Project | Role |
|---|---|
| `NSail.Sample.Sdk` | Writes `NSail.Sample.React/src/sdk.ts` on every build. Messages: `ListContacts`, `GetContact`, `CreateContact`, `UpdateContact`, `DeleteContact`; `GetStrings` (the host's merged string catalog for one language, for the client that runs no .NET) |
| `NSail.Sample.Data` | `Contact` (`IVersioned`), `SampleDbContext`, migrations |
| `NSail.Sample.Wasm` | The Blazor client: layout, menu, home and the three contact screens, the anonymous authentication state, and the app's `strings*.json` — the one catalog both clients read |
| `NSail.Sample.Web` | The host: handlers, generated endpoints, enforcement, the Blazor app at `/` and the React build at `/react` (`MapReactClient`: its files, then `index.html` for every other address under it) |
| `NSail.Sample.React` | The React client (Vite): `src/sdk.ts` is generated, `src/routes.ts` is the route table, `src/contacts/` the two screens |

How the TypeScript side is built — the generator, the `@nsail/*` packages, the npm
workspace — is [react.md](../../../../plugins/nsail-stack/doctrine/react.md).

## Running it

```
npm install                                    # once, at the repository root
dotnet build src/Stack/Sample/NSail.Sample.Web
dotnet run --project src/Stack/Sample/NSail.Sample.Web --launch-profile http
```

Blazor answers at the host's root, React under `/react`. The build writes `sdk.ts`,
typechecks the client and builds it into `react/` beside the host project (gitignored). Without
the root `node_modules` the host still builds, Blazor runs, and `/react` answers a line saying
the client is missing.

For hot reload of the React client, run the host as above and
`npm run dev --workspace=@nsail/sample-react`: Vite serves it on its own port under `/react/`
and proxies `/api` to the host's http port.

## Ports

The sample owns the **41xx** block of NSail's port nomenclature.

| Entry | Port |
|---|---|
| `--launch-profile http` | 4001 |
| `--launch-profile https` (VS F5) | 4000 (https), 4001 (http) |
| Vite dev server | 4002 |

The connection string is `Sample:ConnectionString` (`appsettings.json`: `localhost`, database
`sample_main`, user `postgres`; Npgsql reads the password from `PGPASSWORD`). The first run
creates the database.

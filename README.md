# NSail Stack

A framework for modular monoliths on .NET 10. A message declares an operation once,
and the source generator derives the rest from it: the HTTP endpoint, the
dependency-injection wiring, the typed clients for Blazor and for TypeScript, and the
permission each operation demands. Data runs on EF Core over PostgreSQL. The UI pieces
are Blazor components (MudBlazor underneath) and React packages.

`src/Stack/Sample` is a contacts CRUD app. Its two clients, Blazor WebAssembly and
React, talk to the same API. Start there.

## Status

Pre-release (0.x). Every merge to `main` publishes every Stack project to
[nuget.org](https://www.nuget.org/profiles/NSail) as the next `0.1.<n>` and tags the commit
`v0.1.<n>` ([.github/workflows/ci.yml](.github/workflows/ci.yml)). The source generator
ships as `NSail.Messaging.SourceGenerator`. npm packages come later.

This repository is the Stack's source of truth. Changes land here through pull requests;
the products that consume it pin a published version.

## Claude Code plugin

The Stack's doctrine and its skills ship as a Claude Code plugin in this repository
(`plugins/nsail-stack`, listed by `.claude-plugin/marketplace.json`). A product pins it at the
same tag as its packages:

```bash
claude plugin marketplace add nsail-ar/nsail-stack
claude plugin install nsail-stack@nsail
```

or, for one session from a checkout, `claude --plugin-dir <checkout>/plugins/nsail-stack`.
The skills answer as `/nsail-stack:doctrine`, `/nsail-stack:new-message`,
`/nsail-stack:new-mapping`, `/nsail-stack:new-icon` and `/nsail-stack:test-harness`.

## Build

```bash
npm install
dotnet build NSail.sln
dotnet test NSail.sln
```

The data tests need a PostgreSQL server: `localhost`, user `postgres`, with the password
in `PGPASSWORD` (`PGHOST` and `PGPORT` override the host and port).

## License

[MIT](LICENSE).

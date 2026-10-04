# NSail Stack

A framework for modular monoliths on .NET 10. A message declares an operation once,
and the source generator derives the rest from it: the HTTP endpoint, the
dependency-injection wiring, the typed clients for Blazor and for TypeScript, and the
permission each operation demands. Data runs on EF Core over PostgreSQL. The UI pieces
are Blazor components (MudBlazor underneath) and React packages.

`src/Stack/Sample` is a contacts CRUD app. Its two clients, Blazor WebAssembly and
React, talk to the same API. Start there.

## Status

Pre-release. The packages publish to this repository's GitHub Packages feed as
`0.1.0-ci.*` builds. nuget.org and npm come with the first public release.

The Stack is developed inside a private monorepo. Until the cutover, this repository is
a one-way mirror of it ([tools/sync.ps1](tools/sync.ps1)), so changes to `src/` and
`test/` land in the monorepo first.

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

# Type metadata

Area/feature information for Swagger, logging and tooling derives from the **namespace** — the one metadata that cannot drift from the code (same principle as `ILogger<T>` categories). No `Feature` files, no per-type attributes.

## Two layers: convention and policy

**The convention** is a positional template, resolved per assembly: `[assembly: MetadataTemplate("...")]` declares an assembly's own shape; assemblies without it follow `MetadataTemplateAttribute.Default`. The attribute is the **single source of truth** — no template parameters anywhere (`AddMetadata()` takes none); a company that wants resolution to work differently subclasses `MetadataProvider` from scratch (`Parse`/`Bind` are protected helpers for that).

```
{Root}.{Area}.{Feature}.*.{Object}          ← default
```

- Tokens: `{Root}`, `{Area}`, `{Feature}`, `{SubFeature}`, `{Object}` — typed properties, no dynamic bags.
- **An Area is a grouping concept, not a module**: it does nothing at runtime — no registration, no injection, no enable/disable. It's a name that optionally informs behaviors (localization keys, Swagger tags, logging, tooling). That's why it's not called Module (modules *do* things) nor Kit ("Kit" stays a design word: an area may hold one kit, several sub-kits, an app, or a Stack area).
- `*` (at most one) swallows the segments nobody cares about (`Models`, …). Tokens after the `*` bind from the end (`{Object}` is the type name), tokens before it from the start; tokens without a segment stay null.
- Nothing in the rule says "NSail": `Batman.Contabilidad.Sarlanga.Asiento` resolves with zero configuration. A company with a different shape puts `[assembly: MetadataTemplate(...)]` on its assemblies (one `<AssemblyAttribute>` in `Directory.Build.props` covers them all) — and because the template travels with the assembly, foreign code with any namespace shape coexists with NSail-shaped kits in the same app. The attribute is also why `TryAdd` ordering doesn't matter for the template: every `AddMetadata()` path builds an equally-correct provider. Ordering only matters for a custom *policy* (a `MetadataProvider` subclass registered before the TryAdds).
- Reading the attribute is AOT-safe: `GetCustomAttribute<T>` with a concrete type is preserved metadata, no dynamic reflection.

**The policy** is `MetadataProvider` (Provider family, like `IBrandProvider`: it answers questions; Managers *do* things): `virtual Get(Type)` defaults to the convention; a company overrides it to patch inconsistencies or map third-party types ("Google.* belongs to Mailing") and **every consumer follows** — that's the point of the single door. `virtual KeyFor(type, member?)` renders the canonical `{Area}.{Object}[.{member}]` key; it is defined in terms of `Get`, so overriding `Get` affects every derived key (that's the feature, not a leak).

| Full name | Root | Area | Feature | Object |
|---|---|---|---|---|
| `NSail.Directory.Parties.Models.PartyModel` | NSail | Directory | Parties | PartyModel |
| `NSail.Components.NsButton` | NSail | Components | — | NsButton |

## Consumers (all through the provider)

- **Localization keys**: fields (`ForExpression.GetTarget` + `Metadata.KeyFor`), `NsTh`, `NsPartial.GetTitle()`/`Translate`.
- **Settings storage keys**: `MediatorSettingsManager` (`Components.LanguageSettings`).
- **Swagger tags**: generated endpoints resolve the provider from `builder.ServiceProvider` at map time — a custom policy regroups the API without touching the generator.
- **Swagger schema ids**: `SchemaIdResolver` renders `{Area}.{Feature}.{Object}` (`Optical.Prescriptions.CreatePrescriptionBody`), configured with the provider as a dependency (`AddOptions<SwaggerGenOptions>().Configure<MetadataProvider>`, `NSail.BaseServices.WebApi/Setup.cs`) so a custom policy moves the schemas with the tags. Swashbuckle's own id is the bare type name, and two areas composed into one host may both declare a `CreatePrescription` — the short name belongs to its area, so the id carries the area rather than the message being renamed. Generic arguments join with `Of`/`And` and a nested type carries its declaring type: an OpenAPI component key admits `[a-zA-Z0-9._-]` only. `SwaggerDocumentTests` asks every host for its document.
- **HttpClient names**: the clients generator derives the endpoint name (`HttpClients:{Area}` config section) from the Sdk setup's namespace at generation time. The generator honors `[assembly: MetadataTemplate]` (read from the compilation, same declaration as runtime — the template logic is mirrored in the generator's internal `MetadataTemplate` because netstandard2.0 can't reference runtime assemblies; keep them in sync). The runtime `Get` override does not apply here: compile-time, your own Sdk, infrastructure name tied to config — acceptable asymmetry.

## Market terms override at the app, not at the locale

String sources merge in registration order and the last write wins, so an app's
`strings.{language}.json` overrides any kit key — **which makes *where* the app registers
its own catalogs part of the mechanism: `AddStringsFromAssembly` for the app goes LAST in
the composition, after every `Add{Kit}WebApi`.** Registered before them the override is
present, reads correctly in the diff, and loses every key it was written for.
That is the mechanism for market-specific terminology, and the rule for choosing where a
string lives:

- **A kit string is generic for the language.** Directory says "Identificación fiscal" —
  a kit never names a country's instrument (CUIT, RFC, NIF), because the kit is the part
  that serves any market.
- **An app string speaks its market.** Optical sells in Argentina, so Optical's
  `strings.es.json` carries `Directory.PartyIdentity.TaxId: "CUIT/CUIL"` — same key, app
  file, wins by merge order (the override replaces the generic string, it does not
  prefix it).
- **A country locale (`es_AR`) is for linguistic differences, not market ones.** None
  exist in the repo today. Opening one to hold a market term puts the country in the kit
  with an extra step; the term follows the *app*, which is the thing that has a market.
- **A string the client never renders lives on the server's side of the split.** A
  `*.Shared` catalog is downloaded whole by every browser, so a message's body, its
  subject and its title belong in the sending host's own `strings*.json` — the kit's
  `*.WebApi`, the app's `*.Web` — beside the handler that sends it. The server merges
  both, so `Translate` resolves either way and only the wire changes.
  `OutboundCatalogTests` holds the line.

Future: logging scopes in the Mediator (Area/Feature enrichment), visual tooling. Reserved: `[assembly: MetadataRoot]` for multi-segment company roots, multiple templates.

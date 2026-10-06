# Types

Shared types in `NSail.Types`, `NSail.BclExtensions` and `NSail.Configuration`.

---

## NSail.Types

Namespaces use capability names, not the project name:

| Namespace | Contents |
|---|---|
| `NSail.Problems` | Error model (RFC 7807-inspired) |
| `NSail.Paging` | Pagination primitives |
| `NSail.Serialization` | The wire's JSON contract (`JsonOptions`) |
| `NSail.Tones` | `BrandTone` — the one derivation of an ink or a state from a picked colour. Down here because both the API edge (a kit, at save) and the palette (the Stack, at paint) have to reach it; see [ui/branding.md](ui/branding.md) |
| `NSail.Builds` | `BuildVersion.Current` — the build this process runs, the one word the menu prints, the server stamps on every answer and the client compares against — and `ServerBuild`, what the transport heard the far side answer. Down here for the same reason `Tones` is: the transport writes it and the chrome reads it; see [messaging.md](messaging.md) |

---

## Problems

NSail uses a structured error model for expected failures.

| Type | Role |
|---|---|
| `Problem` | Error payload (code, title, issues, status) |
| `Issue` | Individual validation or business issue |
| `BusinessException` | Thrown by handlers; converted to `Problem` in HTTP layer |
| `BusinessProblem` | Factory helpers for common business errors |
| `MessagingProblem` | Messaging-specific problems |
| `NetworkProblem` | HTTP client failures |
| `SecurityProblem`, `SystemProblem`, `ExternalProblem`, `InputProblem` | Ready factories per family — authorization, internal failures, external dependencies, input validation (`InputProblem.For<T>()` builds through `InputProblemBuilder`, implicitly convertible to `Problem` and to `Exception`) |

Handlers throw `BusinessException`, usually via implicit conversion from `Problem`:

```csharp
throw new BusinessException(BusinessProblem.NotFound(typeof(Prescription), id));
```

**The entity a refusal is about is a `Type`, never a name.** `NotFound`, `Conflict`,
`AlreadyExists` and `InUse` derive the concept's key through `MetadataProvider.KeyFor` and put
that key in `Issue.Arguments["entity"]`; `StringManager` resolves it to the label when it fills
`{entity}`, so a Spanish counter reads *Depósito* and not *Store*. The English `message` and
`Issue.Source` keep the bare type name, for the untranslated fallback and `Problems.{Code}.{Source}`
scoping. The key is derived at the throw site because it is the last place holding
the type: the browser carries the catalogs and none of the `*.Data` assemblies. That is why
`NSail.Types` names `NSail.Metadata`, which itself depends on nothing, and why the provider there
is constructed rather than injected — a static factory has no container, so a company's
`MetadataProvider` override does not reach this one derivation. The **string** overload exists
for the caller with no type in scope (one kit refusing on another's row, a failure with no entity
behind it) and its argument is the concept KEY, not a name; `ProblemEntityStringTests` names
every such site and refuses a new one.

HTTP layer: `NSail.Messaging.WebApi.ErrorMiddleware` catches `BusinessException` and
returns JSON `Problem` responses; `NSail.Messaging.Http.HttpSender` deserializes `Problem`
from failed HTTP responses and throws `BusinessException`.

**Who is asking is read off `Accept`, and off nothing else.** A caller that ranks `text/html`
above `application/json` — a document navigation, and only that — gets the error page instead
of the payload, rendered by whatever registered `IProblemDocumentProvider`; every other caller
gets the JSON, byte for byte. The one exception is not a preference at all: a page that cannot
be *drawn* falls back to the JSON too, because the alternative is the renderer's own failure
masking the exception the page exists to report (baseservices.md). `HttpRequestMessageBuilder` sets no `Accept`, so the generated
client asks with `*/*` and lands on the JSON arm: **the default where the preference is not
explicit is the machine's answer.** See [baseservices.md](baseservices.md).

---

## Serialization

`JsonOptions.Wire` is the JSON shape of every NSail wire payload — web defaults plus
enums as names. The generated HTTP client serializes with it directly; a host owns its
own options instance and copies `JsonOptions.Converters` instead. Nothing else should
construct `JsonSerializerOptions` for message traffic.

`WireSchema.For(type)` renders that contract as a JSON Schema for whoever authors a body
from outside C# — a pack step, a tool call composed off the `MessageRegistry`
(messaging.md, Naming a message). It speaks the wire (enum names, wire casing) and lists as
`required` what a send would reject when absent: the C# `required` modifier and
`[Required]` alike, since every send validates the message's DataAnnotations. It describes
a well-formed body, not the reader's tolerance — a number is `integer`, never the
`["string", "integer"]` the lenient read admits. `MessageSchemaTests` renders every
registered message's schema.

---

## Paging

`DataPage<T>` — page of items with total count. Use for list/query results. Table
components may raise query events that map to paging parameters.

`PagedMessage` — what a message ASKING for one declares: `PageIndex`/`PageSize` and their
bounds, once, for every list in the tree (`public class ListProducts : PagedMessage,
IMessage<DataPage<ProductRow>>`). `PageSize = 0` means "the caller did not choose" and the
handler's own default answers it; `MaxPageSize` is where a page stops being a page and
becomes an export.

---

## NSail.BclExtensions

Low-level BCL helpers (`DelegateDisposable` — used by `Mediator.Subscribe` for cleanup;
other small extensions). Keep this project minimal. Do not add domain types here.

---

## NSail.Configuration

Host configuration helpers:

- `ConfigurationExtensions.Load<T>()` — bind + DataAnnotations validation; throws at startup if `[Required]` (or other annotations) fail. The section name is derived, never written: `typeof(T).Name` minus a trailing `Options` — `Load<OpticalOptions>()` reads section `"Optical"`; a type with no `Options` suffix reads a section named after its own name. The explicit-string overload (`Load<T>(section)`) stays for genuine mismatches

App and kit options types (e.g. `OpticalOptions`, `IamOptions`) live in their own assemblies — not here.

---

## Rules

- Domain models belong in Kit/App Sdk projects, not in `NSail.Types`
- `NSail.Types` is for cross-cutting primitives only
- Do not create DTOs in Types unless they are truly shared infrastructure
- Problems are the standard error path — do not invent parallel exception hierarchies

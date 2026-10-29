# Types

The cross-cutting primitives every other area shares: the error model, paging, the wire's JSON
contract, written text, privacy markers, build identity and a few small value types. Two packages
carry them: `NSail.Types` for Stack vocabulary, `NSail.BclExtensions` for BCL helpers. Neither
holds domain models or DTOs; those belong to a kit's or an app's Sdk.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Types` | Problems, paging, serialization, text, privacy, builds, icons, tones. Depends only on `NSail.Metadata`. | Sdk, Web and client projects |
| `NSail.BclExtensions` | `DelegateDisposable`, `CompositeDisposable`, `Type` helpers (`IsScalar`, `IsCollection`, `IsNullableScalar`). No dependencies. | Anything that needs them |

```xml
<PackageReference Include="NSail.Types" Version="$(NSailStackVersion)" />
```

## What is in NSail.Types

Namespaces are named for a capability, not for the project.

| Namespace | Contents |
|---|---|
| `NSail.Problems` | `Problem` (code, title, issues, status), `Issue`, `BusinessException`, and factories per family: `BusinessProblem`, `InputProblem` (`For<T>()` builds through `InputProblemBuilder`), `SecurityProblem`, `SystemProblem`, `ExternalProblem`, `MessagingProblem`, `NetworkProblem`, `TenancyProblem` |
| `NSail.Paging` | `DataPage<T>`, `PagedMessage` (`PageIndex`, `PageSize`, `MaxPageSize` = 1000), `Selection` (ids or "all rows the filter answers") |
| `NSail.Serialization` | `JsonOptions.Wire` (web defaults, enums as names), `WireSchema.For(type)` (JSON Schema of a wire payload) |
| `NSail.Text` | `Markdown` (`ToHtml`, `Headings`, `Slug`, `ToText`) with `MarkdownOptions.Prose` / `Document`, `NamedTokens` (`In`, `Fill` for `{name}`), `WebAddress.IsNavigable` |
| `NSail.Privacy` | `PersonalData` (Name, Document, Email, Phone, Address, WebAddress, FreeText, BirthDate) and `PersonalDataComment` |
| `NSail.Builds` | `BuildVersion.Current` (the entry assembly's informational version, `dev` if unstamped), `ServerBuild` (client side of the build handshake) |
| `NSail.Tones` | `BrandTone`: derives an ink or a state from a picked colour |
| `NSail.Icons` | `Glyph`, the SVG markup of one catalog icon, serialized as the markup string |
| `NSail.Dates` | `BusinessDate.Today`, the host's local date, never UTC |
| `NSail.References` | `IRef`, the key of every `*Ref` projection |

Handlers throw `BusinessException`, usually by implicit conversion from a `Problem`:

```csharp
throw new BusinessException(BusinessProblem.NotFound(typeof(Contact), id));
```

`NotFound`, `Conflict`, `AlreadyExists` and `InUse` take the entity as a `Type` and put its concept
key in `Issue.Arguments["entity"]`, so a refusal reads in the caller's language. Everything the
messaging layer refuses with is a `Problem`; do not add a parallel exception hierarchy.

## Configuration

None. These are types, not services. Two behaviours worth knowing:

- `JsonOptions.Wire` is the only `JsonSerializerOptions` for message traffic; a host that owns its
  own options copies `JsonOptions.Converters` instead of constructing a second contract.
- `PagedMessage.PageSize` of 0 means the caller did not choose, and the handler's own default
  answers; sizes above `MaxPageSize` are refused.

## How it fits

- [Messaging](../Messaging/README.md): `Problem` is the refusal on the wire; `PagedMessage` and
  `DataPage<T>` shape every list message; `JsonOptions.Wire` is the wire.
- [Metadata](../Metadata/README.md): the one dependency, used to key entities in problems.
- [Components](../Components/README.md) and [React](../React/README.md): `Glyph`, `BrandTone`,
  `Markdown` and `ServerBuild` are read by the chrome.
- [Data](../Data/README.md): `PersonalData` is declared on columns and carried to the schema.
- [Configuration](../Configuration/README.md): a separate package for options loading.

## Going deeper

- Guide: [Messages](../../../docs/messages.md), [Sending](../../../docs/sending.md)
- Doctrine: [types.md](../../../plugins/nsail-stack/doctrine/types.md)
- Tests: [test/Stack/Types](../../../test/Stack/Types)

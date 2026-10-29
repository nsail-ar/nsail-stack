# Localization

Texts live in flat JSON files embedded in each assembly and merge into one catalog per language.
Keys are derived from the code through [Metadata](../Metadata/README.md), never typed by hand,
and a key with no text renders as itself, so a missing translation is visible instead of guessed.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Localization` | `StringManager`, `StringCatalog`, `IStringSource`, `LanguageProvider`, `LanguageOptions`, `Languages`, `AddLocalization()`, `AddStringsFromAssembly()` | Web, client; reached through `NSail.Components` and `NSail.BaseServices.WebApi` |

It depends on `NSail.Metadata` and `NSail.Types` (for `Issue` and `Problem`).

## Use it

Embed the texts in the project that owns them: `strings.json` is the base language, and
`strings.{language}.json` overlays it (flat key to text, at the project root).

```xml
<ItemGroup>
  <Content Remove="strings*.json" />
  <EmbeddedResource Include="strings*.json" />
  <EmbeddedResource Update="strings.*.json">
    <DependentUpon>strings.json</DependentUpon>
    <LogicalName>$(RootNamespace).%(Filename)%(Extension)</LogicalName>
    <WithCulture>false</WithCulture>
  </EmbeddedResource>
</ItemGroup>
```

```json
{ "Tasks.CreateProject.Name": "Name" }
```

Register them from the same assembly (it defaults to the caller's):

```csharp
services.AddStringsFromAssembly();                         // calling assembly
services.AddStringsFromAssembly(typeof(SomeType).Assembly); // another one
```

Translate with `StringManager` (scoped):

```csharp
strings.Translate("Tasks.CreateProject.Name");        // key, or the key itself if missing
strings.Translate("Tasks.Task.Name", fallback: "Task");
strings.Translate("Tasks.Task", count);               // "Tasks.Task.Plural" when count != 1
strings.Translate(ProjectStatus.Open);                // "Tasks.ProjectStatus.Open", else "Open"
strings.Translate("Tasks.Greeting", new Dictionary<string, string> { ["name"] = "Ana" });
```

Blazor components translate their own labels: fields from their binding expression, pages from
their type. See [Components](../Components/README.md).

## Configuration

`AddLocalization()` registers `MetadataProvider` (via `AddMetadata()`), `StringCatalog`
(singleton), `LanguageProvider` and `StringManager` (scoped), all with `TryAdd`.
`AddStringsFromAssembly()` calls it and adds one `IStringSource`.

**Merging.** Sources merge in registration order and the last one wins: kits first, the app last,
so an app's `strings.{language}.json` overrides any kit key. Within a source the base file loads
first and the language overlays it, so gaps fall back to the base text. Register the app's
`AddStringsFromAssembly()` after every kit.

**Language options.** `LanguageOptions` binds from the `Language` configuration section
(`configuration.Load<LanguageOptions>()`, see [Configuration](../Configuration/README.md)):

| Key | Default | Meaning |
|---|---|---|
| `Language:Base` | `en` | Language the base `strings.json` is written in. It carries no code in its name, so it cannot be derived. |
| `Language:Default` | `en` | Language served to someone who chose none and whose browser asks for nothing offered. A preference, not the base. |
| `Language:Offered` | empty | Languages the browser's `Accept-Language` may vote among. Empty means `Default` alone. |

`LanguageOptions` is not registered by `AddLocalization()`; a host that injects it registers it
(`services.AddSingleton(configuration.Load<LanguageOptions>())`, as the Sample does).

**Derivations** (`Languages`, static, shared by server and client so they cannot disagree):

| Method | Returns |
|---|---|
| `Supported(sources, options)` | Every language the sources carry, plus `Base` and `Default`. The settings picker's list and what a forced choice is validated against. |
| `Offered(options)` | `Offered`, else `[Default]`. The only set a browser is resolved against. |
| `Negotiate(preference, served)` | The culture a client boots in: the saved choice, else the language the server served; `null` if neither. |
| `Adopt(culture)` | Sets the default thread culture and UI culture; does nothing off the browser. |

**Current language.** `LanguageProvider.Current` defaults to the ambient UI culture's two-letter
code; set it to switch language without a reload. `StringCatalog` holds one merged dictionary per
language requested, built on first use.

**Custom sources.** Implement `IStringSource` (`Languages` and `GetStrings(language)`) and
register it as a singleton to add texts from somewhere other than embedded files.

**Key shapes.** `StringManager` resolves, besides plain keys: enums as `{Area}.{Enum}.{Value}`;
issues as `Problems.{Code}.{Source}`, then `Problems.{Code}`, then the issue's own message, with
named tokens such as `{max}` filled from its arguments (an `entity` argument is a key and is
translated first); problems' titles as `Problems.{Code}.Title`.

## How it fits

- Keys come from [Metadata](../Metadata/README.md).
- [BaseServices](../BaseServices/README.md) reads `LanguageOptions` to run request localization
  (cookie, then the browser among `Offered`, then `Default`) and to store a user's language.
- [Components](../Components/README.md) translates every label, title and message through
  `StringManager`; `NSail.Components.Mud` ships its own `strings.json` and `strings.es.json`.
- [React](../React/README.md) reads the same catalogs through a message that answers the host's
  merged strings (the Sample's `GetStrings`).
- [Types](../Types/README.md) supplies the `Issue` and `Problem` it translates.

## Going deeper

- Guide: [getting-started.md](../../../docs/getting-started.md) (the client's embedded texts)
- Doctrine: [localization.md](../../../plugins/nsail-stack/doctrine/ui/localization.md),
  [metadata.md](../../../plugins/nsail-stack/doctrine/metadata.md),
  [baseservices.md](../../../plugins/nsail-stack/doctrine/baseservices.md) (culture negotiation)
- Sample: [StringsHandler.cs](../Sample/NSail.Sample.Web/Strings/StringsHandler.cs)
- Tests: [test/Stack/Localization](../../../test/Stack/Localization)

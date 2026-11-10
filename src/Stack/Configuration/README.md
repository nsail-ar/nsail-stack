# Configuration

One helper that turns a configuration section into a typed, validated options object. A host
fails at startup, naming the key, instead of at the first request that reads a missing value.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Configuration` | `ConfigurationExtensions.Load<T>` over `Microsoft.Extensions.Configuration` | Web host, kits, anything that reads options; no UI or host dependency |

## Use it

```xml
<PackageReference Include="NSail.Configuration" Version="$(NSailStackVersion)" />
```

```csharp
public class SampleOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}

var options = builder.Configuration.Load<SampleOptions>();   // section "Sample"
```

## Configuration

`Load<T>` has two overloads, both on `IConfiguration` and both requiring `T : class, new()`:

| Call | Section read |
|---|---|
| `Load<T>()` | `typeof(T).Name` without a trailing `Options` (`SampleOptions` reads `Sample`); a type without the suffix reads a section named after itself |
| `Load<T>(string section)` | the named section, for a genuine mismatch; a blank name throws `ArgumentException` |

Both create a `new T()`, bind the section onto it and validate it with DataAnnotations
(`Validator.TryValidateObject`, all properties). On failure they throw an
`InvalidOperationException` whose message starts `Configuration validation failed.` and lists each
violation as `<Section>.<Member>: <message>`. An absent section is not an error: the defaults of
`T` apply, so mark what must be present with `[Required]`.

The options types the Stack itself loads (`LanguageOptions`, `SecurityOptions`, `TenancyOptions`,
`TelemetryOptions`, `StorageOptions`) live in their own areas, each with its own keys; the options of a kit or an
app live in that kit or app.

## How it fits

Every area that reads configuration goes through `Load<T>`: [BaseServices](../BaseServices/README.md),
[Background](../Background/README.md) and [Telemetry](../Telemetry/README.md) reference this package
directly, and [Localization](../Localization/README.md) and [Data](../Data/README.md) define the
options it loads.

## Going deeper

- Guide: [Getting started, the host](../../../docs/getting-started.md#5-the-host)
- Doctrine: [types.md](../../../plugins/nsail-stack/doctrine/types.md#nsailconfiguration)
- Sample: [SampleOptions](../Sample/NSail.Sample.Web/SampleOptions.cs) and its `Program.cs`
- Tests: [test/Stack/Configuration](../../../test/Stack/Configuration)

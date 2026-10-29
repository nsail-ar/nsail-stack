# Settings

The abstraction for settings: typed POCOs that are read and saved through one `SettingsManager`,
plus the contract a module uses to put its settings pages in the menu. The type carries
everything, so a caller states neither the storage key nor the scope. The Stack ships no storage:
without a store registered, reads return the POCO's defaults and saves are ignored, so a host still
runs.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Settings` | `SettingsManager`, `[SystemSettings]`, `ISettingsContributor`, `SettingsItem`, `StoredSettings<T>`, `NavMenuArrangement` | Client and server projects that read or contribute settings (`NSail.Components.Mud` calls `AddSettings()`) |

## Use it

```xml
<PackageReference Include="NSail.Settings" Version="$(NSailStackVersion)" />
```

A settings type is a class with a parameterless constructor. It is per-user unless marked:

```csharp
[SystemSettings]
public sealed class ShopSettings
{
    public string? Currency { get; set; }
}
```

```csharp
services.AddSettings();                                  // default manager, no storage
services.AddSettings<MyModuleSettingsContributor>();     // and contribute a settings page

var shop = await settings.Get<ShopSettings>(cancellationToken);
var stored = await settings.Read<ShopSettings>(cancellationToken);   // value + version, for a screen
await settings.Save(stored.Value, stored.Version, cancellationToken);
```

## Configuration

| Member | Meaning |
|---|---|
| `[SystemSettings]` | One row for the install, readable with no user in context (jobs, notifications). Without it the type is per user and defaults to `new()`. `SettingsManager.IsSystem(type)` reads it. |
| `SettingsManager.Get<T>()` | The stored value alone, for a reader that acts on settings. |
| `SettingsManager.Read<T>()` | `StoredSettings<T>`: `Value` and an opaque `Version` (0 means no row yet). |
| `SettingsManager.Save<T>(value, version)` | Refused if `version` names a row that moved since. Version 0 is an unconditional write: right for a background writer, never for a form. |
| `AddSettings()` | `TryAddScoped<SettingsManager, NullSettingsManager>`: a store registered earlier or later wins. |
| `AddSettings<TContributor>()` | `AddSettings()` plus `ISettingsContributor` as a scoped service. |
| `ISettingsContributor.GetItems()` | Returns `SettingsItem`s. |
| `SettingsItem` | `Name` (required), `Icon`, `PageType` (a leaf's page; null for a group), `Items`, `Group` (a shared bucket several contributors join), `GroupIcon`, `Weight` (sort key; domain items low, system machinery 80+). |
| `INameWellKnownRows` | A settings type that holds ids of rows the install froze implements `Resolve(Func<Guid, Guid>)`; the manager resolves them through the tenancy seam on read. |
| `NavMenuArrangement` | A `[SystemSettings]` POCO holding what an install did to its own menu (reorder, rename, hide) as `NavMenuArrangementEntry` rows keyed by the contributed `Name`; an entry no row names keeps its place. |

The storage key derives from the type through `MetadataProvider.KeyFor` (`{Area}.{Type}`).

## How it fits

- [Components](../Components/README.md): `NSail.Components.Mud` calls `AddSettings()`, and the nav
  reads `NavMenuArrangement`.
- [Metadata](../Metadata/README.md): the storage key.
- [Data](../Data/README.md): a storage kit registers the real `SettingsManager` over a `DbContext`.
- [Types](../Types/README.md): `Glyph` for icons.

## Going deeper

- Doctrine: [ui/settings.md](../../../plugins/nsail-stack/doctrine/ui/settings.md)
- Tests: [test/Stack/Settings](../../../test/Stack/Settings)

# Storage

The object store: put, get, list and delete a named object, and say where one can be read,
behind a provider per protocol. An install keeps several stores, each by name, with a `default`
every name without a target of its own falls back to. It keeps no entity and no table; the
targets a business configures are a settings row, and the install's are configuration.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Storage.Sdk` | The settings screen's messages and models (`ListStorageStores`, `GetStorageSettings`, `SaveStorageSettings`, `LookupStorageProviders`, `TestStorageTarget`) and `StorageNames.Default` | `NSail.Storage.Shared`, `NSail.Storage.WebApi` |
| `NSail.Storage.WebApi` | `StorageManager`, `AddStore`, the `IStorageProvider` seam, `StorageOptions`, the settings handlers | The host, and any server code that writes objects |
| `NSail.Storage.Shared` | The settings screen, its strings and its guide chapter | The client composition roots that show the screen |
| `NSail.Storage.FileSystem` | A directory on the host's own disk (provider `file`) | The host that may write to its disk |
| `NSail.Storage.S3` | Any store speaking the S3 REST API, path-style, SigV4 (provider `s3`) | The host that writes to a bucket |

## Use it

```xml
<PackageReference Include="NSail.Storage.WebApi" Version="$(NSailStackVersion)" />
<PackageReference Include="NSail.Storage.FileSystem" Version="$(NSailStackVersion)" />
<PackageReference Include="NSail.Storage.S3" Version="$(NSailStackVersion)" />
```

On the server, the install's default store, then the providers it may write through, by name:

```csharp
var install = builder.Configuration.Load<StorageOptions>();   // section Storage, validated

services.AddStorageWebApi(install.OnDisk("/data/storage"));   // OnDisk: optional floor
services.AddFileSystemStorage();
services.AddS3Storage();
```

A kit that writes to a store of its own names it with a constant and declares it; the host may
declare the same name again with the install's target for it:

```csharp
public static class AccountingStores { public const string Reports = "reports"; }

services.AddStore(AccountingStores.Reports);                                   // the kit
services.AddStore(AccountingStores.Reports,
    builder.Configuration.Load<StorageOptions>("Storage:reports"));            // the host, optional
```

On each client composition root that shows the settings screen:

```csharp
services.AddStorageComponents();
```

Code that writes objects takes `StorageManager` — unkeyed for the default store, keyed by name
for its own:

```csharp
public ReportsHandler([FromKeyedServices(AccountingStores.Reports)] StorageManager storage)

await storage.Put($"exports/{tenant:N}/{name}", stream, "text/csv", cancellationToken);
using var content = await storage.Get(key, cancellationToken);   // null: no such object
var objects = await storage.List($"exports/{tenant:N}/", cancellationToken);
var link = await storage.Link(key, cancellationToken);           // null: the provider has none
await storage.Delete(key, cancellationToken);
```

## Configuration

`StorageOptions` is one store's install target, used wherever the settings row names no
provider for it. The host loads the default's with `Load<StorageOptions>()`
([Configuration](../Configuration/README.md)), which reads the `Storage` section and runs its
validation at startup, and passes it to `AddStorageWebApi`; a named store's is the
`Storage:{name}` section, loaded the same way and passed to `AddStore`.

A store resolves its target most specific first: its own settings row, its own install target,
then the default store's row and the default's install target. An install with no target
anywhere stores nowhere.

| Member | Meaning |
|---|---|
| `Provider` | `file` or `s3`. Empty: the install stores nowhere until a row names a target. |
| `Container` | The directory (`file`) or the bucket (`s3`). Required once `Provider` is named; refused at startup otherwise. |
| `Endpoint` | The store's address (`s3`). |
| `Region` | The signing region (`s3`); defaults to `auto`. |
| `AccessKey`, `SecretKey` | The key pair (`s3`). |

| Call | Meaning |
|---|---|
| `AddStorageWebApi(install)` | The default store's `StorageManager` (unkeyed, and keyed `StorageNames.Default`), the endpoints, in-process clients and permissions, and `AddSettings()`. Registers no provider and no `KeyProtector`: the host registers the key that seals the stored secrets. |
| `AddStore(name, install)` | Declares a store: a keyed `StorageManager` under `name`, and a row on the settings screen. `install` is optional; declared again with one, the last target wins. |
| `AddFileSystemStorage()` | The `file` provider. |
| `AddS3Storage()` | The `s3` provider and its `HttpClient`, with no wall-clock timeout; the caller's `CancellationToken` is the deadline. |
| `StorageOptions.OnDisk(directory)` | The `file` provider at `directory` when the install named no provider; a named one is kept. |
| `AddStorageComponents()` | The settings screen (Settings, Integrations), its strings and its guide chapter. |
| `StorageManager.IsConfigured()` | Whether a put would be refused for want of a target, before anything is read to put. |
| `StorageManager.Link(key)` | The address the object can be read at — the S3 provider's path-style URL, none for a directory — computed, not checked. |

Every key passes `StorageManager.Guard`, which refuses one that would climb out of its container.
A key is `<kind>/<tenant:N>/<leaf>`.

## How it fits

- [Settings](../Settings/README.md): the business's targets are one `[SystemSettings]` row, a
  slot per store name.
- [Security](../Security/README.md): `KeyProtector` seals the secret key; the settings messages
  carry their permissions.
- [Components](../Components/README.md): the settings screen.

## Going deeper

- Doctrine: [storage.md](../../../plugins/nsail-stack/doctrine/storage.md)
- Tests: [test/Stack/Storage](../../../test/Stack/Storage)

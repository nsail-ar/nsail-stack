# Storage

The object store: put, get, list and delete a named object, behind a provider per protocol. It
keeps no entity and no table; the target a business configures is a settings row, and the
install's default is configuration.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Storage.Sdk` | The settings screen's messages and models (`GetStorageSettings`, `SaveStorageSettings`, `LookupStorageProviders`, `TestStorageTarget`), and `StorageIcons`, the glyphs the section and each provider name | `NSail.Storage.Shared`, `NSail.Storage.WebApi` |
| `NSail.Storage.WebApi` | `StorageManager`, the `IStorageProvider` seam, `StorageOptions`, the settings handlers | The host, and any server code that writes objects |
| `NSail.Storage.Shared` | The settings screen, its strings and its guide chapter | The client composition roots that show the screen |
| `NSail.Storage.FileSystem` | A directory on the host's own disk (provider `file`) | The host that may write to its disk |
| `NSail.Storage.S3` | Any store speaking the S3 REST API, path-style, SigV4 (provider `s3`) | The host that writes to a bucket |

## Use it

```xml
<PackageReference Include="NSail.Storage.WebApi" Version="$(NSailStackVersion)" />
<PackageReference Include="NSail.Storage.FileSystem" Version="$(NSailStackVersion)" />
<PackageReference Include="NSail.Storage.S3" Version="$(NSailStackVersion)" />
```

On the server, the install's default, then the providers it may write through, by name:

```csharp
var install = builder.Configuration.GetSection("Storage").Get<StorageOptions>();

services.AddStorageWebApi(install.OnDisk("/data/storage"));   // OnDisk: optional floor
services.AddFileSystemStorage();
services.AddS3Storage();
```

On each client composition root that shows the settings screen:

```csharp
services.AddStorageComponents();
```

Code that writes objects takes `StorageManager`:

```csharp
await storage.Put($"exports/{tenant:N}/{name}", stream, "text/csv", cancellationToken);
using var content = await storage.Get(key, cancellationToken);   // null: no such object
var objects = await storage.List($"exports/{tenant:N}/", cancellationToken);
await storage.Delete(key, cancellationToken);
```

## Configuration

`StorageOptions` is the install's default target, used wherever the settings row names no
provider. The host loads it from whatever section it chooses and passes it to `AddStorageWebApi`.

| Member | Meaning |
|---|---|
| `Provider` | `file` or `s3`. Empty: the install stores nowhere until a row names a target. |
| `Container` | The directory (`file`) or the bucket (`s3`). Required once `Provider` is named; refused at startup otherwise. |
| `Endpoint` | The store's address (`s3`). |
| `Region` | The signing region (`s3`); defaults to `auto`. |
| `AccessKey`, `SecretKey` | The key pair (`s3`). |

| Call | Meaning |
|---|---|
| `AddStorageWebApi(install)` | `StorageManager`, the endpoints, in-process clients and permissions, and `AddSettings()`. Registers no provider and no `KeyProtector`: the host registers the key that seals the stored secret. |
| `AddFileSystemStorage()` | The `file` provider. |
| `AddS3Storage()` | The `s3` provider and its `HttpClient`, with no wall-clock timeout; the caller's `CancellationToken` is the deadline. |
| `StorageOptions.OnDisk(directory)` | The `file` provider at `directory` when the install named no provider; a named one is kept. |
| `AddStorageComponents()` | The settings screen (Settings, Integrations), its strings and its guide chapter. |
| `StorageManager.IsConfigured()` | Whether a put would be refused for want of a target, before anything is read to put. |

Every key passes `StorageManager.Guard`, which refuses one that would climb out of its container.
A key is `<kind>/<tenant:N>/<leaf>`.

## How it fits

- [Settings](../Settings/README.md): the business's target is a `[SystemSettings]` row.
- [Security](../Security/README.md): `KeyProtector` seals the secret key; the settings messages
  carry their permissions.
- [Components](../Components/README.md): the settings screen.

## Going deeper

- Doctrine: [storage.md](../../../plugins/nsail-stack/doctrine/storage.md)
- Tests: [test/Stack/Storage](../../../test/Stack/Storage)

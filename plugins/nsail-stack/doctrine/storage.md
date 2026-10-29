# Storage

The object store: put, get, list and delete a named object, with a provider per protocol behind
it. It is infrastructure with no domain — no entity, no table, and nothing it knows about what it
holds — so it is the Stack's, and an archive, an attachment, an export or an uploaded file that
needs somewhere to land uses the same door.

---

## The shape

| Project | Holds |
|---|---|
| `NSail.Storage.Sdk` | the settings screen's messages and models — nothing about objects |
| `NSail.Storage.WebApi` | `StorageManager` (the outward API), `IStorageProvider` (the seam), the sealed `StorageSettings` |
| `NSail.Storage.Shared` | the settings screen |
| `NSail.Storage.FileSystem` | a directory on the host's own disk |
| `NSail.Storage.S3` | anything speaking the S3 REST API |

No `.Data` project: Storage keeps no entity of its own and the target rides a `NSail.Settings`
row. The proof is `test/Stack/Storage/NSail.Storage.WebApi.Tests`, which needs no database.

## Rules

- **`Stream` in, `Stream` out — never `byte[]`.** An archive larger than memory is neither
  loaded nor buffered whole, and that is a test, not a promise
  (`FileSystemStorageTests.WritesWhileTheSourceIsStillProducing`). A get hands back an open
  read the caller disposes. That open read is shared for delete too (`FileShare.Delete`
  alongside `FileShare.Read`), so a caller that reads and then deletes is never refused for
  it — not that the two operating systems then agree: POSIX drops the name at once, Windows
  leaves the delete pending until the read is disposed, and until then the key still
  answers `List` and a `Put` on it still fails.
- **No vendor word in the API.** `StorageTarget` is one flat vendor-free shape — address,
  region, container, key pair — and a provider reads what it needs. That is what keeps the
  settings screen ONE form instead of one per vendor, and it is a deliberate trade against a
  sealed opaque blob per provider, which would have cost an Sdk and a Shared assembly each.
- **`AddStorageWebApi` registers no provider.** Where a host may write is the app's decision,
  asked for by name (`AddFileSystemStorage`, `AddS3Storage`), so a product that must never write
  to a disk does not compose the one that can.
- **A business's own target is a row; the install's default is configuration.** The settings
  row wins wherever it names a provider, and `StorageCredentialProvider` falls back to
  `StorageOptions` — one object, loaded by the host and passed to `AddStorageWebApi` — wherever
  it names none. That fallback is the one seam, so every caller inherits it, and a business that
  never opened the settings screen still uploads a logo. An install that passes no options
  stores nowhere until a row says otherwise; an app whose floor is the host's own disk says so by
  name, with `AddFileSystemStorage`'s `OnDisk`, and names the directory its deploy mounts a
  volume at. A provider named with no container is refused at startup, where a person is
  looking, not at the first upload.
- **A key is `<kind>/<tenant:N>/<leaf>`, and no kind is outside it.** The kind leads so one
  container holds every kind without a collision; the tenant leads inside it because an object
  store is the INSTALL's, not a tenant's — Storage guards a key against climbing out of its
  container and knows nothing about whose data it holds, so the wall at this door is the prefix.
  One flat directory per tenant per kind, no deeper, so a copy that walks one level under
  `<kind>/<tenant:N>` brings across everything the convention wrote. Under a tenancy mode whose
  wall is not the column (MultiDb, None) the tenant segment is one zeroed guid, and that is
  right: those hosts are one container per tenant already. Each kind that writes keys says its
  shape in one place, and a scope that can name no tenant stores nothing rather than keying it
  under nobody.
- **The provider contract may still change.** It freezes after the provider that follows S3,
  the rule `IStorageProvider` carries — one provider teaches you its own shape, not the
  abstraction's.
- **A key never escapes its container.** `StorageManager.Guard` is the one door every key
  comes through, and the filesystem provider checks the RESOLVED path behind it.
- **`IsConfigured` answers the refusal before there is anything to put.** A caller that
  queues the writing for later — a request filed now and written by a job — would otherwise
  accept the ask, read a whole database and fail on its last line. It is false for both of
  `Resolve`'s refusals, an install storing nowhere and a provider no host composed, because a
  caller at the door can do nothing different about either.
- **The credential is sealed with the install's own key** (`KeyProtector`, `NSail.Security`).
  `AddStorageWebApi` does not register one — the key is the host's — so a host that composes
  Storage without it fails by name on first use rather than sealing under a key nobody chose.

## The S3 provider

Path-style addressing (`endpoint / container / key`), which every S3-compatible service
serves, so one provider covers AWS, Cloudflare R2, MinIO and Backblaze. Region defaults to
`auto`, which is what R2 documents.

SigV4 is written here rather than taken from a vendor SDK: the whole of what an S3-compatible
store asks of a caller is that signature, and pulling one vendor's client in would tie Storage
to it to avoid ninety lines of HMAC. `S3SignatureTests` checks it against AWS's own published
worked examples.

Uploads declare `UNSIGNED-PAYLOAD` over https (a plaintext endpoint gets the payload hashed
instead — the signature has nothing else to bind the body to). Both shapes are pinned in
`S3TransportTests`, because that choice is the difference between a signature that binds the
body and one that trusts the wire. The provider writes with a single PUT rather than S3's
multipart upload, so the length has to be known before the first byte leaves and the object
caps at 5 GiB; a non-seekable stream is refused out loud rather than half-written at the far
end, and something generated on the fly is spooled to a seekable source first.

**There is no wall-clock ceiling, and that is a decision.** `HttpClient`'s default would cut
every send at 100 seconds — a clock that covers the whole body, so an object of any real size
dies mid-transfer — so the registered client is given `Timeout.InfiniteTimeSpan`
(`S3TransportTests.TheRegisteredClientCarriesNoWallClockCeiling` holds it). How long a
transfer may take is the caller's to decide, through the `CancellationToken` all four verbs
take.

**What is proven and what is not.** What the provider BUILDS — the path-style address, the
payload hash it declares, the body that follows it, the continuation it walks, the store's
own XML refusal read back — is pinned offline in `S3TransportTests`, a stub transport behind
a one-client `IHttpClientFactory` and no credential anywhere. What is NOT proven is whether a
real store ACCEPTS what is built: that the socket connects, the TLS completes and AWS, R2 or
MinIO answers the signature with a 200. Closing that needs a bucket and a credential from a
deployed instance, and nothing smaller.

## The settings screen

`Configuración > Almacenamiento`, under Integraciones, wherever `AddStorageComponents()` is
called (a product's Wasm composition root). A product parks the screen out of sight by leaving
that one call out of both composition roots, which takes the route, the strings and the
settings leaf with it, and keeps `AddStorageWebApi` for everything that still writes objects.
The provider picker decides whether the address, the region and the key pair are asked for at
all — `StorageProviderRef.IsRemote` is what the screen reads. The secret key is never answered
back: the read says only that one exists, and an empty field on save means "unchanged", never
"clear it".

The test action writes a small probe, reads it back, checks the bytes, lists it and removes
it — against the **saved** target, because testing what the form currently holds would prove
a configuration that exists in one browser.

**A probe that fails is a refusal and answers nothing.** It carries no "did it work" flag: the
install storing nowhere and a provider nobody composed are refused on `Provider`, which the
screen renders as Proveedor and marks, and the probe's own verdicts carry a rule each and stand
at the foot of the form — both drawn by the form the act is pressed inside, never as a toast
that fades ([forms.md](ui/forms.md), *Placing a refusal*). Success is a `Notify`, which is news
and not a refusal.

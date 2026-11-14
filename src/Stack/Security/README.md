# Security

Authorization over messages: a policy answers one question — may this caller send this message
with these values? — and a send is allowed when at least one policy is fully satisfied. There
are no verbs and no deny rules; absence is denial. A policy constrains the values a caller passes
in a message's fields, never the rows a handler returns.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Security.Annotations` | `[PolicyField]` with `RestrictAs`, `[Requires]`, `[RequiresFeature]` | Sdk (through `NSail.Security`) |
| `NSail.Security` | `Session`, `Policy` and `PolicyBuilder`, `SecurityManager`, the gate (`AddSecurityEnforcement`), entitlements (`Entitlements`, `Features`), `PasswordHash`, `KeyProtector`, `PrivateHost` | Sdk, Web, client, test |

## Use it

The Sdk references `NSail.Security` and declares a holder the generator fills with one policy
handler per `IMessage` (a message without `[PolicyField]` fields gets one that restricts nothing):

```csharp
public static partial class Permissions
{
    [Generated(Policies.Handlers)]
    public static partial void AddAppPermissions(this IServiceCollection services);
}
```

The host turns the gate on and registers its policies; every send is denied until a policy grants
it. A policy with no audience matches anyone, signed in or not:

```csharp
services.AddSecurityEnforcement();
services.AcceptPermissiveProviders();   // no Iam here: relations and tickets stay permissive
services.AddAppPermissions();
services.AddBuiltInPolicy(new Policy
{
    Name = "App.Policies.Everyone",   // a localization key, not prose
    Messages = ["App.*"],
});
```

The client calls `AddSecurity()` (permissive: the server refuses) and registers the same
permissions and built-in policies, so a page guarded by `[Authorize<T>]` is not drawn for a caller
who cannot send `T`. Policies authored in code go through `PolicyBuilder`:

```csharp
var policy = PolicyBuilder.Named("Clinic.Prescriptions.Own")
    .For("Clinic.Prescriptions.CreatePrescription", "Clinic.Prescriptions.ListPrescriptions")
    .ForRole("doctor")
    .Restrict("DoctorId", PolicyConstraint.Me)
    .Build();
```

## Configuration

**`Security` section** (`SecurityOptions`; registered with its default by `AddSecurity`, bound by
`AddBaseWebApi`):

| Key | Default | Meaning |
|---|---|---|
| `TicketEpoch` | `0` | The ticket generation this install honours. Raising it signs out everyone holding an older ticket and nobody else: an HTTP request meets the 401 at once, and an open Blazor circuit meets it as a 401 on its next send (the security gate asks again) and reaches the sign-in page on its next document load, where the request wall signs the ticket out. What it cannot reach is a circuit that sends nothing, and a push feed, which are not asked until they act or reconnect. The value is read at startup, so a change takes a restart. `0` means never raised: nothing is stamped or refused. From `1` up, a ticket must carry exactly this generation |
| `KeysDirectory` | none | The directory of one `{slug}.key` file per tenant, read by `AddTenantKeys`. Required outside Development by a host that calls `AddTenantKeys` |
| `TrustedProxies` | `[]` | Proxies (addresses or CIDR networks) whose `X-Forwarded-*` headers are believed when `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is on. Any other peer's are ignored; empty trusts nobody |

**Registration**

| Call | Does |
|---|---|
| `AddSecurity()` | TryAdds `SessionProvider`, `RelationProvider`, `FeatureProvider`, `CredentialProvider`, `RequestOrganizationProvider`, `SecurityManager` (and the process-wide `BuiltInHandlers` it shares; a derived manager passes it to the base constructor, or keeps hydrating per scope) with permissive defaults (anonymous session, every relation true, every feature on). A host with sign-in and a policy store replaces them |
| `AddSecurityEnforcement()` | `AddSecurity()` plus the `SecurityInterceptor` on every send: deny by default. The host refuses to start while `RelationProvider` or `CredentialProvider` is still the permissive default (every relation constraint would pass, stale tickets be honoured): Iam registers the DB-backed ones |
| `AddTenantKeys()` (`NSail.BaseServices.WebApi`, on the builder) | Registers the `KeyProtector` that seals with the current tenant's key from `Security:KeysDirectory`. A tenant with no file makes a seal or an open throw `MissingEncryptionKeyException`; Development without a directory seals under a fixed key, any other environment refuses to compose. The file is the cell's copy of a key kept elsewhere, and is read at every call, so a key written while the host runs is honoured |
| `AcceptPermissiveProviders()` | Says an enforcing host has no Iam (a fixture, a sample) and runs over the permissive providers on purpose |
| `AddBuiltInPolicy(policy)` | A code-owned policy no stored row can delete or widen |
| `AddFeatures<TContributor>()` | An `IFeatureContributor` declaring the entitlement keys a module owns |

**Attributes** (namespace `NSail.Security.Annotations`)

| Attribute | On | Meaning |
|---|---|---|
| `[PolicyField(RestrictAs.X)]` | message property | Policies may restrict the values a caller passes here. `Party` (`@me`, relations), `Organization` (`@current`, `@memberOf`, `@memberOfOrDescendant`), `Reference` (literal ids only), `Flag` (literal true/false only). Unmarked fields are invisible to security |
| `[Requires(typeof(Other))]` | message class | Whoever may send this may also send `Other` (one hop, flat; lookup-shaped messages only). Repeatable |
| `[RequiresFeature("key")]` | message class | The message belongs to a feature; an install whose entitlements switch it off is refused 403 `FeatureUnavailable` |

**Policy shape** (`Policy`, stored as JSON by `PolicyDocument`)

| Part | Meaning |
|---|---|
| `Audience` | Who: `Is` (a party), `HasRole`, `MemberOf` (organization or `@current`, optional role, `Cascade`), `Authenticated`. All given conditions AND; none means anonymous |
| `Messages` | Message keys or patterns (`Area.Feature.*`, `*`); one policy covers a use case's family |
| `Fields` | `PolicyConstraint` per wire field name: `Any`, `Literal`, `Me`, `Current`, `RelatedAs(role)` (never the caller's own party), `MemberOf`, `MemberOfOrDescendant` |
| validity | `PolicyBuilder.Between(from, to)` for temporary grants |
| `EveryOrganization` | Reads the grant allows are not narrowed to the caller's branch (`AcrossEveryOrganization()`) |

**Refusals.** 401 when there is no session, 403 when there is one and no policy matches, 403
`FeatureUnavailable` when the install was not sold the feature.

**Entitlements.** `TenantState.Entitlements` (a JSON object of key to flag or number) is read with
`Entitlements.Read`, which answers permissively to anything it cannot parse. `Features` gathers the
declared keys (`FeatureItem`: `Key`, `Kind` of `Flag` or `Limit`, default `Enabled`/`Limit`); a key
nobody wrote about a tenant reads as on, with no ceiling.

**Helpers.** `PasswordHash.Hash` / `Verify` (PBKDF2-SHA256; `Verify(…, out needsRehash)` flags a
hash under the current count, and one outside the floor and ceiling is refused),
`KeyProtector(key or IEncryptionKeySource).Protect` / `Unprotect` (AES-GCM, a versioned payload with a per-payload salt; the
earlier format still unseals), `Jwt` (base64url and token splitting, not a JWT library),
`SafeReturnTo` (guards a return address before a redirect), `PrivateHost.Is(host)` (whether a
host a setting names points at loopback, private, link-local, CGNAT, unspecified or multicast
space, literally or by what it resolves to, or is empty, `localhost`, `*.localhost`, `*.local`,
`*.internal` or a single label; a name that does not resolve is not refused; it reads no options,
so each caller keeps its own switch, as Storage's `AllowPrivateAddresses`), `SignInRoutes` (the
sign-in address's query names).

## How it fits

- Messaging: the gate is a pipeline interceptor on the mediator ([../Messaging/README.md](../Messaging/README.md)).
- Generation: `Policies.Handlers` is filled by the source generator ([../Generation/README.md](../Generation/README.md)).
- Metadata: message keys come from the namespace through `MetadataProvider` ([../Metadata/README.md](../Metadata/README.md)).
- Data: the org filter reads the organization the gate vetted; `TenantState` carries the entitlements ([../Data/README.md](../Data/README.md)).
- Components: `[Authorize<T>]` on a page and menu visibility read the same policies on the client ([../Components/README.md](../Components/README.md)).
- Test: `HandlerHost` runs with the gate on and a session you set ([../Data/README.md](../Data/README.md)).

## Going deeper

- Guide: [permissions.md](../../../docs/permissions.md)
- Doctrine: [permissions.md](../../../plugins/nsail-stack/doctrine/permissions.md)
- Sample: [Security.cs](../Sample/NSail.Sample.Web/Security.cs) and [Policies.cs](../Sample/NSail.Sample.Wasm/Policies.cs)
- Tests: [NSail.Security.Tests](../../../test/Stack/Security/NSail.Security.Tests)

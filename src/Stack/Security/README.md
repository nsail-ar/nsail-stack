# Security

Authorization over messages: a policy answers one question — may this caller send this message
with these values? — and a send is allowed when at least one policy is fully satisfied. There
are no verbs and no deny rules; absence is denial. A policy constrains the values a caller passes
in a message's fields, never the rows a handler returns.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Security.Annotations` | `[PolicyField]` with `RestrictAs`, `[Requires]`, `[RequiresFeature]` | Sdk (through `NSail.Security`) |
| `NSail.Security` | `Session`, `Policy` and `PolicyBuilder`, `SecurityManager`, the gate (`AddSecurityEnforcement`), entitlements (`Entitlements`, `Features`), `PasswordHash`, `KeyProtector` | Sdk, Web, client, test |

## Use it

The Sdk references `NSail.Security` and declares a holder the generator fills with one policy
handler per message that carries `[PolicyField]` fields:

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
| `TicketEpoch` | `0` | The ticket generation this install honours. Raising it signs out everyone holding an older ticket and nobody else. `0` means never raised: nothing is stamped or refused. From `1` up, a ticket must carry exactly this generation |

**Registration**

| Call | Does |
|---|---|
| `AddSecurity()` | TryAdds `SessionProvider`, `RelationProvider`, `FeatureProvider`, `CredentialProvider`, `RequestOrganizationProvider`, `SecurityManager` with permissive defaults (anonymous session, every relation true, every feature on). A host with sign-in and a policy store replaces them |
| `AddSecurityEnforcement()` | `AddSecurity()` plus the `SecurityInterceptor` on every send: deny by default |
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
| `Fields` | `PolicyConstraint` per wire field name: `Any`, `Literal`, `Me`, `Current`, `RelatedAs(role)`, `MemberOf`, `MemberOfOrDescendant` |
| validity | `PolicyBuilder.Between(from, to)` for temporary grants |
| `EveryOrganization` | Reads the grant allows are not narrowed to the caller's branch (`AcrossEveryOrganization()`) |

**Refusals.** 401 when there is no session, 403 when there is one and no policy matches, 403
`FeatureUnavailable` when the install was not sold the feature.

**Entitlements.** `TenantState.Entitlements` (a JSON object of key to flag or number) is read with
`Entitlements.Read`, which answers permissively to anything it cannot parse. `Features` gathers the
declared keys (`FeatureItem`: `Key`, `Kind` of `Flag` or `Limit`, default `Enabled`/`Limit`); a key
nobody wrote about a tenant reads as on, with no ceiling.

**Helpers.** `PasswordHash.Hash` / `Verify` (PBKDF2), `KeyProtector(key).Protect` / `Unprotect`
(AES-GCM), `Jwt` (base64url and token splitting, not a JWT library), `SafeReturnTo` (guards a
return address before a redirect), `SignInRoutes` (the sign-in address's query names).

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

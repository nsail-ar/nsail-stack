# Permissions

**The message is the permission.** There are no verbs, no `CanEdit` flags, no permission
constants to keep in sync: a role may do exactly what the messages it is granted say, with the
values it is allowed to pass.

## Authorization constrains inputs, never outputs

A policy answers one question: *may this caller send this message with these values?*

```
policy {
  audience { memberOf: @current as doctor }
  messages: [ "Clinic.Prescriptions.CreatePrescription", "Clinic.Prescriptions.ListPrescriptions" ]
  fields   { DoctorId: @me }
}
```

`ListPrescriptions { DoctorId, PatientId }` can filter by doctor or by patient. The policy
above says a doctor may only pass their own id; an admin's policy says `DoctorId: *`. The handler
filters by the fields as it naturally would — **authorization never injects a `WHERE`**, and
there is no row-filtering model to reason about. The message decides what it returns: a lookup
that should expose only names returns a model with id and name.

Consequences:

- **A capability is a field.** If a caller should see "my patients' prescriptions", the message
  has the field that expresses it, and the policy pins it.
- **A view no field can express gets its own message.** "All prescriptions of all my patients in
  one grid" has nothing to constrain, so it is `ListOwnPatientsPrescriptions`, whose handler
  computes the set as part of its contract.
- **Create and Update are separate messages.** An update that does not carry `PatientId` cannot
  move a prescription to another patient — inexpressible, not merely forbidden.

## The shape of a policy

- **Audience** — who: one person, members of an organization with a role, holders of a role,
  anyone signed in. No audience means anonymous.
- **Messages** — what: a list of names or a wildcard (`Clinic.Prescriptions.*`). One policy covers
  a use case's family of messages, so the rows that describe one decision cannot drift apart.
- **Fields** — with which values: `*`, a literal, a list, `@me`, `@current` (the organization
  the session stands at), `@memberOfOrDescendant`, or a relationship to the caller.
- **Validity** — an optional window, for temporary grants and substitutions.

A send is allowed when **at least one** policy is fully satisfied: OR between policies, AND
inside one. There are no denies: absence is denial, and an exception is a finer message.

**Policy or validation?** A constraint always relates the value to the *caller*. A rule true for
everybody (`Date > today`) is validation: it lives on the message and nobody edits it.

## Deny by default, two refusals

Every message is denied until a policy grants it — including the ones a fresh install needs
before anyone signs in, which are granted by **built-in** policies with no audience. The gate
answers in two ways a client must tell apart:

- **403** — *you may not*. Signing in again changes nothing.
- **401** — *there is nobody here*. The session expired; signing in fixes it.

## The same answer on both sides

The policies are data, and the client holds the same ones the server enforces. So a page guarded
by `[Authorize<CreateProject>]` is not drawn for somebody who cannot send `CreateProject`, and a
button for an act the caller cannot perform is not offered — read from the same evaluation, not
from a parallel list of UI permissions. The server still decides: the client only draws by it.

## Tenants and organizations

- **A tenant is a wall, not a policy.** Customer companies are separated below the permission
  system — by connection or by column — and no grant crosses it.
- **Organizations are one company's own hierarchy** — branches, departments. They travel in the
  message as fields (`OrganizationId`), the UI fills them, and policies constrain them like any
  other field. Nothing is read ambiently from the token.

Full rules: [permissions.md](../plugins/nsail-stack/doctrine/permissions.md),
[data-tenancy.md](../plugins/nsail-stack/doctrine/data-tenancy.md).

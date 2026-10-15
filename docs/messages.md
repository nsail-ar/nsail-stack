# Messages

A message is a plain class that says what someone intends: `CreateProject`, `ListProjects`,
`CancelOrder`. It is the one thing the caller and the handler share, and the one thing the
Stack reads to build everything around them.

```csharp
[Http(Put, "api/tasks/projects/{id}")]
public class UpdateProject : IMessage
{
    [AsRoute]
    public required Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
```

## One declaration, many derivations

From that one type the Stack derives, without a line written for each:

| What | Derived from |
|---|---|
| The HTTP endpoint, its verb, route and binding | `[Http]`, `[AsRoute]` — the generator writes a minimal-API entry |
| The browser's typed sender | the same `[Http]`, from the Sdk's `[Generated(Http.Clients)]` holder |
| The host's in-process sender | the same `[Http]`, from `[Generated(Http.InProcess)]` |
| The TypeScript a React client imports | the Sdk's sources, at build time |
| Its name, `Tasks.Projects.UpdateProject` | the namespace (`{Area}.{Feature}.{Object}`) |
| Its permission key and its label in each language | that name, and `{name}.Permission` in the string catalog |
| The form's validation, on both sides | the DataAnnotations on the message |
| Each field's label | `{Area}.{Object}.{Member}` in the string catalog |
| The handler's registration | `[Injectable]` on the handler |

Each derivation is one less thing to keep in sync. A route lives on the message, so the client
cannot call an old address; a label key comes from the member, so renaming the member cannot
leave its label behind. This is the Stack's central bet: **what can be derived is never
written**, because what is written twice drifts.

## Intent, not plumbing

A message reads like an action or an event: `CreatePrescription`, `SignIn`, `OrderShipped` —
never `PrescriptionServiceRequest`. It carries what the act needs and nothing more.

Use one where a message earns its keep — crossing a boundary (browser → server, module →
module), naming an application command or query, announcing that something happened. Do not
use one for a private method call: a message that only adds indirection is a cost.

## One message per act

There is no `SaveProject`. Create and update are separate messages, and an act that changes one
thing is its own message (`RenameProject`, `CloseProject`):

- **C# has no *undefined*.** In a general `Save`, a `null` cannot say whether the caller cleared
  a field or never sent it. A specific message has no such field to be ambiguous about.
- **What a message does not carry, it cannot lie about.** An `UpdatePrescription` without a
  `PatientId` cannot move a prescription to another patient — not refused, *inexpressible*.
- **The message is the permission unit.** Granting "may close projects" is granting
  `CloseProject`; with a `Save` there is no way to grant one act without the others.

## Request and response

`IMessage` answers nothing; `IMessage<TResult>` answers a `TResult`. A handler implements
`IHandler<TMessage>` or `IHandler<TMessage, TResult>`, and one class may handle several:

```csharp
[Injectable]
public sealed class ProjectHandler :
    IHandler<GetProject, ProjectModel>,
    IHandler<UpdateProject>
{
    ...
}
```

Exactly one handler answers a send. Many may react to an event ([Events](events.md)).

**The response shape is the exposure.** A lookup that must show only names returns a model with
id and name; it does not return the entity and trust the screen to hide the rest. Authorization
constrains what a caller may *ask*; the message decides what it *answers*
([Permissions](permissions.md)).

## Names and the registry

Every message has a name derived from its namespace — `Acme.Tasks.Projects.UpdateProject` is
`Tasks.Projects.UpdateProject` — and the generator writes a registry of them, identical on the
server and in the browser. That name is what a policy grants, what a log reads, and what a
**pack** replays:

```json
{
  "name": "demo-data",
  "description": "Three projects to start with",
  "steps": [
    { "message": "Tasks.Projects.CreateProject", "body": { "id": "…", "name": "Website" } }
  ]
}
```

`PackReplayer` sends the steps in order through the same pipeline a screen uses — validation,
permissions and all. A message that answers `AlreadyExists` is skipped, so a pack can be run
again after a partial failure. Because a message's name, its body's shape and its label are all
derived, a catalog of messages is readable by anything outside C#: a policy editor, a seed file,
or a model calling tools.

## Refusals are values

A handler refuses by throwing a `BusinessException` carrying a `Problem` — `NotFound`,
`AlreadyExists`, `Conflict`, `InvalidModel`, or a code of its own. The HTTP edge writes it as an
RFC 7807-style body, and the client turns that body back into the same `Problem`, so a screen
draws a refusal from the server exactly as it draws one it found itself
([Sending](sending.md#refusals)).

Full rules: [messaging.md](../plugins/nsail-stack/doctrine/messaging.md),
[generation.md](../plugins/nsail-stack/doctrine/generation.md).

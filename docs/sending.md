# Sending

Every caller — a Blazor page, a handler, a background job, a pack — sends through one door:

```csharp
await mediator.Send(new UpdateProject { ... });
var project = await mediator.Send(new GetProject { Id = id });
```

What happens behind that door depends on where the code runs, and the caller never knows.

## The sender is a composition decision

`Mediator.Send` builds a pipeline and hands the message to the one `ISender<TMessage>`
registered for it:

| Where | Registered by | Path |
|---|---|---|
| The host that owns the handler | `[Generated(Http.InProcess)]` | `Mediator` → pipeline → `InProcessSender` → handler |
| A browser, or any remote client | `[Generated(Http.Clients)]` | `Mediator` → pipeline → `HttpSender` → HTTP → generated endpoint → handler |

The page's code is identical in both places. A Blazor component prerendered on the server sends
in process; the same component running in WebAssembly sends over HTTP. There is no fallback: no
sender registered is `SenderNotFound`, two is `MultipleSenders` — the composition decides
explicitly, so a message never silently travels a road nobody chose.

A future queue transport fits the same seam: its messages declare `[Queue]` instead of `[Http]`,
and its producer is the sender *everywhere*, including the handler's own host — a queue is a
promise about delivery, not about location.

## The pipeline

Between `Send` and the handler, the same steps run in the same order on every host:

1. **Interceptors** (`IInterceptor`) wrap the send. The security gate is one: it asks whether
   this caller may send this message with these values, before anything else spends a query.
2. **The message's own validation** — its DataAnnotations, the same ones the form used.
3. **Validators** (`IValidator<TMessage>`) — rules another module adds about this message,
   which need data the message does not carry. The kit keeps its contract; the app that knows
   more gets to say no.
4. **The sender**, and through it the handler.

The order is structural, not registration order. An unauthorized caller always meets
`Forbidden` — never `InvalidModel` — so a refusal never reveals what a valid request would look
like, and no rule runs a query for somebody who may not ask.

The same gate answers one layer earlier over HTTP: a generated endpoint carries its message type,
so a request a policy would refuse is turned away before ASP.NET binds its body.

## One operation, one transaction

**The outermost in-process send opens a scope and a transaction; every send nested inside it
joins them.** A handler that sends `CreateInvoice` and then `RegisterPayment` writes both in one
transaction with one `DbContext` — or neither, if anything fails. It commits when the outermost
send returns and rolls back when anything escapes.

A handler writes nothing to get this. The unit travels with the call (`AsyncLocal`, the one
channel nobody can forget to pass), so composing operations is just sending messages.

The boundaries are deliberate:

- **A send over HTTP is a separate operation.** Crossing a process is crossing a transaction.
- **Interceptors and validators run inside the unit**, so their writes roll back with the rest.
- **Parallel sends inside one operation are refused** (`ConcurrentSend`): they would share a
  `DbContext` that survives neither.
- **`AfterCommit` is the one hook.** Work that must not see uncommitted rows — enqueueing a
  notification about a row this operation wrote — registers there and runs right after the
  commit, never after a rollback.
- **Facts that are not the operation's** — a fee an external service charged, a ticket it issued —
  are written through a scope of their own, so a rollback cannot erase something that really
  happened outside.

## Refusals

A refusal is a `Problem`: a code, a title, and issues that name the fields they are about.

| Code | Status | Raised by |
|---|---|---|
| `InvalidModel` | 400 | the message's annotations, or `InputProblem` from a handler |
| `Unauthorized` / `Forbidden` | 401 / 403 | the gate — nobody signed in, or somebody who may not |
| `NotFound` | 404 | a handler, or the mapper on a missing row |
| `Conflict` | 409 | a save against a stale `Version` |
| `AlreadyExists` | 409 | a handler's uniqueness guard |
| `TooManyRequests` | 429 | `[Throttled]` on a public door |

The client receives the same `Problem` it would have built itself, and the form draws it in one
place: under the field it names, or in the form's own strip. A user never reads an internal id —
`AlreadyExists` names the value they typed.

## Headers

What an operation says *about itself* — which control asked, a correlation id — is a header,
never a field on the message:

```csharp
await mediator.Publish(new ProjectSaved { Id = id }, headers);
var source = _deliveries.Context?.GetHeader(MessageHeaders.Source);
```

Headers are an argument on the way in and an injected `MessageContextAccessor` on the way out —
no ambient write API that one call could leak into the next. Over HTTP they travel as
`X-NSail-{name}`. A header is caller input: correlate with it, never authorize by it.

Full rules: [messaging.md](../plugins/nsail-stack/doctrine/messaging.md),
[types.md](../plugins/nsail-stack/doctrine/types.md) (Problems).

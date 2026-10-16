# Events

A send has one owner and expects an answer. An event says that something *happened* and
expects nothing: zero, one or many parties react, and the publisher does not know who.

```csharp
await mediator.Publish(new ProjectSaved { Id = id });

using var subscription = mediator.Subscribe<ProjectSaved>(message => { ... });
```

An event is named in the past tense (`ProjectSaved`, `AppointmentCompleted`) and is published
**after** the act succeeds. It carries no `[Http]`: nobody outside can make one happen by calling
an endpoint.

## Where an event lives says who it is for

| Kind | Lives in | Reaches | Example |
|---|---|---|---|
| **UI event** | the client project | components in the same client | "a project was saved — refresh the list" |
| **Domain event** | the Sdk | `IHandler<TEvent>`s on the host, in any module | "an appointment completed — the app decides whether that bills" |
| **Pushed event** | the Sdk, with `[Pushed]` | every open browser of the same tenant, too | "the inbox moved — read it again" |

**A UI event** keeps screens independent. The edit form publishes `ProjectSaved`; the list, the
dashboard card and the counter in the menu subscribe. None of them knows the others exist, and
a new surface joins by subscribing. In a page, `Subscribe` unsubscribes by itself when the
component goes away.

**A domain event** keeps modules independent. A scheduling module publishes
`AppointmentCompleted`; a billing module that wants to react declares an ordinary
`[Injectable]` handler for it, referencing only the contract. Scheduling never learns that
billing exists.

## A reaction is not a request

The send that caused an event was already authorized. What reacts to it — every handler and
every send those handlers make — runs **without** consulting the caller's grants: the reaction
carries out a decision already taken, often in modules the policy's author never heard of. The
caller's identity is not elevated, only the gate is skipped, so a reaction still records who
acted. And because a domain event has no endpoint, the only code that can publish one is code
the app composed — reviewed like any other code.

## Pushed to the browser

`[Pushed]` on an event sends it, when published on the server, to every open client of the same
tenant, where it is published again through the client's own mediator. A page hears it with the
`Subscribe` it already has; nothing in a screen names a transport.

- **It carries no data.** The audience is a whole tenant, and every seat sees different rows, so
  a pushed event only says *something moved*; each listener re-reads through its own permitted
  query. A pushed event with a public property is a build error.
- **Nothing is replayed.** What happened while a connection was down is gone; every reconnect
  publishes `PushConnected`, and a listener that keeps a screen current asks again on it.
- **Two transports, the client picks.** SignalR or Server-Sent Events, both mapped on the server;
  a client references the one it wants and pays for no other.

## Why no store

There is no client-side store, cache layer or observable graph making screens refresh by
themselves. Each would be a layer every reader must learn before finding out *what refreshes
and why*, over state that already lives correctly in a page's fields. A general "something
changed" event fired at twenty cards is twenty reads — a poll wearing an event's clothes.

The cost of the explicit model is a habit: **when a message changes data a screen shows, name
every screen that shows it and check each one subscribes.** A published event nobody hears fails
in silence — a card that keeps saying "open" after the till was closed. Where a family of
surfaces makes it mechanical (dashboard cards), a test enforces it.

Full rules: [messaging.md](../plugins/nsail-stack/doctrine/messaging.md) (Events, Pushed to
clients), [permissions.md](../plugins/nsail-stack/doctrine/permissions.md) (Evaluation).

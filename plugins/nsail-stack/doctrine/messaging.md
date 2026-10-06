# Messaging

How messaging is used in NSail. Messages represent application intent: use them when they
express a clear action, decouple sender and receiver, and make the flow readable — for
coordinating modules, application commands and queries, domain or UI events, and crossing
boundaries (UI → API, module → module). Do not use them for purely local logic, where a
direct method call is simpler, or where they only add indirection.

---

## Core abstractions

| Type | Role |
|---|---|
| `IMessage` | Marker for a message without a return value |
| `IMessage<TResult>` | Marker for a message that produces a result |
| `IHandler<TMessage>` | Handles a message without a result |
| `IHandler<TMessage, TResult>` | Handles a message with a result |
| `Mediator` | Runtime entry point for Send, Publish, Subscribe |
| `ISender<TMessage>` | Transport sender (`HttpSender` on clients, `InProcessSender` on the handler's host) |
| `MessageRegistry` | Names every message the composition knows, and resolves a name back to its type |
| `PackReplayer` | Sends a pack of serialized messages, in order, through the Mediator |

Handlers are registered via `[Injectable]` and source generation ([generation.md](generation.md)).

---

## Mediator

```csharp
await mediator.Send(new CreatePrescription { ... });
var prescription = await mediator.Send(new GetPrescription { Id = id });
await mediator.Publish(new PrescriptionCreated { ... });

using var sub = mediator.Subscribe<PrescriptionCreated>(msg => { ... });
```

- **Send** — request/response: one logical handler, optional return value, deterministic
  execution. Use when there is a clear owner of the action.
- **Publish** — event-style: zero to many subscribers, no single owner, no response.
- **Subscribe** — temporary listeners, especially in UI; scoped via `IDisposable`, must
  not leak subscriptions.

---

## In-process vs remote

The same message type is the contract on both sides of HTTP. Server: `[Http]` on the
message → generated minimal API endpoints. Client: `[Generated(Http.Clients)]` → generated
`HttpSender`s registered as `ISender<TMessage>`; the named `HttpClient` they use is derived
from the declaring class's Area, not declared. Attributes and generator details:
[generation.md](generation.md).

`Mediator.Send` always resolves an `ISender`. Which sender is registered is a composition
decision per host, never an implicit fallback:

| Context | Registration | Mechanism |
|---|---|---|
| Host that owns the handlers (server, incl. prerender) | `[Generated(Http.InProcess)]` via `Add{Kit}WebApi` | `Mediator` → `SendPipeline` → `InProcessSender` → `IHandler` |
| Remote client (Wasm, external) | `[Generated(Http.Clients)]` via `Add{Kit}HttpClients` | `Mediator` → `SendPipeline` → `HttpSender` → HTTP → server endpoint → handler |

Both targets scan the same `[Http]` messages — `Http.InProcess` is the local counterpart of
`Http.Clients`. No sender registered → `SenderNotFound`; two
→ `MultipleSenders`. A future asynchronous transport (queue) will register its producer as
the sender **everywhere, including the handler's own host** — a queue is a semantic contract
(durability, retry), not a location boundary. Its messages declare `[Queue]` instead of
`[Http]`, so they are excluded from the HTTP targets by construction.

Client hosts get the host's own origin for every named client automatically; the
`HttpClients` section in `appsettings.json` is an override for kits that live elsewhere,
not a list to maintain.

---

## A use case's own notice is fire-and-forget; a channel a caller must confirm is not

A handler whose outcome is already saved before its notice goes out (an order, a sale, a
delivery confirmed) sends the notice through `DeferredWork` (`NSail.Background`), never
awaited inline — the vendor is SMTP or Meta's Graph API, and the operation has no reason to
wait on either. `WorkOrderNotices.Send` (Optical) is the shape to copy:
`DeferredWork.Enqueue(kind, tenant, work)` returns immediately; the runner owns its own
scope, re-enters the tenant, **runs in the install's default language** the way a job's loop
does (data-tenancy.md, Background jobs — a queue sets no culture either, and an item that ran
invariant found no WhatsApp template in a language nobody has), applies a 30s timeout and emits a
`nsail.deferred.work.completed` metric (tagged by kind and outcome), so a failure stays
visible without a task panel. This is NOT the `[Queue]` transport: it is in-process and not
durable, so a crash between Enqueue and pickup loses the notice — accepted because the
operation already persisted the fact that mattered.

**A notice about something THIS operation wrote enqueues through `AfterCommit`, not from inside
the handler.** The runner owns its own scope and connection, so a row the operation has not
committed yet is a row the queued work cannot see — and the notice's own reads (the recipient's
consent, above all) answer as if the person did not exist, which the channel reports as
`Untried` and nobody reads. `PartyHandler.Invite` is the shape: read every value on the live
scope, then `AfterCommit.Register` an enqueue, which the outermost send drains the instant the
unit commits (the rule, and the seam, below under the ambient unit of work). The bar is whether
the queued work has to **read** a row this operation wrote: `WorkOrderNotices` and
`ClientInvitations` enqueue directly, because everything theirs travels as a value and the
person they write to was on file long before.

**A caller that must KNOW the message reached the vendor before it answers the person does
not defer** — it calls `ChannelManager.Send` directly and awaits the `ChannelOutcome`, as a
2FA code (`SecondFactorManager`) or a channel verification (`ChannelVerifier`) does:
`Sent` is a fact the caller's answer depends on ("we sent you a code" vs "we could not
reach you"), and the `Reason` beside it is the refusing road's own sentence for the person
who triggered the send, so the only account of why nothing left is no longer the container
log. A deferred notice gets the same outcome and has nobody to tell it to; it reads `Sent`
and stops. The fold over a person's several addresses, and which screens report it, are in
inventory-kits.md (nsail: `docs/agents/inventory-kits.md`) (Channels).
The two paths share one `ChannelManager`, undisturbed by each other — a use case wanting
the fast lane reaches for `DeferredWork` itself; the shared sender does not grow a mode.

---

## Naming a message, and replaying a pack of them

**`MessageRegistry` (`NSail.Messaging.Runtime`) is the one door from a message's name to its
type.** The key is the permissions rendering of the metadata — `{Area}.{Feature}.{Object}` —
and the set comes from the **generator, never a DI scan**: the `Policies.Handlers` target
emits a `MessageRegistration` beside each `PolicyHandlerFactory`, so the server and the
WebAssembly client (which registers no handler) hold the identical registry. A scan would
hand the client an empty one exactly where the policy editor needs the keys.
`AddMessageRegistry()` composes it; `AddMessaging` and `AddSecurity` both call it.

`SecurityManager` **consumes** the registry (expands wildcards, hydrates handlers against
it) but does not own it: a caller that only wants name→type must not reach through the
security layer.

A message names itself three ways, and each is derived: the registry key is its name,
`WireSchema.For(type)` (types.md, Serialization) is the shape of its body, and the
`{key}.Permission` string is its label in each language — the three things a catalog built
for an author outside C# (a policy editor, a pack, a tool-using model) reads, none of them
written by hand.

**A pack is a curated, replayable list of message invocations**, with exactly one envelope:

```json
{
  "name": "...",
  "description": "...",
  "steps": [ { "message": "{Area}.{Feature}.{Message}", "body": { ... } } ]
}
```

`PackReplayer.Replay` sends the steps in order, resolving each name through the registry and
deserializing each body with `JsonOptions.Wire`. It knows nothing about what a pack contains
and does not find the file — **the owner hands it over**, which keeps an app's packs the
app's. A step answering `AlreadyExists` counts as `Skipped`, so a retry after a partial run
resumes. `PackFailure` sets what a real failure does — `Stop` ends at the first, `Collect`
reports every row — and the caller turns the errors into its own `Problem`; the replayer
names none.

---

## The ambient unit of work

**One in-process operation is one transaction.** The outermost `Mediator.Send` that resolves
to `InProcessSender` opens a service scope and a transaction; every send nested inside it —
at any depth — joins that scope, so the composing handler and everything it sends share one
`DbContext`, one connection and one transaction. It commits when the outermost send's whole
pipeline returns (including interceptor work after `next()`) and rolls back entirely when
anything escapes it.

**A composing handler writes nothing to get this.** `SaleHandler` sends `CreateVoucher`;
`WorkOrderHandler` sends `CreateSettlement` and `RegisterStockMovement`. The mechanism lives
once, in `AmbientUnitOfWork` (`NSail.Messaging.Runtime.UnitOfWork`), and travels by
`AsyncLocal` — the only channel a handler cannot forget to pass. The torn-transaction cases
(orphan rows, half-committed flows) are each pinned in `AtomicidadDelPipelineTests`
(`NSail.Optical.Scenarios.Tests`).

The boundaries, each deliberate:

- **HTTP sends stay outside.** A send crossing a process boundary is a second operation by
  design; `HttpSender` is untouched. If a seam becomes a service, the outbox joins there.
- **Every interceptor and validator runs inside the send's own unit** — outermost or nested.
  The pipeline asks the sender for the operation's scope *before* building anything:
  `InProcessSender` implements `IAmbientSender`, `SendPipeline.Execute` calls its `Run`
  first, and the interceptors, the message's DataAnnotations and every `IValidator` are
  constructed from the scope handed back — same scope, `DbContext` and transaction as the
  handler. So a rule's or interceptor's own writes are undone with everything else ("a
  refused document enrols nobody"). A transport sender implements nothing, owns no scope,
  and its pipeline is built from the caller's. Accepted consequence: the gate is an
  interceptor, so a send refused by `SecurityInterceptor` spends a `BEGIN`/`ROLLBACK` on an
  already-open connection — still before any handler, rule or query.
- **The transaction opens at the outermost scope, not at the first write.** There is no
  honest "first write" signal: an EF interceptor would have to be wired by every host (and
  lose atomicity when forgotten), and a guess from the message's shape would silently drop a
  command that returns a result. A read-only send pays one `BEGIN`/`COMMIT` on a connection
  it opens anyway.
- **A fork inside one operation is refused by name.** Two parallel sends from one handler
  would share a `DbContext` that survives neither — `ConcurrentSend` says so. Two
  *top-level* sends (two pages prerendering in one request) are separate operations with
  separate scopes.
- **The one thing a handler may say about the unit is `AfterCommit`** — `Register(work)` on the
  scoped `AfterCommit` (`NSail.Messaging.Runtime.UnitOfWork`), drained once by the outermost
  send right after `Commit` and never after a `Rollback`. It suppresses nothing and inspects
  nothing: it is the transactional on-commit hook, for work that must not race the rows this
  operation has not committed yet. It belongs to the *operation* — a nested send registers on
  the outermost one's instance, so a composing handler's registrations drain together with its
  inner handler's. A registration that throws is logged and swallowed: the answer the caller is
  waiting for is about a commit that stood, and raising would report a failure for a save that
  did not fail. The one caller is `PartyHandler.Invite`, whose `DeferredWork.Enqueue` the runner
  would otherwise win the race against — see the fire-and-forget section above.
- **There is otherwise no escape hatch** — no ambient to inspect, no way to suppress it. A fact that
  does not belong to the operation is written through a **scope of its own**
  (`CreateAsyncScope` — own `DbContext`, own connection, committed where written). The bar:
  the write is about something the operation did not do and cannot undo. The three cases, each
  pinned by a test that finds its row standing:
  - `AccessTickets` (`NSail.Arca.WebApi`): WSAA issues an access ticket and refuses a second
    inside the twelve-hour window, so a ticket row rolled back with a refused invoice would
    leave the next attempt asking for a ticket the authority still counts as held
    (`ArcaAuthorizationTests.ARefusedDocumentLeavesTheAccessTicketStanding`).
  - `ArcaAttempts`: a CAE ARCA granted is granted whatever happens to the operation, so the
    request row is opened before it leaves and closed with the answer on its own connection
    — rolled back, the document would be unauthorized here and authorized there, and the
    next attempt would bill twice
    (`ArcaAuthorizationTests.AGrantNeverAppliedIsAppliedWithoutAskingAgain`).
  - `AssistantUsages` (`NSail.Assistant.WebApi`): the vendor charges for every call the tool
    loop made before the one that failed or was cancelled, so a question refused or
    abandoned after them still cost the install — rolled back, the bill would be lost with
    the refusal or the cancellation
    (`AssistantQuestionTests.AnEngineThatGaveUpLeavesTheUsageRowAndRefuses`,
    `APersonWhoLeftMidAnswerIsBilledAndReadsNoRefusal`).

A handler calling `SaveChanges` mid-flow keeps doing so: with a transaction already open, EF
stops wrapping each call in its own, so every save is visible to the rest of the operation
immediately and durable only at the outermost commit. Both halves are pinned —
`ApproveClaimResponse` reads the row `CreateClaimResponse` saved in the same flow, and a
refusal after them leaves neither.

Store side: `SetDefaultDbContext<T>()` registers `DbContextUnitOfWork` as the scope's
`IUnitOfWork` ([data.md](data.md)), and `HandlerHost` calls `AddUnitOfWork()` for the same
reason. A host with no store (a client, a Wasm host) has nothing to enlist.

---

## Events (Publish/Subscribe)

Something happened and others react: `Mediator.Publish`. The past participle names the event
(naming.md); **where it lives says who it is for**, and neither kind carries `[Http]`:

- A **UI event** ("a party was saved, refresh the list") lives in the **`*.Shared`** project —
  it never leaves the client process. Example: `PartySaved` in `NSail.Directory.Shared`.
  Pages and partials subscribe via `NsPartial.Subscribe<TMessage>(handler)` — it marshals to
  the renderer and unsubscribes when the component unloads (backed by `NsComponent.Using`).
- A **domain event** ("an appointment completed; the app decides whether that bills") lives in
  the **Sdk** — it crosses module boundaries inside the host, not the wire, so another kit or
  the app handles it referencing only the contract. Consumers declare an ordinary
  `[Injectable]` `IHandler<TEvent>`; `InProcessPublisher` invokes every registered handler
  plus every subscription. Example: `AppointmentCompleted` in `NSail.Scheduling.Sdk`.
- The producer publishes after the operation succeeds:
  `await Mediator.Publish(new PartySaved { Id = ... })`.
- In-process publish is always available: `AddMessaging` registers `IPublisher<>` →
  `InProcessPublisher<>`. Publish is broadcast — future transport publishers coexist (no
  exclusivity, unlike senders).
- **A publish is not gated, and neither is what it causes.** `PublishPipeline` enters
  `AmbientPublish` around the broadcast, so the event, every subscriber and everything they
  send run without consulting the caller's grants: a reaction carries out a decision already
  authorized. It is entered structurally and stops at a process boundary. Full rule and the
  DI boundary it rests on: [permissions.md](permissions.md) (Evaluation, point 3).

### A mutation review checks both ends of the event, not just the write

This is the pub/sub case of testing.md ruling 12 (every surface that agreed with the old
truth is asked whether it still does).

**A published event with no subscriber is a surface that will not update, and it fails in
silence** — no error, no red test, just a screen that stopped telling the truth. Example:
`CloseTillPage` published `TillSessionSaved` correctly, but `TillCard` (the dashboard card)
never subscribed, so a closed caja kept showing OPEN with a button offering to close it
again; the fix was the one-line `Subscribe<TillSessionSaved>` every sibling already had.

**When a message ships that mutates something a screen renders, name every surface that reads
that data and confirm each one is subscribed — not just that the write side publishes.** For
dashboard cards this is a mechanism (`CardSubscriptionTests`, hosts.md); everywhere else it
is a review habit.

**A UI event a card can hear is published by a PAGE, in a `*.Shared` project.**
`Subscriptions<>` is scoped, so a publish from a background job — or any handler in its own
scope — reaches no subscription a component made (different scopes and, on WebAssembly,
different processes). So the Sdk's domain events (`AppointmentPlaced`, `HolidayWithdrawn`) do
not wake a card, and a card must not be written as though they might — unless the event is
`[Pushed]` (below). A card whose data only a server job changes hears nothing, and says so
where its exemption is written.

**A door that moves ANOTHER kit's ledger publishes that kit's event too.** A cobro reaches the
books through Cobrar Venta (Sales) and Cobrar OT (Optical) as well as Nuevo Cobro
(Accounting), and Accounting's cards may not reference either kit — so those doors publish
Accounting's `BooksPosted` beside their `SaleSaved`/`WorkOrderSaved`, as the Sales and Optical
doors that move goods publish Products' `StockChanged`. Both carry no id on purpose: what
changed is a balance or a shelf nobody holds, and the only honest reaction is to ask again.
Where doors share a seam, the seam publishes: `TillGate.Through` (which every act settling
against a drawer passes) publishes `BooksPosted`, so a new cobro door cannot forget.

NSail deliberately does not adopt a store, cache layer or observable graph to make this
automatic: it would cost a layer every reader must learn before finding out what refreshes
and why, on top of state that already lives correctly in a page's fields. A general
"something was saved" subscription is ruled out for the same reason — one event fired at
twenty cards is twenty reads, a poll wearing an event's clothes. The cost of a missed check
is a stale card, caught by someone looking at the screen.

### Pushed to clients

**A server-side publish of a `[Pushed]` event also reaches every open client signed in to the
same tenant**, published again through the client's own Mediator. A screen hears it with the
`Subscribe` it already uses; nothing in a page, a card or a kit's client code names SignalR.

- **The declaration rides the event** (`[Pushed]`, `NSail.Messaging.Annotations`), as `[Http]`
  and `[Throttled]` ride theirs. Nothing is registered per event, and nothing is pushed that
  did not say so.
- **It is generated, per kit, at both ends** (generation.md, `SignalR.Hubs` /
  `SignalR.Clients`). The server end registers a `PushPublisher<T>` as one more `IPublisher<T>`
  beside the in-process one; it sends the message's name (`PushedMessage<T>.Key`, the full
  type name — the one rule both ends read) and its body in `JsonOptions.Wire` to the group of
  this scope's `PushAudience`. The client end registers a `PushedMessage<T>` per message, and
  `HubFeed` (`NSail.Messaging.SignalR`, composed by `builder.AddPush()` in a Wasm host)
  publishes only what that closed list names — the server's word picks an entry, never which
  type a string becomes, and nothing is reflected at runtime.
- **Not exclusive with anything.** Publish is broadcast: the event still reaches every
  server-side `IHandler` and subscription in-process, and the push is one more audience.
  `[Pushed]` says nothing about `Send`; a message that is sent is a command, not something
  that happened.
- **The audience is the tenant, so a pushed event carries no row and no words.** Every seat
  of the tenant hears it whatever its branch. It says that something moved; the listener
  re-reads through its own org-scoped read and decides. Channels' `ConversationsMoved` is the
  shape: empty, answered by `ConversationWatch` asking the seat's own stamp (inventory-kits.md,
  Channels). The generator holds the rule: a `[Pushed]` event with a public property is
  `NSG003`, and `[Pushed]` on a type that is not an `IMessage` is `NSG002` — build errors,
  never a silent skip (generation.md, Diagnostics).
- **The wall is the edge's.** `PushHub` (`NSail.Messaging.WebApi`, `[Authorize]`, mapped by
  `UseBaseWebApi` at `PushFeed.Path`, under `api/` so an anonymous connect is a 401) joins each
  connection to its scope's `PushAudience`, and a publish reaches the same one. The Stack's
  messaging knows no tenant, so the audience is a seam: the install's one by default, the
  tenant's under `AddBaseWebApi` (`TenantPushAudience`) — the tenant `TenancyMiddleware`
  resolved for the request, after `TenantClaimMiddleware` refused a ticket minted for another.
  A client says nothing on the hub; it only listens.
- **Nothing is replayed.** What was pushed before a line was up — before the first connect
  or during a drop — is gone, and `HubFeed` publishes `PushConnected` every time the line comes
  up. A listener that keeps a screen current through a pushed event asks again on it: the
  catch-up is its own read. The feed retries forever on a fixed ladder — the first connect,
  a drop, and a line the server closed cleanly (a deploy stopping, an aborted connection),
  which SignalR's own reconnect leaves closed (`PushTests`, the drop) — except when the hub refuses the session (401/403: a cookie that expired, a
  ticket for another tenant): then it stops, and the sign-in that follows reloads the app and
  opens a feed of its own. A listener that throws on a pushed event or on `PushConnected` is
  logged through `ILogger<HubFeed>`, never swallowed.
- **The chrome opens it.** `NsSetup` calls `PushFeed.Open()` for a signed-in session and
  `Close()` on sign-out, because it is the one root every page renders under, in the scope the
  screens subscribe in. A host with no transport composes the closed `PushFeed`
  (`AddMessaging`), which is what the server prerender gets.
- **Publish after the work is done.** The push is fast enough to beat a sibling handler of the
  same publish, so an event that tells clients to re-read is published after the event the
  re-read depends on returns (`DbChannelJournal.Moved`: Tickets' routing files the arrival,
  then the clients are told).
- **One process.** Groups live in the process that holds the connections. A second replica of a
  product host needs a SignalR backplane before it ships.

---

## Headers, and the delivery's own context

An operation sometimes has to say something ABOUT itself that is not part of what it means —
which control asked for it, one day which request it came in on. That is a header (a
correlation id), never a field on the message: `PartySaved` keeps its shape, and
`IHandler`/`SubscriptionDelegate` keep their signatures.

```csharp
await mediator.Publish(new PartySaved { Id = id }, headers);   // write side: the call itself
...
var asker = _deliveries.Context?.GetHeader(MessageHeaders.Source);   // read side: injected
```

- **The write side is the argument, and only the argument.** `Send` and `Publish` take an
  optional `IReadOnlyDictionary<string, string>` before the token. There is deliberately **no
  ambient write API** (no `mediator.Context.Headers.Add(...)`): that is temporal and
  call-order coupling, and a caller would use it like `HttpClient.DefaultRequestHeaders` and
  leak a token onto the next operation.
- **The read side is `MessageContextAccessor`, injected** — the `IHttpContextAccessor` pattern.
  The pipelines open a `MessageContext` around the delivery; every interceptor, rule, handler
  and subscriber of THAT operation reads it; it is gone when the operation returns. Null when
  the call carried no headers.
- **A delivery's context is assigned, never inherited.** An event published from inside another
  delivery is its own operation and starts clean.
- **Values are strings**, so they cross HTTP with no invented serialization; names match
  case-insensitively, as HTTP's do.
- **Nothing but the Stack writes the ambient.** `AsyncLocal` is only the accessor's
  implementation. A test injects a `MessageContextAccessor` subclass and needs no flow.
- **The UI fills one header for free.** `NsPartial.Publish` adds `MessageHeaders.Source` from
  the component's own `Source`, which `NsPage` reads off its query. What that buys a lookup:
  [ui/fields.md](ui/fields.md).

### The same context after a transport hop

**A delivery says the same thing whichever transport carried it.** A send crossing HTTP
carries its headers as **`X-NSail-{name}`**, and the far side reads them through the same
injected accessor:

- **One prefix, owned once.** `WireHeaders` (`NSail.Messaging.Runtime.Context`) holds the
  prefix and both conversions. The logical name survives: the far side reads `source`, not
  `X-NSail-source`.
- **The sender stamps.** `HttpSender.WithContext` reads `MessageContextAccessor` and stamps
  what the open delivery carries — `SendPipeline` opens the context around everything and a
  transport sender owns no scope, so the sender runs inside it. **`ISender`'s signature says
  nothing about the wire**; the generated sender gained one constructor parameter.
- **The endpoint extracts.** The generated entry binds the `HttpContext` and calls
  `DeliveryHeaders.Read` (`NSail.Messaging.WebApi`). A request with no `X-NSail-*` header
  answers **null, not an empty context**, like a headerless in-process send.
- **Loose first, deliberately: there is no allowlist.** Every `X-NSail-*` header on the
  request becomes a delivery header.
- **Reserved names (not an allowlist).** `X-NSail-Tenant` is the transport's own word — the
  proxy strips the incoming copy and writes its own ([data-tenancy.md](data-tenancy.md#caddy-assigns-the-tenant-postgres-is-the-registry)) — so a delivery
  neither stamps it nor arrives carrying it; otherwise a header named `Tenant` would shadow
  the install's tenancy answer. `TenantHeaderReservationTests`
  (`NSail.BaseServices.WebApi.Tests`) holds the two declarations equal. `X-NSail-Version` is
  the second (below).

**The deferred tightening (an open task).** *An allowlist of accepted header names becomes
mandatory before any public or multi-tenant surface consumes a header.* Today any caller of a
generated endpoint can set any `X-NSail-*` name and value, so **a header is caller input**: a
handler may correlate with one, and must never authorize, identify or scope by one. The
tightening owes: the set of names an install accepts (and where a kit declares its own), a
ceiling on count and value length, and what a refused name does (dropped silently or
answered). The stamp, `HttpSender.WithContext`, uses `TryAddWithoutValidation` — which is
what lets an arbitrary NAME cross — and does not validate values.

Proof: `DeliveryHeadersCrossTheWireTests` (`NSail.Messaging.WebApi.Tests`) runs the real
generated sender and endpoint over one real request; the halves are pinned in
`NSail.Messaging.Http.Tests` and `NSail.SourceGenerator.Tests`.

### The answer says which build gave it

**Every response carries `X-NSail-Version`**, the server's build (`BuildVersionMiddleware`,
baseservices.md), and the client compares it against the build it booted with. Otherwise a
tab opened before a release keeps calling the new API with old generated senders until a
value that build cannot read arrives (an enum member added since, travelling as a string) and
dies in the browser over a 200.

- **The read is under the client, not inside the sender.** `ServerBuildHandler` is a
  `DelegatingHandler` added to **every** named client through
  `ConfigureAll<HttpClientFactoryOptions>` (like the default base address), so an
  unconfigured name is covered, and so is the **non-2xx** path, which leaves `HttpSender`
  through `HandleError` without returning a response.
- **The comparison is one rule in one place**, `ServerBuild.Answered` (`NSail.Builds`,
  types.md): a missing header, a blank one, and this process's own build are all silence.
  **Nothing stamps a version outside the container build** (`deploy/Dockerfile` passes
  `InformationalVersion` to the whole publish, client assembly included), so at a developer's
  desk both halves read the same default and compare equal.
- **It fires once, and offers**: the chrome above every page raises a non-blocking bar and
  the operator picks the moment (`NsSetup`, intentional-ui.md). Nothing reloads by itself — a
  forced reload mid-form loses what was typed.
- **`X-NSail-Version` is the second reserved name**: the transport speaks it on the way back,
  so a delivery may not speak it on the way out.

---

## Naming

Messages are short, explicit, intention-driven — they read like actions or events, not
technical constructs. Good: `CreatePrescription`, `SignIn`. Bad:
`PrescriptionServiceRequest`, `ProcessPrescriptionMessage`.

---

## Handlers

Small and focused: one action, one responsibility. Multiple message types can share one
handler class:

```csharp
[Injectable]
public class PrescriptionHandler :
    IHandler<CreatePrescription>,
    IHandler<GetPrescription, Prescription>
{
    // ...
}
```

Avoid large multi-purpose handlers and hidden side effects.

**A handler that reads or writes an org-scoped entity owes org-map.md a row** (Messages — where
each send stands), and so does the `IValidator<T>` beside it: whether the message names a branch
or consolidates over the seat's, what protects a row it names by id, and what a write that can
move the row between branches puts to the policy. `OrgAxisMessageTests` fails on a message with
no row, so a new send is classified in the same PR that founds it.

---

## Errors

Server handlers throw `BusinessException` (from `NSail.Problems`) for expected failures; the
HTTP layer serializes them as RFC 7807-style `Problem` responses ([types.md](types.md)).

**A Problem the user reads names what the user typed, never an internal id.** `AlreadyExists`
gets the conflicting VALUE the caller supplied — the name, the code, the tax id — never the
raw Guid. Some older call sites still pass the raw id: check the argument, not the
surrounding style, when you touch one.

---

## Validators

A composing app refuses a kit operation that breaks the app's rules by registering an
`IValidator<TMessage>` (`NSail.Messaging.Runtime.Validation`). **The mechanism is the
Stack's, the rule is the app's**: the kit keeps its contract, and the app that knows more
about the article gets to say no.

```csharp
[Injectable(As = [typeof(IValidator<UpdateProduct>)])]
public class ProductTypeValidator : IValidator<UpdateProduct>
{
    public async Task<Problem?> Validate(UpdateProduct message, CancellationToken cancellationToken)
    {
        // the Problem that refuses the send, or null to allow it
    }
}
```

`MessageValidator` (the message's own DataAnnotations — what the *message* declares about
itself) and `IValidator` (what a *module downstream* declares about the message) are
different jobs. Both run structurally after every interceptor — DataAnnotations first, then
business rules.

- **Refusal speaks `Problem`.** The pipeline turns a returned `Problem` into the same
  `BusinessException` a handler throws; nothing new travels on the wire.
- **Order is structural, not registration order.** `SendPipeline` runs DataAnnotations and
  every `IValidator<TMessage>` innermost — after every `IInterceptor`, before the sender. So
  no validation is reachable ahead of the security gate whatever the host or composition: an
  unauthorized caller meets `Forbidden`, never `InvalidModel` or `Invalid`, and never makes a
  rule spend a query. `SendPipeline` calls `MessageValidator.Guard` directly rather than
  resolving it from the interceptor bag, so no composition can land validation elsewhere.
- **One contract, either arity.** `IValidator<TMessage>` is keyed on the message alone — a
  save-time rule has nothing to say about what the operation returns.
- **First refusal wins** and short-circuits: a `Problem` carries one code and one title.
- **Register a rule where its data lives.** A validator that reads the database belongs to
  the host that owns it, never to a client; the client's own refusals are the message's
  DataAnnotations, which `SendPipeline` checks unconditionally on both sides.
- **An attribute may name its own problem code.** `MessageValidator` maps the BCL vocabulary
  (`Required`, `Mismatch`, `MaxLength`, `Range`, the format family) and answers the generic
  `Invalid` for everything else. A `ValidationAttribute` implementing `ICodedValidation`
  (`NSail.Messaging.Runtime.Validation`) supplies its own code, so its rows are
  `Problems.{Code}` in the owning module's catalog — and `Problems.{Code}.{field}` where one
  rule answers differently per field (the ladder `StringManager` takes for any Issue). That
  scoped rung is also how a field earns its own sentence with no attribute of its own:
  `Problems.InvalidFormat.AreaCode` (Directory) is a plain `[RegularExpression]` saying the rule
  in the screen's words.
  **The screen reads the same code off the same attribute, through the same switch**:
  `RefusalWords` hands the failing attribute to `MessageValidator.IssueFor` rather than keeping
  a vocabulary of its own, so the sentence under the field and a server refusal are one string
  in one file and neither end can gain a code the other does not say.
  - The Stack ships the mechanism and the domain-free attributes: `[NotFuture]` (calendar
    bound over `BusinessDate.Today`); `[NotNegative]` (floor at zero; zero passes);
    `[PositiveAmount]` (refuses zero, a negative, and a fraction under a cent);
    `[NotAbove(nameof(Other))]` (a "desde" held under its "hasta"; equal passes, and so does
    a pair with only one end). A rule about a country's tax id belongs to the kit owning the
    field (`[Cuit]`, Directory).
  - **The numbers its sentence names travel with it**: `Arguments` ({`from`}, {`to`}) fills
    the catalog's tokens from the same bound the attribute refuses by, so moving a bound
    needs no second edit.
  - **Every declared code has its sentence in every language**: `CodedValidationStringTests`
    (`NSail.Architecture.Tests`) sweeps the tree's declarations, not its attribute types — a
    code may be derived from the attribute's argument.
- **A rule a HANDLER owns names a code the same way.** `InputProblemBuilder.AddInvalid(code,
  field, message, arguments)` is the shape; the two-argument sibling puts every rule on one
  field under the shared `Invalid` code, so none of them can be worded or placed apart (the
  screen draws "Valor inválido" and names nothing). The same sweep counts these sites, so a
  minted code with no row fails the build, and a file half converted is the thing to avoid —
  the claim worth being able to make is that no bare `AddInvalid(field, message)` is left in it.
- **A rule may be about two members, and it reads the second off the model.**
  `MessageValidator` calls `GetValidationResult` with a `ValidationContext` built on the
  model, never the `IsValid(value)` overload, so a cross-field attribute refuses on the wire
  exactly as on the screen. The refusal is drawn under the member carrying the attribute, so
  a pair declares its order on one end only.
- **A nested model is walked only where a member opens it.** `MessageValidator` reads the
  message's own members; `[Validated]` (`NSail.Messaging.Runtime.Validation`) walks into the
  member's model, naming the field by its path (`Right.Sphere`) — which `NsForm` resolves to
  find the input. Opt-in per member on purpose: a row model (a sale line, a channel) is
  enforced by neither end until something says so, and the screen keeps the same scope
  (`ui/fields.md`). Optical's `CreatePrescription.Right`/`Left` are the precedent.
- **A number or a code declares its bound, or says on the record that it has none.**
  `DeclaredBoundTests` (`NSail.Architecture.Tests`) walks exactly `MessageValidator`'s reach —
  every shipped `IMessage` plus any model opened with `[Validated]` — and fails the build when
  a numeric or `string` member declares nothing. A number wants a range (`[Range]` **or a
  subclass**, `[NotNegative]`, `[NotAbove]`; a floor alone counts). A string wants a length
  or a format. A string whose name reads as an identifier (`*Cae`, `*Code`, `*Cuit`, `*TaxId`
  — the list lives in the test) must have a FORMAT: a CAE under `[MaxLength(14)]` still takes
  "abc". The opt-out is `[Unbounded("<why>")]` (`NSail.Messaging.Annotations`, in scope in
  every `.Sdk`); it has no runtime behaviour, and **the reason is the record** — required by
  the constructor, a blank one refused. It covers both honest cases: the domain names no
  bound (`uint Version` is a concurrency token, not a quantity), or the bound depends on a
  value the message does not carry and an `IValidator` owns it
  (`AuthorizeWorkOrder.CoveredAmount` runs from zero to the order's own total). Answering the
  gate is a decision, never a number picked to reach green (testing.md, ruling 11). Out of
  scope, deliberately: dates, `TimeSpan`, `Guid`, `bool`, enums and collections — dates are
  ~75 distinct decisions (a birth date is not a booking date) and are the widening testing.md
  ruling 3's shrinking exemption list is for.
- **A bound is declared once where it repeats.** `PagedMessage` (`NSail.Paging`) carries
  `PageIndex`/`PageSize` for the 37 list messages, and `[SearchTerm]`
  (`NSail.Messaging.Runtime.Validation`, a `MaxLength` subclass) caps a typed search term for
  the 57 that have one. `PageSize = 0` still means "the caller did not choose" (the handler's
  clamp answers it), so the declaration refuses only what no caller sends.

### The gate answers before model binding too

The pipeline starts only after ASP.NET has bound every parameter of the generated endpoint,
so anything refused during binding — a `[FromBody]` DTO with no body, an omitted `required`
query member — would answer `400 InvalidModel` ahead of the security gate. So the gate is
answered one layer further out by `EndpointGateMiddleware` (`NSail.BaseServices.WebApi`),
which `UseBaseWebApi` runs after authentication and ahead of `MapEndpoints`:

- **It reads the message off the route, not the request.** Every generated entry carries
  `MessageEndpointMetadata` (`.WithMetadata(...)` in the entry template), so the matched
  endpoint names its message type before the body is read.
- **It asks `SecurityManager.PreAuthorize`, never `Authorize`** — there is no message yet to
  read field constraints from. `PreAuthorize` ignores fields, so it can only be *more*
  permissive than `SecurityInterceptor`: a false answer means no policy applies to that key
  for that session at all, which `Authorize` would refuse whatever the values.
- **`SecurityInterceptor` is still the real guard**, unchanged. This one denies sooner; it
  never allows.
- **Opt-in like the interceptor.** `AddSecurityEnforcement` registers the
  `SecurityEnforcement` marker; without it the middleware no-ops, so a client or Wasm host
  stays permissive and lets the server say 403.

An authorized caller notices nothing: binding runs as before, and a malformed request gets
the same `400`/`422`.

Known and deliberate: a wrong `Content-Type` still answers **415** ahead of everything — the
router's content-type matcher decides during endpoint *selection*, before any endpoint or
metadata exists to gate on. It is transport negotiation: it names no field, spends no query,
and answers every caller identically.

`EndpointGateTests` (`NSail.Architecture.Tests`) pins the contract: every generated endpoint,
probed anonymously with no body and no content type, must answer the gate's denial. It runs
against a host with **no policy registered at all**, so the sweep is total, with no
anonymous-exception list to maintain.

### A public door declares its ceiling, and the message is where

**`[Throttled(perMinute)]`** (`NSail.Messaging.Annotations`) caps how often one caller may send
a message over HTTP. `ThrottleMiddleware` (`NSail.BaseServices.WebApi`) reads it off the same
`MessageEndpointMetadata` the gate reads and partitions .NET's fixed-window limiter per
**message and caller**, the caller being `Connection.RemoteIpAddress` (already rewritten by
`UseForwardedHeaders` where the install runs behind a proxy). Refusal is a `Problem`,
`SecurityProblem.TooManyRequests` — 429, code `TooManyRequests` — so a client never has to
recognise a refusal by status number alone.

- **The declaration rides the message, never a path**: the route is derived from the
  message, so a path-based declaration could only name an address that had moved.
- **It stands in FRONT of the gate.** Volume is not a permission question; a flood is refused
  before any policy evaluation.
- **Who declares one**: a message whose endpoint is reachable by somebody the install never
  authenticated — a door opened by a token in a URL (`Channels.Replies.*`), a webhook. A
  message behind a policy is already bounded by who holds the grant; a ceiling there would
  only refuse a customer mid-work. The budget is per message, not shared, so a flood on one
  public door cannot close the others.
- **Write the number for the worse reading of "one caller".** An install that does not set
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` sees the proxy's address for everybody, so the ceiling
  must hold for a whole shop; an install that turns the header on gets the per-person reading
  for free. On a token door a ceiling limits the cost of being hammered, not guessing — 32
  bytes of entropy makes guessing infeasible at any rate.

Proof: `ThrottleTests` (`NSail.BaseServices.WebApi.Tests`), through `AddBaseWebApi`/
`UseBaseWebApi` as a product calls them.

---

## Anti-patterns

- multiple handlers for a single `Send`
- using messaging for every method call, or replacing a simple private method call with a message
- over-abstracting simple logic
- tightly coupling handlers to infrastructure details

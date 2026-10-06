---
name: new-message
description: Add an NSail operation end to end — a message in the Sdk ([Http] route, validation), its IHandler in WebApi, the Row/Ref/Model projection, BusinessException failures and strings; endpoint, DI and HTTP client are generated. Use when adding a query or command such as ListInvoices, GetInvoice, CreatePayment or LookupSuppliers, when choosing a verb (there is no Save), or when a send fails with SenderNotFound.
---

# New message

An operation in NSail is a message plus a handler. The endpoint, the DI registration and
the HTTP client are generated — you write neither a controller nor a `services.Add` call.
Read `${CLAUDE_PLUGIN_ROOT}/doctrine/messaging.md` and `${CLAUDE_PLUGIN_ROOT}/doctrine/generation.md`.

## Pick the verb and the shape together

The verb set is **closed**, and each query verb owns its result shape, so the consumption
names both types:

| Consumed by | Message | Returns |
|---|---|---|
| a grid | `List{Entity}s` | `{Entity}Row`, paged in a `DataPage<T>` |
| a dropdown | `Lookup{Entity}s` | `{Entity}Ref` — id plus display, nothing more |
| a form | `Get{Entity}` | `{Entity}Model` |

Writes are `Create{Entity}` / `Update{Entity}` / `Delete{Entity}`. **There is no `Save`**:
separate contracts are what make it impossible for an update to carry an immutable field.
Search is a filter *inside* `List{Entity}s`, never its own verb.

`Create` and `Update` carry their own flat fields, duplicated on purpose — the two
contracts must be free to differ, and `[PolicyField]` marks flat fields.

The handler that copies a message onto rows, or rows onto a `Row`/`Model`/`Ref`, does it
through the generated mapper (`[MapFrom]`/`[MapTo]` on the entity): the `new-mapping` skill.

## Steps

1. **Message** in the Sdk, in the module folder — `{Kit}.Sdk/Invoices/ListInvoices.cs`:
   - `IMessage` for no result, `IMessage<TResult>` for one
   - `[Http(Get, "api/invoices")]` — the `Method` enum, never ASP.NET's `[HttpGet]`
   - bind with `[AsRoute]`, `[AsQuery]`, `[AsHeader]`; POST and PUT default to the body
   - `[Required]` and friends for validation — rules true regardless of *who* asks belong
     here, not in a policy
   - route constraints matter: `{id:guid}` filters the match instead of reaching a broken
     handler
2. **Projection** in `Models/` if the verb needs one. A `Ref` carries id and display only —
   the response shape is what enforces exposure, not frontend goodwill.
3. **Handler** in the WebApi project, `[Injectable]`, implementing `IHandler<TMessage>` or
   `IHandler<TMessage, TResult>`. Several message types may share one handler class.
   Inject `DbContext` and use `Set<T>()`; there is no repository layer.
4. **Failures**: throw `BusinessException` with a `Problem`. The HTTP layer serialises it.
5. **Nothing to wire.** The generators pick the message up from the existing
   `[Generated(...)]` holders. If a send fails at runtime with `SenderNotFound`, the host is
   missing `Http.InProcess` — there is no implicit fallback by design.
6. **Localization**: keys derive from the type (`{Area}.{Message}.{Field}`). Add the strings
   to that project's `strings.json` and `strings.es.json`. A missing key renders as the key.
7. If the message is a dependency of a use case — a lookup feeding a dropdown — declare it
   on the consumer with `[Requires(typeof(LookupParties))]`. Only lookup-shaped messages
   belong there.

## Checks

- `dotnet build NSail.sln` — the generators run at build, so wiring mistakes surface there.
- Two handlers for one `Send` is a defect; so is a route string written anywhere but on the
  message.

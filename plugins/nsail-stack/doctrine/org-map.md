# The org map

This file classifies every entity in the model against the org axis, then every MESSAGE that
reads or writes one of those entities (Messages — where each send stands, below). It is the input
to the filter, never the filter: nothing here adds a column, a WHERE or a handler edit. The filter
reads it through one marker — every row in the org-scoped table carries `IOrgScoped` in its own
file — and `OrgAxisTests` fails on an entity that carries an organization column without an
answer on this page ([data-tenancy.md](data-tenancy.md), The org filter). The message half is
held by `OrgAxisMessageTests`, which parses the message table below (keep its heading, `### The
table`, and its row shape).

The tenant is a different axis ([data-tenancy.md](data-tenancy.md), The tenant is a column): it filters everything, always,
and no row on this page escapes it.

---

## The law

One question decides every row, and it is asked of the row, not of its columns:

- **org-scoped** — it records something that **happened AT a branch**. A sale rung up, stock
  moved, a voucher numbered, an appointment kept.
- **tenant-level** — the **company owns or defines it**. Its clients, its catalog, its chart of
  accounts, its books, its people and the grants over them.
- **shared** — **nobody edits it**. One planted row answers every tenant; the marker is
  `IInstallScoped` and this bucket is exactly what carries it.

The worked examples the law was written against, and where they land:

| Said as | Reads as | Bucket |
|---|---|---|
| the ledger | `JournalEntry`, `EntryLine`, `FiscalPeriod` | tenant-level |
| the chart of accounts | `Account`, `EntryTemplate`, `VoucherTemplateLine` | tenant-level |
| debtors | no entity of its own — a `Party` carrying a balance through `EntryLine.PartyId` | tenant-level |
| parties | `Party` and its channels and locations | tenant-level |
| catalogs | `Product`, `ProductVariant`, `Brand`, `Category`, `Color`, `Insurer`, `Offering` | tenant-level |
| vouchers | `Voucher`, `VoucherLine`, `ArcaAuthorization` | org-scoped |
| settlements | `Settlement`, `Tender` | org-scoped |
| tills | `PointOfSale` — the drawer, not the method that fills it | org-scoped |
| stock | `StockMovement`, `Store` | org-scoped |
| points of sale | `PointOfSale` | org-scoped |

**The boundary: an org is a branch INSIDE a tenant, never the tenant itself.** The org is a
permission scope, like an Azure resource group, so what it scopes is access to what happened,
not ownership of what the company is — which pins the ledger, the chart and the fiscal periods
tenant-level even though every document feeding them is org-scoped.

**A legal identity belongs to an organization and is inherited down the chart.** The normal
shape is one `Organization.LegalPartyId` at the root and every branch issuing under it; a branch
may name its own — a collaborator who invoices as a monotributista — and then it and everything
under it issue under that one. `LegalIdentities` (`NSail.Directory.Data`) is the one walk: its
own, else the nearest ancestor's, and every reader asks it rather than climbing parents itself.
**What stays tenant-level is the ledger**: the books, the chart and the fiscal periods, whose
rows below still say "one CUIT". Until the fiscal authority's settings can hold more than one
CUIT, a branch whose resolved identity is not the configured one cannot authorize a comprobante
and is refused by name (`ArcaAuthorizationProvider`); books, bank accounts and an ARCA
registration per legal entity come when a real second razón social shows up.

**A row that inherits its answer states its parent, not its reason.** A line, a tender, a
voucher belongs where its head belongs; the satellite rule (permissions.md) keeps one link at
rest and carries the org beside it on the wire.

**An `OrganizationId` column is not the classification.** It answers two different questions —
*where this happened* (`Sale`, `StockMovement`, `WorkOrder`) and *which branch this row is
ABOUT* (`Membership`, `Branding`, `OrganizationChannel`). Only the first is the axis. The rows
that DEFINE the org graph — the organization, its satellites and the grants over it — are read
to compute the scope, so scoping them by it is circular; they are tenant-level and say so.

## The party axis

Two rows on this page are **edges** rather than documents: `Membership` says which branch a
person stands in, `Relationship` which branch a pair of people are related within. They are
tenant-level for the reason just given — the scope is computed FROM them — so the model's org
filter never touches them, and a list of ONE person's edges would otherwise hand a branch that
person's rows in every other branch. That is the party axis, and it is answered where the list
is rather than where the model is.

A third row stands on this axis for a different reason: `Conversation` is not an edge the
scope is computed from, it is a document whose branch is **optional by nature** — what arrived
from outside names a branch only when the source says which of the install's own addresses it
reached. Everything below holds for it as written, and the mechanical bar is the same:
`OrgFilter.Axes` takes a non-nullable `Guid` column and throws at model build otherwise, so a
nullable organization is answered in the list or nowhere.

- **`ListMemberships` and `ListRelationships` compare the edge's organization against the
  subtree the operation already resolved** (`OrgScopeProvider`, data-tenancy.md — The org filter): the
  same set the org filter is set to, read rather than walked a second time. In-query, always —
  the page's `TotalCount` is read from the same query, and a count that says two over rows that
  say one has leaked the row it hid (permissions.md, Evaluation).
- **Neither message carries an `OrganizationId`, and neither may grow one.** The subject of both
  is a person. The branch is not the caller's to claim, it is the seat the caller stands in, so
  a field here would be a second axis on a list about a person — one a caller could narrow and
  never widen. That is also what keeps the subtree above the session's: the operation's scope is
  the organization the MESSAGE names (data-tenancy.md, The org filter), and a message that names
  none falls back to the seat — which is the whole intent here.
- **An edge that names NO organization is the tenant's own, and every seat in the tenant reads
  it** — the branch's and the root's alike. `Relationship.OrganizationId` is nullable and
  `CreateRelationshipPage` leaves it optional, so a subtree WHERE written literally would hide
  those edges from every session, the root included. Naming no branch means no branch owns it,
  which is what tenant-level means everywhere else on this page.
- **A by-id read of an edge is not this axis.** `GetMembership` reads across the chart exactly
  as `GetParty` does: its subject is tenant-level, so there is no branch in the message for
  anyone to constrain — the sentence `OrganizationAxisTests`' by-id predicate states for both.

## What has no row here

**The map is the model, not the folder.** Owned types have no table and ride their owner's row:
`Eye` (rides `Prescription` and `FittingMeasure`), `BrandingColors` (rides `Branding`). Two
files under `Entities/` are not entities at all — `Validity` is a predicate, and
`OrganizationHierarchy` is a picker. Optical's product extensions (`FrameProduct`,
`LensProduct`, `ContactLensProduct`, `LiquidProduct`) do have tables and do have rows, one each.

**The entity tables are kept by hand.** Every row of the org-scoped table carries `IOrgScoped`.
A row whose head is across a kit boundary (`ArcaAuthorization`) carries the column itself: the
branch travels on the contract and is stamped, rather than reached through a join nobody may
write. No test checks that every marked entity is listed here, or that the tenant-level table is
complete; drift there is caught only by reading.

---

## Org-scoped — it happened at a branch

| Entity | Kit / App | Why |
|---|---|---|
| `WorkOrder` | Optical | The job taken in at a branch; carries the column already |
| `WorkOrderLine` | Optical | Rides `WorkOrder` |
| `Sale` | Sales | Rung up at a branch; carries the column already |
| `SaleLine` | Sales | Rides `Sale` |
| `SaleBundle` | Sales | Rides `Sale` — a group of one sale's lines, standing where the sale that grouped them does |
| `SaleReturn` | Sales | Rides `Sale` |
| `SaleReturnLine` | Sales | Rides `SaleReturn` |
| `SaleModifierLine` | Sales | Rides `Sale` — an Ajuste is priced onto the sale that was rung up, so it stands where the sale does |
| `PurchaseOrder` | Products | Raised by a branch into its own store; carries the column already |
| `PurchaseOrderLine` | Products | Rides `PurchaseOrder` |
| `PurchaseOrderVoucher` | Products | Rides `PurchaseOrder` — the invoice a branch's order was billed with; carries the column already |
| `StockMovement` | Products | Stock moved at a branch; carries the column already. It names a `ProductVariant` — what moved is the unit, and the unit is catalog |
| `Store` | Products | A stockroom standing in a branch; carries the column already |
| `ConsultingRoom` | Therapy | A room standing in a practice, the same sentence as `Store`; carries the column already. It is not an organization — one is a grouping of people, a room is a place — so the circularity that pins the org graph tenant-level does not reach it |
| `PointOfSale` | Accounting | A drawer standing in a branch — ARCA numbers per domicile |
| `Settlement` | Accounting | Money taken at a branch; carries the column already |
| `Tender` | Accounting | Rides `Settlement` |
| `TillSession` | Accounting | Rides `PointOfSale` — a shift is one drawer's, opened and counted at the branch it stands in |
| `Voucher` | Accounting | Rides `PointOfSale` — the till is where the number was issued |
| `VoucherLine` | Accounting | Rides `Voucher` |
| `ArcaAuthorization` | Arca | The CAE is asked for against one point of sale, so the attempt stands where that till stands. It carries `OrganizationId` on its own row rather than riding `Voucher`: the ride is a soft reference across a kit boundary and the model has no join to the head, so the branch is stamped off the authorization request (`IVoucherAuthorizationProvider`) |
| `SaleSettlement` | Sales | Rides `Sale` — which of Accounting's receipts answered it, and for how much; the link stands where the sale does |
| `ScheduledMessage` | Channels | One promise to send later, signed by the branch that filed it — the `Outbound` sentence one clock earlier; carries the column already |
| `Agenda` | Scheduling | A diary open at a branch; carries the column already |
| `BookingInvitation` | Scheduling | The one door a person with no session opens, and the one table in the kit an anonymous read touches; carries the column already, on the row rather than through the agenda, because it is read while standing in no branch |
| `Appointment` | Scheduling | Rides `Agenda` |
| `AppointmentAttendee` | Scheduling | Rides `Appointment` |
| `AppointmentCancellation` | Scheduling | Rides `Appointment` |
| `Availability` | Scheduling | Rides `Agenda` |
| `AvailabilityException` | Scheduling | Rides `Agenda` |
| `Series` | Scheduling | Rides `Agenda` |
| `SeriesAttendee` | Scheduling | Rides `Series` |
| `Holiday` | Scheduling | A branch closes its own doors; carries the column already |
| `Encounter` | Medical | Someone was attended AT a branch; carries the column already. The organization is where the people stood, `LocationId` the room inside it |
| `ChargeItem` | Medical | Rides `Encounter` — a practice happens inside the act that performed it |
| `Claim` | Medical | Handed to a payer at a branch and numbered per branch; carries the column already |
| `ClaimResponseItem` | Medical | Rides `ChargeItem` — what the payer decided about one practice is the branch's, wherever the liquidación's own head sits |
| `WorkOrderCoverageSettlement` | Optical | Rides `WorkOrder` — which of the sale's receipts is the payer's leg on this order; the handle Autorizar needs, standing where the order does |
| `AppointmentNote` | Therapy | Rides `Appointment`; the note itself stays the patient's |
| `AppointmentEncounter` | Therapy | Rides `Appointment`; both ends are org-scoped already, so the hitch declares no column of its own |
| `EncounterVoucher` | Therapy | Rides `Encounter` — the comprobante a session was billed with, keyed on the act so a second one is unrepresentable |
| `EncounterSettlement` | Therapy | Rides `Encounter`; settlement side unique, a receipt answers to at most one session |
| `Outbound` | Channels | One attempt to send from a branch — sent or refused — signed by the organization the sender named; carries the column already |
| `Ticket` | Tickets | Opened at a branch, the `Outbound`/`Encounter` sentence; carries the column already |

## Tenant-level — the company owns or defines it

| Entity | Kit / App | Why |
|---|---|---|
| `Party` | Directory | A client loaded at one branch must be the same client at the next |
| `PartyChannel` | Directory | Rides `Party` |
| `PartyLocation` | Directory | Rides `Party` |
| `Location` | Directory | One address book; a branch and a person point at the same row |
| `Country` | Directory | The company writes its own places (the lookups' quick add), so each tenant is born with its own copy (geography.md (nsail: `src/Kits/Directory/docs/geography.md`)) |
| `Province` | Directory | Same as `Country` |
| `City` | Directory | Same as `Country` |
| `TenantInfo` | Directory | The tenant's own identity — the one row every branch's site hangs off; a branch neither owns it nor edits it |
| `Organization` | Directory | The branch IS the axis, not a row on it |
| `OrganizationChannel` | Directory | A branch's phone, dialled from anywhere in the company |
| `OrganizationLocation` | Directory | A branch's address, read from anywhere in the company |
| `Membership` | Directory | The grant that DEFINES the scope; the evaluator reads it to compute one. What scopes a list of one person's is The party axis, above |
| `Relationship` | Directory | The graph `RelationProvider` walks; scoping it by the walk is circular. Same axis for its list, and the nullable column is ruled there |
| `Role` | Directory | The company edits its own roles (`UpdateRolePage`) |
| `User` | Iam | A person signs in once for the company, not once per branch |
| `Login` | Iam | Rides `User` |
| `LoginInvitation` | Iam | Rides `Party` |
| `Verification` | Iam | Rides `Party` |
| `SignInLink` | Iam | Rides `User`; a one-time link opens the same session at any branch the user stands in |
| `Policy` | Iam | The company's own grants (`UpdatePolicyPage`) |
| `AssistantUsage` | Assistant | The install's bill — a branch could not hide its consumption from the company (the `ClinicalAccess` reasoning); its `OrganizationId` says where the person stood when asking, not where a fact happened |
| `Practitioner` | Medical | A professional of the company; where they work is `Membership` and `Agenda` |
| `Note` | Medical | A clinical fact about the patient, readable wherever they are seen |
| `DocumentReference` | Medical | Rides `Note` |
| `ClinicalAccess` | Medical | An audit trail a branch could hide from the company is not an audit trail |
| `Consent` | Medical | A signed document about the patient, not about a branch |
| `ConsentAccess` | Medical | Same as `ClinicalAccess` — the company audits, not the branch |
| `Referral` | Medical | A clinical fact about the patient |
| `SaleModifier` | Sales | Catalog the company keeps — "Cliente recurrente −5%" is the shop's price policy, not one branch's |
| `Insurer` | Medical | Catalog the company keeps |
| `InsurancePlan` | Medical | Rides `Insurer` |
| `Coverage` | Medical | The patient's coverage, a fact about the person |
| `Specialty` | Medical | Catalog the company keeps; it groups the practices below and the credentials above |
| `ChargeItemDefinition` | Medical | Catalog — what this practice does and charges for, company-wide |
| `InsurancePlanBenefit` | Medical | Rides `InsurancePlan` — what a payer pays for a practice is the company's agreement, not a branch's |
| `ClaimResponse` | Medical | The insurer's word about a patient; the `WorkOrder` consuming it carries the org. Its `ClaimId` is OPTIONAL, so a liquidación answering a branch's folder does not make the row a branch's — the adjudication rows below it are the ones that ride a practice |
| `Product` | Products | One catalog and one price list for the company |
| `ProductVariant` | Products | Rides `Product` — the stockable unit is still catalog: what a branch does with it is the `StockMovement`, which carries the column |
| `ProductAttribute` | Products | Rides `ProductVariant` — the key/value text that tells one unit of an article from another |
| `ProductDimension` | Products | Rides `Product` — how the article is broken down is the catalog's declaration, the same axis as the variants it crosses into |
| `ProductDimensionValue` | Products | Rides `ProductDimension` |
| `ProductCategory` | Products | Rides `Product` |
| `ProductImage` | Products | Rides `Product` |
| `Brand` | Products | Catalog |
| `BrandSupplier` | Products | Rides `Brand` |
| `Category` | Products | Catalog |
| `Supplier` | Products | Satellite on `Party` — the company defines who it buys from, beside `Brand`; a branch raises the order, it does not enrol the supplier |
| `PriceList` | Products | The company defines what it buys at and quotes at; a branch neither owns the list nor its currency |
| `PriceListItem` | Products | Rides `PriceList` |
| `SellingPriceList` | Products | The company decides what it SELLS at — the rules over the cost list above; the list declares which branches and channels it prices, a branch does not write one |
| `PriceRule` | Products | Rides `SellingPriceList` |
| `SellingPriceListScope` | Products | Rides `SellingPriceList`. Carries `OrganizationId` and is tenant-level anyway: the column says which branch the LIST is about, the way `PriceList.PartyId` says whose costs it holds — a branch reading only its own pair could not see the company's price scheme it is part of |
| `Account` | Accounting | The chart is the shop's own (`CreateAccount`) — data-tenancy.md's directional law |
| `EntryTemplate` | Accounting | Names accounts, so it belongs to whoever owns them |
| `VoucherTemplateLine` | Accounting | Names accounts, so it belongs to whoever owns them |
| `VoucherType` | Accounting | The company edits it (`IsEnabled`) |
| `Currency` | Accounting | The company edits it (`CreateCurrency`) |
| `JournalEntry` | Accounting | One CUIT, one ledger — branches share the legal entity |
| `EntryLine` | Accounting | Rides `JournalEntry` |
| `FiscalPeriod` | Accounting | One CUIT, one exercise |
| `PartyFiscalCondition` | Accounting | Rides `Party` |
| `SalesChannel` | Accounting | The company defines what it sells through — el mostrador, la web, un mayorista. The branch is where a sale happened and the till is the drawer; the channel is neither, and one channel is named by the tills of every branch |
| `TenderMethod` | Accounting | The company defines how it takes money, and names the account it lands in |
| `TenderMethodModality` | Accounting | Rides `TenderMethod` |
| `ArcaAccessTicket` | Arca | The CUIT's session with ARCA, keyed by CUIT, environment and service, never by branch: every branch issuing under that identity shares it |
| `Asset` | Assets | A blob any branch's row may point at; `OwnerOrganizationId` names its owner, not its scope |
| `Branding` | Branding | A branch's look, resolved for an ANONYMOUS request that has no scope yet |
| `Inbound` | Channels | What arrived from outside; no branch sent it, and the `Outbound` it answers is optional |
| `Conversation` | Channels | What one contact and the install have written to each other. Carries a NULLABLE `OrganizationId` — the branch whose own address it reached, or the last one that wrote — so it is the party axis below, not the org axis: an edge naming no branch is the tenant's and every seat reads it |
| `OutboundTemplate` | Channels | The words the install speaks, overlaid on the kit's own; the branch's part of a notice is the letterhead around it (`Branding`), never the wording |
| `InboundTemplate` | Channels | The answer a notice expects, declared beside what it says |
| `InboundOption` | Channels | Rides `InboundTemplate` |
| `OutboundPreference` | Channels | Whether the install sends a notice at all and what its own settings hold — the company's answer about its own notices, beside the words it speaks |
| `OutboundPreferenceValue` | Channels | Rides `OutboundPreference` |
| `CalendarConnection` | Calendar | A person's own credential |
| `CalendarFeed` | Calendar | Rides the owner party |
| `CalendarEvent` | Calendar | Rides the owner party — the mirror is the person's, the appointment is the branch's |
| `CalendarLink` | Calendar | Rides `CalendarEvent` |
| `Offering` | Scheduling | The service catalog the company defines; the branch books it |
| `HealthcareService` | Medical | Rides `Offering` — what a service of that catalog means clinically is the company's answer too, and the key is the offering's own id |
| `AgendaLocation` | Scheduling | One row per location, company-wide — "scheduling knows this location" |
| `Setting` | Settings | The company's settings, plus the per-user rows keyed by `UserId` |
| `Prescription` | Optical | A clinical fact about the patient; the `WorkOrder` that fills it carries the org |
| `FittingMeasure` | Optical | Same shape as `Prescription` — measured once, reused wherever the patient goes |
| `Workshop` | Optical | A taller serves every branch |
| `Color` | Optical | Catalog |
| `FrameProduct` | Optical | Rides `Product` |
| `LensProduct` | Optical | Rides `Product` |
| `ContactLensProduct` | Optical | Rides `Product` |
| `LiquidProduct` | Optical | Rides `Product` |
| `ContractAcceptance` | Optical | A user accepts the terms once, for the company |

## Shared — nobody edits it

Exactly the entities that carry `IInstallScoped`, and the bucket is defined by that marker
rather than agreeing with it by coincidence: an entity that grows an editor crosses to
tenant-level in the same PR (data-tenancy.md, The install's own rows).

| Entity | Kit | Why |
|---|---|---|
| `LocationType` | Directory | Planted, `IsSystem`, no screen writes it |
| `TaxRate` | Accounting | ARCA's percentages, not the shop's |
| `FiscalCondition` | Accounting | ARCA's conditions, not the shop's |

---

## Contested

Rows where the entity's columns or first intuition point the other way. **The law's answer is
the one above; nothing here is fixed on this page.**

| Entity | What points the other way | The law's answer |
|---|---|---|
| `OrganizationChannel`, `OrganizationLocation` | Carry `OrganizationId` | Tenant-level — same circularity as `Membership`: they define the org graph, so scoping them by it is circular |
| `Membership` | Carries `OrganizationId` | Tenant-level — it is the grant the scope is COMPUTED from, so filtering it by the scope is circular |
| `Relationship` | Carries a nullable `OrganizationId` | Tenant-level — same circularity. An edge naming no branch is the tenant's and every seat reads it (The party axis) |
| `Branding` | Carries `OrganizationId` | Tenant-level — an anonymous request resolves it before any scope exists |
| `Asset` | Carries `OwnerOrganizationId` | Tenant-level — the column names an owner, and a logo scoped to one branch disappears at the next |
| `SellingPriceListScope` | Carries `OrganizationId` and is written on the Listas de Venta screen | Tenant-level — the column says which branch the list PRICES, not where the row happened; the company writes its price scheme whole and a branch that saw only its own pair could not read it |
| `Prescription` | Taken at a branch | Tenant-level — a clinical fact about the patient; the OT already carries the org |
| `Workshop` | Serves orders taken at branches | Tenant-level — a taller serves every branch, and carries `PartyId` when external |
| Scheduling `Resource` | **Does not exist in the model** | Nothing to classify: a room's bookable identity is its `Agenda`. `ConsultingRoom` is the place and `Agenda` is what people book in it, and both carry the column |
| Treasury per branch | `TenderMethod` names ONE account while each branch has its own caja | `TenderMethod` stays tenant-level; whether the account resolves per-org is an open design question, not a classification one |
| `FittingMeasure` | Read by-id with no scope; taken by staff standing in a branch | Tenant-level — reuse across branches is the point, exactly as for `Prescription` |
| `AppointmentNote` | Joins a tenant-level `Note` to an org-scoped `Appointment` | Org-scoped — the link follows the stricter parent; the note itself stays readable |

Older docs and issues say *global* for the Directory, Iam, the Products catalog and the
Accounting reference tables; that meant "not per branch", never "not per company" — they are
tenant-level.

---

## Messages — where each send stands

The tables above classify the ROWS. This one classifies the SENDS: a message that reads or
writes an org-scoped entity gets one written answer to what it does with the organization, what
keeps a row it names by id inside the caller's reach, and what a write that can move the row to
another branch puts to the policy — plus the gap, where the answer is one nobody chose.
`OrgAxisMessageTests` fails on a message that touches an org-scoped entity and has no row here.

**The set is computed, not curated.** A message is in the table when the IL reachable from its
handler — and from its `IValidator<T>`, which runs in the same pipeline and reads the same
database — names a type marked `IOrgScoped`. The walk follows calls into NSail's own assemblies,
enters an async method's state machine and a lambda's display class, and reads the entity off the
metadata token, so `Set<Sale>()`, `new Sale()` and `sale.Status` all count alike. It is
mechanical and it is a floor. Two things it cannot see:

- **A collaborator reached through an interface a peer kit owns.** A nested *send* crosses the
  boundary and is classified as its own row, which is the right answer; an injected *service*
  whose implementation the walk cannot see statically is read as the interface.
  `AuthorizeVoucher` is the case in the tree: the CAE's own row is written by
  `ArcaAuthorizationProvider` behind Accounting's `IVoucherAuthorizationProvider`, so the walk
  stops at the contract and the row below is written by hand rather than demanded by the sweep.
- **Raw SQL.** There is none in the tree today, and the sweep would not see one.

**Directory has no row here, and that is an answer.** Every entity the kit owns is tenant-level —
the party, the organization, the grants over it — so no send of its own reaches an org-scoped
table. `ListParties` filters by ROLE and not by organization (`PartyHandler`), which is right for
a subject the company owns: a client loaded at one branch must be the same client at the next.
The one list Directory does scope is a person's EDGES, and that is the party axis above, answered
in the handler's own WHERE because no model-wide filter can reach it without circularity.

### What the columns say

**Stance** — what the send does with the organization:

| | |
|---|---|
| `filtered` | carries `[PolicyField(RestrictAs.Organization)]` on a **nullable** field: naming a branch is legal, and naming none is legal too and falls back to the seat's own subtree. |
| `pinned` | carries the axis on a **non-nullable** `Guid`: the send always puts a value in front of the policy, and a policy that pins the axis refuses `Guid.Empty` along with every other branch. |
| `consolidated` | carries no organization field: the seat's whole subtree is the only answer, so a session at the root reads every branch at once and one at a branch reads that branch. |

**By id** — what keeps a row the message NAMES inside the caller's reach:

| | |
|---|---|
| `filter` | the model's `Org` filter and nothing else: the row is `IOrgScoped`, so one outside the operation's subtree answers not-found before the handler reads it (data-tenancy.md, The org filter). |
| `filter + claimed=stored` | the handler ALSO refuses a stored organization that differs from the claimed one. **How loudly is the handler's own call and the tree does both**: `RuleViolation` naming the mismatch (`SaleHandler.Load`, `StockHandler.EnsureStoreBelongsToOrganization`, the two `both ends` moves), an invalid field (`Bookings.RequireActiveAgenda`), or NotFound (`ConsultingRoomHandler.Require`, the `UpdateAgenda` precedent). Neither answer is an oracle, and that is the point of the pairing: the filter answered FIRST, so a row that reached the check at all is one the caller could already `Get`, and the mismatch it reports is between two branches the caller holds. |
| `handler scope` | the row is NOT `IOrgScoped`, so no model filter reaches it, and the handler writes the WHERE itself against the seat's own subtree (`ConversationHandler.Read`). |
| `tenant subject` | the id names a tenant-level row. Nothing on this axis protects it and nothing is meant to; what is org-scoped here is a guard the handler reads over every branch (`OrgScopeProvider.ReadEverywhere`, data-tenancy.md — The org filter — G2 below). |
| `—` | the message names no row that already exists. |

**Channels' threads are the one place a handler writes the whole answer, and it is not the same
answer.** `Conversation` is tenant-level on this axis — a NULLABLE `OrganizationId`, which
`OrgFilter.Axes` cannot take (the entity table above) — so the model filter has no join to it
and `ConversationHandler` narrows by hand: `Visible()` for the reads over several roads, `Read`
for the one by id, both against `OrgScopeProvider.Current.Organizations` and both carrying the
half no filter can express — a thread naming NO branch is the tenant's own and every seat reads
it. So `GetConversation` and `ListConversationMessages` answer `handler scope`
rather than `filter`, and `consolidated` means the seat's subtree PLUS the unbranched threads.
`GetConversationsStamp` runs that same `Visible()` and has no row at all — it reaches no
org-scoped table — which is why its answer is written here. The inbox itself is Tickets'
(`ListTickets`, org-scoped through the filter).

Two readings `filter + claimed=stored` does NOT cover. Where the marked field is nullable the
check runs only when the caller named a branch (`Bookings.RequireActiveAgenda` asks
`claims.OrganizationId is { }` first), so `UpdateAppointment`, `Reschedule` and `CreateSeries`
fall back to the filter alone on a send that names none. And a rule comparing a named row against
the ROW's own branch rather than against the caller's claim is consistency, not authorization:
`UpdateWorkOrder` and the three transitions behind it judge their lines' stores against
`order.OrganizationId`, and only `CreateWorkOrder` — the one that has no order yet — judges them
against the value the caller sent.

**Moves the org** — what a write that can change the branch puts to the policy:

| | |
|---|---|
| `stamps` | a create: the marked field is written onto the new row, so the gate's verdict on that value is the whole answer and there is no old one to check. |
| `both ends` | an update that re-stamps `OrganizationId` on a loaded row and carries the branch the row stands in TODAY beside it (`FromOrganizationId`, marked). The gate reads both, so a move needs a grant over the source as well as the destination; the filter reads the union of the two subtrees, so a sideways move between siblings answers 403 rather than not-found; and the handler refuses a stored organization differing from the claimed source (permissions.md, A write that MOVES a row between branches). |
| `—` | the message cannot change a row's branch. |

**A create whose branch the caller never names is `—`, not `stamps`.** `AuthorizeVoucher` writes
the CAE's own row and stamps its `OrganizationId`, and the value is the voucher's point of sale's
— read off a head the filter already scoped, never a field on the message. There is nothing for a
policy to constrain and nothing a caller could move, which is what this column asks.

**The standing condition, true of every `filtered` and `pinned` row and therefore NOT repeated in
the Gap column: the shipped policies constrain the axis** (the section below is the rule). Every
staff role an install is born with pins its organization fields to
`@memberOfOrDescendant`: the owner standing at the root still reads every branch, an employee
reads the branch they hold hours at and whatever hangs below it, and a branch nobody stands in
is a 403 rather than a filter pointed wherever the caller likes.

### The table

| Message | Stance | By id | Moves the org | Gap |
|---|---|---|---|---|
| `Accounting.Books.ListVat` | filtered | — | — | — |
| `Accounting.Currencies.DeleteCurrency` | consolidated | tenant subject | — | — |
| `Accounting.FiscalPeriods.DeleteFiscalPeriod` | consolidated | tenant subject | — | — |
| `Accounting.Liquidations.LiquidateTenders` | consolidated | — | — | — |
| `Accounting.Liquidations.ListPendingLiquidations` | consolidated | — | — | — |
| `Accounting.PointOfSales.CreatePointOfSale` | pinned | — | stamps | — |
| `Accounting.PointOfSales.DeletePointOfSale` | consolidated | filter | — | — |
| `Accounting.PointOfSales.GetPointOfSale` | consolidated | filter | — | — |
| `Accounting.PointOfSales.ListPointOfSales` | filtered | — | — | — |
| `Accounting.PointOfSales.LookupPointOfSales` | filtered | — | — | — |
| `Accounting.PointOfSales.UpdatePointOfSale` | pinned | filter + claimed=stored | both ends | — |
| `Accounting.SalesChannels.DeleteSalesChannel` | consolidated | tenant subject | — | — |
| `Accounting.Settlements.CreateSettlement` | pinned | filter | stamps | — |
| `Accounting.Settlements.GetSettlement` | consolidated | filter | — | — |
| `Accounting.Settlements.ListClaimCollections` | consolidated | — | — | — |
| `Accounting.Settlements.ListSettlements` | filtered | — | — | — |
| `Accounting.Settlements.ReclassifyAdvances` | pinned | — | — | — |
| `Accounting.TenderMethods.DeleteTenderMethod` | consolidated | tenant subject | — | — |
| `Accounting.TillSessions.CloseTillSession` | pinned | filter + claimed=stored | — | — |
| `Accounting.TillSessions.GetTillSession` | pinned | filter + claimed=stored | — | — |
| `Accounting.TillSessions.ListTillSessions` | filtered | — | — | — |
| `Accounting.TillSessions.OpenTillSession` | pinned | filter + claimed=stored | — | — |
| `Accounting.Vouchers.AuthorizeVoucher` | consolidated | filter | — | — |
| `Accounting.Vouchers.CreateVoucher` | pinned | filter + claimed=stored | — | — |
| `Accounting.Vouchers.DeleteVoucherType` | consolidated | tenant subject | — | — |
| `Accounting.Vouchers.GetVoucher` | consolidated | filter | — | — |
| `Accounting.Vouchers.ListVouchers` | filtered | — | — | — |
| `Accounting.Vouchers.LookupVouchers` | filtered | — | — | — |
| `Accounting.Vouchers.SendVoucher` | consolidated | filter | — | — |
| `Channels.Omnichannel.ConversationMessageArrived` | consolidated | — | — | — |
| `Channels.Omnichannel.ConversationMessageWritten` | consolidated | — | — | — |
| `Channels.Omnichannel.GetConversation` | consolidated | handler scope | — | — |
| `Channels.Omnichannel.ListConversationMessages` | consolidated | handler scope | — | — |
| `Channels.Replies.GetReplyRequest` | consolidated | — | — | — |
| `Channels.Replies.RecordAnswer` | consolidated | — | — | — |
| `Medical.ChargeItems.ListChargeItems` | filtered | — | — | — |
| `Medical.ClaimResponses.AdjudicateClaim` | consolidated | tenant subject | — | — |
| `Medical.ClaimResponses.CreateClaimResponse` | consolidated | tenant subject | — | — |
| `Medical.ClaimResponses.DeleteClaimResponse` | consolidated | tenant subject | — | — |
| `Medical.Claims.CreateClaim` | pinned | filter | stamps | — |
| `Medical.Claims.GetClaim` | consolidated | filter | — | — |
| `Medical.Claims.ListClaims` | filtered | — | — | — |
| `Medical.Encounters.CreateEncounter` | pinned | — | stamps | — |
| `Medical.Encounters.GetEncounter` | consolidated | filter | — | — |
| `Medical.Encounters.ListEncounters` | filtered | — | — | — |
| `Medical.Encounters.UpdateEncounter` | consolidated | filter | — | — |
| `Optical.FittingMeasures.DeleteFittingMeasure` | consolidated | tenant subject | — | — |
| `Optical.Sales.ListPatientPurchases` | consolidated | — | — | — |
| `Optical.WorkOrders.AuthorizeWorkOrder` | consolidated | filter | — | — |
| `Optical.WorkOrders.Cancel` | consolidated | filter | — | — |
| `Optical.WorkOrders.CreateWorkOrder` | pinned | filter + claimed=stored | stamps | — |
| `Optical.WorkOrders.DeliverWorkOrder` | pinned | filter + claimed=stored | — | — |
| `Optical.WorkOrders.GetWorkOrder` | consolidated | filter | — | — |
| `Optical.WorkOrders.GetWorkOrdersAttention` | pinned | — | — | — |
| `Optical.WorkOrders.GetWorkOrdersBoard` | pinned | — | — | — |
| `Optical.WorkOrders.ListWorkOrders` | filtered | — | — | — |
| `Optical.WorkOrders.LookupWorkOrders` | filtered | filter | — | — |
| `Optical.WorkOrders.LookupWorkshops` | consolidated | — | — | — |
| `Optical.WorkOrders.ReceiveAndVerify` | consolidated | filter | — | — |
| `Optical.WorkOrders.ReceiveFromWorkshop` | consolidated | filter | — | — |
| `Optical.WorkOrders.SendToWorkshop` | consolidated | filter | — | — |
| `Optical.WorkOrders.UpdateWorkOrder` | consolidated | filter | — | — |
| `Optical.WorkOrders.Verify` | consolidated | filter | — | — |
| `Optical.Workshops.CreateWorkshop` | consolidated | tenant subject | — | — |
| `Optical.Workshops.DeleteWorkshop` | consolidated | tenant subject | — | — |
| `Optical.Workshops.UpdateWorkshop` | consolidated | tenant subject | — | — |
| `Products.Catalog.DeleteProduct` | consolidated | tenant subject | — | — |
| `Products.Catalog.GetProductVariantAxes` | consolidated | filter | — | — |
| `Products.Catalog.SearchProductVariants` | consolidated | filter | — | — |
| `Products.Purchasing.Approve` | consolidated | filter | — | — |
| `Products.Purchasing.Cancel` | consolidated | filter | — | — |
| `Products.Purchasing.CreatePurchaseOrder` | pinned | filter + claimed=stored | stamps | — |
| `Products.Purchasing.GetPurchaseOrder` | consolidated | filter | — | — |
| `Products.Purchasing.Issue` | consolidated | filter | — | — |
| `Products.Purchasing.LinkPurchaseOrderVoucher` | consolidated | filter | — | — |
| `Products.Purchasing.ListPurchaseOrders` | filtered | — | — | — |
| `Products.Purchasing.Receive` | consolidated | filter | — | — |
| `Products.Purchasing.UpdatePurchaseOrder` | consolidated | filter | — | — |
| `Products.Stock.CreateStore` | pinned | — | stamps | — |
| `Products.Stock.DeleteStore` | consolidated | filter | — | — |
| `Products.Stock.GetLowStock` | pinned | — | — | — |
| `Products.Stock.GetStore` | consolidated | filter | — | — |
| `Products.Stock.ListStockBalancePage` | pinned | — | — | — |
| `Products.Stock.ListStockBalances` | pinned | — | — | — |
| `Products.Stock.ListStores` | filtered | — | — | — |
| `Products.Stock.LookupAvailableProducts` | pinned | — | — | — |
| `Products.Stock.LookupStores` | filtered | — | — | — |
| `Products.Stock.RegisterStockMovement` | pinned | filter + claimed=stored | stamps | — |
| `Products.Stock.TransferStock` | pinned | filter + claimed=stored | stamps | — |
| `Products.Stock.UpdateStore` | pinned | filter + claimed=stored | both ends | — |
| `Sales.Counter.BillSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.CancelSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.CollectSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.CompleteSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.CreateSale` | pinned | filter + claimed=stored | stamps | — |
| `Sales.Counter.DeliverSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.GetDailySales` | pinned | — | — | — |
| `Sales.Counter.GetSale` | filtered | filter | — | — |
| `Sales.Counter.ListSales` | filtered | — | — | — |
| `Sales.Counter.ReadySaleBundle` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.RefundSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.ReturnSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.SourceSaleLines` | pinned | filter + claimed=stored | — | — |
| `Sales.Counter.UpdateSale` | pinned | filter + claimed=stored | — | — |
| `Sales.Modifiers.SaveSaleModifiers` | consolidated | — | — | — |
| `Scheduling.Agendas.CloseAgendas` | pinned | — | — | — |
| `Scheduling.Agendas.CreateAgenda` | pinned | — | stamps | — |
| `Scheduling.Agendas.GetAgenda` | consolidated | filter | — | — |
| `Scheduling.Agendas.ListAgendas` | filtered | — | — | — |
| `Scheduling.Agendas.LookupAgendas` | filtered | — | — | — |
| `Scheduling.Agendas.UpdateAgenda` | pinned | filter + claimed=stored | — | — |
| `Scheduling.Appointments.AppointmentBooked` | consolidated | — | — | — |
| `Scheduling.Appointments.AppointmentCompleted` | consolidated | — | — | — |
| `Scheduling.Appointments.AppointmentPlaceChanged` | consolidated | — | — | — |
| `Scheduling.Appointments.AppointmentPlaced` | consolidated | — | — | — |
| `Scheduling.Appointments.AppointmentRescheduled` | consolidated | — | — | — |
| `Scheduling.Appointments.Cancel` | consolidated | filter | — | — |
| `Scheduling.Appointments.Complete` | consolidated | filter | — | — |
| `Scheduling.Appointments.Confirm` | consolidated | filter | — | — |
| `Scheduling.Appointments.CreateAppointment` | pinned | filter + claimed=stored | — | — |
| `Scheduling.Appointments.CreateSeries` | filtered | filter + claimed=stored | — | — |
| `Scheduling.Appointments.GetAppointment` | consolidated | filter | — | — |
| `Scheduling.Appointments.GetAppointmentsBoard` | consolidated | — | — | — |
| `Scheduling.Appointments.GetBookingMovePreview` | pinned | filter + claimed=stored | — | — |
| `Scheduling.Appointments.GetSeriesMovePreview` | consolidated | filter | — | — |
| `Scheduling.Appointments.GetUpcomingAppointments` | consolidated | — | — | — |
| `Scheduling.Appointments.ListAppointments` | filtered | — | — | — |
| `Scheduling.Appointments.ListFreeIntervals` | consolidated | — | — | — |
| `Scheduling.Appointments.ListPendingOutbounds` | filtered | — | — | — |
| `Scheduling.Appointments.MarkNoShow` | consolidated | filter | — | — |
| `Scheduling.Appointments.MarkOutboundGiven` | consolidated | filter | — | — |
| `Scheduling.Appointments.MoveBookings` | pinned | filter + claimed=stored | — | — |
| `Scheduling.Appointments.Reschedule` | filtered | filter + claimed=stored | — | — |
| `Scheduling.Appointments.SendOutbound` | consolidated | filter | — | — |
| `Scheduling.Appointments.UpdateAppointment` | filtered | filter + claimed=stored | — | — |
| `Scheduling.Availabilities.CreateAvailability` | consolidated | filter | — | — |
| `Scheduling.Availabilities.CreateAvailabilityException` | consolidated | filter | — | — |
| `Scheduling.Availabilities.DeleteAvailability` | consolidated | filter | — | — |
| `Scheduling.Availabilities.DeleteAvailabilityException` | consolidated | filter | — | — |
| `Scheduling.Availabilities.ListAvailabilities` | consolidated | — | — | — |
| `Scheduling.Availabilities.ListAvailabilityExceptions` | consolidated | — | — | — |
| `Scheduling.Availabilities.UpdateAvailability` | consolidated | filter | — | — |
| `Scheduling.Availabilities.UpdateAvailabilityException` | consolidated | filter | — | — |
| `Scheduling.Holidays.CreateHoliday` | pinned | — | stamps | — |
| `Scheduling.Holidays.DeleteHoliday` | consolidated | filter | — | — |
| `Scheduling.Holidays.GetUpcomingHolidays` | consolidated | — | — | — |
| `Scheduling.Holidays.ImportNationalHolidays` | pinned | — | stamps | — |
| `Scheduling.Holidays.ListHolidays` | filtered | — | — | — |
| `Scheduling.Holidays.ListNationalHolidays` | pinned | — | — | — |
| `Scheduling.Invitations.GetBookingInvitation` | consolidated | — | — | — |
| `Scheduling.Invitations.GetBookingLink` | consolidated | — | — | — |
| `Scheduling.Invitations.TakeBookingSlot` | consolidated | — | — | — |
| `Scheduling.Settings.SaveSchedulingSettings` | consolidated | — | — | — |
| `Therapy.ConsultingRooms.CloseConsultingRoom` | pinned | filter + claimed=stored | — | — |
| `Therapy.ConsultingRooms.CreateConsultingRoom` | pinned | — | stamps | — |
| `Therapy.ConsultingRooms.GetConsultingRoom` | consolidated | filter | — | — |
| `Therapy.ConsultingRooms.ListConsultingRooms` | filtered | — | — | — |
| `Therapy.ConsultingRooms.ReplaceConsultingRoom` | pinned | filter + claimed=stored | stamps | — |
| `Therapy.ConsultingRooms.UpdateConsultingRoom` | pinned | filter + claimed=stored | — | — |
| `Therapy.Encounters.CollectSession` | consolidated | — | — | — |
| `Therapy.Encounters.GetSessionBilling` | consolidated | — | — | — |
| `Therapy.Encounters.GetSessionCoverage` | consolidated | — | — | — |
| `Therapy.Notes.CreateAppointmentNote` | consolidated | filter | — | — |
| `Tickets.Tickets.AddTicketNote` | consolidated | filter | — | — |
| `Tickets.Tickets.AnswerTicket` | consolidated | filter | — | — |
| `Tickets.Tickets.AttachTicketDocument` | consolidated | filter | — | — |
| `Tickets.Tickets.CountOpenTickets` | consolidated | — | — | — |
| `Tickets.Tickets.GetConversationTicket` | consolidated | filter | — | — |
| `Tickets.Tickets.GetTicket` | consolidated | filter | — | — |
| `Tickets.Tickets.ListTickets` | consolidated | — | — | — |
| `Tickets.Tickets.LinkTicketRequester` | consolidated | filter | — | — |
| `Tickets.Tickets.OpenTicket` | pinned | — | stamps | — |
| `Tickets.Tickets.ReassignTicket` | consolidated | filter | — | — |
| `Tickets.Tickets.RejectTicketSuggestion` | consolidated | filter | — | — |
| `Tickets.Tickets.ResolveTicket` | consolidated | filter | — | — |
| `Tickets.Tickets.SetTicketStatus` | consolidated | filter | — | — |
| `Tickets.Tickets.TakeTicket` | consolidated | filter | — | — |

### The shipped policies

**Every marked organization field a policy an install is born with can reach is constrained to a
membership symbol.** That is the rule, and `OrgAxisMessageTests.
EveryShippedPolicyConstrainsTheOrganizationFieldsItReaches` is what holds it: it reads the óptica's
`RolePresetsPack.json` and each product's `SeedPolicies` — the two roads a product ships a policy
by — expands each row's message keys against the registry, and fails on a marked field the row
leaves free. A new pack row, or a marked field arriving on a message a shipped grant already
covers, lands in that failure.

Three things the rule leans on, and none is code a story has to write again:

- **A feature grant carries the constraint.** The Encargado row grants `Products.*`, `Sales.*`,
  `Tickets.*` and a dozen more, and a pattern may carry fields: it is expanded before the
  constraint is bound, and a message that joins the pattern next release arrives NARROWED rather
  than free (permissions.md, Wildcard rule). An enumerated list would have been one message out
  of date by the next story.
- **Naming no branch is not a refusal.** `@memberOfOrDescendant` on a nullable field refuses a
  branch the caller does not stand in and allows the send that names none, because the filter
  scopes that one to the seat's own subtree (permissions.md, Null semantics). Without that, every
  `filtered` row above and every by-id read would have needed its screen to fill the field. That
  tolerance is that symbol's alone: a row pinned to `@memberOf` — a seat that must not see below
  its branch — turns the field MANDATORY, and nothing shipped picks it today for that reason.
- **A `both ends` move is two fields, and the rule counts both.** `UpdateStore` and
  `UpdatePointOfSale` carry `FromOrganizationId` beside `OrganizationId`, so a row that
  pinned only the destination would leave the source free and hand a manager the till of a branch
  they stand in nowhere — G3 reopened by data. The Encargado row pins both. No screen notices: the
  source is filled from the loaded row, and the row is org-filtered, so what a screen can send is
  always a branch the seat already reads.

**A shipped row an install already holds is not reached by shipping it armed.** A migration's row
travels by its `TenantSeed` twin, and a row a pack planted travels by nothing at all — an import
runs once and Restaurar preset is the shop's own hand. So arming a shipped policy owes a tenant
step that MERGES the constraint into the stored document, leaving a field the shop answered about
alone: Optical's is `TenantSeed.PolicyBranches` over `SeedPolicies.BranchFields` (the seeded floor
plus the pack's rows), Therapy's is `TenantSeed.AgendaBranch` over its seeded row.

The written exceptions live beside the rule in the test, and all of them shrink rather than stay:

- **G5 — the organization door's own two fields, which a name-keyed constraint cannot narrow.**
  Both are on the Encargado row, both are the same mechanism limit — a constraint is keyed by field
  NAME alone, across every message the row covers — and the door they leave open is one door.
  - `Id` carries both AXES in the tree: `GetOrganization.Id`, `UpdateOrganization.Id` and
    `DeleteOrganization.Id` are marked `Organization`, `GetParty.Id` and its siblings are marked
    `Party`. The Encargado row lists both families, so narrowing `Id` would deny one of them in
    silence — the failure a permission must not have.
  - `ParentOrganizationId` carries two QUESTIONS. On `CreateOrganization` it is the branch the new
    row hangs UNDER, which a subtree symbol answers correctly; on `UpdateOrganization` it is what
    sits ABOVE the row being edited, and for the branch the seat itself stands in that is the root
    — above the seat, so outside every membership symbol. One constraint has to answer both, and
    pinning it refuses a manager at Centro the save of Centro, which the screen makes on every
    edit: `UpdateOrganizationPage.Load` submits the parent even where the picker is hidden.
    `VendedorReadsHisOwnBranchTests.A_manager_saves_the_branch_he_stands_in` is what holds that
    open.

  What is left open is an Encargado reading, renaming or deleting a sibling branch's `Organization`
  row and hanging a new branch under it (tenant-level, so no filter reaches it either). It closes by
  giving the organization door a field of its own — one that names the row's SCOPE rather than its
  identity or its parent — not by a constraint here.

### The gaps, by kit

**Directory**

- **G5** (`Id` on `GetOrganization`, `UpdateOrganization` and `DeleteOrganization`;
  `ParentOrganizationId` on `CreateOrganization` and `UpdateOrganization`), written out under The
  shipped policies above because it is a policy's gap and not a handler's: a constraint is keyed by
  field name alone, and neither name means one thing across the row that lists them.

Accounting, Products and Optical have none open. What the closed labels answer, since other docs
name them:

- **G2 — an in-use guard the filter would narrow** (`DeleteCurrency`, `DeleteFiscalPeriod`,
  `DeleteSalesChannel`, `DeleteTenderMethod`, `DeleteVoucherType`, `DeleteProduct` —
  `PurchaseOrderLine`, `StockMovement` and `SaleLine` — `DeleteWorkshop` and
  `DeleteFittingMeasure` — `WorkOrder`): the guard reads every branch; the rule's sentence is in
  data-tenancy.md — The org filter, "An in-use guard that must NOT find a row fails open when it
  is filtered".
- **G3 — a move between branches** (`UpdateStore`, `UpdatePointOfSale`): both ends are marked
  fields, so the move needs a grant over both (`both ends` above; the rule and its corollary for
  whoever writes the policy — narrowing the axis means constraining BOTH fields — are in
  permissions.md, A write that MOVES a row between branches). `UpdatePointOfSale` also refuses to
  move a till with an issued comprobante no authority has granted, because the CAE's attempts are
  stamped and do not follow the head (data-tenancy.md — a stamp is not a ride).
- **G4 — `ArcaAuthorization`**: the row carries its own `OrganizationId` and `AuthorizeVoucher`
  says what it does with the axis; what remains is the sweep's blindness through a peer kit's
  interface, named with the walk above.

### Two answers that read like gaps and are not

- **The anonymous doors stand in no branch.** `Scheduling.Invitations.GetBookingInvitation`,
  `GetBookingLink` and `TakeBookingSlot` run for a caller with no session, and work standing in no
  branch reads every branch (`OrgScope.Everywhere`, data-tenancy.md). The filter therefore answers nothing
  on these three. What keeps the read honest is the token — 32 bytes minted per invitation and
  spent by the hour — and `BookingInvitation` carries the organization on its own row rather than
  reaching it through the agenda for exactly this reason. Consolidation here is the design.
  `Channels.Replies.GetReplyRequest` and `RecordAnswer` are the same door in another kit:
  a session-less caller resolves and then burns one `Outbound` named by a token of the same
  size, over the tenant's whole chart because it stands in no branch. The token is the whole
  credential — the row it names carries the address, so the door asks the caller nothing — and
  the consolidation is the design there too.
- **`consolidated` is not a verdict against a message.** A screen that consolidates and one that
  does not are both legitimate; this page says which is which and decides
  neither. Of the rows that consolidate, the ones worth a second look are the ones where a
  branch operator would expect to narrow and cannot — `Tickets.Tickets.ListTickets`,
  `Scheduling.Availabilities.ListAvailabilities` — each
  of which is a design question for its own kit, not a defect in this one.

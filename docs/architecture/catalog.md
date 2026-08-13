# Catalog service

The service that owns **what the shop has**: the catalog sets, the physical copies, their
condition grades and their photographs. It covers [UC-1](../IDEA.md) end to end — staff
catalogue sets and register copies — and [UC-7](../IDEA.md) — customers browse, search and
compare copies before reserving one.

It is the first service designed because it is the only one with **no upstream dependency**.
Rentals, payments, inspection and notifications all read what catalog owns or ask it to change
something. Designing it first means the boundaries the others need already exist.

> **Scope.** This document covers the catalog service alone. Where a decision belongs to the
> system rather than to this service — which messaging transport, how the read side of other
> services is shaped — it is named and deferred rather than settled here on one service's
> evidence. `docs/ARCHITECTURE.md` will be the system map, written once there are several
> services to map.

## Where it sits

```
                          ┌────────────────────────────┐
   staff (Entra ID) ─────▶│                            │──▶ Rebrickable  (set facts)
                          │      Catalog API           │
   customers (anon) ─────▶│      App Service           │──▶ Blob Storage (images)
                          │                            │
   other services ───────▶│                            │──▶ Postgres     (sets, copies)
   (status transitions)   └──────────────┬─────────────┘
                                         │
                                         ▼
                              domain events (outbox)
                          copy available · retired · regraded
                                         │
                                         ▼
                       rentals · notifications · inspection
```

## What the service owns

| Use case | What lands here |
| --- | --- |
| **UC-1.1** | Catalogue a set — Rebrickable lookup, the four staff-typed fields, the unique-piece checklist |
| **UC-1.2** | Register copies individually or in a batch; mint label codes; record baseline weight |
| **UC-1.3** | Retire a copy |
| **UC-1.4** | Review stock; look up a copy by scanning its label |
| **UC-1.5** | An admin edits the grade multipliers |
| **UC-7.1–7.5** | Browse, search, filter, set detail, every copy with its grade, price, deposit and photographs |
| **UC-10.2** | Regrade and reprice — *the write only*. The inspection workflow around it is a later service. |

It also owns **copy status** as data, and publishes the domain events the rest of the system
reacts to.

**Not here:** reservations, any movement of money, the inspection workflow, notifications,
customer standing. Those are separate services with their own documents.

## Two ownership decisions

Everything else in this design follows from these, so they come first.

### Copy status is catalog's data; other services request transitions

The rentals service does not write `On rent` into a table it shares with catalog. It **calls**
catalog, which applies the transition or refuses it.

The reason is a single business rule from UC-1.3: *a copy cannot be retired while a rental is
active on it*. If rental state lived elsewhere, enforcing that rule would mean asking another
service a question whose answer can be stale by the time it arrives — a copy could be retired
in the gap between the check and the write. Keeping status here makes it a **local check inside
one transaction**, which is the only version of that rule that actually holds.

The cost is that catalog carries a concept it does not otherwise care about. It does not know
what a rental *is*; it only knows a copy is out. That is the right amount of knowledge: enough
to protect its own invariant, not enough to couple it to someone else's model.

One state machine encodes the legal moves from IDEA.md — `Available → Reserved → On rent →
Awaiting inspection → In inspection → Available | In repair | Retired`. Illegal transitions
are refused by the domain, not merely absent from the UI.

### Catalog owns *current* price; rentals owns *frozen* price

IDEA.md fixes a copy's rental price and deposit **at the moment of reservation**, and a regrade
must never disturb a reservation that already exists.

So catalog computes what a copy costs *today* and publishes it. It does **not** store it. The
reservation copies the numbers at the moment it is created, and from then on the reservation is
authoritative for that rental.

Two owners, and no ambiguity about which number applies to what: catalog answers "what would
this cost now", rentals answers "what did this cost then". Trying to make one place answer both
is how you end up with a regrade quietly repricing a rental already in progress.

## Compute — App Service

The Catalog API is a long-lived HTTP API with steady traffic. There is no queue to scale on, no
burst profile, and nothing to gain from scaling to zero — a shop's catalog is browsed all day
and edited occasionally.

**The justification is the absence of a scaling driver, not the presence of one.** That is
worth stating plainly, because the reflex is to reach for Container Apps and the reflex is
wrong here: Container Apps earns its place when there is something to scale *on* — queue depth,
event rate, a service that should cost nothing while idle. This service has none of those.
Services that do (the event consumers, UC-11's scheduled jobs) appear in later documents and
get a different home for a stated reason.

This is genuinely close, and pretending otherwise would teach badly. Container Apps would work
fine. The tiebreaker is operational surface: App Service gives slots, scaling, certificates and
health probes without a container platform underneath, and this service needs nothing a
container platform adds.

### Deployed as a container anyway

The API is built as a **Linux container** and deployed to App Service for Containers, not
code-deployed.

- The same image runs under Docker Compose locally, so dev and production are the same artifact
  rather than the same source.
- If the service ever does acquire a scaling driver, moving to Container Apps becomes a
  deployment change rather than a rewrite.

The cost is a container build in CI and a registry to keep. Worth it for parity alone.

### Two operational details that bite people

**Health endpoints are split.** `/health/live` answers "is the process up" and touches nothing;
`/health/ready` checks Postgres and Blob. App Service's health probe points at readiness, so an
instance that has lost its database is taken out of rotation instead of serving errors.

**Migrations do not run at startup.** With more than one instance, startup migration means
several processes racing to alter the same schema. Migration is a **pipeline step** that runs
once before the new revision is released. This is the single most common way a first Azure
deployment goes wrong, and it only shows up when you scale past one instance — which is
exactly when you least want to discover it.

## Data — Azure Database for PostgreSQL

Catalog data is relational and transactional, and it is not a close call:

- A copy belongs to a set; a checklist item belongs to a set; a photograph belongs to a copy.
- A status transition and the event announcing it must commit **together** (see *Events*).
- Registering three copies as a batch is one unit of work — three copies or none.
- A price is a **join**: the copy's grade to the multiplier table to the set's base price.
- The invariants are exactly the kind a relational database enforces for free — one set per set
  number, one copy per label code, a grade that must be one of four values.

Postgres Flexible Server, one database, EF Core on top.

### Schema

| Table | Holds | Notes |
| --- | --- | --- |
| `catalog_sets` | One row per LEGO set the shop catalogues | `set_number` unique. Product facts from Rebrickable plus the four staff-typed fields. |
| `catalog_set_checklist_items` | The unique-piece checklist | `source` = `rebrickable` \| `manual`, so a re-fetch never silently deletes what staff added |
| `copies` | One row per physical box | `label_code` unique, `status`, `grade`, `baseline_weight_grams`, `retired_at` |
| `copy_photos` | Photographs of a copy | `kind` = `published` \| `evidence`; evidence carries the rental it belongs to |
| `grade_multipliers` | Four rows: New, Excellent, Good, Fair | Every edit audited — one change re-prices the whole catalog |
| `rebrickable_snapshots` | The raw fetched payload per set number, as `jsonb` | See *Rebrickable*, below — this exists for a security reason |
| `outbox_messages` | Events awaiting publication | See *Events*, below |

### Things the schema must get right

**Money is `numeric(10,2)`, never `float` or `double`.** Binary floating point cannot represent
`0.10`, so money in a `double` drifts. This is not a style preference; it produces deposits that
are a cent wrong and refunds that do not reconcile. `decimal` in C#, `numeric` in Postgres, all
the way through.

*Assumption, stated rather than implied:* a single shop trading in a single currency. There is
no currency column. If BrickShare ever opened a second shop in another country, every money
column would need one — which is precisely why it is written down here rather than discovered
later.

**Derived values are computed, never stored.** A copy's rental price, its daily rate and its
deposit are all functions of the grade multiplier:

```
rental price = set.base_rental_price × multiplier[copy.grade]
daily rate   = rental price ÷ set.minimum_rental_days
deposit      = set.retail_price      × multiplier[copy.grade]
```

UC-1.5 lets an admin change a multiplier, and IDEA.md says plainly that this **re-prices the
entire catalog**. If those three numbers were stored on the copy, that one edit would become a
bulk update across every row the shop owns — slow, non-atomic in effect, and wrong for any row
it missed. Computing them on read makes the re-pricing instantaneous and total, which is the
stated intent.

The cost is that every read that shows a price does a join. At a few thousand copies that is
nothing.

**Grades only fall.** The downward-only rule and *New is unrecoverable* are enforced in the
domain model, not left to whatever calls the API. A raise is legal only as the explicit
post-repair override from UC-10.5, and it is a different operation with a different name — not
the same regrade call with a higher value.

**Retire is a state change.** `retired_at` is set; nothing is deleted. Rental history has to
survive a retirement, and a deleted copy would take its history with it.

### EF Core, and no second data-access story

EF Core for both reads and writes, with explicit migrations checked into the repository. No
Dapper alongside it: there is no read path in this service that EF cannot express well, and a
second way to reach the database is a second thing to learn, keep consistent and get wrong. If
a query ever genuinely needs raw SQL, EF runs raw SQL.

Optimistic concurrency on `copies` via a row version. Two staff scanning the same box at once
is not hypothetical, and the second write should fail loudly rather than overwrite the first.

## Search — and why nothing extra is needed

UC-7.2 asks for search by name and set number, and filters on theme, piece count, age rating,
price and current availability. All of it is ordinary SQL over a few thousand rows: B-tree
indexes for the filters, `pg_trgm` for fuzzy name matching.

**Azure AI Search is deliberately not used.** The reflex is "search feature ⇒ search service",
and resisting it is more useful to learn than another resource in the diagram. A dedicated
search service earns its place with relevance tuning, faceting over large corpora, synonyms and
typo tolerance at scale. A single shop's catalog has none of those problems.

The one part that deserves thought is the per-set aggregates on the results page:

- **available copy count** — copies in `Available` status only. Reserved copies do not count;
  they are already spoken for.
- **starting price** — the cheapest **available** copy, so the page never advertises a price
  nobody can act on.

Both are aggregates over `copies` computed per query. That is fine at this size. If the catalog
grew to where it was not, the fix is a maintained per-set summary updated on copy status
changes — the events for it already exist. Worth knowing the escape route; not worth building
before it is needed.

### The read-side rules that are easy to get wrong

Three of UC-7's rules are the kind a query written from intuition will break:

- **Every copy is listed, including unavailable ones.** A customer has to be able to see a copy
  in order to subscribe to it (UC-7.5). Filtering the list down to what is rentable would
  quietly remove the subscribe path.
- **A set with no available copies still shows in full**, for the same reason. So does a set
  with **no copies at all** — IDEA.md allows a catalog entry with zero copies, a planned
  purchase or a set whose every copy has been retired. It simply has no starting price, and the
  response says so rather than inventing a zero.
- **Only published photographs are returned to customers.** The read query filters on
  `kind = published`; evidence photographs are reachable only through a staff endpoint. Two
  paths, not one path with a flag on the caller.

The distinction that ties them together: *available* controls what can be **acted on**, never
what is **shown**.

## Images — Blob Storage, and a privacy rule that lives in code

Two kinds of image, and the difference between them is the whole design:

| Kind | What it is | Who may see it |
| --- | --- | --- |
| **Published** | The set's product image, and current-condition photographs of a copy | Anyone. It is product information. |
| **Evidence** | A photograph taken to justify a deduction against a named customer's rental | Staff only. |

IDEA.md is explicit that publishing an evidence photograph would leave one customer's dispute
on a public page indefinitely. **That is a privacy rule about an identifiable person, not a
display preference**, and it decides how the storage is built.

### One account, anonymous access disabled, SAS for everything

A single storage account with **anonymous public access turned off at the account level**, one
container, two prefixes:

```
copy-photos/
  published/{copyId}/{photoId}.jpg     ← SAS minted for anyone
  evidence/{rentalId}/{photoId}.jpg    ← SAS minted for staff only
```

Reads never hit Blob directly. The API mints a **short-lived user-delegation SAS** with its
managed identity and returns the URL in the response body, so `GET /sets/{id}` comes back with
each copy's photographs already resolved to signed URLs that expire in minutes.

The reason for routing every read through the API is that it puts the published/evidence rule
**in one function, in code, under test**. The alternative — a public container for published
images — puts that rule in storage configuration instead, where the difference between correct
and catastrophic is one boolean that a future Terraform edit can flip. With anonymous access
disabled account-wide, no container in this account can be made public even by mistake. That is
a smaller blast radius for the same feature.

**The accepted cost:** no CDN caching, and a signing operation on every image read. For one
shop that is irrelevant. The point at which you would revisit it is measurable — when image
egress or SAS minting shows up in the traces — and the fix is a second, separate storage
account for published images fronted by a CDN, keeping evidence in this one. Splitting *later*
is cheap; un-publishing something is not.

### Uploads go through the API

Staff upload photographs to the API, which validates content type and size and then writes to
Blob — rather than the API handing out a write SAS and the client uploading directly.

Direct-to-blob is the pattern that scales, and at real volume it is correct: multi-megabyte
uploads should not occupy an application instance. But it means the API never sees the bytes,
so validation has to happen after the fact, and the "did the upload actually land" problem
becomes yours. A handful of photographs a day does not justify that. The trigger for switching
is volume, and it is named here so the switch is a decision rather than a rewrite.

## Events — and why there is an outbox

> **Delivered later than it is designed.** This section describes the finished service. The
> course builds catalog **without** events and retrofits them once a second service exists —
> see [`docs/course-plan/catalog/catalog-api.md`](../course-plan/catalog/catalog-api.md). The reason is that an
> event published into a system with no subscriber is a message to nobody, and the problem the
> outbox solves cannot be demonstrated without a consumer reacting wrongly on the other side.
> The design is settled here; only its arrival is staged.

Catalog publishes the facts the rest of BrickShare reacts to:

| Event | Who cares |
| --- | --- |
| `CatalogSetAdded` | Nothing yet — but it is the fact, and facts get published |
| `CopyRegistered` | Stock reporting |
| `CopyStatusChanged` | **Subscriptions** (UC-3.3–3.5): a copy reaching *Available* is what triggers the "it is free" notification |
| `CopyRetired` | Subscriptions must be **ended** and subscribers told the copy is gone (UC-3.6) |
| `CopyRegraded` | Anything showing a price |
| `GradeMultipliersChanged` | Anything showing a price — this one re-prices everything at once |

### The problem

A copy reaching *Available* has to do two things: change a row in Postgres, and publish an
event. There is no distributed transaction between Postgres and Azure Service Bus, so the
naive orderings both fail:

- **Publish, then commit** — the publish succeeds, the commit fails. Subscribers are told a copy
  is free that is not. An event was invented.
- **Commit, then publish** — the commit succeeds, the process dies before publishing. The copy
  is free and nobody is ever told. An event was lost, silently, and no retry exists because
  nothing knows it was owed.

Neither is acceptable when the event is what makes a customer's subscription mean anything.

### The fix — transactional outbox

The event is written to `outbox_messages` **in the same transaction as the state change**. Both
commit or neither does. A background dispatcher then reads unpublished rows and sends them.

Three consequences that must be understood, not just implemented:

**Multiple instances mean multiple dispatchers.** App Service scaled to three instances runs
three dispatchers over one table. `SELECT ... FOR UPDATE SKIP LOCKED` lets each claim rows
without blocking the others or double-sending.

**Delivery is at-least-once.** A dispatcher can publish and then die before marking the row
sent, so the same event goes out twice. This cannot be engineered away, only absorbed: **every
consumer of a catalog event must be idempotent**, and every event carries a stable id for that
purpose. This is a contract catalog imposes on the whole system, which is why it is stated here
rather than left for each consumer to rediscover.

**Ordering is not guaranteed** across events. Consumers that care must reconcile against the
current state rather than assume the sequence they received is the sequence that happened.

### Which transport — deferred

Broadly: **Event Grid** for broadcast facts with many or unknown subscribers, **Service Bus**
where competing consumers, ordering or a dead-letter queue matter. Catalog's events look like
broadcast facts.

That is a system-wide decision and it is **deferred to the messaging round**. Settling the
transport on one publisher's evidence, before any consumer exists, is how you end up with a
transport that suits the sender and nobody else. The outbox is deliberately transport-agnostic
so the choice costs nothing to defer — the dispatcher is the only thing that changes.

## Rebrickable — a two-step flow, for a security reason

UC-1.1: staff type a set number, BrickShare fetches the product facts, staff fill in the four
fields Rebrickable cannot supply. IDEA.md is firm that cataloguing **blocks** until the lookup
succeeds.

So lookup and create are **two endpoints**, not one:

```
POST /catalog/lookups          { setNumber: "10294" }
   → fetches from Rebrickable
   → copies the set image into Blob
   → persists the payload as a rebrickable_snapshot
   → returns the prefilled draft + a lookupId

POST /catalog/sets             { lookupId, retailPrice, baseRentalPrice,
                                 minimumRentalDays, ageRating, checklistAdditions }
   → reads the snapshot server-side
   → creates the set
```

**The reason is not ergonomics.** If the create call accepted `name`, `pieceCount` and `theme`
in its request body, then anyone holding a staff token could invent a product — a set that
Rebrickable has never heard of, with whatever facts they liked. Persisting the snapshot at
lookup time means **the server is the only source of the facts it stores**; the create call
carries only the fields staff are actually entitled to decide.

It also happens to be the better user experience, and it makes the block rule trivially true:
you cannot reach create without a successful lookup.

The set image is copied into Blob during the lookup, while staff are already waiting on a
network call. After that BrickShare serves its own copy and no longer depends on a third
party's CDN staying up or its URLs staying stable.

**Resilience.** A typed `HttpClient` with timeout, retry and circuit breaker via
`Microsoft.Extensions.Http.Resilience`. The Rebrickable API key lives in Key Vault.

**The outage blast radius is narrow, by design.** Registering further copies of an
already-catalogued set makes no external call, because the facts were fetched once and stored.
During a Rebrickable outage the shop can still register stock, retire copies, regrade and sell
rentals; the only thing it cannot do is catalogue a set it has never stocked before. That is
the consequence IDEA.md accepts, and it is this caching that keeps it that small.

## Identity and authorization

**Staff, via the Entra ID workforce tenant.** Three app roles on the API's app registration,
matching the actors in IDEA.md:

| Role | May |
| --- | --- |
| `Staff` | Catalogue sets, register and retire copies, regrade, upload photographs, view evidence |
| `Manager` | Everything staff may (the exceptional decisions managers own live in other services) |
| `Admin` | Edit the grade multipliers |

Multiplier edits are `Admin` alone because one edit re-prices every copy the shop owns — the
same reasoning that made Admin a separate actor in the first place. UC-1.5 also asks that the
scope of that edit be **stated plainly before it is applied**, so the endpoint is two-phase:
a preview returns how many copies change price and by how much, and the apply call carries
the preview's token. An admin should never discover the blast radius afterwards. Evidence photographs are
staff-only, which is the API-side half of the privacy rule the storage design implements.

**Customer browse is anonymous.** UC-7 has no rule requiring a signed-in customer to look at
the catalog, and adding one would be a product change nobody asked for — a rental shop that
hides its stock behind a sign-up wall sells less. So the read endpoints take no token.

Consequently **Entra External ID does not appear in this service at all.** It starts mattering
at reservation, where money is locked against a named person, and it belongs to the rentals
service's document. A service that does not need to know who the customer is should not be
wired to an identity provider.

*Consequence, accepted:* anonymous reads cannot be rate-limited per user, only per IP.
Acceptable for a public product catalog with no expensive queries behind it.

## API surface

REST over JSON, minimal APIs grouped by resource, versioned under `/api/v1`, errors as
`ProblemDetails` (RFC 9457), OpenAPI from the built-in `Microsoft.AspNetCore.OpenApi`.

| | Endpoint | Who |
| --- | --- | --- |
| **Staff** | `POST /catalog/lookups` | Staff |
| | `POST /catalog/sets` · `PATCH /catalog/sets/{id}` | Staff |
| | `POST /catalog/sets/{id}/copies` (single or batch) | Staff |
| | `POST /copies/{id}/retire` | Staff |
| | `POST /copies/{id}/grade` · `POST /copies/{id}/grade-after-repair` | Staff |
| | `POST /copies/{id}/photos` | Staff |
| | `GET /copies/by-label/{code}` — the scan endpoint | Staff |
| | `POST /pricing/grade-multipliers/preview` | **Admin** |
| | `PUT /pricing/grade-multipliers` — carries the preview token | **Admin** |
| **Internal** | `POST /copies/{id}/status` — transition requested by another service | Service identity |
| **Public** | `GET /sets` — browse, search, filter | Anonymous |
| | `GET /sets/{id}` — detail with every copy, price, deposit, photos | Anonymous |

Two shapes worth noting. Regrading is `POST .../grade` and post-repair raising is a
**different endpoint** — the rule that grades only fall is expressed in the API surface rather
than as a validation branch inside one handler, so it cannot be bypassed by passing a different
argument. And the status transition endpoint is a **command**, not a `PATCH` of a status field:
callers ask for a transition and the state machine decides, rather than asserting a new value.

## Local development

Docker Compose, per the locked constraint. Four containers:

| Container | Why |
| --- | --- |
| `catalog-api` | The same image that ships to App Service |
| `postgres` | The real database engine, not a substitute |
| `azurite` | Blob emulator — SAS minting and container semantics behave as they do in Azure |
| `rebrickable-stub` | A tiny stub behind the same typed-client interface |

The Rebrickable stub exists so the service builds and its tests run **offline and without
burning a rate-limited quota**, which matters when a class of thirty students hits the same key.
Configuration toggles between the stub and the real API, and the real one is used deliberately
when the integration itself is what is being exercised.

No Service Bus dependency yet: the outbox dispatcher logs what it would publish. There is a
Service Bus emulator container and it will appear in the messaging round — adding it now would
be infrastructure with nothing on the other end of it.

## Infrastructure

Terraform module shape only; the HCL is the next round.

| Resource | Notes |
| --- | --- |
| App Service plan + Linux container app | Health probe on `/health/ready`, one deployment slot |
| Azure Database for PostgreSQL Flexible Server | Entra authentication — the API connects as its managed identity, no password anywhere |
| Storage account | Anonymous public access **disabled**; access via user-delegation SAS, no account keys |
| Key Vault | Rebrickable API key. Read via managed identity. |
| Application Insights | OpenTelemetry from the API |
| Container registry | The image App Service pulls |

**Managed identity throughout, no secrets in configuration.** Entra auth to Postgres instead of
a connection-string password, and user-delegation SAS instead of a storage account key. Both
are slightly more work to set up and remove an entire category of incident. A course that
teaches connection strings in app settings teaches a habit students carry into production.

**Network posture — recommended: private endpoints, public network access disabled**, with the
API reaching Postgres and Storage through VNet integration. The alternative is public endpoints
with firewall rules, which is cheaper and simpler and is what most tutorials do. The reason to
prefer private here is that "allow access from Azure services" is a rule that admits every
Azure tenant in the region, and students should see what the real answer looks like at least
once. The cost — a VNet, subnets, private DNS zones, and a Basic-tier plan that cannot do VNet
integration — is real and is the reason this is a recommendation rather than a settled fact.

## Deliberately absent

The governing rule in `CLAUDE.md` says no Azure service appears because the syllabus needs a
module for it. The honest half of that is recording what was **left out**, and why.

| Not used here | Why not | Where it might genuinely belong |
| --- | --- | --- |
| **Cosmos DB** | Catalog data is relational and transactional. Nothing here is document-shaped. See below. | Inspection findings — to be tested honestly, not assumed |
| **Container Apps** | No queue to scale on, no scale-to-zero benefit, no sidecar | The event consumers, which scale on queue depth |
| **Azure Functions** | Nothing in this service is event- or timer-triggered | UC-11's four scheduled jobs — a textbook fit |
| **Azure AI Search** | Postgres indexes cover UC-7.2 completely at this scale | Nowhere in BrickShare, at this size |
| **Azure Cache for Redis** | No measured read pressure. Caching before measuring is guessing at which reads are hot. | Only if read load is ever shown to justify it |
| **Front Door / CDN** | Follows from serving images through the API — see *Images* | If image egress becomes measurable |
| **API Management** | One API, one client, no partners, no quotas to sell | If BrickShare ever fronts several services publicly |

### Cosmos DB deserves saying out loud

Cosmos DB is a **locked course requirement**, and it does not fit the catalog service. Forcing
it in — a document read model beside Postgres, sold as CQRS — would be a demo architecture: it
would work, it would look impressive, and it would teach students to add a second database and
a synchronisation problem to a workload one database handles comfortably. That is exactly the
failure mode the governing rule exists to prevent.

The most promising honest home is **inspection findings and damage logs**: per-copy documents
whose shape varies by set, appended over a copy's life, read whole and never joined. That will
be tested properly in the inspection round, against the workload, with the option of it not
fitting there either.

If no genuine home is found, the correct answer is to **cover Cosmos DB outside BrickShare** —
in an exercise built around a workload that suits it — rather than bend a reference
architecture to accommodate a syllabus. A reference architecture a real team would not ship
teaches worse than an honest gap.

## Decisions

| Area | Decision |
| --- | --- |
| Service boundary | Catalog owns sets, copies, **copy status**, grades, photographs and current prices. One service serves both the staff write side and the customer read side. |
| Status ownership | Other services **request** transitions; catalog applies or refuses them. Keeps UC-1.3's "no retiring an active rental" a local check. |
| Price ownership | Catalog owns **current** price; rentals **freezes** it at reservation. Neither answers the other's question. |
| Compute | **App Service**, Linux container. Justified by the *absence* of a scaling driver. Close call against Container Apps; tiebreaker is operational surface. |
| Deployment artifact | A container image, the same one Compose runs locally |
| Migrations | A pipeline step, never at instance startup — instances race |
| Database | **Postgres**, one database, EF Core. Relational, transactional, joined. No second data-access library. |
| Money | `numeric` / `decimal`. Single shop, single currency, **no currency column** — stated as an assumption. |
| Derived values | Rental price, daily rate and deposit are **computed on read**, never stored, so a multiplier edit re-prices everything at once (UC-1.5). |
| Search | Postgres indexes + `pg_trgm`. **No search service.** |
| Aggregates | Available count and starting price computed per query; a maintained summary is the named escape route |
| Images | One storage account, anonymous access **disabled**, all reads via short-lived **user-delegation SAS** minted by the API |
| Published vs evidence | The split is a **privacy rule about a named customer** and lives in code, where no storage misconfiguration can defeat it |
| Uploads | Through the API, so validation is in one place. Direct-to-blob is the volume answer, and the trigger is named. |
| Events | **Transactional outbox.** At-least-once, unordered; **every consumer must be idempotent**. |
| Transport | Event Grid vs Service Bus **deferred to the messaging round**. The outbox is transport-agnostic so deferring is free. |
| Rebrickable | **Lookup and create are separate endpoints.** The payload is persisted server-side so a client can never POST invented product facts. |
| Rebrickable outage | Only *cataloguing a never-stocked set* fails. Everything else works, because facts are fetched once and stored. |
| Staff identity | Entra ID workforce tenant, app roles `Staff` / `Manager` / `Admin`. Multiplier edits are Admin-only and **two-phase** — preview the blast radius, then apply. |
| Read-side rules | *Available* governs what can be **acted on**, never what is **shown**. Every copy is listed; a set with no copies still appears, with no starting price. |
| Customer identity | **None.** Browse is anonymous; Entra External ID first appears in the rentals service. |
| API shape | REST, minimal APIs, `/api/v1`, `ProblemDetails`. Grade raises are a **separate endpoint** so "grades only fall" cannot be bypassed by an argument. |
| Local dev | Compose: api · postgres · azurite · Rebrickable stub. Offline and quota-free. |
| Secrets | **None in configuration.** Managed identity to Postgres and Storage; Key Vault for the Rebrickable key. |
| Network | Recommended private endpoints with public access disabled; the cheaper public-plus-firewall option is named, with its cost. |

## Open questions

- **Where does the label code come from?** UC-1.2 says BrickShare mints a barcode/QR label
  because LEGO boxes carry no per-unit serial. Whether that is a sequence, a short random code
  or an encoded id changes what a mis-scan does — a sequence makes neighbouring boxes one
  character apart, which is the worst property a scanned identifier can have.
- **Does regrading need a reason recorded?** UC-10.3 logs damage against the copy, but the
  regrade itself carries no justification. Since grade sets price, an unexplained regrade is an
  unexplained price change.
- **Batch registration and baseline weight.** UC-1.2 allows "we bought 3" as one action, but
  baseline weight is per copy and has to be weighed individually. Whether the batch creates
  three copies pending weights, or the weights are required up front, is a workflow question
  for whoever designs the staff screens.
- **Photograph retention.** Evidence photographs are tied to a named customer's rental, so they
  are personal data with a retention obligation. Nothing in IDEA.md says how long they are kept.

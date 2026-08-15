# Course plan — the catalog API

The recording order for the catalog module: what gets built, in which video, and why that
order and not another.

This is not the order the architecture document is written in, and it should not be.
[`docs/architecture/catalog.md`](../../architecture/catalog.md) describes the finished service —
every part of it, arranged by topic. A course has to arrive at that service one idea at a time,
and each idea has to be usable the moment it lands.

## The two rules the order follows

**1. A walking skeleton reaches production before any feature exists.**

Episodes 1–9 build an API that does nothing, health-check it, test it, containerise it, give it
a private registry and deploy it to Azure through GitHub Actions. Only then does a single business rule get written.

This feels like a detour and is the opposite. Every feature after episode 9 lands through a
loop that already works: write it, test it, push it, it is live. If deployment is left until
the end, then the first time it is attempted there are twenty episodes of code to get running
at once, and the episode where it goes wrong is the episode nobody watches twice.

**2. Quality gates arrive as soon as there is a pipeline to enforce them.**

`.editorconfig`, warnings-as-errors and analyzers land in episodes 10–11 — immediately after
CI/CD, not before it. A gate is only a gate if something fails because of it. Before there is a
pipeline, warnings-as-errors is a local preference that a `--no-restore` or a different IDE can
argue with; after it, a rule introduced on camera is enforced on every push from that moment
on.

The cost of waiting nine episodes is one cleanup pass over a `Program.cs` and a single test —
close to nothing, because the skeleton is deliberately almost empty. That is the whole reason
this ordering works, and it stops working if the codebase is allowed to grow first.

## What is deliberately not in this module

**No events, no messaging, no outbox.** The architecture document designs catalog with a
transactional outbox, and catalog will get one — but not here.

The reason is that **there is no subscriber yet**. An event published into a system with
nothing listening is a message to nobody, and the outbox exists to solve a problem
(state and event must commit together) that cannot even be demonstrated without a consumer on
the other side reacting wrongly.

So the messaging module, later, starts by wiring events **the naive way** — publish, then
commit — shows the failure with a real consumer, and introduces the outbox as the fix. A
pattern met after you have felt the bug is understood. A pattern met before it is a ritual
students copy without knowing what it is for.

The catalog service therefore ships eventless, and is retrofitted. That retrofit is itself an
episode worth watching.

---

# Part 1 — a walking skeleton to production

*Ends with a green pipeline deploying a feature-free API to Azure.*

### Episode 1 — What we are building, and why there is no code yet

The only episode with no code in it.

**Covers:** the LEGO rental business, the use cases in `docs/IDEA.md`, the catalog design in
`docs/architecture/catalog.md`, and the rule that governs the whole course — *no Azure service
appears because the syllabus needs a module for it*.

**The point to land:** the design document was written before the code and that is not
ceremony. Students will spend most of this course reading decisions rather than typing, and it
is worth saying out loud that the reasoning is the deliverable.

**Ends with:** nothing built. A repository containing two markdown files and a clear idea of
where it is going.

### Episode 2 — Solution skeleton and the first endpoint

**Builds:** the solution, one API project, one endpoint returning something trivial.

**Teaches:** minimal APIs in .NET 10; repository layout; and *restraint* — one project, not
four. The Clean Architecture template with `Domain`, `Application`, `Infrastructure` and `Api`
on day one is structure ahead of need, and every layer it adds is one a student has to justify
without an example in front of them. Projects appear in this course when something forces them,
and the first force arrives in episode 13.

**Lands in:** `BrickShare.slnx`, `src/Catalog/BrickShare.Catalog.Api/`, plus the repository
hygiene that belongs with a skeleton — `.gitignore`, `global.json` pinning the SDK, and a
`.http` file to call the endpoint with. Notes: [`episode-2.md`](episode-2.md).

**Done when:** `dotnet run` serves a request.

### Episode 3 — Health checks

**Builds:** `/health/live` and `/health/ready`.

**Teaches:** why these are two endpoints and not one. *Live* asks "is this process running" and
touches nothing — if it fails, the answer is to restart. *Ready* asks "can this instance serve
traffic" and checks dependencies — if it fails, the answer is to take it out of rotation and
leave it alone. Wire a restart probe to a dependency check and a database blip restarts every
instance you own, which is how a small outage becomes a large one.

Nothing is checked yet — there are no dependencies. The shape goes in now so that Postgres and
Blob have somewhere obvious to register later.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`. Notes: [`episode-3.md`](episode-3.md).

**Done when:** both endpoints answer, and it is clear what would make each fail.

### Episode 4 — The first test

**Builds:** the test project, and one integration test asserting `/health/live` returns 200.

**Teaches:** xUnit, `WebApplicationFactory`, and how tests will be named and arranged for the
rest of the course.

**Say this out loud:** *this is not TDD.* There is no behaviour to drive out — the test protects
that the application starts and is wired correctly, which is worth protecting and is a
different job. TDD begins properly in episode 12, on the first real rule. Claiming otherwise
for eleven episodes that do not practise it would cost more credibility than it buys.

**Why an integration test before any unit test:** at this point the only thing that can break
is composition — DI registration, configuration, startup. A unit test cannot see that.

**Lands in:** `tests/BrickShare.Catalog.IntegrationTests/`. Notes: [`episode-4.md`](episode-4.md).

**Done when:** `dotnet test` is green.

### Episode 5 — One image, run locally

**Builds:** a multi-stage `Dockerfile`, a `.dockerignore`, and a `docker-compose.yml` with a
single service.

**Teaches:** why the deployed artifact and the local artifact should be the same thing rather
than the same source. Non-root user, small runtime base, layer caching so a code change does
not re-restore every package.

Compose has one service in it and looks pointless. It is the socket that Postgres, Azurite and
a Rebrickable stub plug into later, and adding it now costs one file.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/Dockerfile`, `docker-compose.yml`. Notes:
[`episode-5.md`](episode-5.md).

**Done when:** `docker compose up` serves the same endpoints `dotnet run` did.

### Episode 6 — The Azure portal, by hand — then delete it all

**Does:** creates a resource group, an App Service plan and a Linux web app **by clicking**,
points the app at the container image, watches it come up, and reads the log stream.

**Teaches:** what the building blocks actually are, before anything generates them. The
plan-versus-app distinction that confuses everyone the first time — the plan is the machine you
rent, the app is the thing running on it, and one plan holds many apps. Application settings,
the container pull, where logs come out.

**Ends by deleting the resource group.** Deliberately, on camera. Everything from episode 7
onward is created by Terraform, and leaving a hand-made resource next to a Terraform-managed
one is how state files start lying.

**The point to land:** you should be able to recognise everything Terraform is about to create
before you let it create it. `terraform apply` against resources you have never seen is not
automation, it is trust.

**Lands in:** nothing in the repository. This episode's output is understanding, and a deleted
resource group. Script: [`episode-6.md`](episode-6.md).

### Episode 7 — Terraform: recreate it declaratively

**Builds:** the same resources as episode 6, in HCL.

**Teaches:** providers and version pinning; **remote state and its bootstrap problem** — the
storage account holding the state cannot itself be created by the state it holds, so something
has to come first and the course says plainly what that is; and `plan` as the feature that
justifies the whole tool. Being able to see what will change before it changes is the reason to
write infrastructure this way, and it is the thing the portal cannot do.

Starts as a single `main.tf` — provider, backend, three resources, no module — and gains
file structure or a module only once a second service or a growing file actually calls for
it. The image still comes from Docker Hub, exactly as episode 6 left it; replacing that is the
next episode's job, not this one's.

**Lands in:** `infra/`. Notes: [`episode-7.md`](episode-7.md).

**Done when:** `terraform apply` produces the environment episode 6 deleted, and `terraform
plan` afterwards reports no changes.

### Episode 8 — A registry of our own

**Builds:** an Azure Container Registry, and the App Service pulling from it as its own managed
identity.

**Teaches:** **managed identity as the alternative to a credential**, on the smallest possible
example. A private Docker Hub repository would work — and would put a registry username and
password into application settings. A public one avoids the secret by publishing the artifact
instead. ACR with an `AcrPull` role assignment is the only option that is private *and* has
nothing to store, and the episode ends by showing the app settings list with no registry
password in it, because there never was one.

Also: `plan` showing an **incremental** change for the first time — one resource added against
infrastructure that already exists and is serving traffic. Episode 7 argued that was the point
of the tool; this is where the argument gets paid.

**Why here, immediately before the pipeline.** Episode 9 has to *push* an image, and pushing to
Docker Hub from GitHub Actions means a personal access token stored as a repository secret —
the exact thing episode 9's OIDC lesson exists to eliminate. Push to ACR and the same federated
credential covers both. Switching registries afterwards would mean teaching the pipeline twice,
the second time as a correction.

**Lands in:** nothing in the repository — one resource and one role assignment added to the
`infra/main.tf` from episode 7, which loses its only variable in the process. Notes:
[`episode-8.md`](episode-8.md).

**Done when:** the three URLs answer from a private image, and there is no registry credential
anywhere in the app's configuration.

### Episode 9 — GitHub Actions: the loop closes

**Builds:** build → test → container image → push → deploy, on every push to `main`.

**Teaches:** **OIDC federated credentials**, so no Azure secret is ever stored in GitHub. This
is the episode that decides whether students spend their careers pasting service principal
passwords into repository settings. Also: what belongs in CI versus CD, why the image is built
once and promoted rather than rebuilt per environment, and what "the pipeline is the definition
of done" means in practice.

**The milestone.** From here on, every episode ends with a push that deploys itself. Nothing
later in this course has to stop and fix deployment.

**Lands in:** `.github/workflows/`. Notes: [`episode-9.md`](episode-9.md).

**Done when:** a commit reaches Azure without anyone touching a terminal.

---

# Part 2 — now make the build strict

*The pipeline exists, so every rule added here is enforced from the moment it is introduced.*

### Episode 10 — Consistency and strictness

**Builds:** `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`.

**Teaches:**

- **`.editorconfig`** — formatting and naming settled once, in a file every editor and the SDK
  both read, so "my IDE reformatted the whole file" stops being a pull request.
- **Warnings as errors**, nullable reference types enabled, and a raised analysis level. A
  warning nobody has to fix is a warning nobody fixes; after a few months a build with 300 of
  them tells you nothing at all. The cure is to never let the first one survive.
- **Central package management** — one `Directory.Packages.props` holding versions, so two
  projects cannot quietly depend on two versions of the same library.

**The cleanup pass happens on camera**, and it is deliberately small: a `Program.cs` and one
test. That is the entire reason this episode comes after deployment rather than before it — the
skeleton was kept almost empty precisely so this would cost nothing.

**Lands in:** repository root — `.editorconfig`, `Directory.Build.props`,
`Directory.Packages.props`, and the cleanup they force on both `.csproj` files. Notes:
[`episode-10.md`](episode-10.md).

**Done when:** the solution builds with zero warnings, and a deliberately sloppy line fails the
build.

### Episode 11 — Gates with teeth

**Builds:** analyzers and format verification wired into the pipeline.

**Teaches:** **SonarAnalyzer.CSharp** as a Roslyn analyzer — running in the IDE and in the
build, not as a separate portal to visit; `dotnet format --verify-no-changes` as a CI step, so
formatting is checked rather than argued about; and branch protection, so a red check blocks a
merge instead of merely reporting one.

**The distinction this episode exists to make:** episode 10 configured rules on one machine.
This one makes them fail somebody else's push. Local configuration is advice; a red build is
enforcement, and the difference is the whole reason the two episodes are separate.

**No code coverage here** — not collected, not reported, not gated. A coverage number on a
project with one test is not a quality signal, and a measurement nobody can act on teaches
students to add reporting as a reflex rather than because a question needed answering. It waits
until part 3 has produced a body of tests worth describing, and the episode it lands in is not
decided yet.

**Lands in:** `.github/workflows/ci.yml` (new), `Directory.Build.props`,
`Directory.Packages.props` — plus a branch protection ruleset configured on camera, which is the
only thing in this module that lives in GitHub settings rather than in the repository. Notes:
[`episode-11.md`](episode-11.md).

**Done when:** a formatting violation and an analyzer violation each fail CI, and the merge button
is blocked until they are fixed.

---

# Part 3 — the domain, test-first

*No infrastructure at all. Four episodes of pure C# and tests, which is where TDD becomes
honest rather than performed.*

### Episode 12 — TDD, properly

**Builds:** the pricing rules, test-first — rental price, daily rate, deposit, all derived from
a grade multiplier.

**Teaches:** red, green, refactor, on a rule that has no infrastructure attached to it.

**Why this rule and not another:** TDD is easy to demonstrate badly, and the usual reason is a
first example that needs a database. Pricing is arithmetic over three numbers from
`docs/IDEA.md`:

```
rental price = base rental price × multiplier[grade]
daily rate   = rental price ÷ minimum rental days
deposit      = retail price   × multiplier[grade]
```

No dependencies, real business consequence, and the tests read like the rules do.

**The design consequence, driven out by the tests:** none of these three values is ever stored.
An admin changing one multiplier re-prices the entire catalog (UC-1.5), and stored values would
turn that into a bulk update that is wrong for every row it misses.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`, `tests/BrickShare.Catalog.UnitTests/`.
Notes: [`episode-12.md`](episode-12.md).

### Episode 13 — Money and identifiers

**Builds:** `Money`, `SetNumber`, `LabelCode` as value objects — and the `Domain` project they
force into existence.

**Teaches:**

- **`decimal`, never `double`.** Demonstrated, not asserted: sum `0.1` ten times in a `double`
  and show the result. Money in binary floating point produces deposits a cent wrong and
  refunds that will not reconcile, and it is the single most common data type mistake in
  business software.
- **Primitive obsession.** A `string setNumber` and a `string labelCode` are the same type to
  the compiler and swapping them compiles cleanly. Wrapping them means it does not.
- **A single currency, stated as an assumption** — one shop, no currency column. Written down
  so a second country is a known change rather than a discovery.

**And now a second project.** The domain rules have earned separation: they reference no
ASP.NET types, and putting them behind a project boundary makes that dependency direction
enforced rather than intended. This is what "projects appear when something forces them" looks
like in practice — episode 2 deferred it, episode 13 pays for it.

**Lands in:** `src/Catalog/BrickShare.Catalog.Domain/` (new).
Notes: [`episode-13.md`](episode-13.md).

### Episode 14 — Grades only fall

**Builds:** the condition grade rules, test-first.

**Teaches:** two rules from `docs/IDEA.md` that look like validation and are actually design:

- A grade **never improves on its own**, and **New can never be regained** once a copy has been
  rented — a sealed box is a thing that happened once.
- Raising a grade after a repair is legal, and it is a **different operation with a different
  name**, not the same regrade call with a higher argument. Expressing it as a separate
  operation means the downward-only rule cannot be bypassed by passing a different value, which
  a validation branch inside one method always can.

**The general lesson:** when a rule has an exception, model the exception as its own operation
rather than as a flag on the general one. The tests for each stay short and the rule stays true.

**Lands in:** `src/Catalog/BrickShare.Catalog.Domain/`

### Episode 15 — The copy state machine

**Builds:** copy status transitions, test-first.

**Teaches:** the lifecycle from `docs/IDEA.md` — `Available → Reserved → On rent → Awaiting
inspection → In inspection → Available | In repair | Retired` — with illegal moves refused by
the domain rather than merely absent from a user interface.

**The rule that shapes the service:** a copy **cannot be retired while a rental is active on
it**. Because copy status is catalog's own data, that is a local check inside one transaction.
If rental state lived in another service, enforcing it would mean asking a question whose
answer can be stale before it arrives — and a copy could be retired in the gap between the
check and the write. This is the concrete example behind an abstract principle: **put the data
where the invariant is**.

**Lands in:** `src/Catalog/BrickShare.Catalog.Domain/`

---

# Part 4 — persistence

### Episode 16 — Postgres in Compose, EF Core mapping

**Builds:** Postgres added to Compose, a `DbContext`, entity configurations, the first
migration.

**Teaches:** explicit `IEntityTypeConfiguration` classes over attributes and convention;
`numeric(10,2)` for money and the mapping that guarantees it; unique constraints on set number
and label code as **database** constraints rather than application checks; migrations generated
and committed as reviewable files.

**One data-access story only.** EF Core for reads and writes both. Dapper alongside it is a
second way to reach the database, a second thing to keep consistent and a second thing to get
wrong, and nothing in this service needs it. If a query ever genuinely needs raw SQL, EF runs
raw SQL.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`, `docker-compose.yml`

### Episode 17 — Integration tests against a real database

**Builds:** Testcontainers for PostgreSQL, and the test isolation strategy used from here on.

**Teaches:** why the in-memory provider and SQLite are traps. They pass tests that the real
database fails — different collation, no real constraint enforcement, different transaction
behaviour, no `numeric` semantics. **A fake database gives you green tests and a broken
production deploy**, which is worse than no tests, because it is confidently wrong.

Also: how to isolate tests that share a database, and why every integration test needs a known
starting state rather than whatever the previous test left behind.

**And it works in CI**, because episode 5 already made Docker part of the workflow. Nothing in
the pipeline changes.

**Lands in:** `tests/BrickShare.Catalog.IntegrationTests/`

### Episode 18 — Terraform: Postgres and managed identity

**Builds:** Postgres Flexible Server in Terraform, and the app connecting to it **as its managed
identity**.

**Teaches:** Entra authentication to Postgres instead of a password in configuration. This is
the first episode with a real secret to handle, and the answer is to not have one.

**Say why it matters:** a connection string with a password in app settings is the default in
most tutorials, and it is a credential that is copied into local files, pasted into chat, and
never rotated. A managed identity has no value to steal. The setup is slightly more work once
and removes an entire category of incident permanently.

Network posture is decided here too — private endpoints with public access disabled as the
recommendation, with the cost stated honestly so choosing the cheaper option is a decision.

**Lands in:** `infra/`

### Episode 19 — Migrations in the pipeline

**Builds:** a migration step in GitHub Actions, running before the new revision goes live.

**Teaches:** **why migrations must not run at application startup.** With one instance it works
and looks elegant. With three, three processes race to alter the same schema, and the failure is
intermittent, environment-specific and appears the first time the service is scaled — which is
the worst possible moment to discover it.

Also: **backward-compatible migrations**, because for a few seconds during a deployment the old
code and the new schema are both live. Add before you remove; never rename in one step.

**Lands in:** `.github/workflows/`

**Done when:** a schema change reaches Azure through the pipeline, with the app never starting
against a schema it does not expect.

---

# Part 5 — the write API

### Episode 20 — Cataloguing a set

**Builds:** the staff endpoint that creates a catalog entry, with validation and proper errors.

**Teaches:** endpoint groups and route organisation; versioning under `/api/v1` from the first
public endpoint rather than when it hurts; validation at the edge with the domain still
enforcing its own rules; **`ProblemDetails` (RFC 9457)** so failures are machine-readable and
consistent instead of an ad-hoc JSON shape per endpoint; OpenAPI from the built-in
`Microsoft.AspNetCore.OpenApi`.

**The one to spell out:** validation at the edge does not replace the domain rules from
episodes 12–15. The edge rejects nonsense early with a good message; the domain refuses illegal
states no matter who calls it. Doing only the first gives an API that is safe until something
calls it another way.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`

### Episode 21 — Talking to Rebrickable

**Builds:** the Rebrickable client — typed `HttpClient`, resilience, and its tests.

**Teaches:** `Microsoft.Extensions.Http.Resilience` — timeout, retry with backoff, circuit
breaker — and what each one is actually protecting against, because retry without a circuit
breaker turns a slow dependency into a self-inflicted outage.

**Testing an outbound call** without hitting the real service: a stub behind the same interface,
so tests run offline and do not burn a rate-limited key. This matters more than it sounds when
a class of students is sharing one API quota.

**Key Vault appears here**, because this is the first genuine secret in the system — the
Rebrickable API key. Introduced at the moment of need rather than as a security module bolted
on at the end.

**And the blast radius is narrow by design:** registering more copies of an already-catalogued
set makes no external call, because the facts were fetched once and stored. During a Rebrickable
outage the shop can still register stock, retire copies and regrade — only cataloguing a
never-stocked set fails. Caching is what keeps the consequence that small.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`, `infra/`

### Episode 22 — Two endpoints, for a security reason

**Builds:** the split flow — `POST /catalog/lookups` fetches and stores the Rebrickable payload
server-side; `POST /catalog/sets` references it and carries only the staff-typed fields.

**Teaches:** an authorization bug that looks like an API design choice.

If create accepted `name`, `pieceCount` and `theme` in its request body, anyone holding a staff
token could invent a product — a set Rebrickable has never heard of, with whatever facts they
liked. Persisting the snapshot at lookup time makes **the server the only source of the facts
it stores**, and the create call carries only the four fields staff are actually entitled to
decide: retail price, base rental price, minimum rental duration, age rating.

**The general lesson:** *whose data is this?* — asked of every field in every request body. A
field the client should not be able to choose must not be a field the client can send. This is
one of the most transferable ideas in the course and it costs one extra endpoint.

It also makes `docs/IDEA.md`'s rule — cataloguing blocks until the lookup succeeds — trivially
true, since create cannot be reached without a successful lookup.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`

### Episode 23 — Registering and retiring copies

**Builds:** copy registration, individually and in batch, and retirement.

**Teaches:** a batch as one transaction — three copies or none; minting label codes, since LEGO
boxes carry no per-unit serial and identity has to be issued by BrickShare; baseline weight
recorded per copy while it is known complete, and why it is per copy rather than per set (a
replacement manual shifts the number, and a shared reference would make that copy read as short
on every future return).

**Retire is a state change, never a delete.** Rental history has to survive it, and a deleted
row takes its history with it. The endpoint sets a timestamp and the copy stops being rentable.

The episode-15 rule now runs end to end through HTTP: retiring a copy that is out on rent is
refused, and the test proves it.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`

---

# Part 6 — the read API

### Episode 24 — Browse, search and filter

**Builds:** the public catalog endpoints — search by name and set number, filters on theme,
piece count, age rating, price and availability, with paging.

**Teaches:** indexing for the filters, `pg_trgm` for fuzzy name matching, and keyset paging over
offset paging once the catalog is large enough for it to matter.

**And why there is no search service.** The reflex is *search feature ⇒ search service*, and
resisting it is more useful than another resource in the diagram. Azure AI Search earns its
place with relevance tuning, faceting over large corpora, synonyms and typo tolerance at scale.
A single shop's catalog has none of those problems, and adding it would mean a second data store
to keep in sync with nothing paying for the sync.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`

### Episode 25 — Set detail, and the rules that are easy to break

**Builds:** the set detail endpoint and the per-set aggregates.

**Teaches:** three rules from UC-7 that a query written from intuition gets wrong:

- **Every copy is listed, including unavailable ones.** A customer must be able to see a copy in
  order to subscribe to it. Filtering the list down to what is rentable silently removes the
  subscribe path.
- **A set with no available copies still shows in full** — and so does a set with **no copies at
  all**, which `docs/IDEA.md` explicitly allows. It simply has no starting price, and the
  response says so rather than inventing a zero.
- **Only published photographs are returned to customers.** Damage-evidence photographs are
  reachable through a staff endpoint only — two paths, not one path with a flag on the caller.

**The idea that ties them together:** *available* controls what can be **acted on**, never what
is **shown**. Writing that sentence down prevents all three bugs.

Also here: available count and starting price as aggregates, the starting price being the
cheapest **available** copy so the page never advertises a price nobody can act on.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`

---

# Part 7 — files and identity

### Episode 26 — Uploading photographs

**Builds:** Azurite in Compose, the staff upload endpoint, and its storage in Terraform.

**Teaches:** working with Blob Storage from .NET; validating content type and size in one place;
and why uploads go **through the API** here rather than direct-to-blob with a write SAS.

Direct-to-blob is the pattern that scales and at real volume it is correct — multi-megabyte
uploads should not occupy an application instance. But the API never sees the bytes, so
validation happens after the fact and "did the upload actually land" becomes your problem. A
handful of photographs a day does not justify that. **The trigger for switching is volume, and
naming it now makes the switch a decision rather than a rewrite.**

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`, `docker-compose.yml`, `infra/`

### Episode 27 — SAS, and a privacy rule in code

**Builds:** short-lived user-delegation SAS minting, and the published/evidence split.

**Teaches:** what a SAS is, why a **user-delegation** SAS (signed with the managed identity)
beats one signed with an account key, and how to hand a client a URL that expires.

**The reason this is its own episode:** `docs/IDEA.md` splits photographs into published
current-condition images and internal damage evidence tied to a named customer's rental. That
is **a privacy rule about an identifiable person**, not a display preference, and it decides how
the storage is built.

Anonymous access is disabled at the account level, so no container here can be made public even
by mistake. The rule about who may see what lives **in code, under test** — not in storage
configuration, where the difference between correct and catastrophic is one boolean a future
Terraform edit can flip.

**The trade-off, stated:** no CDN caching, and a signing operation on every image read.
Irrelevant for one shop. The point at which you revisit it is measurable, and the fix is a
second storage account for published images behind a CDN. Splitting later is cheap;
un-publishing something is not.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`, `infra/`

### Episode 28 — Entra ID: protecting the staff endpoints

**Builds:** authentication and authorization — app roles, policies, and tests that run as each
role.

**Teaches:** the Entra ID **workforce tenant** for staff, three app roles matching the actors in
`docs/IDEA.md`, and policy-based authorization rather than role strings scattered through
endpoints.

| Role | May |
| --- | --- |
| `Staff` | Catalogue, register, retire, regrade, upload, view evidence |
| `Manager` | As staff — the exceptional decisions managers own live in later services |
| `Admin` | Edit the grade multipliers |

**Multiplier edits are Admin-only, and two-phase**: a preview returns how many copies change
price and by how much, then apply carries the preview's token. UC-1.5 asks that the scope be
stated before it is applied, and an admin should never learn the blast radius afterwards.

**Customer browse stays anonymous.** UC-7 has no rule requiring a signed-in customer to look at
the catalog, and a rental shop that hides its stock behind a sign-up wall sells less. So Entra
External ID does not appear in this service at all — it starts mattering at reservation, in the
rentals service. **A service that does not need to know who the customer is should not be wired
to an identity provider.**

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`, `infra/`

---

# Part 8 — production readiness

### Episode 29 — Observability

**Builds:** OpenTelemetry wired to Application Insights — traces, metrics, structured logs.

**Teaches:** structured logging over string interpolation, so logs can be queried rather than
grepped; correlation across a request; what a trace shows that a log cannot; and useful
dashboards versus decorative ones.

**And what must never be logged.** Customer names attached to damage findings, anything from an
evidence photograph, tokens, connection strings. A log is a data store with weaker access
control than the database, and it is usually the one that leaks.

**Lands in:** `src/Catalog/BrickShare.Catalog.Api/`, `infra/`

### Episode 30 — Hardening the pipeline

**Builds:** the finished delivery pipeline.

**Teaches:** GitHub environments and manual approval before production; a post-deploy smoke test
that fails the deployment rather than leaving it green and broken; deployment slots and warm-up;
rollback as a practised operation rather than an improvisation.

**Closes the loop opened in episode 9.** That pipeline proved a commit could reach Azure. This
one makes it safe to let it.

**Lands in:** `.github/workflows/`, `infra/`

---

## Compressing the course

Thirty episodes is the honest count once each holds a single idea. Three pairs merge
cleanly if fewer, longer videos are wanted:

| Merge | Gives |
| --- | --- |
| **10 + 11** | One "quality gates" episode, config and enforcement together |
| **24 + 25** | One "read API" episode |
| **26 + 27** | One "photographs" episode covering upload and access together |

Nothing else merges without an episode doing two unrelated things. In particular **4 and 5 do
not merge** — testing and containerisation share nothing, and the seam between them is where a
student who is stuck will stop and rewatch.

## What comes after

Named in order, not broken down — each depends on decisions not yet made:

1. **The rentals service** — and why it does *not* live on App Service, which is the episode
   that makes the catalog compute choice mean something.
2. **Messaging** — events wired the naive way, the failure demonstrated with a real consumer,
   and then the **transactional outbox** as the fix. Retrofitted into catalog on camera.
3. **Event Grid versus Service Bus** — broadcast facts against work queues, decided with two
   services in hand rather than one.
4. **Azure Functions** — UC-11's four scheduled jobs, a workload that genuinely wants a timer
   trigger and nothing else.
5. **Stripe** — authorization, capture, partial capture, and the 28/30 deposit window.
6. **The inspection service** — where **Cosmos DB is tested honestly** against the workload
   rather than assumed into it. If it does not fit there either, the course says so and covers
   Cosmos somewhere it genuinely belongs.
7. **The system map** — `docs/ARCHITECTURE.md`, written last, when there is something to map.

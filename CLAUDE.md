# BrickShare

Backend for a LEGO set rental business: the organisation owns the sets, customers rent them,
collect them in store, and bring them back. Backend only — there is no frontend, and none is
planned. See [`docs/IDEA.md`](docs/IDEA.md) for the product itself.

## This is course material

BrickShare exists as the running real-world example for a course teaching .NET developers to
build on Azure. That changes what "good" means here.

The audience is learning. Code will be read far more often than it is run, and read by
people who do not already know why it looks the way it does. So: clarity beats cleverness,
and every non-obvious choice needs a stated reason. **The reasoning is the product** — a
correct implementation with no explanation of why it is built that way has failed at the
actual job.

Where a decision is genuinely close, say so and give the trade-off. Pretending a judgement
call was obvious teaches worse than admitting it was a judgement call.

## Current status

There is still no code and no infrastructure. Design is in progress.

| Step | State |
| --- | --- |
| 1. Define the use cases → `docs/IDEA.md` | **Done.** Eleven use cases, UC-1 to UC-11. |
| 2. Design the architecture against them | **In progress**, one service at a time |
| 3. Scaffold and implement | Not started |

**Do not scaffold or implement a service before its architecture document exists.** The
reasoning has to be written down first — that is what this repository is for.

### Where architecture lives

Per-service documents under `docs/architecture/`, one file each, readable on its own:

- [`docs/architecture/catalog.md`](docs/architecture/catalog.md) — sets, copies, grades,
  photographs. App Service · Postgres · Blob Storage.

`docs/ARCHITECTURE.md` is reserved for the **system map** and gets written last, once there are
several services to map. Writing it first would be guessing.

### Build order

`docs/course-plan/` holds the recording order — which episode builds what, and why that order.
[`docs/course-plan/catalog/catalog-api.md`](docs/course-plan/catalog/catalog-api.md) is the catalog module.

The build order is **not** the architecture document's structure, and the difference is
deliberate. Notably, catalog ships **without events or an outbox**; they are retrofitted once a
second service exists to consume them. Check the course plan before implementing something the
architecture document describes — it may be staged for later on purpose.

`docs/IDEA.md` describes the business and contains **no technology at all** — no Azure service,
no framework, no database. That separation is deliberate: keep it.

## Locked technical constraints

These are settled. Do not relitigate them or quietly substitute alternatives.

**Must be covered by the course**, so the architecture has to give each a real home:
App Service · Container Apps · Azure Functions · Azure Database for PostgreSQL ·
Cosmos DB · event-driven architecture.

| Area | Constraint |
| --- | --- |
| Platform | .NET 10 (LTS), C# |
| Messaging | Azure Service Bus + Event Grid. No Event Hubs, no Dapr. |
| Infrastructure as code | Terraform modules |
| Local development | Docker Compose |
| Tooling | No .NET Aspire, no `azd`. Everything explicit — nothing generated or hidden behind a scaffolder. |
| Payments | Stripe, as a real integration rather than a stub |
| Identity | Entra ID — workforce tenant for staff, Entra External ID for customers |

The Terraform/Compose and no-Aspire choices are pedagogical: students should be able to read
every resource that exists and understand why. A generator that produces working
infrastructure nobody can explain defeats the point.

## Governing architecture rule

**No Azure service appears in this system because the syllabus needs a module for it.**

Every service must be justified by a workload characteristic — request shape, scaling
profile, consistency requirement, access pattern, cost behaviour. If a service cannot be
justified that way, it does not go in, and the course covers it somewhere it genuinely fits.

The failure mode this guards against is a demo architecture: technically impressive, using
everything, and teaching students to over-engineer. A reference architecture that a real
team would not ship is not a reference architecture.

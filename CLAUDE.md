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

## Current status — stop rule

The repository is greenfield. There is no code, no architecture document and no
infrastructure.

**The use cases are not yet defined. Do not implement anything until they are.**

Order of work:

1. Define the use cases → fill in the Use Cases section of `docs/IDEA.md`.
2. Design the architecture against them → `docs/ARCHITECTURE.md`.
3. Only then scaffold and implement.

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

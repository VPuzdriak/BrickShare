# BrickShare

BrickShare is an online shop for renting Lego sets. It is also a hands-on course: you build the shop step by step as .NET 10 microservices on Azure.

> **Status:** design complete, implementation not started. See [docs/Progress.md](docs/Progress.md).

## What the shop does

- **Customers** browse and search the catalog, reserve a set that is ready now, pay rental and deposit with Stripe, and see their reservation history.
- **Employees** hand sets to customers and receive them back by scanning a QR label on the box, inspect returned sets, and record damage.
- **Admins** manage the catalog (with specs from Rebrickable) and the physical copies, register staff, and ban customers who break shop rules.
- **The system** reminds customers about returns, escalates overdue rentals, settles the deposit against late and damage fees, and treats a set that never comes back as lost.

Full feature descriptions: [docs/features](docs/features/README.md).

## What you learn

| Area | Topics |
|------|--------|
| ASP.NET Core | Minimal APIs, vertical slices, OpenAPI and Scalar, `ProblemDetails`, YARP gateway |
| Design | DDD (aggregates, value objects, domain events, ports and adapters), microservices, database per service |
| Testing | TDD, xUnit v3, AwesomeAssertions, Testcontainers, architecture and contract tests |
| Data | PostgreSQL with EF Core (`pg_trgm` fuzzy search, partial unique indexes), Cosmos DB for NoSQL, Blob Storage |
| Messaging | Azure Service Bus, transactional outbox, idempotent consumers |
| Serverless | Azure Functions (Service Bus and Event Grid triggers), Durable Functions |
| Identity | Microsoft Entra ID (staff, app roles), Entra External ID (customers: e-mail OTP, social logins), managed identities |
| Payments | Stripe Checkout, webhooks, deposits, off-session charges, sandboxes |
| Integrations | Rebrickable API, Microsoft Graph, Azure Communication Services |
| Hosting | Azure Container Apps, App Service, Functions Flex Consumption, Docker, docker compose |
| DevOps | Terraform, GitHub Actions with OIDC, Development / Test / Production environments |
| Quality | Central package management, central `.editorconfig`, SonarAnalyzer, warnings as errors |

Bonus modules: migrate to .NET Aspire and upgrade to .NET 11.

## Architecture at a glance

| Component | Responsibility | Data | Hosting |
|-----------|----------------|------|---------|
| Gateway | Single entry point, routing, token validation | None | Container Apps |
| Catalog | Sets, copies, inventory codes, QR labels, search | PostgreSQL, Blob Storage | App Service |
| Reservation | Availability, reservations, pick-up, return | PostgreSQL | Container Apps |
| Payment | Stripe payments, deposit settlement | PostgreSQL | Container Apps |
| Inspection | Inspection reports and photos | Cosmos DB, Blob Storage | Container Apps |
| Customer | Customer profiles, bans, staff roles | PostgreSQL | Container Apps |
| Notification Functions | E-mails | None | Functions |
| Media Functions | Image processing | Blob Storage | Functions |
| Rental Timer Functions | Pick-up deadlines, overdue and lost escalation | Durable state | Functions |

Details, diagrams, and decisions: [docs/Architecture.md](docs/Architecture.md).

## Documentation

| Document | Content |
|----------|---------|
| [docs/Course idea.md](docs/Course%20idea.md) | Original requirements |
| [docs/Architecture.md](docs/Architecture.md) | Technical design |
| [docs/features](docs/features/README.md) | Business rules, endpoints, events, flows |
| [docs/CoursePlan.md](docs/CoursePlan.md) | Modules and lessons |
| [docs/Progress.md](docs/Progress.md) | Status of every lesson |
| [CLAUDE.md](CLAUDE.md) | Working rules for Claude Code in this repo |

## Prerequisites

Knowledge:

- Building ASP.NET Core Web APIs
- EF Core
- Git and GitHub
- PowerShell

Tools:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker Desktop (or another Docker engine)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) and an Azure subscription
- [Terraform](https://developer.hashicorp.com/terraform/install)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Stripe CLI](https://docs.stripe.com/stripe-cli) and a Stripe account (sandboxes)
- A free [Rebrickable](https://rebrickable.com/api/) API key

## Getting started

The solution does not exist yet. These commands will work once the first modules are done:

```powershell
dotnet build BrickShare.slnx
dotnet test BrickShare.slnx          # Docker must be running (Testcontainers)
docker compose up                    # services and local emulators
```

Local development uses emulators (PostgreSQL, Cosmos DB, Azurite, Service Bus, Mailpit) and development tokens from `dotnet user-jwts`. No Azure resources are needed until the Identity and infrastructure modules.

## Repository layout

Planned structure (see [docs/Architecture.md](docs/Architecture.md#7-solution-structure)):

```text
src/
  BuildingBlocks/   shared kernel, contracts, auth, messaging, service defaults
  Gateway/          YARP gateway
  Services/         Catalog, Reservation, Payment, Inspection, Customer (Domain + Api each)
  Functions/        Notifications, Media, RentalTimers
tests/              domain, integration, function, architecture tests
deploy/terraform/   infrastructure as code
docs/               architecture, features, course plan, progress
```

## Cost note

The course provisions only the Development and Test environments in Azure, on low-cost tiers. Production is fully designed and can be planned with Terraform, but it is not provisioned because of cost.

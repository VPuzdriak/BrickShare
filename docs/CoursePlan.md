# BrickShare course plan

The course builds BrickShare step by step. Technical design: [Architecture.md](Architecture.md). Features: [features](features/README.md). Progress per lesson: [Progress.md](Progress.md).

Each lesson is one small, working increment that ends with green tests. Lesson ids (`2.10`) are stable: [Progress.md](Progress.md) refers to them. The "Features" column links the feature docs a lesson implements (F01 = [01-catalog-management.md](features/01-catalog-management.md), and so on).

## 1. Foundations

Repository setup, central build rules, test setup, and the DDD building blocks.

| Lesson | Title | Features |
|--------|-------|----------|
| 1.1 | Repository, solution (`BrickShare.slnx`), `global.json`, `.gitignore` | |
| 1.2 | Central build settings: `Directory.Build.props`, nullable, warnings as errors | |
| 1.3 | Central package management: `Directory.Packages.props` | |
| 1.4 | Code style: `.editorconfig`, SonarAnalyzer, `dotnet format` | |
| 1.5 | Test projects with xUnit v3 and AwesomeAssertions, the first red-green-refactor cycle | |
| 1.6 | Shared kernel: `Entity`, `AggregateRoot`, `ValueObject`, domain events, results | |
| 1.7 | `Money` value object, test-first | |
| 1.8 | `BrickShare.TestKit`: builders, `FakeTimeProvider`, test tokens | |
| 1.9 | Architecture tests: dependency rules for Domain and services | |
| 1.10 | CI workflow `ci.yml`: build, test, format check | |
| 1.11 | GitHub repository setup: pull requests only, squash merges, branch protection with the `ci` check | |

## 2. Catalog Service

Minimal APIs, the Domain/Api split, PostgreSQL with EF Core, authorization from day one with development tokens, Rebrickable lookup, inventory codes and QR labels, fuzzy search.

| Lesson | Title | Features |
|--------|-------|----------|
| 2.1 | Catalog Domain and Api projects, first Minimal API endpoint, OpenAPI and Scalar | F01 |
| 2.2 | `LegoSet` aggregate and value objects (`LegoSetNumber`, prices, minimum rental days), test-first | F01 |
| 2.3 | EF Core with PostgreSQL: `DbContext`, mappings, migrations, UUID v7 ids | F01 |
| 2.4 | Integration tests with `WebApplicationFactory` and Testcontainers | F01 |
| 2.5 | Authorization policies, development tokens with `dotnet user-jwts`, TestKit tokens in tests | F01 |
| 2.6 | Create, update, and deactivate sets: validation filters, `ProblemDetails` | F01 |
| 2.7 | Themes and theme mapping | F01 |
| 2.8 | Rebrickable lookup: `ISetSpecificationProvider`, typed `HttpClient`, resilience, caching, stub-based tests | F01 |
| 2.9 | Submit a set from a Rebrickable draft, refresh specs | F01 |
| 2.10 | `InventoryCode` value object: alphabet, scramble, check character, parsing, test-first | F01 |
| 2.11 | Add instances: sequence, quantity, condition and physical status changes | F01 |
| 2.12 | QR labels with QRCoder (SVG, label sheet), lookup by scanned code | F01 |
| 2.13 | Set images: SAS upload URLs with Azurite | F01 |
| 2.14 | Browse sets: filters, sorting, paging, set details | F02 |
| 2.15 | Fuzzy search with `pg_trgm` | F02 |
| 2.16 | Theme facets with in-memory caching | F02 |

## 3. Docker and docker compose

| Lesson | Title | Features |
|--------|-------|----------|
| 3.1 | Multi-stage Dockerfile: chiseled runtime image, non-root user | |
| 3.2 | docker compose: PostgreSQL, Azurite, Catalog Service | |
| 3.3 | Configuration through environment variables, `docker-compose.override.yml`, init scripts | |

## 4. Identity

Replace the development issuer with Entra ID and External ID. Endpoints and policies stay unchanged.

| Lesson | Title | Features |
|--------|-------|----------|
| 4.1 | Workforce tenant: BrickShare API app registration, scope, app roles `Admin` and `Employee` | F04 |
| 4.2 | External ID tenant: customer user flow with e-mail OTP, Google, and Facebook | F03 |
| 4.3 | `BrickShare.Auth`: `Staff` and `Customer` schemes, policy scheme by issuer, role claim mapping | F03, F04 |
| 4.4 | Scalar with real sign-in, keeping the development issuer for local work | |

## 5. Customer Service

| Lesson | Title | Features |
|--------|-------|----------|
| 5.1 | Customer Service projects, `Customer` aggregate, just-in-time provisioning (`/customers/me`) | F03 |
| 5.2 | Profile update, staff customer search | F03 |
| 5.3 | Bans in the `Customer` aggregate: ban, lift, automatic ban, test-first | F05 |
| 5.4 | Ban endpoints | F05 |
| 5.5 | Staff management: `IStaffDirectory` port, Microsoft Graph adapter, app role assignment | F04 |
| 5.6 | Staff endpoints | F04 |

## 6. Reservation Service

| Lesson | Title | Features |
|--------|-------|----------|
| 6.1 | Reservation projects, `Reservation` aggregate and state machine, test-first | F06 |
| 6.2 | Booking rules: minimum rental days, price snapshot, `RentalDays` | F06 |
| 6.3 | Persistence and the partial unique index, concurrency test with Testcontainers | F06 |
| 6.4 | Instance availability, availability endpoint, Catalog proxy endpoint | F02, F06 |
| 6.5 | Create reservation with a checkout port fake | F06 |
| 6.6 | `PendingPayment` expiry job with `TimeProvider` | F06 |
| 6.7 | Customer cancellation, reservation history, staff search | F06 |
| 6.8 | Pick-up with scanned inventory code, due date | F08 |
| 6.9 | Return registration, overdue days, active reservation by scanned code | F09 |
| 6.10 | Internal timer endpoints: idempotent and status-aware | F08, F10 |

## 7. Messaging

| Lesson | Title | Features |
|--------|-------|----------|
| 7.1 | Service Bus emulator, topics and subscriptions with SQL filters | |
| 7.2 | `BrickShare.Contracts`: integration events, envelope, contract tests | |
| 7.3 | Transactional outbox in PostgreSQL and the dispatcher | |
| 7.4 | Consumers, inbox for idempotency, retries, dead-lettering | |
| 7.5 | Domain events to integration events in Catalog, Customer, and Reservation | F01, F05, F06 |
| 7.6 | Catalog projections in Reservation: sets, instances, inventory codes | F06 |
| 7.7 | Ban projection in Reservation, soft block for overdue customers | F05, F06 |

## 8. Serverless with Azure Functions

| Lesson | Title | Features |
|--------|-------|----------|
| 8.1 | Notification Functions: isolated worker model, Service Bus trigger, Mailpit | F11 |
| 8.2 | E-mail templates for all events, Communication Services adapter | F11 |
| 8.3 | Media Functions: blob trigger, set image processing with SkiaSharp, `SetImageProcessed` | F01 |
| 8.4 | Inspection photo processing: resize, strip EXIF | F09 |
| 8.5 | Durable Functions basics: `PickupDeadline` orchestration, external events | F08 |
| 8.6 | `Rental` orchestration: durable timers, escalation steps, replay rules, tests | F10 |

## 9. Payment Service

| Lesson | Title | Features |
|--------|-------|----------|
| 9.1 | Payment projects, `IPaymentGateway` port, Stripe sandboxes and restricted API keys | F06 |
| 9.2 | Checkout Session with rental and deposit lines, internal endpoint, replace the fake in Reservation | F06, F07 |
| 9.3 | Webhooks: signature verification, idempotent processing, `PaymentSucceeded`, `DepositSecured` | F06, F07 |
| 9.4 | Reserve and pay end to end with the Stripe CLI | F06 |
| 9.5 | Cancellation, expiry, and no-show: refunds and expired Checkout Sessions | F06, F07, F08 |
| 9.6 | `Settlement` aggregate, test-first | F07 |
| 9.7 | Settlement execution: partial refunds, off-session charges, Payment Links | F07 |
| 9.8 | Lost set settlement, disputes, manual refund, payment history | F07, F10 |

## 10. Inspection Service

| Lesson | Title | Features |
|--------|-------|----------|
| 10.1 | Cosmos DB for NoSQL basics: emulator, database, container, partition key | F09 |
| 10.2 | `InspectionReport` aggregate, test-first | F09 |
| 10.3 | Cosmos persistence, ETag concurrency, outbox with transactional batch | F09 |
| 10.4 | Draft report from `SetReturned`, photo upload URLs, recording findings | F09 |
| 10.5 | Complete inspection: Catalog condition update, deposit settlement end to end | F09, F07 |
| 10.6 | Customer view of inspection findings | F09 |
| 10.7 | Overdue and lost set end to end: automatic ban, instance `Lost`, settlement | F10, F05 |

## 11. Gateway

| Lesson | Title | Features |
|--------|-------|----------|
| 11.1 | YARP routing to all services | |
| 11.2 | Token validation at the gateway, Stripe webhook pass-through | |
| 11.3 | Rate limiting, CORS, request size limits | |

## 12. Observability and resilience

| Lesson | Title | Features |
|--------|-------|----------|
| 12.1 | `BrickShare.ServiceDefaults`: OpenTelemetry traces, metrics, logs | |
| 12.2 | Correlation across HTTP and Service Bus | |
| 12.3 | Health checks for databases, Service Bus, and Cosmos DB | |
| 12.4 | Resilience review: HTTP, Stripe, Rebrickable, Service Bus | |

## 13. Azure infrastructure with Terraform

| Lesson | Title | Features |
|--------|-------|----------|
| 13.1 | Bootstrap: state storage and GitHub OIDC identity | |
| 13.2 | Terraform layout, providers, remote state, shared Container Registry | |
| 13.3 | Network, monitoring, Key Vault | |
| 13.4 | Data: PostgreSQL (extensions, Entra auth), Cosmos DB, Storage | |
| 13.5 | Service Bus namespace, topics, subscriptions | |
| 13.6 | Managed identities and RBAC | |
| 13.7 | Entra app registrations with the `azuread` provider | |
| 13.8 | Environments: dev and test tfvars, Production plan only | |

## 14. Hosting

| Lesson | Title | Features |
|--------|-------|----------|
| 14.1 | Container Apps environment and the `container-app` module | |
| 14.2 | App Service for Catalog: VNet integration, access restrictions | |
| 14.3 | Function apps on Flex Consumption, Event Grid subscriptions | |
| 14.4 | [Deployment slots demonstration](#deployment-slots-demonstration) | |
| 14.5 | Smoke test of the deployed system | |

## 15. CI/CD and environments

| Lesson | Title | Features |
|--------|-------|----------|
| 15.1 | GitHub Environments and Azure login with OIDC | |
| 15.2 | `infra.yml`: plan on pull requests, apply to dev and test | |
| 15.3 | Service workflows: build image once, push to ACR, promote | |
| 15.4 | Catalog zip deploy and Function app deployments | |
| 15.5 | EF Core migration bundles in the pipeline | |
| 15.6 | Production stage (disabled) and approvals | |

## 16. Bonus: .NET Aspire

| Lesson | Title | Features |
|--------|-------|----------|
| 16.1 | AppHost replacing docker compose | |
| 16.2 | Aspire service defaults and integrations (PostgreSQL, Cosmos DB, Service Bus, Storage) | |
| 16.3 | Azure Functions in Aspire | |
| 16.4 | Deploy with `azd` | |

## 17. Bonus: .NET 11 upgrade

| Lesson | Title | Features |
|--------|-------|----------|
| 17.1 | Upgrade SDK and target framework, bump packages, fix new analyzer warnings, verify the pipeline | |

## 18. Optional: hybrid deposit hold for short rentals

| Lesson | Title | Features |
|--------|-------|----------|
| 18.1 | `DepositMode` policy and hold window rule, test-first | F07 |
| 18.2 | Deposit hold with manual capture, partial capture at settlement | F07 |
| 18.3 | Fallback to charge and refund, hold expiry safety job | F07 |

## Deployment slots demonstration

Deployment slots are a key App Service feature, but they need the Standard tier or higher. The course shows them in one lesson as a demonstration only. After the lesson, the App Service goes back to B1 without slots.

Lesson steps (in Development):

1. In `envs/dev/terraform.tfvars`, set `app_service_sku = "S1"` and `app_service_slots = ["staging"]`. Run `terraform apply`.
2. Deploy a new Catalog version to the `staging` slot (`az webapp deploy --slot staging`).
3. Open the staging URL and run smoke tests against it.
4. Show slot-specific settings ("deployment slot setting") versus settings that move with the swap.
5. Swap `staging` into production (`az webapp deployment slot swap`). Show that the swap has no downtime and that the old version is now in `staging`.
6. Swap back to show a quick rollback.
7. Revert `terraform.tfvars` to `app_service_sku = "B1"` and `app_service_slots = []`. Run `terraform apply`. Terraform deletes the slot and scales the plan down.

The `app-service` Terraform module creates slots only when `app_service_slots` is not empty, so the same module serves B1 (no slots) and the Production design (with slots). Run the lesson in one sitting: S1 is billed per hour while it exists.

## Production in the course

Production resources are not provisioned, because they cost too much. Students run `terraform plan` for Production to see the difference, but they do not run `terraform apply`. The Production deploy stage in GitHub Actions is defined but disabled. See [Architecture: Azure deployment topology](Architecture.md#9-azure-deployment-topology).

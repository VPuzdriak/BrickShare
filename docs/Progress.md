# Implementation progress

One marker per lesson from [CoursePlan.md](CoursePlan.md). Lesson ids match the course plan.

- `[ ]` not started, `[~]` in progress, `[x]` done.
- Mark a lesson `[~]` when work starts and `[x]` when it is finished and its tests are green.
- Add a line to the log for every status change.
- If a lesson is added, split, or renamed, change [CoursePlan.md](CoursePlan.md) first, then this file.

**Current lesson:** none (1.1 to 1.7 done)

Last updated: 2026-11-26

## 1. Foundations

- [x] 1.1 Repository, solution (`BrickShare.slnx`), `global.json`, `.gitignore`
- [x] 1.2 Central build settings: `Directory.Build.props`, nullable, warnings as errors
- [x] 1.3 Central package management: `Directory.Packages.props`
- [x] 1.4 Code style: `.editorconfig`, SonarAnalyzer, `dotnet format`
- [x] 1.5 Test projects with xUnit v3 and AwesomeAssertions, the first red-green-refactor cycle
- [x] 1.6 Shared kernel: `Entity`, `AggregateRoot`, domain events, results
- [x] 1.7 `Money` value object, test-first
- [x] 1.10 CI workflow `ci.yml`: build, test, format check
- [x] 1.11 GitHub repository setup: pull requests only, squash merges, branch protection with the `ci` check

## 2. Catalog Service

- [x] 2.1 Catalog Api project, liveness check `/health/live`, OpenAPI and Scalar
- [x] 2.2 Catalog Domain project, `LegoSet` aggregate and value objects (`LegoSetNumber`, prices, minimum rental days), test-first (F01)
- [x] 2.3 EF Core with PostgreSQL: `DbContext`, mappings, migrations, UUID v7 ids (F01)
- [x] 2.4 Integration tests with `WebApplicationFactory` and Testcontainers (F01)
- [ ] 2.5 Authorization policies, development tokens with `dotnet user-jwts`, test tokens in the test project (F01)
- [ ] 2.6 Create, update, and deactivate sets: validation filters, `ProblemDetails` (F01)
- [ ] 2.7 Themes and theme mapping (F01)
- [ ] 2.8 Rebrickable lookup: `ISetSpecificationProvider`, typed `HttpClient`, resilience, caching, stub-based tests (F01)
- [ ] 2.9 Submit a set from a Rebrickable draft, refresh specs (F01)
- [ ] 2.10 `InventoryCode` value object: alphabet, scramble, check character, parsing, test-first (F01)
- [ ] 2.11 Add instances: sequence, quantity, condition and physical status changes (F01)
- [ ] 2.12 QR labels with QRCoder (SVG, label sheet), lookup by scanned code (F01)
- [ ] 2.13 Set images: SAS upload URLs with Azurite (F01)
- [ ] 2.14 Browse sets: filters, sorting, paging, set details (F02)
- [ ] 2.15 Fuzzy search with `pg_trgm` (F02)
- [ ] 2.16 Theme facets with in-memory caching (F02)

## 3. Docker and docker compose

- [ ] 3.1 Multi-stage Dockerfile: chiseled runtime image, non-root user
- [ ] 3.2 docker compose: PostgreSQL, Azurite, Catalog Service
- [ ] 3.3 Configuration through environment variables, `docker-compose.override.yml`, init scripts

## 4. Identity

- [ ] 4.1 Workforce tenant: BrickShare API app registration, scope, app roles `Admin` and `Employee` (F04)
- [ ] 4.2 External ID tenant: customer user flow with e-mail OTP, Google, and Facebook (F03)
- [ ] 4.3 `BrickShare.Auth`: `Staff` and `Customer` schemes, policy scheme by issuer, role claim mapping (F03, F04)
- [ ] 4.4 Scalar with real sign-in, keeping the development issuer for local work

## 5. Customer Service

- [ ] 5.1 Customer Service projects, `Customer` aggregate, just-in-time provisioning (`/customers/me`) (F03)
- [ ] 5.2 Profile update, staff customer search (F03)
- [ ] 5.3 Bans in the `Customer` aggregate: ban, lift, automatic ban, test-first (F05)
- [ ] 5.4 Ban endpoints (F05)
- [ ] 5.5 Staff management: `IStaffDirectory` port, Microsoft Graph adapter, app role assignment (F04)
- [ ] 5.6 Staff endpoints (F04)

## 6. Reservation Service

- [ ] 6.1 Reservation projects, architecture test: no references to other services' projects, `Reservation` aggregate and state machine, test-first (F06)
- [ ] 6.2 Booking rules: minimum rental days, price snapshot, `RentalDays` (F06)
- [ ] 6.3 Persistence and the partial unique index, concurrency test with Testcontainers (F06)
- [ ] 6.4 Instance availability, availability endpoint, Catalog proxy endpoint (F02, F06)
- [ ] 6.5 Create reservation with a checkout port fake (F06)
- [ ] 6.6 `PendingPayment` expiry job with `TimeProvider` (F06)
- [ ] 6.7 Customer cancellation, reservation history, staff search (F06)
- [ ] 6.8 Pick-up with scanned inventory code, due date (F08)
- [ ] 6.9 Return registration, overdue days, active reservation by scanned code (F09)
- [ ] 6.10 Internal timer endpoints: idempotent and status-aware (F08, F10)

## 7. Messaging

- [ ] 7.1 Service Bus emulator, topics and subscriptions with SQL filters
- [ ] 7.2 `BrickShare.Contracts`: integration events, envelope, contract tests
- [ ] 7.3 Transactional outbox in PostgreSQL and the dispatcher
- [ ] 7.4 Consumers, inbox for idempotency, retries, dead-lettering
- [ ] 7.5 Domain events to integration events in Catalog, Customer, and Reservation (F01, F05, F06)
- [ ] 7.6 Catalog projections in Reservation: sets, instances, inventory codes (F06)
- [ ] 7.7 Ban projection in Reservation, soft block for overdue customers (F05, F06)

## 8. Serverless with Azure Functions

- [ ] 8.1 Notification Functions: isolated worker model, Service Bus trigger, Mailpit (F11)
- [ ] 8.2 E-mail templates for all events, Communication Services adapter (F11)
- [ ] 8.3 Media Functions: blob trigger, set image processing with SkiaSharp, `SetImageProcessed` (F01)
- [ ] 8.4 Inspection photo processing: resize, strip EXIF (F09)
- [ ] 8.5 Durable Functions basics: `PickupDeadline` orchestration, external events (F08)
- [ ] 8.6 `Rental` orchestration: durable timers, escalation steps, replay rules, tests (F10)

## 9. Payment Service

- [ ] 9.1 Payment projects, `IPaymentGateway` port, Stripe sandboxes and restricted API keys (F06)
- [ ] 9.2 Checkout Session with rental and deposit lines, internal endpoint, replace the fake in Reservation (F06, F07)
- [ ] 9.3 Webhooks: signature verification, idempotent processing, `PaymentSucceeded`, `DepositSecured` (F06, F07)
- [ ] 9.4 Reserve and pay end to end with the Stripe CLI (F06)
- [ ] 9.5 Cancellation, expiry, and no-show: refunds and expired Checkout Sessions (F06, F07, F08)
- [ ] 9.6 `Settlement` aggregate, test-first (F07)
- [ ] 9.7 Settlement execution: partial refunds, off-session charges, Payment Links (F07)
- [ ] 9.8 Lost set settlement, disputes, manual refund, payment history (F07, F10)

## 10. Inspection Service

- [ ] 10.1 Cosmos DB for NoSQL basics: emulator, database, container, partition key (F09)
- [ ] 10.2 `InspectionReport` aggregate, test-first (F09)
- [ ] 10.3 Cosmos persistence, ETag concurrency, outbox with transactional batch (F09)
- [ ] 10.4 Draft report from `SetReturned`, photo upload URLs, recording findings (F09)
- [ ] 10.5 Complete inspection: Catalog condition update, deposit settlement end to end (F09, F07)
- [ ] 10.6 Customer view of inspection findings (F09)
- [ ] 10.7 Overdue and lost set end to end: automatic ban, instance `Lost`, settlement (F10, F05)

## 11. Gateway

- [ ] 11.1 YARP routing to all services
- [ ] 11.2 Token validation at the gateway, Stripe webhook pass-through
- [ ] 11.3 Rate limiting, CORS, request size limits

## 12. Observability and resilience

- [ ] 12.1 `BrickShare.ServiceDefaults`: OpenTelemetry traces, metrics, logs
- [ ] 12.2 Correlation across HTTP and Service Bus
- [ ] 12.3 Health checks for databases, Service Bus, and Cosmos DB
- [ ] 12.4 Resilience review: HTTP, Stripe, Rebrickable, Service Bus

## 13. Azure infrastructure with Terraform

- [ ] 13.1 Bootstrap: state storage and GitHub OIDC identity
- [ ] 13.2 Terraform layout, providers, remote state, shared Container Registry
- [ ] 13.3 Network, monitoring, Key Vault
- [ ] 13.4 Data: PostgreSQL (extensions, Entra auth), Cosmos DB, Storage
- [ ] 13.5 Service Bus namespace, topics, subscriptions
- [ ] 13.6 Managed identities and RBAC
- [ ] 13.7 Entra app registrations with the `azuread` provider
- [ ] 13.8 Environments: dev and test tfvars, Production plan only

## 14. Hosting

- [ ] 14.1 Container Apps environment and the `container-app` module
- [ ] 14.2 App Service for Catalog: VNet integration, access restrictions
- [ ] 14.3 Function apps on Flex Consumption, Event Grid subscriptions
- [ ] 14.4 Deployment slots demonstration
- [ ] 14.5 Smoke test of the deployed system

## 15. CI/CD and environments

- [ ] 15.1 GitHub Environments and Azure login with OIDC
- [ ] 15.2 `infra.yml`: plan on pull requests, apply to dev and test
- [ ] 15.3 Service workflows: build image once, push to ACR, promote
- [ ] 15.4 Catalog zip deploy and Function app deployments
- [ ] 15.5 EF Core migration bundles in the pipeline
- [ ] 15.6 Production stage (disabled) and approvals

## 16. Bonus: .NET Aspire

- [ ] 16.1 AppHost replacing docker compose
- [ ] 16.2 Aspire service defaults and integrations (PostgreSQL, Cosmos DB, Service Bus, Storage)
- [ ] 16.3 Azure Functions in Aspire
- [ ] 16.4 Deploy with `azd`

## 17. Bonus: .NET 11 upgrade

- [ ] 17.1 Upgrade SDK and target framework, bump packages, fix new analyzer warnings, verify the pipeline

## 18. Optional: hybrid deposit hold for short rentals

- [ ] 18.1 `DepositMode` policy and hold window rule, test-first (F07)
- [ ] 18.2 Deposit hold with manual capture, partial capture at settlement (F07)
- [ ] 18.3 Fallback to charge and refund, hold expiry safety job (F07)

## Log

| Date       | Lesson | Change                                                                                                                                                                                                                                                                                           |
| ---------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2026-11-25 |        | Course plan split into lessons. Implementation not started.                                                                                                                                                                                                                                      |
| 2026-11-25 | 1.1    | Done: `global.json` (10.0.100, latestFeature), `BrickShare.slnx`, `.gitignore`. Solution file written by hand because SDK 10.0.100 `dotnet new sln` has no `--format slnx`.                                                                                                                      |
| 2026-11-25 | 1.11   | Added to the course plan: GitHub repository setup lesson (pull requests only, squash merges, branch protection requiring the `ci` check). Lesson script to be written later.                                                                                                                     |
| 2026-11-25 | 1.2    | Done: `Directory.Build.props` (net10.0, nullable, warnings as errors). Verified with a probe project that was deleted again. Not committed.                                                                                                                                                      |
| 2026-11-26 | 1.8    | Removed from the plan: no `BrickShare.TestKit`. Money needs no builder, and a token helper that issues fake tokens would only test itself. Test helpers live in each test project. Lesson script deleted.                                                                                        |
| 2026-11-26 | 1.7    | Done: `Money` record in `BrickShare.SharedKernel` (amount in minor units, currency code, EUR only, non-negative), 14 tests green. Lesson script corrected: test count and summary. Not committed.                                                                                                |
| 2026-11-26 | 1.6    | Done: `BrickShare.SharedKernel` with `Entity`, `AggregateRoot`, domain events and results, 9 tests green. Lesson script fixed for the real CS0103/CS0246 errors in step 13. Not committed.                                                                                                       |
| 2026-11-26 | 1.5    | Done: `xunit.v3` and AwesomeAssertions in `Directory.Packages.props`, `global.json` test runner (Microsoft Testing Platform), `.editorconfig` CA1707 off for test projects. Probe with two red-green-refactor tests kept in `probe/`, not committed. Lesson script corrected to match the build. |
| 2026-11-26 | 1.9    | Removed from the plan: architecture tests start in lesson 2.1, when the Catalog Domain and Api projects exist. Cross-service rule added to lesson 6.1, when Reservation exists. No lesson script existed.                                                                                        |
| 2026-11-26 | 2.1    | Plan changed: lesson 2.1 is the Catalog Api project with `/health/live`, OpenAPI and Scalar. Architecture tests and the Domain project moved out of it; the Domain project comes with lesson 2.2. Lesson script rewritten.                                                                       |

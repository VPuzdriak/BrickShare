# BrickShare

Lego set rental shop built as a teaching course: .NET 10 microservices on Azure. The code is the course material, so it must be clean, idiomatic, and easy to follow.

## Documentation map

Read the relevant doc before you change code. Do not duplicate doc content in code comments or in other docs. Link to it instead.

| Doc | Use it for |
|-----|-----------|
| `docs/Course idea.md` | Original requirements from the author. Do not edit |
| `docs/Architecture.md` | Technical design: services, data stores, events, identity, infra, CI/CD |
| `docs/features/*.md` | Business rules, endpoints, events, and flows per feature. The source of truth for behavior |
| `docs/CoursePlan.md` | Module order. Build in this order |
| `docs/Progress.md` | One marker per lesson. Keep it current |
| `docs/lessons/*.md` | Lesson scripts: summary, problem, and code step by step. Written by the `lesson-script` skill |

## Workflow

- Before implementing a feature, read its file in `docs/features/` and the matching service section in `docs/Architecture.md`.
- Work happens lesson by lesson (lesson ids from `docs/CoursePlan.md`). When a lesson starts, mark it `[~]` in `docs/Progress.md` and set "Current lesson". When it is done and its tests are green, mark it `[x]`. Add a dated line to the log for each change.
- A lesson that turns out too big is split in `docs/CoursePlan.md` first, then in `docs/Progress.md`.
- If implementation needs a design change, update the docs in the same change. Ask first if the change alters a business rule.
- Follow the module order in `docs/CoursePlan.md`. Do not build later modules early.
- Docs use plain technical English: short sentences, active voice, one term per concept.

## Code conventions

- **Minimal APIs only**, no MVC controllers. Vertical slices in `<Service>.Api`: `Features/<Area>/<UseCase>/` with the endpoint, request/response, validation, and handler together. `MapGroup` per resource, `TypedResults`, authorization policies on groups.
- Central package management: versions only in `Directory.Packages.props`. Never put `Version` on a `PackageReference`.
- Shared build settings live in `Directory.Build.props` (net10.0, nullable, warnings as errors, SonarAnalyzer). Fix warnings, do not suppress them. A suppression needs a justification comment.
- Code style comes from the root `.editorconfig`. Run `dotnet format` before you finish.
- Errors return RFC 9457 `ProblemDetails`.
- Money: `long` minor units (cents) plus currency code. Never `decimal`/`double` for stored amounts.
- Time: UTC only (`DateTimeOffset`, `timestamptz`). Inject `TimeProvider`. Never call `DateTime.Now` or `DateTime.UtcNow` in business code.
- Customer and staff ids come from the `oid` token claim. Customer endpoints never take a customer id from the route.
- Every endpoint group declares its authorization policy from the start (`AdminOnly`, `EmployeeOrAdmin`, `CustomerOnly`, or explicit `AllowAnonymous`). Policies are role-based and do not depend on the token issuer.
- Until the Identity module, local tokens come from `dotnet user-jwts` (`docs/Architecture.md` 5.3). The development issuer is registered only in the `Development` environment. Never enable it anywhere else.

## DDD and TDD

Details: `docs/Architecture.md` sections 7.1 and 7.2.

- **TDD by default.** Start each change with a failing test derived from a rule in `docs/features/`. Then write the simplest code that passes, then refactor. Do not write production code without a test that needs it.
- Domain rules are covered by fast unit tests in `<Service>.Domain.Tests` (xUnit v3, Shouldly, no mocks). Endpoints, auth, and persistence are covered by integration tests (`WebApplicationFactory` + Testcontainers).
- Each service has `<Service>.Domain` (plain C#, no EF Core, ASP.NET Core, Azure, or Stripe references) and `<Service>.Api` (slices, EF Core, adapters).
- Business rules live in aggregates and value objects, not in endpoints or handlers. Handlers load an aggregate, call a method, and save.
- No public setters on aggregates. One transaction changes one aggregate.
- Domain events stay internal. Handlers translate them into integration events (`BrickShare.Contracts`) through the outbox.
- External systems sit behind ports (`IPaymentGateway`, `IStaffDirectory`, `ISetSpecificationProvider`). Tests use fakes. Never call Stripe or Rebrickable in automated tests.
- Use DDD where the domain has rules (Reservation, Payment, Inspection, Customer bans). Keep CRUD parts simple. Do not add patterns that a feature does not need.
- Test names describe behavior: `Pickup_fails_when_deposit_is_not_secured`. Use TestKit builders and `FakeTimeProvider`.
- Do not use FluentAssertions (commercial license from v8).

## Architecture rules

- Each service owns its database. Never read or write another service's data. Use events (projections) or the service API.
- Integration events: records in `BrickShare.Contracts`, published through the transactional outbox. Consumers are idempotent (inbox). Change contracts only by adding fields.
- Reservation is the only writer of reservation data. Rental Timer Functions call its `/internal/reservations/{id}/timers/{step}` endpoints, which must stay idempotent and status-aware.
- Durable orchestrator code must be deterministic: no I/O, use `context.CurrentUtcDateTime`.
- Azure access uses managed identity. No connection strings with keys and no secrets in the repo. Stripe secrets live in Key Vault only.
- Every Stripe call sends an idempotency key derived from the business operation.
- Stripe uses **sandboxes**, never the shared test mode: `brickshare-local`, `brickshare-ci`, `brickshare-dev`, `brickshare-test`. Production uses live mode. Use restricted API keys (`rk_`).

## Skills

Project skills live in `.claude/skills/` and are pinned in `skills-lock.json` (restore with `npx skills experimental_install`). Installed: `stripe-best-practices`, `terraform-style-guide`, `cosmosdb-best-practices`, `azure-functions`, `azure-service-bus`.

Local skill (not in `skills-lock.json`): `lesson-script`. `/lesson-script <id>` writes `docs/lessons/<id>-<slug>.md` on Haiku and changes no other file.

**If a skill conflicts with this file or `docs/`, follow this file and `docs/`.** Do not install skills whose rules conflict with the docs. Note: `terraform-style-guide` examples use AWS and `staging`. Our provider is `azurerm`, and environments are `dev`, `test`, `prod`.

## Infrastructure

- Terraform in `deploy/terraform`. Environments: `dev`, `test`, `prod`.
- **Never run `terraform apply` for `prod`.** Production is designed but not provisioned because of cost. `terraform plan` is fine.
- App Service stays on B1 without slots. Slots exist only during the demonstration lesson.

## Commands

Planned commands. Update this section when the solution exists.

```powershell
dotnet build BrickShare.slnx          # warnings are errors
dotnet test BrickShare.slnx           # integration tests use Testcontainers (Docker required)
dotnet format --verify-no-changes
docker compose up                     # services + emulators (Postgres, Cosmos, Azurite, Service Bus, Mailpit, Stripe CLI)
terraform -chdir=deploy/terraform/envs/dev plan
```

# Episode 19 — Migrations in the pipeline

← [Course plan](catalog-api.md) · Previous: [Episode 18 — Terraform: Postgres and managed identity](episode-18.md)

Episode 18 finished with `/health/ready` returning 200 from Azure for the first time in fifteen
episodes, and that 200 is telling the truth about a smaller thing than it sounds. `AddDbContextCheck`
calls `CanConnectAsync`. It proves the application can open an authenticated connection. It says
nothing about tables, and there are none: `20260821222411_InitialCatalog` has been applied to a
Compose container and to a Testcontainers instance, and never once to the database customers will
eventually use.

Fixing that is three lines in `Program.cs`, and those three lines are the reason this episode exists.
They work perfectly with one instance, which is exactly what makes them dangerous.

So the episode is really about two questions, and only the second one is interesting:

1. **What applies the migration?** A pipeline job, running a bundle built alongside the image.
2. **When, relative to the new code going live?** *Before* — which quietly commits every migration
   this course will ever write to being backward compatible with the code already running.

**Done when** a schema change reaches Azure through the pipeline with nobody touching a terminal,
`copies` and `__EFMigrationsHistory` exist in the Azure database, a failed migration **skips** the
deployment instead of preceding it, and the application still has no permission to alter its own
schema.

## Before recording

- Episode 18 merged: the Postgres Flexible Server, the Entra admin group, the app's managed-identity
  connection, `/health/ready` green in Azure.
- `psql` on the path, and the token trick from episode 18 step 8 to hand:
  `export PGPASSWORD=$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)`.
- The Azure CLI logged in with rights to edit an Entra group's membership — step 2 needs
  `az ad group member add`.
- The object id of the pipeline's service principal. It is the app registration episode 9 federated,
  and `vars.AZURE_CLIENT_ID` is its **application** id, which is not the same number. Step 2 turns
  one into the other on camera, because getting this wrong is a twenty-minute detour.
- A branch.
- [`episode-18.md`](episode-18.md) open at **step 1** and **step 8**. This episode is where both get
  paid, and it is worth having the exact sentences on screen when they do.
- [`docs/architecture/catalog.md`](../../architecture/catalog.md) open at *Migrations*.

**This episode is not TDD, and the exemption is the plainest one in the course.** A GitHub Actions
job, two Terraform outputs and a group membership are infrastructure, configuration and wiring —
`CLAUDE.md` names all three. Of the two pieces of C# written today, one is added **in order to fail
and then deleted**, and the other is a design-time factory with no behaviour of its own: it composes
objects, and the thing it composes is already covered by every integration test in the suite.

There are two reds in here, and both are worth pointing at when they arrive: step 5 hits an
exception that episode 18 wrote on purpose, and step 9 breaks the
migration job on purpose to prove the deployment is gated on it. **A gate you have never watched
close is a gate you are assuming.**

## The target shape

`.github/workflows/deploy.yml` gains one job and two steps in an existing one.
`infra/main.tf` gains two `output` blocks — no resources, nothing that costs money. And the API
project gains **one new file and one edited line of `.csproj`**, both discovered the hard way in
step 5: the migration runner turns out to need a composition root of its own.

`Program.cs` ends the episode byte-for-byte identical to how it started, which is the interesting
half of that sentence.

The finished job graph:

```
build-test ──► image ──► migrate ──► deploy
```

Four jobs, strictly in a line, and step 6 argues about the two arrows in the middle.

---

## Step 0 — A database with nothing in it

Put the actual state on screen before proposing a fix for it. As the admin group, from episode 18:

```bash
export PGPASSWORD=$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)

psql "host=psql-brickshare-catalog-dev.postgres.database.azure.com \
      port=5432 \
      user=BrickShare\ Catalog\ DB\ Admins \
      dbname=brickshare_catalog \
      sslmode=require"
```

```
brickshare_catalog=> \dt
Did not find any relations.
```

And yet:

```bash
curl -i "$(cd infra && terraform output -raw web_app_url)/health/ready"   # 200
```

**Both of those are correct**, and holding them together is the first small idea of the episode.
Readiness asks *can I reach my dependency*, not *is my dependency shaped the way I expect*. Episode
18 said that in one sentence at the end of step 8; here is the sentence with a consequence attached.

A readiness check that also verified the schema would sound more thorough and would be worse: it
would turn every deployment into a window where healthy instances report unhealthy, and it would
give App Service a reason to pull instances out of rotation for a condition no restart and no
rebalancing can fix. **Readiness answers a question the platform can act on.** "Is my schema
current" is not one of those questions — it is the pipeline's job, and it is this episode.

**This step is not code.**

---

## Step 1 — The three lines everyone writes, and the database refusing them

Write the tempting version, on camera, and deploy it. In `Program.cs`, after `var app = builder.Build();`:

```csharp
// Not this. Ever. Deployed here only so the failure is something you have watched.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
}
```

Push it, wait for the pipeline, and read the log:

```bash
az webapp log tail --name app-brickshare-catalog-dev --resource-group rg-brickshare-dev
```

```
Npgsql.PostgresException (0x80004005): 42501: permission denied for schema public
```

### The failure everybody talks about

Say it first, because it is the one the architecture document names and the one students will meet
in the wild:

> Migrations do not run at startup. With more than one instance, startup migration means several
> processes racing to alter the same schema. Migration is a **pipeline step** that runs once before
> the new revision is released. This is the single most common way a first Azure deployment goes
> wrong, and it only shows up when you scale past one instance — which is exactly when you least
> want to discover it.

Three points worth making about that failure specifically, because "there is a race" is where most
explanations stop:

- **It is invisible on one instance, and this app has one instance.** Every local run works. Every
  deployment works. The bug ships in a state where nothing can detect it.
- **The trigger is a scale-out**, which is to say: the moment traffic is highest and somebody is
  already stressed. The failure and the load spike arrive together and the second one gets the blame.
- **EF Core does not serialise this for you.** `MigrateAsync` is not a distributed lock. Two
  processes can both read `__EFMigrationsHistory`, both conclude the migration is pending, and both
  run it. The Postgres-level failure is arbitrary — a duplicate table, a deadlock, a half-applied
  migration — which is a fair description of the worst class of bug: *non-deterministic, in the
  schema*.

### The failure this repository can actually show

But that is not the error on screen. The error on screen is `42501: permission denied for schema
public`, and it is there because of a decision made in episode 18 step 8:

```sql
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
  TO "app-brickshare-catalog-dev";
```

DML and no DDL. No `CREATE`, no `DROP`, no ownership. Episode 18 said what that was for and this is
it: **the application cannot alter its own schema, so it cannot accidentally alter its own schema on
startup, on all three instances at once.**

Sit on the difference between the two failures for a moment, because it is the transferable part.
The first is prevented by everyone on the team knowing a rule and remembering it under pressure. The
second is prevented by a grant. **A rule enforced by a permission does not depend on the next
developer having watched this video** — and the next developer, in six months, adding
`MigrateAsync()` back because a tutorial said to, gets a red deployment instead of an intermittent
production incident.

Now delete those lines. `Program.cs` goes back to exactly what episode 18 left, and stays that way
for the rest of the course.

---

## Step 2 — Who the migration connects as

Before the YAML, the identity, because it is the part that has been designed for two episodes and
takes one command.

Episode 18 step 8 ended with a constraint written down and unpaid:

> `ALTER DEFAULT PRIVILEGES` … applies only to objects created **by the role that runs this
> statement**. We are running it as the admin group, so it covers tables created by the admin group
> and nothing else. That quietly decides something about episode 19: **whatever runs the migrations
> must connect as a member of this group**, or the tables it creates will be invisible to the
> application and the failure will be a `permission denied` on a table that plainly exists.

So the pipeline's service principal joins the group:

```bash
# vars.AZURE_CLIENT_ID is the APPLICATION id. The group wants the service principal's OBJECT id,
# and they are different numbers for the same thing — which is the single most common way this
# command gets typed wrong.
SP_OBJECT_ID=$(az ad sp show --id "<AZURE_CLIENT_ID>" --query id -o tsv)

az ad group member add \
  --group "BrickShare Catalog DB Admins" \
  --member-id "$SP_OBJECT_ID"
```

```bash
az ad group member list --group "BrickShare Catalog DB Admins" --query "[].displayName" -o tsv
```

Two members: a human and a pipeline.

**This is the moment episode 18 step 1 predicted, and it is worth reading that prediction back:**

> in episode 19, when the pipeline needs to run migrations as a principal that can create tables,
> that is one `az ad group member add` and **no Terraform change at all** — which is the moment this
> choice pays for itself.

Count what did not happen. No Terraform edit, so no `plan`, no `apply`, no chance of drift. No second
Entra administrator on the server — which is what the alternative would have required, since a
Flexible Server has exactly one. **No new credential of any kind**: the pipeline already had an
identity, and this is a membership, not a secret. And nothing to rotate, because there is nothing
here that expires.

Compare it honestly with the version where the admin is a person. Adding the pipeline would mean
*replacing* the administrator — a Terraform change, applied by the human whose access it removes —
and the next local `terraform apply` proposing to swap it back. Episode 18 called that "the two take
it in turns to reassign it". The group made this a no-op, and no-ops are the whole reward for having
thought about it earlier.

**One thing to say out loud about what has just been granted.** The pipeline is now an administrator
of that database. It can create tables, drop them, and read every row. That is a real escalation and
it is the correct one — something has to be able to change the schema, and a pipeline is the most
auditable candidate available: every action it takes arrives as a commit, in a pull request, with a
diff. The alternative is a human with the same rights and no diff.

---

## Step 3 — The artifact that applies the migration

There are three defensible ways to apply a migration from CI. Put all three on screen, because
picking one without naming the others teaches a habit rather than a decision.

| | What it is | Where it belongs |
| --- | --- | --- |
| `dotnet ef database update` | The command everyone types locally, run on the runner | Fine, and it makes the deploy path depend on a source checkout, the SDK, and a design-time build — three things that can break for reasons unrelated to the change |
| `dotnet ef migrations script --idempotent` | Generates SQL; somebody applies it | **The right answer when a DBA gates schema change.** The artifact is reviewable by a person who does not have .NET installed |
| `dotnet ef migrations bundle` | Compiles the migrations into a standalone executable | **This one.** See below |

The bundle wins here for a reason this pipeline has already committed to. Episode 9 built the
container image once and promoted it, and the comment in `deploy.yml` still says so:

```yaml
  # Build the artifact once. Everything downstream refers to this tag, never rebuilds it.
```

A migration bundle is the same discipline applied to the schema change: **built once, in a job that
has the source, and executed in a job that does not.** The job that touches the production database
is handed an executable and a connection string, and it cannot accidentally deploy a different
commit's migrations because it has no way to build any.

Two more things it buys, both of which matter on a bad day:

- **It is a file you can run by hand.** Downloaded from a failed run, executed against a database
  from a laptop, with the same code that CI ran. A `dotnet ef` invocation is only reproducible if you
  can reconstruct the exact source tree and SDK that produced it.
- **The runner that touches the database does not need the SDK, the source, or the NuGet cache.** The
  smaller that job is, the fewer things can go wrong between "the migration is correct" and "the
  migration ran".

### Building it

In the existing `build-test` job, after the tests:

```yaml
      - name: Restore local tools
        run: dotnet tool restore

      # --self-contained so the artifact carries its own runtime: the job that runs it needs
      # nothing installed. -r linux-x64 because that is the runner it will execute on, and a
      # self-contained build has to be told what it is targeting.
      - name: Build migration bundle
        run: |
          dotnet ef migrations bundle \
            --project src/Catalog/BrickShare.Catalog.Api \
            --configuration Release \
            --self-contained -r linux-x64 \
            --output efbundle

      - uses: actions/upload-artifact@v4
        with:
          name: efbundle
          path: efbundle
```

**Why it is built in `build-test` and not in its own job.** Everything it needs — the checkout, the
SDK, a restore, a Release build — is already on that runner and paid for. Building it elsewhere
means paying all of it twice for a file that takes seconds to produce. Say the general rule: *a
build step goes where the build already is; a step that touches production goes on its own.*

`dotnet tool restore` is the manifest episode 16 committed doing its job — `dotnet-ef` at exactly
`10.0.11`, the same version on the runner as on every laptop, for the same reason `global.json` pins
the SDK.

### What a bundle actually contains

Worth thirty seconds, because step 5 is entirely about it. The bundle is the migrations, the
compiled model, the Npgsql provider, and **EF's ordinary design-time machinery** — the same code
that runs when you type `dotnet ef database update` on a laptop, compiled into an executable.

Which matters because of how that machinery finds a `DbContext`. It looks for an
`IDesignTimeDbContextFactory<TContext>` in the project first, and **falls back to starting the
application's own host** when there is none. There is none in this repository, so today the bundle
boots `Program.cs` and asks its service container — which is also why `dotnet ef database update`
needed a working connection string at design time back in episode 16.

Say that fallback out loud and leave it on screen, because the episode is about to walk into it.

---

## Step 4 — The migrate job

```yaml
  # Apply the schema change before the code that depends on it is released. This job is the only
  # thing in the repository that connects to the database as an administrator.
  migrate:
    needs: image
    runs-on: ubuntu-latest
    env:
      ARM_USE_OIDC: true
      ARM_CLIENT_ID: ${{ vars.AZURE_CLIENT_ID }}
      ARM_TENANT_ID: ${{ vars.AZURE_TENANT_ID }}
      ARM_SUBSCRIPTION_ID: ${{ vars.AZURE_SUBSCRIPTION_ID }}
    steps:
      - uses: actions/checkout@v4

      # DefaultAzureCredential inside the bundle needs a signed-in Azure context. This step is
      # what provides it, and it is the same OIDC login the image job already uses.
      - uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      - uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: 1.15.8

      # Reading two outputs, not applying anything. The state is the one place the server's
      # address is written down; retyping it here would be the literal episode 18 step 5 refused.
      - name: Read database address from Terraform state
        working-directory: infra
        run: |
          terraform init -input=false
          {
            echo "PG_FQDN=$(terraform output -raw postgres_fqdn)"
            echo "PG_DATABASE=$(terraform output -raw postgres_database)"
          } >> "$GITHUB_ENV"

      - uses: actions/download-artifact@v4
        with:
          name: efbundle

      - run: chmod +x efbundle

      - name: Apply migrations
        env:
          # The same configuration key App Service sets, and the same shape: no password, because
          # the bundle fetches a token exactly the way the application does.
          ConnectionStrings__Catalog: "Host=${{ env.PG_FQDN }};Port=5432;Database=${{ env.PG_DATABASE }};Username=${{ vars.POSTGRES_ADMIN_NAME }};SSL Mode=Require"
        run: ./efbundle
```

And `infra/main.tf` gains the two outputs it reads:

```hcl
output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.catalog.fqdn
}

output "postgres_database" {
  value = azurerm_postgresql_flexible_server_database.catalog.name
}
```

Four things in that job are decisions.

### `ConnectionStrings__Catalog`, not `--connection`

`efbundle` accepts a `--connection` flag, and it is not used here — for a reason that is about this
codebase rather than about taste.

`--connection` replaces the connection string held in the provider's options. Episode 18 stopped
putting one there: it builds an `NpgsqlDataSource` in code and hands *that* to `UseNpgsql`, because
the datasource is what carries the token callback. There is no connection string in the options for
the flag to replace, so the way in is configuration — the same `ConnectionStrings__Catalog` key App
Service sets, arriving as an environment variable exactly as it does in Azure.

**The general rule: configure the migration runner the way you configure the application, because
the decision it has to make — password present or absent — is read from the same value.**

### The username is the group, and the password is absent

`Username=${{ vars.POSTGRES_ADMIN_NAME }}` — `BrickShare Catalog DB Admins`, spaces, capitals and
all. Episode 18 warned twice that the group's display name *is* the Postgres role name; this is the
third place it has to match exactly, and the reason it comes from a repository variable rather than a
literal.

There is no `Password=`, which means the branch episode 18 wrote takes the Azure path: fetch a token,
hand it to Postgres as the password, let Entra decide. Exactly what the App Service does, with a
different principal.

That is the theory. Push it.

### Terraform is used to read, not to write

`terraform init` and two `terraform output -raw` calls. No plan, no apply, no lock taken.

The alternative was writing `psql-brickshare-catalog-dev.postgres.database.azure.com` into the
workflow file, and episode 18 step 5 already argued that one out at length: two literals that must
agree, with nothing linking them, is the same bug one careless edit away. The state file is where
that name is written down, so that is where the pipeline reads it from.

### `needs: image`

The migration runs after the image is built and before it is deployed. The image build is the last
cheap way to find out that this commit does not produce a deployable artifact, and there is no reason
to change a production schema for a build that was never going to ship.

**The alternative is defensible and is not taken.** `migrate` could run in parallel with `image`,
shaving a minute off every deployment, and it would be safe *given* the discipline step 6 is about to
impose — a backward-compatible migration applied to a database whose code never ships is harmless by
construction. It is not taken because a schema change that ships nothing is still a change somebody
has to reason about at 4pm on a Friday, and one minute is not worth it. Say the trade-off; do not
pretend the parallel version is wrong.

---

## Step 5 — The exception episode 18 wrote on purpose

The `migrate` job fails, and the message is one this repository has met before — because this
repository wrote it:

```
Unhandled exception. System.NotSupportedException: Open connections asynchronously: a blocking
Open() would hold a thread-pool thread for the length of a network call.
   at Program.<>c__DisplayClass0_0.<<Main>$>b__2(NpgsqlConnectionStringBuilder _)
   at Npgsql.NpgsqlDataSource.GetPassword(...)
```

**Stop and enjoy this for a second, because it is the best thing that happens in the episode.**
Episode 18 wrote a callback whose entire job was to throw if anybody ever opened a connection the
blocking way, and said why:

> Throwing turns a production performance mystery into an exception on the first line of the first
> caller that takes the wrong path.

That is precisely what has happened. The guard fired, in CI, before anything reached production, and
it named the problem in one line. **A tripwire that never fires is indistinguishable from a comment.
This one is now evidence.**

### Reading the failure properly

Now the diagnosis, and the important part is that the caller is not wrong.

**EF Core's migration surface is synchronous.** `IMigrator.Migrate()`, `DatabaseFacade.Migrate()`,
`dotnet ef database update`, and the `UpdateDatabase` operation compiled into the bundle — none of
them has an async path. It is not an oversight; migration tooling is a console program doing one
thing in order, and there is nothing else for the thread to be doing.

So Npgsql opens the connection with a blocking `Open()`, reaches for the synchronous password
provider, and finds an exception where a token should be.

**And notice what step 1 proves by contrast.** That step deployed `MigrateAsync()` — the *async*
one — and it got far enough to be refused by Postgres for lack of permission. The token was fetched
successfully; the application simply was not allowed to create a table. Two failures, two episodes
of design showing their work: **step 1 is the database refusing the application, and this is the
application refusing itself.**

**Say what this rules out**, because it is worth knowing that the choice of tool was not the
mistake: `dotnet ef database update` in the job would fail in exactly the same place, for exactly
the same reason. The bundle is not the problem. **The problem is that a web application and a
migration runner are being asked to share one composition root, and they have opposite threading
requirements.**

Put the two side by side:

| | The web application | The migration runner |
| --- | --- | --- |
| Opens connections | Asynchronously, thousands of times | Synchronously, twice |
| Blocking a thread costs | Throughput, under load, invisibly | Nothing. There is one thread and it is waiting anyway |
| Connects as | `app-brickshare-catalog-dev`, DML only | `BrickShare Catalog DB Admins`, full DDL |
| Lives for | Months | Four seconds |

Four rows, four differences. **That is not one component configured two ways. That is two
components** — and the fix is to stop pretending otherwise.

### The green — a composition root of its own

EF's context discovery, from step 3: it looks for an `IDesignTimeDbContextFactory<TContext>` first,
and only boots the application's host when it cannot find one. So give it one.

`src/Catalog/BrickShare.Catalog.Api/Persistence/CatalogDbContextFactory.cs`:

```csharp
using Azure.Core;
using Azure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

using Npgsql;

namespace BrickShare.Catalog.Api.Persistence;

/// <summary>
/// How `dotnet ef` and the migration bundle build a <see cref="CatalogDbContext"/>. Not used by the
/// running application, which composes its own in Program.cs.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(
            configuration.GetConnectionString("Catalog"));

        // Same rule as Program.cs: a password means Compose, no password means Azure.
        if (string.IsNullOrEmpty(dataSourceBuilder.ConnectionStringBuilder.Password))
        {
            var credential = new DefaultAzureCredential();
            var request = new TokenRequestContext(
                ["https://ossrdbms-aad.database.windows.net/.default"]);

            dataSourceBuilder.UsePasswordProvider(
                // EF's migration commands are synchronous, so this is the one that gets called.
                // Blocking here costs nothing: this process exists to run one migration and exit.
                // GetToken is a real synchronous method, not an async one wearing a disguise.
                passwordProvider: _ => credential.GetToken(request, default).Token,
                passwordProviderAsync: async (_, cancellationToken) =>
                    (await credential.GetTokenAsync(request, cancellationToken)).Token);
        }

        return new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>()
                .UseNpgsql(dataSourceBuilder.Build())
                .Options);
    }
}
```

### The `.csproj` line that has to change first

That file will not compile yet, and the error is a good one to hit on camera:

```
error CS0246: The type or namespace name 'IDesignTimeDbContextFactory<>' could not be found
```

The package is referenced. The type is in it. Look at how episode 16 referenced it:

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
```

**Read that `IncludeAssets` list and notice what is missing: `compile`.** That is not a typo in the
NuGet template — it is deliberate. The Design package exists to serve a command-line tool, and
excluding its compile assets means application code *cannot* reference design-time APIs by accident.
It is the same instinct as `PrivateAssets`, one level in: keep the tool's surface out of the
program's reach.

We now want exactly one file to reach it, so the list gains one word:

```xml
      <IncludeAssets>compile; runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
```

**Say what has just been traded away**, because "add the missing word until it compiles" is the habit
worth not teaching. The guard rail is gone for the whole project, not for one file, and nothing now
stops somebody referencing EF's design-time services from a request handler. `PrivateAssets` stays
`all`, so at least it does not spread to projects that reference this one. The honest summary is
that this is a real, small loss, accepted because the alternative — a separate project existing
solely to hold twenty lines — costs more than it saves at this size.

### Push it again

```
✓ migrate     Applying migration '20260821222411_InitialCatalog'.
              Done.
```

### What this actually bought, said carefully

Episode 18 admitted to a gap:

> It also means the interesting path is the one nothing can test — which is stated here rather than
> hidden.

That gap is **narrower now, and it is not closed**, and the difference is worth being precise about
rather than claiming a win.

What runs on every push to `main` is the *factory's* credential path: the same
`DefaultAzureCredential`, the same `ossrdbms-aad` scope, the same Entra round trip, against the same
server — exercised minutes after anyone edits it, in a job whose failure blocks the deployment. If
the scope string is wrong, if the principal loses its membership, if Entra changes its mind about
this application, **CI finds out before customers do.**

What still runs nowhere but production is the `NpgsqlDataSource` registration in `Program.cs`. It is
twenty lines away from the factory and it is not the same twenty lines. Two files that must agree,
with nothing linking them, is the shape episode 18 step 5 warned about — and it is being accepted
here on purpose, because the alternative is a shared helper with an `allowBlockingTokenFetch` flag,
which is a parameter whose only job is to say which process you are in. **Duplication that says
"these are two things" beats a flag that says "this is one thing, sometimes".** The scope string is
the one piece worth pulling into a shared constant, and that is left as the obvious refactor.

And one detail worth naming, since `DefaultAzureCredential` is a chain: which link fires depends on
what `azure/login` exported. With OIDC it may be the workload-identity or environment credential;
failing those it is `AzureCliCredential`, using the session that step established. The outcome is
the same token for the same service principal either way, and *that* is the argument for the chain
rather than naming one credential — **the code does not have to know which environment it woke up
in**, which is the whole reason the identical line works on a laptop, in CI and in App Service.

### One thing this quietly fixed

`dotnet ef` now finds the factory before it tries to boot the web host, locally as well as in CI.
Episode 16 documented the friction that used to cause — the tool builds the application's host, so
it reads `appsettings.Development.json`, so `ASPNETCORE_ENVIRONMENT` has to say `Development` or
nothing works. That is gone. The factory loads its own configuration explicitly, in four lines you
can read, instead of inheriting a host's opinion about environments.

**A good sign for a design, and worth saying:** the change was made to solve a threading problem and
it removed an unrelated piece of accidental complexity. That usually means the seam was in the right
place.

---

## Step 6 — Why the migration goes first, and the window that opens

The order is `migrate` then `deploy`, and this is where the episode stops being about YAML.

Between the two jobs there is a window — seconds here, minutes in a bigger system, and with
deployment slots or a rolling restart it can be much longer — in which **the new schema is live and
the old code is still serving traffic.** That window is not a flaw in this design; it is a property
of every deployment that is not a total outage. The alternative order does not remove it, it inverts
it: deploy first and the new code runs against the old schema, which is worse, because the new code
is the one that was written expecting a change.

So the rule the pipeline now enforces on every change this course makes from here on:

> **Every migration must be backward compatible with the code that is currently running.**

Not "with the previous release" as a matter of taste. With the code that is, at that instant, live.

**And name the thing that makes a pipeline step safe**, because it is not that migrations are
magic. It is this, from episode 9's `deploy.yml`:

```yaml
concurrency:
  group: deploy-main
  cancel-in-progress: false
```

Exactly one deployment runs at a time, so exactly one migration runs at a time. That is a property
the pipeline has and instance startup can never have — three instances starting have no group to
serialise on and no way to acquire one. **The fix for the race in step 1 was not moving the code
somewhere tidier. It was moving it somewhere that only one of it exists.**

---

## Step 7 — Expand and contract, demonstrated and then reverted

Backward compatibility is easy to agree with and easy to break, and the way it breaks is almost
always a rename. So break it on camera.

In `CopyConfiguration.cs`, change one line:

```csharp
        builder.Property(copy => copy.Label)
            .HasColumnName("label")          // was "label_code"
```

```bash
dotnet ef migrations add RenameLabelCode --project src/Catalog/BrickShare.Catalog.Api
```

The generated `Up`:

```csharp
migrationBuilder.RenameColumn(
    name: "label_code",
    table: "copies",
    newName: "label");
```

and the SQL behind it, worth showing because the C# is deceptively gentle:

```bash
dotnet ef migrations script --idempotent --project src/Catalog/BrickShare.Catalog.Api
```

```sql
ALTER TABLE copies RENAME COLUMN label_code TO label;
```

**Now walk the deployment with that in it.** The `migrate` job succeeds. The `deploy` job has not
started. The instance that is live was built from the previous commit, and every statement it issues
against `copies` names `label_code`. There is no `label_code`. Every read and every write fails with
`42703: column "label_code" does not exist` until the new image is running.

Say the size of it plainly: **one line, in a file that looked like configuration, produced an
outage** — and it produced one on a database with no rows in it, which is the only reason this is a
demonstration rather than an incident.

### The version that does not

Three deployments instead of one:

| Deployment | Migration | Code |
| --- | --- | --- |
| **1 — expand** | Add `label`, nullable. Nothing dropped | Writes both columns, reads `label_code` |
| **2 — migrate** | Backfill `label` from `label_code` | Reads `label`, still writes both |
| **3 — contract** | Drop `label_code` | Reads and writes `label` only |

Every arrow between those rows is a state in which the old code and the new schema coexist happily,
which is the entire requirement. The rule in one line, and it is the sentence to leave on screen:

> **Add before you remove. Never rename in one step.**

Two consequences worth naming while the table is up:

- **The new column must be nullable or have a default.** The old code's `INSERT`s do not mention it,
  and a `NOT NULL` column with no default turns every write from the live instance into a constraint
  violation. This is the same failure as the rename, wearing a hat.
- **Rollback stops needing `Down`.** Episode 16 said `Down` restores the schema and never the data —
  a dropped column comes back empty. Under expand-and-contract, rolling back deployment 2 means
  deploying the previous image and leaving the schema exactly where it is, because the old code still
  works against it. **The migration you never have to reverse is the one that only added things.**
  `Down` stays in the file, and it is the emergency exit rather than the plan.

### And now put it back

```bash
dotnet ef migrations remove --project src/Catalog/BrickShare.Catalog.Api
```

then revert the `HasColumnName` edit, and:

```bash
git status   # clean
```

`migrations remove` deletes the two files of the last migration **and reverts
`CatalogDbContextModelSnapshot.cs`** — episode 17 made the same point when it removed
`DropLabelUniqueness`, and the reason it keeps coming up is step 8.

Nothing about this rename ships. It existed for ninety seconds so that the danger is something you
have watched rather than something you have been told.

---

## Step 8 — Do we keep every migration file forever?

Short answer: **yes, all of them, indefinitely — and there is exactly one situation where you
deliberately stop, which is not routine tidying.** The long answer is worth having because "we have
four hundred files in there" is a real complaint that gets solved wrongly.

### What the files are

`dotnet ef migrations add` writes two files and edits a third:

| File | What it holds |
| --- | --- |
| `20260821222411_InitialCatalog.cs` | `Up` and `Down` — the change, as operations |
| `20260821222411_InitialCatalog.Designer.cs` | The **entire model as of that migration** |
| `CatalogDbContextModelSnapshot.cs` | The **entire model as of now** |

The second and third are the ones people are tempted to delete, because they are enormous,
auto-generated, and appear to be duplicates of each other. They are not duplicates. They are the same
kind of object at different points in time, and both are load-bearing.

### Why they are kept — four reasons, strongest first

**1. `migrations add` diffs against the snapshot, not against a database.**

This is the one that surprises people. `dotnet ef migrations add` never connects to anything —
episode 17 said so explicitly. It builds the model from your `DbContext`, compares it to
`CatalogDbContextModelSnapshot.cs`, and writes the difference. The snapshot *is* EF's memory of what
the schema should currently be.

Delete it, or hand-edit it, and the next migration is wrong in a specific and nasty way: EF diffs
against an empty or stale model and emits `CreateTable` for tables that already exist. Episode 17
already showed the mirror image of this — `migrations remove` reverts the snapshot, and forgetting
that it did is why a subsequent `add` produces an empty `Up`.

**2. A new database is built by replaying the chain from empty — and this repository does it on
every test run.**

Not hypothetically. `CatalogDatabase.InitializeAsync` calls `MigrateAsync` against a fresh
Testcontainers instance, so every `dotnet test` replays every migration this project has ever
committed, in order, from nothing. Delete the first one and no new database can be created anywhere:
not a test run, not a new environment, not a developer's laptop after `docker compose down -v`.

That is the concrete version of a general truth: **the migration chain is not a log of what
happened, it is the program that builds the schema.** A log can be pruned. A program cannot.

**3. `__EFMigrationsHistory` records ids, and EF applies whatever is in the assembly and not in that
table.**

Every environment has its own history table and its own position in the chain. Production is at
migration 40; the staging server somebody restored last week is at 12; a contributor's laptop is at
0. Deleting migrations 13 to 20 does nothing to production and permanently breaks the other two,
and the failure appears in whichever environment nobody was looking at.

**4. The `.Designer.cs` is what makes point-in-time operations possible.**

`Down` has to know what the model looked like *before* the migration. `dotnet ef migrations script 12
40` has to know what it looked like at 12 and at 40. Without the per-migration snapshot EF has one
model — today's — and no way to compute anything relative to a past state. This is also why
`migrations remove` can restore the previous snapshot: it reads the Designer file of the migration
before the one being removed.

**And the reason episode 16 gave, which still stands:** a migration is a file, in the repository, in
a pull request, reviewable by somebody who knows nothing about EF. That is the whole argument for
generated-and-committed over runtime reconciliation, and it only holds if the files stay.

### Two rules that follow

- **Never edit a migration that has been applied anywhere.** Some database has already run the old
  version and will never run the new one, so the file and the reality have silently diverged. A
  change means a *new* migration. (Editing one you generated ten seconds ago and have not pushed is
  fine — that is what `migrations remove` is for.)
- **Never delete one as tidying-up after a merge.** It is not clutter that survived review; it is a
  step in the program that builds the schema.

### The honest counterweight — when it genuinely becomes a problem

Presenting this as "keep everything forever, no exceptions" would be dogma, and the complaint behind
the question is real. After some years:

- The folder holds hundreds of files, and a from-empty replay takes minutes — which is a tax on every
  test run, in a suite episode 17 argued is only used in proportion to how fast it is.
- An old migration can stop compiling, usually because somebody wrote C# in it that referenced a type
  or a constant that has since been deleted.
- A migration can reference a table that a later migration dropped, so the chain works but reads like
  archaeology.

The answer is a **squash**, and it is a deliberate, scheduled operation rather than hygiene:

1. **Confirm every environment is at or beyond the cut point.** Every database — production,
   staging, the demo tenant, the machine of the person on sabbatical. This is the step that decides
   whether the whole thing is safe, and it is the step that gets skipped.
2. Delete the old migration files and the snapshot.
3. Regenerate one baseline migration from the current model.
4. **Insert the baseline's id into `__EFMigrationsHistory` on every existing database**, so none of
   them tries to replay a `CreateTable` for tables they already have. New databases run it normally.
5. Ship it on its own, with nothing else in the release.

The trigger is *the oldest live environment has passed the cut point* — not a file count, not
somebody's discomfort scrolling a folder. And note what makes the file count cheap in the meantime:
the `[**/Migrations/*.cs]` section episode 16 added to `.editorconfig` marks them generated, so they
carry no analyzer or style burden and cost nothing in review. **The folder is large and the
maintenance is not.**

This project has one migration. It will have several by episode 35 and none of this will be relevant.
It is worth knowing anyway, because the day it matters, the wrong version — delete the files, hope —
is the intuitive one.

---

## Step 9 — Prove the gate closes

The job order says migrations run before deployment. That is a claim about a failure nobody has
watched, and step 6 is the sort of argument that is comfortable enough to stop checking.

So break it. The quickest way is a wrong database name in the migrate job's connection string — one
character, no Entra changes to undo:

```yaml
          ConnectionStrings__Catalog: "Host=${{ env.PG_FQDN }};Port=5432;Database=nope;Username=${{ vars.POSTGRES_ADMIN_NAME }};SSL Mode=Require"
```

Push it and watch the run:

```
✓ build-test
✓ image
✗ migrate     3s   FATAL: database "nope" does not exist
- deploy      Skipped
```

Then, while it is red:

```bash
curl -i "$(cd infra && terraform output -raw web_app_url)/health/ready"   # still 200
```

**The previous revision is still serving, and that is the whole point.** A pipeline where a failed
migration deploys the new code anyway has a migration *step*; this one has a migration *gate*, and
the difference is one word in a `needs:` line and an outage.

Worth adding what a red migrate job means operationally, since it is the on-call answer: nothing has
been half-deployed. The schema is whatever it was, the code is whatever it was, and the system is in
a state it was already running in five minutes ago. **The safest failure is the one that leaves
everything exactly where it was**, and putting the risky step behind a gate rather than inside the
running application is what buys that.

Fix the typo, push, and watch the real thing:

```
✓ build-test
✓ image
✓ migrate     Applying migration '20260821222411_InitialCatalog'.
              Done.
✓ deploy      Deployed https://app-brickshare-catalog-dev.azurewebsites.net
```

---

## Step 10 — Look at what landed

Back in `psql`, as the admin group:

```
brickshare_catalog=> \dt
                 List of relations
 Schema |         Name          | Type  |          Owner
--------+-----------------------+-------+--------------------------
 public | __EFMigrationsHistory | table | BrickShare Catalog DB Admins
 public | copies                | table | BrickShare Catalog DB Admins
```

**Read the owner column**, because it is step 2 proving itself. The tables are owned by the group,
which is the role that ran `ALTER DEFAULT PRIVILEGES` in episode 18 — so the grants that statement
described actually attached:

```sql
select grantee, privilege_type
from information_schema.table_privileges
where table_name = 'copies' and grantee = 'app-brickshare-catalog-dev';
```

```
          grantee           | privilege_type
----------------------------+----------------
 app-brickshare-catalog-dev | SELECT
 app-brickshare-catalog-dev | INSERT
 app-brickshare-catalog-dev | UPDATE
 app-brickshare-catalog-dev | DELETE
```

Four rows, no `CREATE`, no ownership. **Say what an empty result would have meant**, because it is a
failure that would not surface until episode 21's first endpoint: the application gets
`permission denied for table copies` on a table it can plainly see. Two causes produce it, and this
query cannot tell them apart:

- **The migration ran as some other principal**, so the default privileges did not apply to the
  tables it created. Episode 18 predicted this one. The owner column above is what rules it out.
- **Episode 18 step 8's grants went to the wrong database** — its three `SCHEMA public` statements
  resolve against whatever database you were connected to, and all of them succeed either way. Step
  8's own verification is what catches this; if the result here is empty and the owner column looks
  right, that is where to go back to.

**This is the last cheap moment to find either.** Nothing is broken here, and an empty result is a
five-minute fix; the same empty result found from a `42501` in production is an afternoon.

And the history table:

```sql
select * from "__EFMigrationsHistory";
```

```
          migration_id           | product_version
---------------------------------+-----------------
 20260821222411_InitialCatalog   | 10.0.11
```

One row, which is how the next run knows there is nothing to do. Re-run the workflow from the Actions
tab and watch:

```
✓ migrate     No migrations were applied. The database is already up to date.
```

**Green, and it did nothing.** That is what makes this step safe to have in every deployment forever,
including the ninety per cent that change no schema at all: it is idempotent, so nobody has to decide
whether to run it, and *a step somebody has to decide whether to run is a step that eventually runs
at the wrong time.*

---

## Step 11 — Why there is no firewall rule in that job

Nothing was added to the Postgres firewall for the migrate job, and that deserves an explanation
rather than a silence, because it looks like something was forgotten.

**Why it works.** Episode 18 created this rule:

```hcl
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
```

That `0.0.0.0` pair is not an IP range — it is Azure's flag for *traffic originating inside Azure*.
GitHub-hosted runners are virtual machines in Azure, so the migrate job arrives on the allowed side
of that rule without anything being configured.

**Say plainly what that sentence rests on: where GitHub happens to host its runners.** Not a
guarantee this repository owns, not a contract anybody signed, and not something visible in the
Terraform. The pipeline is getting in through a rule that was written for the App Service.

Four consequences, and they are the honest cost of the convenience:

- **It is episode 18's over-broad rule seen from the other side.** That episode said this rule admits
  *every Azure tenant in the region* and called it a course cost decision. The pipeline gets in for
  free for precisely the reason a stranger's virtual machine does. **The convenience and the weakness
  are the same fact**, and noticing that is worth more than either half on its own.
- **A self-hosted runner breaks it immediately.** Move CI on-premises, to another cloud, or onto a
  corporate network — a normal thing for a company to do — and the migrate job starts timing out. The
  error is a connection timeout, which reads as an outage rather than as a policy, and it will be
  debugged as one.
- **Episode 35 breaks it on purpose.** Moving the database behind a private endpoint with public
  access disabled removes this route entirely, by design. The replacements are known and each is real
  work: a runner inside the virtual network, a Container Apps job that runs the same bundle from
  inside, or a firewall rule created and torn down around the migration.
- **And name what the just-in-time rule would have cost**, so its absence is a decision rather than
  laziness. It means an `az` call creating a resource Terraform does not own — invisible to `plan`,
  which is the tool episode 7 spent an episode arguing is how you know what exists — and a cleanup
  step that does not run when a job is cancelled, leaving exactly the permanent hole it was invented
  to avoid.

The rule to leave students with: **an access path you did not configure is an access path you are not
maintaining.** It works today, it is written down here as depending on something outside this
repository, and it has a scheduled expiry date.

---

## What this episode is not

**No zero-downtime tooling beyond expand-and-contract.** No online schema change tool, no shadow
tables, no dual-write framework. Postgres does most common DDL without a long lock, and the
disciplines in step 7 cover the rest for a service at this size. The trigger for needing more is a
table large enough that `ALTER` takes minutes, and this one has no rows.

**No rollback automation.** A red migrate job leaves everything untouched, which is step 9's whole
point, but there is no "roll the schema back" button and there deliberately is not one. Rolling
*forward* — with the expand-and-contract discipline making the previous code still valid — is the
practised operation. Deployment slots and a rehearsed rollback are episode 35.

**No seed data.** A migration that inserts rows is a real technique with a real cost, and the first
thing this service will want seeded is the grade multiplier table. That belongs with the episode that
introduces multipliers, where it can be argued about with something concrete on screen.

**No separate migration identity.** The pipeline's existing service principal joined an existing
group, and that is the whole identity story. It inherits the hazard episode 18 named: these roles are
bound to principals, and the *application's* one is system-assigned and dies with the web app.
Episode 35 fixes that with a user-assigned identity, alongside slots.

**No environments.** There is still one resource group called `dev` serving as production, so
"migrate staging first" is not a thing this pipeline can do. It is the obvious next question about
this job and it needs a second environment to be a real question.

**No `Program.cs` change** — and after step 5 that is a claim rather than an accident. The obvious
way out of that exception was to soften the guard where it was thrown; the file ends this episode
byte-for-byte as episode 18 left it, and the blocking token fetch lives in a class that only a
console process ever constructs. **The application still refuses to fetch a token on a blocking
open, which is what episode 18 wanted and what the migration runner is not.**

---

## Verification

| Check | Expected |
| --- | --- |
| `MigrateAsync` at startup, deployed (step 1) | `42501: permission denied for schema public` in the log stream |
| `dotnet ef migrations bundle` locally | An `efbundle` executable, ~90 MB self-contained |
| `migrate` job **before** the factory (step 5) | `NotSupportedException`, thrown by `Program.cs`'s synchronous password provider |
| `dotnet build` after the `IncludeAssets` edit | 0 warnings — the new file compiles under episode 10's `TreatWarningsAsErrors` |
| `dotnet ef migrations add Scratch`, then `remove` | Works without `ASPNETCORE_ENVIRONMENT` set, and `git status` is clean afterwards |
| `az ad group member list` | Two members: a human and the pipeline's service principal |
| `migrate` job, first real run | `Applying migration '20260821222411_InitialCatalog'.` |
| `psql` → `\dt` | `copies` and `__EFMigrationsHistory`, both owned by the admin group |
| `information_schema.table_privileges` | Four rows for `app-brickshare-catalog-dev`: SELECT, INSERT, UPDATE, DELETE |
| Re-run the workflow, no schema change | `No migrations were applied. The database is already up to date.` — green |
| Migrate job forced red (step 9) | `deploy` **Skipped**, `/health/ready` still 200 from the old revision |
| `dotnet ef migrations remove` after step 7 | `git status` clean, snapshot reverted |
| `terraform plan`, finally | `No changes.` — the two outputs added nothing to change |

And the local suite, which none of this touched:

```bash
dotnet build     # 0 warnings
dotnet test      # green — the same migration, replayed into a Testcontainers Postgres
```

The tests are unaffected for a reason worth one sentence: `CatalogDatabase` constructs its
`DbContext` directly, so it uses neither `Program.cs`'s datasource nor the new factory. Three
composition roots now exist in this repository — the application, the migration runner and the test
fixture — and each is short enough to read. **That is a defensible number when each one is
answering a different question, and it stops being defensible the moment they start disagreeing
about something that matters**, which is why the connection-string rule (*password means local, no
password means Azure*) is written the same way in all of them.

**That last line is worth pointing at one more time.** The migration that just ran against Azure is
the same file the test suite has been running against a throwaway container since episode 17. The
pipeline did not introduce a new code path to production; it gave an existing, tested one somewhere
to run. That is the difference between a deployment step you trust and one you supervise.

---

## Next

[Episode 20 — The set the copies are copies of](episode-20.md): there is now a database with a table
in it, an application with permission to read and write that table, and not a single endpoint that
does either.

Everything since episode 12 has been rules with no way in — pricing, `Money`, `LabelCode`, grades, the
copy state machine, a mapping and a schema. **Episodes 20 to 24 build the staff endpoint that
catalogues a set**, one idea at a time, and the first of them has no HTTP in it at all: `Copy` has
existed since episode 13 and the thing it is a copy of has never been modelled.

Then episode 21 opens the door, and 22, 23 and 24 close the three gates behind it — the edge
rejecting nonsense with a `400`, the domain refusing a legal-looking request with a `409`, and the
database refusing a set number somebody has already used. The middle one is where a perfectly
reasonable staff request returns **500**, because a refused business rule and a null-reference bug
look identical to an API layer, and where the domain exception finally earns its existence on camera
as a fix to something visibly wrong.

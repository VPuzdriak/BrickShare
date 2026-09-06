# Episode 18 — Terraform: Postgres and managed identity

← [Course plan](catalog-api.md) · Previous: [Episode 17 — Integration tests against a real database](episode-17.md)

There has been a `DbContext` deployed to Azure since episode 16, and it has never once opened a
connection. `/health/ready` has been returning 503 in production for two episodes, correctly, and
nothing has been acting on the answer. This episode gives it a database.

The part worth the episode is not the database. Creating a Postgres Flexible Server is one Terraform
resource, and every tutorial on the internet has it. It is the four lines that usually come next:

```
Host=…;Database=…;Username=brickshare_admin;Password=SomeThing!2024
```

That connection string is the default answer, it works, and it is a credential that gets copied into
a local `appsettings.Development.json`, pasted into a chat window when somebody cannot connect, read
by everyone who can open the portal, and rotated never. **This episode does not have one.** The
server is created with password authentication switched off entirely — not discouraged, not left
unused, _off_ — and the App Service connects as its own managed identity, the same move episode 8
made for the container registry, arriving somewhere with a great deal more to lose.

**Done when** `/health/ready` returns 200 from Azure for the first time in fifteen episodes, and
`az webapp config appsettings list` shows a database connection string with a `Username=` in it and
no `Password=`, because there never was one.

## Before recording

- Episode 17 merged: Testcontainers, the four persistence tests, `dotnet test` green.
- Terraform and the Azure CLI from episode 7, logged in, against the right subscription.
- **`psql` on the path.** Episodes 16 and 17 said it was useful but not required. This time it is
  required: step 8 is a thing Terraform cannot do, and `psql` is what does it.

  | Platform              | Install                                                                                    |
  | --------------------- | ------------------------------------------------------------------------------------------ |
  | macOS                 | `brew install libpq && brew link --force libpq`                                            |
  | Windows               | `winget install PostgreSQL.psqlODBC` — or the client tools from the EnterpriseDB installer |
  | Linux (Debian/Ubuntu) | `sudo apt install postgresql-client`                                                       |

  `docker compose exec postgres psql` also works and needs nothing installed, but it connects from
  inside the container, and step 8 needs to reach Azure. Either bring `psql` to the host, or use
  Azure Cloud Shell, which has it.

- A branch.
- `docs/architecture/catalog.md` open at **Infrastructure**, especially _Managed identity
  throughout, no secrets in configuration_ and _Network posture_.
- [`episode-8.md`](episode-8.md) open at **Step 3**. That episode made the managed identity argument
  on the smallest possible example — a container image nobody would care about stealing. This
  episode makes the same argument where it counts, and it is worth showing the two side by side.

**This episode is mostly not TDD, and says so.** A Terraform resource, a firewall rule, an
application setting and a data-source registration are exactly what `CLAUDE.md` exempts:
infrastructure, configuration, and wiring with no behaviour of its own. The one piece of new C# is a
callback that fetches a token, and it cannot run anywhere except inside Azure — there is no test
that can exercise it that is not just a re-transcription of it. The honest verification for this
episode is a deployed `/health/ready` returning 200, and step 9 makes it fail on purpose first,
which is the closest thing to a red this material has.

## The target shape

Still one file. `infra/main.tf` gains three resources, two firewall rules, one data source and three
variables. Two files that have never been touched since they were written get one line each:
`.gitignore` and `.github/workflows/deploy.yml`. And the API gains a package and about fifteen lines
of `Program.cs`.

Ten resources in one `main.tf` is the point at which the file starts being worth a second look.
It is not split here, and that is a decision rather than an oversight — episode 7 named the two
triggers, and the one that matters is _a second service wanting to reuse this shape_. There is still
only one service. When the rentals service arrives, the split is its own episode and it will have
something concrete to factor out.

---

## Step 0 — Three things the DbContext has never had

Before writing anything, put the current state on screen. Hit the deployed `/health/ready`:

```bash
curl -i "$(cd infra && terraform output -raw web_app_url)/health/ready"
```

`503 Service Unavailable`. It has been saying that since episode 16 and it has been right every
time.

Three things are missing, and they are worth naming out loud because only one of them is
interesting:

1. **A server.** There is no Postgres in Azure. This is a `terraform apply` and it is the least
   interesting part of the episode.
2. **An identity the server recognises.** The App Service has a managed identity — episode 8 gave it
   one, and Azure Container Registry accepts it. Postgres has never heard of it. **This is the
   episode.**
3. **A route between them.** A network path, and a firewall rule saying who may take it. This is one
   line of HCL and one decision that most tutorials make silently. Step 3 makes it out loud.

**This step is not code.**

---

## Step 1 — The Entra admin, and why it is a group

A Postgres Flexible Server with Entra authentication has an **Entra administrator**: a principal
that can create database roles for other principals. It is not optional — with password
authentication off, a server with no Entra admin is a server nobody can log into at all.

The question is _which_ principal, and the obvious answer is wrong.

**The obvious answer:** whoever runs `terraform apply`. Terraform can read that with
`data "azurerm_client_config" "current"`, and it takes one line.

**Why it breaks:** two different principals run `terraform apply` in this repository. A human does,
from a laptop, in this episode. The pipeline does, as its OIDC service principal, on every push to
`main` since episode 9. Bind the admin to "whoever is applying" and the two take it in turns to
reassign it, every apply shows a change, and `plan` stops meaning what episode 7 spent an episode
arguing it means.

**The answer: an Entra group**, containing whoever needs it. The group's object id never changes, so
Terraform sees no drift no matter who applies. And in episode 19, when the pipeline needs to run
migrations as a principal that can create tables, that is one `az ad group member add` and **no
Terraform change at all** — which is the moment this choice pays for itself.

Create it on camera:

```bash
az ad group create \
  --display-name "BrickShare Catalog DB Admins" \
  --mail-nickname brickshare-catalog-db-admins

# Add yourself.
az ad group member add \
  --group "BrickShare Catalog DB Admins" \
  --member-id "$(az ad signed-in-user show --query id -o tsv)"

# The object id. Keep it; it goes in terraform.tfvars in step 4.
az ad group show --group "BrickShare Catalog DB Admins" --query id -o tsv
```

**One thing to say while that runs:** the display name matters here in a way display names usually
do not. When a group member connects to Postgres, **the username is the group's display name**,
spaces and capitals included. Rename the group in Entra later and the database role does not follow —
the docs say so plainly, and it is the kind of thing that produces a login failure nobody can
explain six months from now. Pick the name once.

Creating groups needs directory permissions that a personal subscription has and a locked-down
corporate tenant may not. If `az ad group create` is refused, the fallback is to make the admin your
own user account — everything else in the episode works unchanged — and to note that the episode 19
handoff then needs a second admin instead of a group membership.

---

## Step 2 — The server, with password authentication switched off

Add to `infra/main.tf`:

```hcl
resource "azurerm_postgresql_flexible_server" "catalog" {
  name                = "psql-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  version               = "18"
  sku_name              = "B_Standard_B1ms"
  storage_mb            = 32768
  backup_retention_days = 7
  zone                  = "1"

  # No administrator_login, no administrator_password. Not omitted for tidiness — the provider
  # rejects them when password authentication is disabled, which is the API telling you that
  # "Entra only" and "there is an admin password lying around" are not compatible states.
  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = false
  }
}

resource "azurerm_postgresql_flexible_server_active_directory_administrator" "catalog" {
  server_name         = azurerm_postgresql_flexible_server.catalog.name
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  object_id           = var.postgres_admin_object_id
  principal_name      = var.postgres_admin_name
  principal_type      = "Group"
}

resource "azurerm_postgresql_flexible_server_database" "catalog" {
  name      = "brickshare_catalog"
  server_id = azurerm_postgresql_flexible_server.catalog.id
  collation = "en_US.utf8"
  charset   = "utf8"
}
```

And the data source, next to the provider block at the top of the file:

```hcl
data "azurerm_client_config" "current" {}
```

### The block the episode is about

```hcl
  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = false
  }
```

**Read both lines out.** The first one is the one every tutorial has, usually alongside a password:
Entra authentication is _enabled_, meaning tokens are accepted _as well_. The second is the one that
matters. `password_auth_enabled = false` means there is no administrator password, no local role
with a password, and no way to authenticate to this server except with a token issued by Entra ID
for a principal this server has been told about.

The difference between those two configurations is the difference between "we support the secure
option" and "the insecure option does not exist". A password that exists and is unused is still a
password: it is in the state file, it is in whoever created it's password manager, it is in the
portal, and it works from anywhere that can reach port 5432. **The only credential that cannot leak
is the one that was never issued** — which is, word for word, the argument episode 8 made about
`admin_enabled = false` on the container registry, now applied to something that will hold customer
data.

### The rest of the block, briefly

**`sku_name = "B_Standard_B1ms"`** — burstable, one vCore, 2 GiB of memory. It is the cheapest thing
that is still a genuine Postgres Flexible Server, and it is chosen for that reason and said out
loud. A burstable tier accumulates CPU credits while idle and spends them under load, which is a
terrible fit for steady traffic and a perfect fit for a database that is idle except when somebody
is recording. Production sizing is a conversation about the workload, and this service does not have
one yet.

**`storage_mb = 32768`** — 32 GiB, the smallest offered. Worth one sentence because of a genuine
trap: **storage on Flexible Server can be grown and cannot be shrunk.** Starting small is free;
starting large is a decision you cannot take back.

**`version = "18"` — the same major version as `postgres:18` in Compose, and that is the whole
reason it says 18.** Not a coincidence and not a default: the container a developer runs and the
managed server that serves customers are deliberately the same engine.

**Say the general rule rather than the version number**, because the number is the part that
expires: pin both, check they agree, and when a managed service genuinely cannot offer the version
you run locally, know which direction the gap runs. A feature that exists locally and not in Azure is
a test that passes and a deploy that fails — exactly the failure mode episode 17 spent a whole
episode arguing against.

**And say that keeping them matched costs vigilance rather than nothing**, which is what makes it a
rule instead of a happy accident. Azure pins a minor version you do not choose. The `postgres:18` tag
in Compose floats to whatever 18.x it points at today. Postgres 19 will land in one place before the
other, and it will not be Azure first. **Matching majors is a standing commitment**, and the day it
cannot be kept is a decision somebody makes with the consequence in front of them, not a drift
nobody noticed.

**`zone = "1"`** — pinned so that a later apply does not propose moving the server between
availability zones. Azure picks one if you do not, records it in state, and then a plan can show a
change nobody asked for.

**`backup_retention_days = 7`** — the minimum, and the point is that it is not zero and cannot be.
Automatic backups are on and there is no way to turn them off, which is a good default doing its
job.

**The database is a separate resource**, and the name is the one Compose has been using since
episode 16 — `brickshare_catalog`. The collation is spelled `en_US.utf8` here and Postgres reports
it as `en_US.UTF-8`; that is Azure's spelling of the same thing and not worth chasing.

**This takes five to ten minutes to create.** Say so before starting the apply so nobody thinks it
has hung, and mention `az postgres flexible-server stop` — a stopped server bills for storage only,
restarts in a couple of minutes, and is how a student following along on a personal subscription
avoids paying for a database between recording sessions. It auto-starts after seven days whether you
want it to or not.

---

## Step 3 — The network decision, made out loud

This step creates two lines of HCL and spends five minutes on them, because it is the decision in
this episode a student is most likely to copy without noticing they made it.

### The two options, in plain terms

**Public access with firewall rules.** The server gets a public DNS name and a listener on the
public internet. A list of allowed source IP ranges decides who gets as far as the TLS handshake.
The database is on the internet, with a bouncer on the door.

**Private endpoint, public access disabled.** The server gets a private IP inside a virtual network
and stops answering on the internet entirely. The only things that can reach it are things on that
network — which, for us, would mean the App Service reaching it through regional VNet integration.
There is no bouncer, because there is no door.

The difference is not "one is more secure". It is _what an attacker has to already have_. Against
the first, they need a network position anyone on the internet has, plus a credential. Against the
second, they need a position inside your virtual network before the conversation starts.

### What we build, and the honest reason

```hcl
# "Allow Azure services" — the 0.0.0.0/0.0.0.0 pair is not an IP range. It is Azure's flag for
# "traffic originating inside Azure". This is how App Service reaches the server; see below for
# what it actually admits.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.catalog.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# The machine doing the bootstrap in step 8. Optional: with no developer_ip set, this resource
# does not exist, which is what happens when the pipeline applies.
resource "azurerm_postgresql_flexible_server_firewall_rule" "developer" {
  count = var.developer_ip == null ? 0 : 1

  name             = "developer"
  server_id        = azurerm_postgresql_flexible_server.catalog.id
  start_ip_address = var.developer_ip
  end_ip_address   = var.developer_ip
}
```

**Say what `AllowAzureServices` actually does, because the name is flattering.** It does not mean
"our Azure resources". It means _any traffic originating from inside Azure_ — including a virtual
machine in a completely unrelated subscription belonging to somebody you have never met. Every Azure
customer in the world is on the allowed side of this rule. The architecture document says exactly
this, in these words: _"allow access from Azure services" is a rule that admits every Azure tenant in
the region_.

Two things make it survivable here, and both are conditions rather than reassurances:

- **Password authentication is off.** Getting through the firewall buys a TLS handshake and a login
  prompt that only accepts an Entra token for a principal that exists as a role in _our_ database.
  There is no password to guess because there is no password.
- **There is nothing in the database.** Episode 19 creates the first table. Right now the worst
  outcome of the firewall being wide is that somebody reaches an empty server they cannot log into.

**And then the part that matters most: this is a cost decision for a course, not a recommendation.**
It is the wrong choice for production and it is being made deliberately, for a reason that has
nothing to do with security. The private version needs a virtual network, a subnet delegated to the
database, a second subnet for App Service regional VNet integration, a private DNS zone,
a zone-to-VNet link, and — depending on which App Service scale unit the plan landed on — possibly a
tier upgrade from B1, because VNet integration on Basic is not guaranteed everywhere. That is a real
monthly bill, charged to a student following along on a personal subscription, to protect a database
with no rows in it.

**So the switch is scheduled rather than skipped.** At the end of the course, once the service is
finished and there is genuinely something worth protecting, the network posture becomes private
endpoints with public access disabled — and that is an episode of its own, not a footnote, because
private DNS resolution failures are worth watching somebody hit and fix rather than reading about.

The rule to leave students with, said in one sentence: **public plus firewall is a reasonable way to
develop and a bad way to ship, and the thing that decides which one you are doing is whether the
database has anything in it yet.**

---

## Step 4 — Three variables, and where their values live

`infra/main.tf` gains three variables. Episode 8 deleted this file's last variable and episode 9
added `image_tag` back when the pipeline genuinely varied it; the same test applies here. Each of
these three is a value that is _personal to whoever is following along_ and cannot be a literal.

```hcl
variable "postgres_admin_object_id" {
  description = "Object id of the Entra group that administers the Postgres server."
  type        = string
}

variable "postgres_admin_name" {
  description = "Display name of that group. Postgres uses it as the login role name, so it must match Entra exactly."
  type        = string
}

variable "developer_ip" {
  description = "Public IP allowed through the Postgres firewall for the step 8 bootstrap. Null in CI."
  type        = string
  default     = null
}
```

`infra/terraform.tfvars`, created locally and **not committed**:

```hcl
postgres_admin_object_id = "00000000-0000-0000-0000-000000000000"
postgres_admin_name      = "BrickShare Catalog DB Admins"
developer_ip             = "203.0.113.24"
```

```bash
curl -s https://api.ipify.org   # the value for developer_ip
```

And `.gitignore` gains the two lines its Terraform section has been missing since episode 7:

```gitignore
*.tfvars
*.tfvars.json
```

**Say why, since none of these three values is a secret.** An object id is not a credential, a group
name is not a credential, and an IP address is barely private. The reason `*.tfvars` is ignored is
that it is the file everyone's habits eventually put a secret into — it is the conventional home for
per-environment values, so it is the conventional place a password ends up. Ignoring it now, while
it holds nothing interesting, means the habit is already right by the time it holds something.

### The pipeline needs these too

`.github/workflows/deploy.yml`, in the `deploy` job's `env` block:

```yaml
env:
  ARM_USE_OIDC: true
  ARM_CLIENT_ID: ${{ vars.AZURE_CLIENT_ID }}
  ARM_TENANT_ID: ${{ vars.AZURE_TENANT_ID }}
  ARM_SUBSCRIPTION_ID: ${{ vars.AZURE_SUBSCRIPTION_ID }}
  # Terraform reads any TF_VAR_<name> environment variable as an input variable. Cleaner
  # than a growing string of -var flags, and it keeps the apply step a single line.
  TF_VAR_postgres_admin_object_id: ${{ vars.POSTGRES_ADMIN_OBJECT_ID }}
  TF_VAR_postgres_admin_name: ${{ vars.POSTGRES_ADMIN_NAME }}
```

with the two matching **repository variables** added in
_Settings → Secrets and variables → Actions → Variables_, alongside the three episode 9 created.
Variables, not secrets: nothing here is confidential, and putting a non-secret in a secret makes the
logs less readable for no gain.

**`developer_ip` is deliberately absent from the pipeline**, which is the whole reason it has
`default = null` and the firewall rule has a `count`. The pipeline sets no value, the rule does not
exist, and the next local apply from the same machine puts it back. Say what that means honestly:
your developer rule gets torn down by the next deployment. That is a mild annoyance and it is the
correct behaviour — the alternative is a permanent hole in a firewall for a laptop that moves
between networks.

### Apply

```bash
cd infra
terraform init      # the provider already knows these resources; init is for the lock file
terraform plan
```

`5 to add` — the server, the Entra administrator, the database and both firewall rules. Nothing
changes on the web app yet; that is step 5.

```bash
terraform apply
```

Then wait. This is the five-to-ten-minute one.

---

## Step 5 — A connection string with a hole where the password goes

The App Service needs to be told where the database is, and the username in that connection string
has to be the web app's own name — because that is what its system-assigned identity is called in
Entra.

**Write the obvious version first, and let it fail.** Every other value in this file is a reference
rather than a retyped literal, so the natural thing to write is:

```hcl
    # Wrong, and wrong for an interesting reason.
    ConnectionStrings__Catalog = "…;Username=${azurerm_linux_web_app.catalog.name};…"
```

inside `azurerm_linux_web_app.catalog` itself. Terraform refuses:

```
Error: Self-referential block

  on main.tf line 132, in resource "azurerm_linux_web_app" "catalog":

Configuration for azurerm_linux_web_app.catalog may not refer to itself.
```

**Sit on why**, because the instinct that produced it was a good one. `.name` is an *attribute of the
resource*, and an argument of a resource cannot be resolved from an attribute of that same resource —
that is a cycle in the dependency graph. Terraform does not special-case the fact that this
particular attribute is a constant written fifteen lines higher up; the rule is about the shape of
the graph, not about whether the value could in principle be known.

**Two ways to fix it, and only one of them is a fix.** Retyping `"app-brickshare-catalog-dev"` in the
connection string works and leaves two literals that must agree with nothing linking them — the same
bug, one careless edit away. So the file gets its first `locals` block, near the top, under the
variables:

```hcl
locals {
  # The web app's name is also the name of its system-assigned identity in Entra, which is also
  # the Postgres role name created in step 8. One value, three meanings, written once.
  catalog_app_name = "app-brickshare-catalog-dev"
}
```

The web app's own `name` becomes `local.catalog_app_name`, and so does the username:

```hcl
resource "azurerm_linux_web_app" "catalog" {
  name = local.catalog_app_name
  # …

  app_settings = {
    WEBSITES_PORT          = "8080"
    ASPNETCORE_ENVIRONMENT = "Production"

    # No Password=, because there is no password.
    ConnectionStrings__Catalog = "Host=${azurerm_postgresql_flexible_server.catalog.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.catalog.name};Username=${local.catalog_app_name};SSL Mode=Require"
  }
}
```

**The general rule worth keeping:** when two arguments must agree and one of them belongs to the
resource itself, the shared value goes *outside* both. That is what `locals` is for, and it is why
this file gets its first one here — at the moment something forced it — rather than as boilerplate in
episode 7.

**And one thing about the toolchain, which is the more valuable half of this.** Run
`terraform validate` against the broken version and it prints `Success! The configuration is valid.`
The self-reference is caught by `plan`, not by `validate` — after several resources have already been
planned. Since episode 7 this course has recommended `fmt -check` and `validate` as the offline gate,
and here is a real error that walks straight through both. **A check is only as good as what it is
capable of failing on**, and knowing which of your gates can see which class of mistake is part of
knowing your tools — the same shape of lesson episode 17 made about fake databases.

Three more things to stop on.

**`ConnectionStrings__Catalog`, with a double underscore.** That is the .NET configuration provider's
encoding of `ConnectionStrings:Catalog` in an environment variable, and it is why
`builder.Configuration.GetConnectionString("Catalog")` in `Program.cs` — unchanged since episode 16 —
finds it without any Azure-specific code. Compose has been doing exactly the same thing since episode 16. The application does not know it is in Azure.

**`Username=` is the name of the web app**, which is why that value earned a local of its own. This
is the part that reads wrong the first time. A
system-assigned managed identity is named after the resource that owns it, so
`app-brickshare-catalog-dev` is simultaneously a web app, a service principal in Entra, and — after
step 8 — a role in Postgres. The username here is not a claim about who you are; the token proves
that. It is a statement about _which role to become_ once the token has been believed.

**`SSL Mode=Require`.** Azure will not accept an unencrypted connection, and Npgsql's default is
weaker than "require", so this is not decoration. `VerifyFull` is the stronger setting — it checks
that the certificate actually belongs to the host you asked for, which is what stops an attacker who
can redirect your DNS. It is the right production value; it is not set here because the certificate
chain is one more thing to debug on camera, and pretending that is a principled choice would be
dishonest. Name it as a to-do rather than as a decision.

```bash
terraform plan     # 1 to change
terraform apply
```

---

## Step 6 — Where the password comes from instead

Now the application. Two changes, both small.

`Directory.Packages.props`:

```xml
    <PackageVersion Include="Azure.Identity" Version="1.21.0"/>
```

`src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj`:

```xml
    <PackageReference Include="Azure.Identity" />
```

The version-free `PackageReference` is central package management from episode 10 doing its job, and
worth pointing at for one second: the version lives in exactly one file, and a second project taking
this dependency cannot pick a different one.

`Program.cs` — the `AddDbContext` registration from episode 16 is replaced by two:

```csharp
using Azure.Core;
using Azure.Identity;

using BrickShare.Catalog.Api.Persistence;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(
        builder.Configuration.GetConnectionString("Catalog"));

    // A connection string that carries a password is Compose or Testcontainers, and it is left
    // exactly as it is. One with no password is Azure, where the password is a token that has to
    // be fetched and expires.
    if (string.IsNullOrEmpty(dataSourceBuilder.ConnectionStringBuilder.Password))
    {
        var credential = new DefaultAzureCredential();

        dataSourceBuilder.UsePasswordProvider(
            passwordProvider: _ => throw new NotSupportedException(
                "Open connections asynchronously: fetching a token from a blocking Open() deadlocks."),
            passwordProviderAsync: async (_, cancellationToken) =>
            {
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]),
                    cancellationToken);

                return token.Token;
            });
    }

    return dataSourceBuilder.Build();
});

builder.Services.AddDbContext<CatalogDbContext>((serviceProvider, options) =>
    options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
```

The health check registration below it does not change.

### The five things worth saying here

**The token is the password.** Not a header, not a separate authentication mode — Postgres is
handed a base64 blob in the password field of a perfectly ordinary libpq login, and the server
validates it with Entra instead of comparing a hash. That is why this works with `psql`, with any
libpq client, and with a driver that has never heard of Azure. It is a genuinely elegant piece of
protocol reuse and it is worth ten seconds of admiration.

**The scope is a URL and it is not the server's URL.** `https://ossrdbms-aad.database.windows.net/.default`
is the audience for _the Azure Database for open-source relational databases service_, the same for
every Postgres and MySQL Flexible Server in the world. A token issued for a different audience is
refused, which is the point — a token stolen from this application cannot be spent anywhere else.

**`DefaultAzureCredential` is the reason there is no `#if AZURE` in this file.** In App Service it
reads the instance metadata endpoint and gets a token for the system-assigned identity. On a laptop
it would use your `az login` session. In neither case does the application hold a credential; it
holds the ability to ask for one, which the platform grants because of _where the code is running_.
No `AZURE_CLIENT_ID` setting is needed here, because there is exactly one identity attached to this
app and nothing to disambiguate.

**The `if` is what keeps episodes 16 and 17 working, and it is deliberate rather than a shortcut.**
Compose supplies a password. Testcontainers supplies a password. Both take the branch that does
nothing, so this code is entirely inert locally, and `dotnet test` behaves exactly as it did at the
end of episode 17. It also means the interesting path is the one nothing can test — which is stated
here rather than hidden, and is why step 7 deploys it and reads the log.

**The synchronous provider throws on purpose.** Fetching a token is a network call. Doing it from
inside a blocking `Open()` is how thread-pool starvation happens under load — the failure appears as
timeouts everywhere, at the worst moment, and points at nothing. Throwing turns a production
performance mystery into an exception on the first line of the first test that takes the wrong path.

---

## An aside — why local development keeps its own database

That `if` deserves a harder question than it usually gets, and this is the moment somebody watching
asks it. There is now a real, managed, Entra-authenticated Postgres sitting in Azure. The same code
can reach it. Compose is still starting a container. **Why keep the container at all?**

This is not a step — nothing is typed here — and it is worth three minutes anyway, because the answer
is not "because containers" and the question is a good one.

### The case for pointing local development at the Azure server

Take it seriously. One of these is genuinely strong, and it is worth saying which, because a list
where everything is presented as decisive is a list nobody believes.

1. **The versions cannot drift apart.** They are the same today — step 2 made Azure `18` to match
   `postgres:18` in Compose deliberately. But that match is maintained, not guaranteed: an image tag
   floats, Azure pins a minor nobody chose, Postgres 19 arrives in one place first, and a server
   restored from a backup can come back somewhere else entirely. Developing against the one server
   makes divergence **structurally impossible** rather than merely currently absent. That is a real
   advantage, and it is a smaller one than it would have been if the versions already disagreed.
2. **The token path stops being untested**, and this is the strong one, by some distance,
   because the previous section just handed it over. `DefaultAzureCredential` works perfectly well on
   a laptop: with no managed identity available it falls back to your `az login` session. Point a
   local run at the Azure server and the branch that is currently inert runs on every `dotnet run` —
   same callback, same scope, same Entra round trip, same failure modes, discovered at a keyboard
   instead of in a log stream.
3. **No Docker on the machine.** Worth more than it sounds where Docker Desktop licensing is a
   procurement conversation rather than a download.
4. **Real managed behaviour** — actual backups, actual sizing, actual server parameters — instead of a
   container running whatever the image's defaults are.

That is a real list. A team that developed this way would not be doing anything stupid.

### Why this course does not

Strongest first.

**Shared mutable state, and it is disqualifying on its own.** One database, several developers. Now
re-read what episode 17 built: an isolation strategy whose central move is **resetting the database
before every test**. Against a shared server that is not isolation — it is deleting a colleague's data
in the middle of their debugging session, from a test they did not run. And Testcontainers does not
merely write rows; it creates and drops whole databases per run. Against a shared server that is
either refused by permissions or successful, and the second is worse.

**Cost — but not the cost it looks like.** Say the objection before somebody else does: the Azure
server is paid for either way. It is the deployed application's database, it exists because episode
18 created it, and it accrues charges whether or not a single laptop ever connects to it. Running
Postgres in Docker locally saves **nothing** on that bill, and anyone claiming otherwise is not
counting.

The cost appears one step later — when you fix the problem in the bullet above. Shared state is
disqualifying, so a team that seriously wanted to develop against Azure would not share one server;
they would give each developer their own, and each CI run its own. That is the version of this idea
that actually works, and it is the version whose bill has to be counted: **N developers plus
concurrent CI runs is N-plus Flexible Servers**, each billed by the hour, each idle overnight, at
weekends, and for the fortnight somebody is on holiday.

And nobody dodges that by making them ephemeral, because **a Flexible Server takes five to ten
minutes to create** — step 2 said so, with the warning not to think it has hung. Per-test-run
provisioning is not on the table at that speed, so the servers get created once and left running,
which is exactly how an idle bill happens. Testcontainers answers the same requirement with a
container that starts in seconds and is destroyed with the test run.

One small consequence worth naming, because it resolves something step 2 already said:
`az postgres flexible-server stop` is the mitigation this episode recommended for a server nobody is
using. It stops being available the moment somebody's daily work depends on that server being up.

**The test suite would need an internet connection, an Azure login and a quota.** Episode 17 closed
on the claim that `dotnet test` starts a Postgres, uses it, and kills it with the run. This turns that
into "`dotnet test` requires `az login`". CI runs serialise on one server; two branches running tests
at the same time corrupt each other's fixtures; a red build can now mean *somebody else was testing*.
**A suite that can fail for reasons outside the repository is a suite people stop believing**, and a
suite people stop believing is worse than no suite, for exactly the reason episode 17 gave about fake
databases: it is confidently wrong.

**Latency turns a fast suite into one nobody runs.** Every query becomes a round trip to a region.
Episode 17's tests run in seconds against a container on the same machine; against a server two
hundred milliseconds away, the same tests take minutes. Test suites are used in proportion to how
fast they are, and this is the change that quietly ends test-first development on a project.

**No offline work.** Small and real. Named as the small one.

**And least privilege inverts.** Local development wants `CREATE` and `DROP` — trying a migration is
the whole point. Step 8 deliberately gave the application role neither, for reasons episode 19 is
entirely about. Developing against the shared server means either granting schema rights broadly on a
server whose application role is carefully denied them, or developing as the admin — and developing
as the admin is precisely how a half-finished migration gets applied for everybody at 4pm on a
Thursday.

### The line worth writing down

> **Fidelity is a property of the engine, not of the instance.**

Episode 17 spent an episode arguing that a fake database is worse than no database: SQLite and the
in-memory provider fail because they are *not Postgres*, and everything argued in episode 16 is
invisible to them. That argument demands a **real Postgres**. It does not demand **this** Postgres.

Everything the fidelity argument wants is bought by running the same engine locally. Everything that
goes wrong on the list above comes from sharing a single instance. Keeping those two ideas apart is
what stops a student drawing the reasonable-sounding and wrong conclusion that episode 17's argument
implies developing against the deployed database — or worse, against production.

### And the door is left open, deliberately

The gap is real: the token path is not exercised by anything local, and this episode does not pretend
otherwise. Three honest answers, in order of how much work they are:

- **Pin both to the same major, and treat any divergence as a deliberate change.** Already the advice
  in step 2, and it is the whole of what the fidelity argument actually needs.
- **The deployment is the test.** This is what a pipeline that has worked since episode 9 buys: the
  Azure-only branch runs on every push, minutes after it is written, against the real thing. It is why
  step 7 deploys it and reads the failure out of the log stream rather than asserting success — the
  verification happens on camera, it just does not happen on a laptop.
- **And when you genuinely need to debug the auth path itself, point at Azure on purpose**, for one
  session:

  ```bash
  az login
  export ConnectionStrings__Catalog="Host=psql-brickshare-catalog-dev.postgres.database.azure.com;Port=5432;Database=brickshare_catalog;Username=BrickShare Catalog DB Admins;SSL Mode=Require"
  dotnet run --project src/Catalog/BrickShare.Catalog.Api
  ```

  No password, so the `if` takes the Azure branch, and `DefaultAzureCredential` uses your CLI session.
  You need to be in the admin group and your IP needs the firewall rule from step 4 — both of which
  you already have.

  **Say clearly what that is and is not.** It is a debugging tool, reached for when the thing being
  debugged is the authentication itself. It is not the inner loop, it is not what `dotnet test` does,
  and it is not something to leave set in a shell profile. The default stays a container on the
  machine, and the default is the thing that decides how a team actually works.

---

## Step 7 — Deploy it, and watch it fail

Commit, push, and let episode 9's pipeline do its job. Then:

```bash
az webapp log tail --name app-brickshare-catalog-dev --resource-group rg-brickshare-dev
```

`/health/ready` is still 503, and now the log says why — something close to:

```
Npgsql.PostgresException (0x80004005): 28P01: password authentication failed for user "app-brickshare-catalog-dev"
```

**Read that error and then say what it actually means, because the message is lying to you.** There
is no password. There is no password authentication. What Postgres is reporting is that it was
handed a valid Entra token for a principal it has never been told about, and the closest thing it has
to a code for that is the code for a bad password.

That is the whole of step 8 in one line: **the server accepts tokens, but it does not accept
strangers.** Azure created the server, Terraform granted nothing, and — exactly like episode 8's
`AcrPull` — living in the same subscription grants precisely zero. This is the third time this
pattern has appeared in the course, after the storage account in episode 7 and the registry in
episode 8, and it is worth naming as a pattern rather than a quirk: **control plane access is not
data plane access, and nothing bridges them for you.**

---

## Step 8 — The bootstrap Terraform cannot do

Here is the honest seam of this episode, and it should not be papered over: **`azurerm` has no
resource for a role inside a database.** It manages the server, the firewall and the Entra
administrator. What lives _in_ Postgres — roles, grants, schemas — is Postgres's business, reachable
only through a Postgres connection. So this step is `psql`, once, by hand, on camera.

(There is a `cyrilgdn/postgresql` Terraform provider that can manage roles and grants. It is real
and it works. It is not introduced here because it needs its own provider block, its own
authentication story and its own state, and it would land in an episode already carrying a database,
an identity model and a network decision. Name it as the thing that exists; do not use it today.)

### Connect as the admin group

```bash
export PGPASSWORD=$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)

psql "host=psql-brickshare-catalog-dev.postgres.database.azure.com \
      port=5432 \
      user=BrickShare\ Catalog\ DB\ Admins \
      dbname=postgres \
      sslmode=require"
```

Three details, each of which costs somebody an afternoon:

- **The token goes in `PGPASSWORD`.** It is far longer than the password `psql` will accept at an
  interactive prompt, so it has to arrive through the environment.
- **The username is the group's display name**, spaces and all, escaped for the shell. Not your own
  name — you are connecting _as the group_, because the group is the admin.
- **`dbname=postgres`, not `brickshare_catalog`.** The next command lives only in the `postgres`
  database, and running it anywhere else fails with a message that sounds like a syntax problem.

### Create the role

```sql
select * from pgaadauth_create_principal('app-brickshare-catalog-dev', false, false);
```

```
    pgaadauth_create_principal
-----------------------------------------------
 Created role for "app-brickshare-catalog-dev"
(1 row)
```

**Worth demonstrating the wrong version first**, because the error is misleading. Connected to
`brickshare_catalog` instead, the same statement gives:

```
ERROR:  function pgaadauth_create_principal(unknown, boolean, boolean) does not exist
```

which reads as "you typed the function name wrong" and means "you are in the wrong database".
`pgaadauth_create_principal` is provided by an extension installed only in `postgres`.

The two `false` arguments are `is_admin` and `is_mfa_required`. The application is neither: it is not
an administrator, and a background service cannot be prompted for a second factor.

**What this function actually did:** it looked up `app-brickshare-catalog-dev` in your Entra tenant,
found the service principal belonging to the web app's system-assigned identity, and created a
Postgres role bound to that principal's **object id**. The name is a label. The object id is the
binding.

### Grant it what it needs, and nothing else

Reconnect to the application database — `\c brickshare_catalog` — and:

```sql
GRANT CONNECT ON DATABASE brickshare_catalog TO "app-brickshare-catalog-dev";
GRANT USAGE ON SCHEMA public TO "app-brickshare-catalog-dev";

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
  TO "app-brickshare-catalog-dev";

ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO "app-brickshare-catalog-dev";
```

Four things to say, and the last two are the ones that matter later.

**The quotes are not optional.** An unquoted identifier is folded to lower case by Postgres, and the
role name contains no capitals here but will the moment somebody names a resource `Catalog-Api`.
Quote Entra-derived role names always; it is one habit instead of one bug.

**`GRANT … ON ALL TABLES` grants nothing today.** There are no tables — episode 19 creates the
first. `ON ALL TABLES` means "on all tables that exist right now", which is currently none of them,
and running it anyway keeps the two statements together where they can be read as one intention.

**`ALTER DEFAULT PRIVILEGES` is the one that will actually do the work**, and it has a condition
that catches everybody: it applies only to objects created **by the role that runs this statement**.
We are running it as the admin group, so it covers tables created by the admin group and nothing
else. That quietly decides something about episode 19: **whatever runs the migrations must connect
as a member of this group**, or the tables it creates will be invisible to the application and the
failure will be a `permission denied` on a table that plainly exists. Name the constraint here; pay
it there.

**The application gets DML and no DDL.** No `CREATE`, no `DROP`, no ownership. That is not caution
for its own sake — it is the architecture document's rule about startup migrations enforced by the
database rather than by everyone remembering. An application that _cannot_ alter the schema cannot
accidentally alter the schema on startup, on all three instances at once, which is episode 19's
entire subject.

### The hazard this creates, named now

That role is bound to the object id of a **system-assigned** identity, and a system-assigned identity
dies with the resource that owns it. Run `terraform destroy` and re-apply — or do anything else that
replaces the web app — and Azure creates a _new_ principal with a _new_ object id. Postgres still has
a role called `app-brickshare-catalog-dev`, pointing at a principal that no longer exists. The
application cannot log in, `terraform plan` says `No changes.`, and nothing anywhere hints at the
cause.

The fix is a **user-assigned** managed identity: an independent resource with its own lifetime, which
outlives the app it is attached to. It is not the fix taken today, for the reason episode 8 gave —
there is one consumer, no deployment slots, and the simpler option is still the right one. It lands
at the end of the catalog module, alongside deployment slots, which want the same thing for their own
reason. Until then, this is a known limitation with a known trigger: **if you destroy the web app,
drop and recreate the Postgres role.**

### Restart, and watch it come up

```bash
az webapp restart --name app-brickshare-catalog-dev --resource-group rg-brickshare-dev
```

```bash
curl -i "$(cd infra && terraform output -raw web_app_url)/health/ready"
```

`200 OK`. **Fifteen episodes.** Episode 3 built the endpoint, episode 16 gave it something to check,
episode 17 proved the check works against a real database, and this is the first time it has
answered yes in Azure.

Worth noting what it is actually asserting: `AddDbContextCheck` calls `CanConnectAsync`, so a 200
means _this instance can open an authenticated connection to that database right now_. It says
nothing about tables, because there are none. That is the correct scope for a readiness check —
"can I reach my dependency", not "is my dependency shaped the way I expect".

---

## Step 9 — Make the health check matter

One more line on the web app's `site_config`:

```hcl
  site_config {
    container_registry_use_managed_identity = true
    health_check_path                       = "/health/ready"

    application_stack {
      docker_image_name   = "brickshare-catalog-api:${var.image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }
```

```bash
terraform apply
```

**This is the line episode 3 was written for.** That episode argued at length that liveness and
readiness are different questions with different answers — restart me, versus stop sending me
traffic — and then wired neither to anything, because there was nothing to check and nothing to act
on. Both halves now exist. App Service polls `/health/ready`, and an instance that has lost its
database is taken out of rotation instead of serving five hundreds to customers.

### Prove it, by breaking it

Back in `psql`, as the admin group:

```sql
ALTER ROLE "app-brickshare-catalog-dev" NOLOGIN;
```

Nothing was deployed, nothing was restarted, no configuration changed. Within a minute:

```bash
curl -i "$(cd infra && terraform output -raw web_app_url)/health/ready"   # 503
curl -i "$(cd infra && terraform output -raw web_app_url)/health/live"    # 200
```

**Sit on that pair.** Ready is failing, live is fine, and both are correct: the process is perfectly
healthy and is entirely unable to do its job. This is the whole argument of episode 3 reduced to two
`curl`s, and it is why wiring a restart probe to a dependency check would be a disaster — a database
that hiccups would restart every instance you own, turning a small outage into a large one, and none
of the restarts would help.

The App Service _Health check_ blade shows the instance marked unhealthy. Then put it back:

```sql
ALTER ROLE "app-brickshare-catalog-dev" LOGIN;
```

and readiness recovers on its own, with nothing redeployed.

---

## Step 10 — The payoff

```bash
az webapp config appsettings list \
  --name app-brickshare-catalog-dev \
  --resource-group rg-brickshare-dev \
  -o table
```

Three settings. `WEBSITES_PORT`, `ASPNETCORE_ENVIRONMENT`, and a database connection string reading:

```
Host=psql-brickshare-catalog-dev.postgres.database.azure.com;Port=5432;Database=brickshare_catalog;Username=app-brickshare-catalog-dev;SSL Mode=Require
```

**Read it out on camera, slowly, and then say this:** that is a complete, working connection string
to a production database, and it can be pasted into a public issue tracker without consequence.
There is nothing in it to steal. Nothing to rotate when someone leaves. Nothing sitting in a
password manager, a `.env` file, or a Slack thread from eight months ago. The credential is not
protected — **it does not exist**, and the thing that does exist expires in under an hour and only
works from inside this application.

Episode 8 made that claim about a container image, which was the smallest thing in this system worth
protecting. This is the same claim about the database, and it is where the argument actually pays.

```bash
terraform plan
```

`No changes.` — the same bar episode 7 set and every infrastructure episode since has had to clear.

---

## Appendix — the assembled `infra/main.tf`

The full file at the end of this episode, for anyone who lost their place:

```hcl
terraform {
  required_version = ">= 1.15.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "rg-brickshare-tfstate"
    storage_account_name = "stbricksharetfstate"
    container_name       = "tfstate"
    key                  = "catalog.tfstate"
    use_azuread_auth     = true
  }
}

provider "azurerm" {
  features {}
}

data "azurerm_client_config" "current" {}

variable "image_tag" {
  description = "Container image tag to deploy — the commit SHA the pipeline built."
  type        = string
}

variable "postgres_admin_object_id" {
  description = "Object id of the Entra group that administers the Postgres server."
  type        = string
}

variable "postgres_admin_name" {
  description = "Display name of that group. Postgres uses it as the login role name, so it must match Entra exactly."
  type        = string
}

variable "developer_ip" {
  description = "Public IP allowed through the Postgres firewall for the bootstrap. Null in CI."
  type        = string
  default     = null
}

locals {
  # The web app's name is also the name of its system-assigned identity in Entra, which is also
  # the Postgres role name created in step 8. One value, three meanings, written once.
  catalog_app_name = "app-brickshare-catalog-dev"
}

resource "azurerm_resource_group" "main" {
  name     = "rg-brickshare-dev"
  location = "westeurope"
}

resource "azurerm_container_registry" "main" {
  name                = "crbrickshare"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
}

resource "azurerm_service_plan" "catalog" {
  name                = "plan-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "B1"
}

resource "azurerm_postgresql_flexible_server" "catalog" {
  name                = "psql-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  version               = "18"
  sku_name              = "B_Standard_B1ms"
  storage_mb            = 32768
  backup_retention_days = 7
  zone                  = "1"

  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = false
  }
}

resource "azurerm_postgresql_flexible_server_active_directory_administrator" "catalog" {
  server_name         = azurerm_postgresql_flexible_server.catalog.name
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  object_id           = var.postgres_admin_object_id
  principal_name      = var.postgres_admin_name
  principal_type      = "Group"
}

resource "azurerm_postgresql_flexible_server_database" "catalog" {
  name      = "brickshare_catalog"
  server_id = azurerm_postgresql_flexible_server.catalog.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.catalog.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "developer" {
  count = var.developer_ip == null ? 0 : 1

  name             = "developer"
  server_id        = azurerm_postgresql_flexible_server.catalog.id
  start_ip_address = var.developer_ip
  end_ip_address   = var.developer_ip
}

resource "azurerm_linux_web_app" "catalog" {
  name                = local.catalog_app_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.catalog.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    container_registry_use_managed_identity = true
    health_check_path                       = "/health/ready"

    application_stack {
      docker_image_name   = "brickshare-catalog-api:${var.image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }

  app_settings = {
    WEBSITES_PORT              = "8080"
    ASPNETCORE_ENVIRONMENT     = "Production"
    ConnectionStrings__Catalog = "Host=${azurerm_postgresql_flexible_server.catalog.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.catalog.name};Username=${local.catalog_app_name};SSL Mode=Require"
  }
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.catalog.identity[0].principal_id
}

output "web_app_url" {
  value = "https://${azurerm_linux_web_app.catalog.default_hostname}"
}
```

---

## What this episode is not

**No migrations, and therefore no tables.** The database is empty on purpose. Running
`dotnet ef database update` against Azure from a laptop right now would work, and that is precisely
why episode 19 exists — the interesting question is not how to apply a migration, it is _what
applies it, and when relative to the new code going live_.

**No private endpoint.** Explained and priced in step 3, dated to the end of the course, not built.

**No user-assigned managed identity.** The hazard it would fix is named in step 8 with its exact
trigger. It arrives with deployment slots at the end of the module.

**No high availability, no read replica, no connection pooler.** Zone-redundant HA roughly doubles
the bill and protects against a failure mode this course will never demonstrate. PgBouncer matters
when connection count is a problem; one App Service instance with one EF Core pool is not that
problem. Both are real, both are measurable, and neither is measurable yet.

**No second environment.** There is one resource group called `dev` and it is serving as production.
Splitting environments is a real episode about state files, naming and promotion, and doing it now
would double the running cost to demonstrate a problem nobody has.

**No Key Vault.** Still nothing to put in one — which is the nicest thing this episode can say about
itself. Episode 21 introduces the first genuine secret in the system, the Rebrickable API key, and
Key Vault arrives there because something needs it.

**No local change at all.** Compose still uses `Username=brickshare;Password=brickshare`, and that is
not a double standard. That password guards a container on a laptop that is destroyed with
`docker compose down -v`, and it is written in a file committed to a public repository precisely
because it protects nothing. The rule is not "passwords are forbidden" — it is **"no secret in
configuration"**, and a value that is not a secret does not qualify.

That answers why the local database keeps a password. The separate question — why there is a local
database at all, now that a real one exists in Azure — is the aside between steps 6 and 7.

---

## Verification

Offline, before touching a subscription:

```bash
cd infra
terraform fmt -check
terraform validate
```

**And know what that pair cannot see.** Step 5 demonstrated it: a self-referential block passes both
and is refused by `plan`. `fmt` checks layout and `validate` checks that the configuration is
internally well-formed — types, required arguments, unknown attributes — but the dependency graph is
built by `plan`, so every error about how resources refer to each other waits until then.
`terraform plan` is therefore the last offline-ish gate that matters, and the first one that needs
credentials.

And the check that step 6 did not break the last two episodes:

```bash
dotnet build
dotnet test
```

Still green, still with a real Postgres from Testcontainers, still untouched by any of this — because
the `if` in `Program.cs` takes the other branch locally. **This pair is the actual verification of the
application change**, and it is worth saying that plainly: it proves the change is inert where it
should be inert. What it cannot prove is the Azure path, and nothing local can.

Then the Azure checks, in order:

| Check                               | Expected                                              |
| ----------------------------------- | ----------------------------------------------------- |
| `terraform plan` after step 4       | `5 to add` (`4 to add` with no `developer_ip` set)    |
| `terraform plan` after step 5       | `1 to change`                                         |
| `/health/ready` after step 7        | 503, with `28P01` in the log stream                   |
| `pgaadauth_create_principal`        | `Created role for "app-brickshare-catalog-dev"`       |
| `/health/ready` after step 8        | **200**                                               |
| `ALTER ROLE … NOLOGIN`              | ready 503, live 200, instance unhealthy in the portal |
| `az webapp config appsettings list` | three settings, connection string with no `Password=` |
| `terraform plan`, finally           | `No changes.`                                         |

---

## Next

[Episode 19 — Migrations in the pipeline](catalog-api.md#episode-19--migrations-in-the-pipeline):
this episode created a database with nothing in it, and every table this course has designed since
episode 13 is still only a C# class and a migration file nobody has run against Azure.

The obvious way to fix that is three lines in `Program.cs` calling `Database.MigrateAsync()` at
startup. It works, it looks elegant, and it is a fault that stays invisible until the day the service
is scaled to a second instance — which is the worst possible day to find it. Episode 19 makes
migration a pipeline step that runs once, before the new revision goes live, and inherits a
constraint this episode already wrote down: the `ALTER DEFAULT PRIVILEGES` in step 8 means whatever
runs those migrations has to connect as a member of the admin group. That is one
`az ad group member add` — and it is the moment step 1's argument for a group instead of a person
gets paid.

# Episode 26 — The first real secret

← [Course plan](catalog-api.md) · Previous: [Episode 25 — Talking to Rebrickable](episode-25.md)

Episode 25 ended on a promise and a debt. The promise: the key travels on every request, and there
is a test that says so. The debt was one sentence long — **none of it works in Azure.**

This episode is the first one in the module whose red is not a failing test. It is the deployed
application, which has been crash-looping since episode 25 merged, and which nothing in the pipeline
noticed.

**Done when** the deployed API reaches Rebrickable with a key that appears in no repository, no app
setting and no Terraform output, and the local build still works with nothing but user secrets.

> **Runtime: about 20 minutes**, which is longer than this course's usual target and is being spent
> deliberately — a secret split across a vault, a role assignment, a variable, a pipeline and a
> configuration provider is not a topic that survives being told in eight. The one subsection marked
> **(cuttable)** is where to save two minutes if the recording runs long; nothing else here can be
> dropped without leaving the deployment broken or a decision unexplained.

## Before recording

- Episode 25 merged: the typed client, the resilience pipeline, four tests.
- `az login`, and a `terraform apply` you can run from the laptop — this episode creates a role
  assignment, which the pipeline can do but a human has to do first, for reasons step 6 gets to.
- Your Rebrickable API key to hand. The same one that is already in `dotnet user-secrets`.
- Directory permissions to create an Entra group, as in episode 18. The fallback if the tenant
  refuses is in step 2.
- A branch, and the deployed app's URL open in a tab.

**Nothing in this episode is driven by a test**, and it says so rather than pretending. `CLAUDE.md`
names three kinds of code that are not: infrastructure, configuration, and wiring with no behaviour
of its own. This episode is all three and nothing else. The regression net it runs on was written in
the last episode on purpose — `Every_request_carries_the_api_key` in `RebrickableClientTests.cs`
must still be green at the end, having never been opened, because **where the key comes from is not
allowed to change what the client does with it.**

Every sample below names its file and where in it the code goes. Where something is added to a file
that already exists, the **first block is what is already there** — the anchor to find on screen —
and the **second block is what to paste**. Every block is copy-paste clean: no markers, no ellipses.

### Everything this episode touches

| File | New or edited |
| --- | --- |
| `infra/main.tf` | One variable, one vault, two role assignments, one app setting |
| `infra/terraform.tfvars` | One line |
| `src/…/Api/Program.cs` | Four lines |
| `Directory.Packages.props` · `BrickShare.Catalog.Api.csproj` | One line each |
| `.github/workflows/deploy.yml` | One line |
| GitHub repository variables | One new variable |

No test project is opened. No `appsettings.json` change. No `docker-compose.yml` change.

---

## Step 1 — The red: production has been down for an episode

Before writing anything, go and look. Two commands, both worth being on camera:

```bash
# The liveness probe that has never depended on anything. It should be the safest call in the system.
curl -i https://app-brickshare-catalog-dev.azurewebsites.net/health/live

# What the container says on the way down.
az webapp log tail \
  --name app-brickshare-catalog-dev \
  --resource-group rg-brickshare-dev
```

A `503`, and in the log:

```
Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException:
DataAnnotation validation failed for 'RebrickableOptions' members: 'ApiKey'
with the error: 'The ApiKey field is required.'
```

**That is `ValidateOnStart()` doing precisely its job**, and it is worth taking thirty seconds to
enjoy it rather than apologising for it. Episode 25 argued that a misconfigured instance should
refuse to start instead of discovering the problem on a staff member's first lookup. This is that
argument being cashed: the instance is not half-working, it is not serving stale reads, it is not
returning a 500 to one endpoint out of six. It is down, loudly, with the name of the missing setting
in the first line.

**Now the uncomfortable half**, and this is the part that earns the minute. The pipeline that
deployed this is **green**. `terraform apply` succeeded, because App Service accepted a new image tag
and reported success; it does not wait to find out whether the container it started stayed up. The
`health_check_path` set in episode 18 will evict an unhealthy instance, which on a one-instance plan
means restarting it forever. So: a green deploy, a dead service, and nobody told.

That gap is real and it is not this episode's job — **episode 38 makes the pipeline wait for the
health probe** and fail if it never goes green. Naming it now is the point, because the instinct when
you see a crash-looping app is to assume the pipeline lied, and it did not. It was never asked.

---

## Step 2 — A group for the people who may write secrets

The key has to get into the vault somehow, and the entire argument of this episode is about *who*
is allowed to do that. So the identity comes first, before the resource it will be granted on.

Episode 18 made this choice already and the reasoning transfers wholesale: **an Entra group, not a
person and not "whoever runs `terraform apply`".** Two principals apply this repository — a laptop,
in a minute, and the pipeline's OIDC service principal on every push to `main` since episode 9. A
role assignment bound to "whoever is applying" flips between them and every `plan` shows a change.

A group's object id never moves.

```bash
az ad group create \
  --display-name "BrickShare Secret Admins" \
  --mail-nickname brickshare-secret-admins

# Add yourself.
az ad group member add \
  --group "BrickShare Secret Admins" \
  --member-id "$(az ad signed-in-user show --query id -o tsv)"

# The object id. Keep it; it goes in terraform.tfvars in step 4.
az ad group show --group "BrickShare Secret Admins" --query id -o tsv
```

**Why a second group rather than reusing `BrickShare Catalog DB Admins`.** It would work today, and
it would be one variable instead of two. It is still wrong, and for a reason that is easier to see
from the other direction: the database admin group exists so that a human can create Postgres roles,
and in episode 19 the *pipeline* was added to it so migrations could run. Merge the two and the
pipeline silently gains the ability to read and rewrite every secret in the vault — not because
anybody decided that, but because somebody needed a migration to work. **One group, one reason to
exist**, and the blast radius of adding a member to either stays something you can reason about.

The two are not even the same shape, which is the second half of the answer. `BrickShare Catalog DB
Admins` is **per-service** — it administers one server, `psql-brickshare-catalog-dev`, and episode 19
added the pipeline to it for one server's migrations. `BrickShare Secret Admins` is
**per-environment**, matching the vault it is about to be granted on. Folding a per-service group into
a per-environment grant is the mechanism by which a migration job ends up holding a payment key, and
nobody would ever write that down as a decision.

Unlike episode 18, the display name here is cosmetic — nothing derives a login from it, so a rename
costs nothing. Say so, because episode 18 made a fuss about exactly the opposite and students will
remember.

If `az ad group create` is refused by a locked-down tenant, the fallback is the same as episode 18's:
use your own user's object id, and note that the day a second person needs to set a secret they need
a second role assignment instead of a group membership.

---

## Step 3 — The vault

`infra/main.tf`. The container registry block ends like this:

```hcl
resource "azurerm_container_registry" "main" {
  name                = "crbrickshare"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
}
```

Paste directly below it, before the Postgres server:

```hcl
resource "azurerm_key_vault" "main" {
  name                = "kv-brickshare-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  # The data plane is governed by Azure RBAC — the same role assignments, the same `az role
  # assignment list`, the same audit trail as every other resource in this file.
  rbac_authorization_enabled = true

  # Seven days is the floor. Purge protection is the ceiling, and it is off — see below.
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
}
```

### The lines worth stopping on

**`rbac_authorization_enabled = true`.** A vault has two possible permission models and it is not
obvious from the portal which one you are looking at. **Access policies** are the original: a list
on the vault itself, per-principal, with its own verbs (`get`, `list`, `set`), invisible to
`az role assignment list` and to every RBAC tool the rest of this course uses. **Azure RBAC** is the
model everything else in this repository already uses — `AcrPull` in episode 8, the Postgres admin in
episode 18. Choosing it here means students learn one mechanism, not two, and it means the question
"who can read this secret?" is answered by the same command that answers it for the registry.

The honest counterweight: access policies can express finer verbs than the built-in roles do, and
some organisations are on them for that reason. It is not a difference this system needs, and
**two permission models in one course is a tax paid on every subsequent episode.**

If you are reading older documentation, this argument was `enable_rbac_authorization` in azurerm 3.x.
Version 4 renamed it. Worth mentioning out loud, because the 3.x spelling is what most search results
still show and the error it produces is unhelpful.

**`purge_protection_enabled = false`, deliberately.** With purge protection on, a deleted vault
cannot be removed for the retention period **and the setting cannot be turned back off**. That is
correct for production — it is the control that stops an attacker with Contributor from destroying a
vault and its secrets in one call — and it is wrong for a course, where every student will eventually
delete `rg-brickshare-dev` and expect it to go away. Choosing convenience over the safer default and
**saying that is what you are doing** is the whole point of putting the line in the file explicitly
rather than letting it default.

**`soft_delete_retention_days = 7`** is not optional — soft delete cannot be disabled at all any
more. Which leads directly to the trap.

**One vault per environment, not per service** — and the way this decision got made is worth being
honest about, because it did not start as an architecture argument. It started as a character limit.
`kv-brickshare-catalog-dev`, which is what every other name in this file would have produced, is
**25 characters and Key Vault allows 24.** A cap is not a reason to choose a boundary, so the cap only
forced the question; something else had to answer it.

What answers it is episode 7's own rule, the one it stated about `main.tf` and then about modules:
**structure grows for concrete reasons, not on a schedule.** There is one service. A second vault
would separate it from nothing. `rg-brickshare-dev` and `crbrickshare` are already per-environment for
exactly that reason, which is also why this resource pastes in beside them, above the per-service
Postgres server and web app — and why its Terraform label is `main` rather than `catalog`. That
label is the file saying which of the two groups a resource belongs to, and it is worth thirty seconds
because the next person to add a resource has to pick one.

**And the cost, stated rather than glossed over.** A vault is an access-control boundary. `Key Vault
Secrets User`, granted at vault scope in the next step, means *read every secret in this vault* — today
that is one secret and the distinction is academic. The moment a **different service** puts a secret in
here, it stops being academic: the catalog API would be able to read a payment key it has no business
seeing. There are two answers at that point and neither needs building now:

- a second vault, one per service, which is what the name would have said all along, or
- **per-secret scope**, which Azure RBAC supports and most people do not know about:
  `scope = "${azurerm_key_vault.main.id}/secrets/Rebrickable--ApiKey"`.

**The signal to watch for is a second service, not a second secret.** Two secrets belonging to the
same service share a blast radius already. Say which one you are waiting for, so that whoever adds the
Stripe key knows this line was left deliberately and what it is waiting on.

**The name is globally unique, and stays reserved while soft-deleted.** Like `stbricksharetfstate` in
episode 7 and `crbrickshare` in episode 8, `kv-brickshare-dev` has to be unique across all of
Azure; pick another literal if it is taken. The Key Vault-specific part is the second half: a vault
you delete keeps its name for seven days, so **`terraform destroy` followed by `terraform apply` fails
on a name conflict with your own vault.** The escape hatch, worth showing before anyone needs it:

```bash
az keyvault list-deleted --query "[].name" -o tsv
az keyvault purge --name kv-brickshare-dev --location westeurope
```

Purge only works because purge protection is off. The two settings are one decision.

---

## Step 4 — Two roles, and the variable that carries the group

`infra/main.tf`, at the end of the variable block near the top. Already there, as the last variable
in it:

```hcl
variable "developer_ip" {
  description = "Public IP allowed through the Postgres firewall for the step 8 bootstrap. Null in CI."
  type        = string
  default     = null
}
```

Paste below it. **Appended, not sorted** — read the list top to bottom and it is the order the episodes
added things: `image_tag` in episode 9, the two Postgres variables and `developer_ip` in episode 18,
this one now. Alphabetical would read better and lose that, and the history is the more useful of the
two in a file this size:

```hcl
variable "secret_admin_object_id" {
  description = "Object id of the Entra group allowed to write secrets into the vault. Not the application, which only reads."
  type        = string
}
```

Then, at the bottom of the file, the existing role assignment:

```hcl
resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.catalog.identity[0].principal_id
}
```

Paste directly below it:

```hcl
# The application. Read a secret's value; that is the entire list.
resource "azurerm_role_assignment" "secrets_read" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.catalog.identity[0].principal_id
  principal_type       = "ServicePrincipal"
}

# The humans. Write a secret's value, which the application must never be able to do.
resource "azurerm_role_assignment" "secrets_write" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = var.secret_admin_object_id
  principal_type       = "Group"
}
```

`infra/terraform.tfvars`, which is where the object ids live for local applies. Already there, as the
last line:

```hcl
developer_ip             = "213.93.4.203"
```

Paste below it, with the id step 2 printed — same order as the variable block above, and the extra
spaces are what keep the `=` column lined up:

```hcl
secret_admin_object_id   = "<the object id from step 2>"
```

### Why two roles and not one

`Key Vault Administrator` would cover both principals in one assignment and one less line of
Terraform. It also gives the web app permission to **rewrite** the key it is reading.

**The role a component holds is the worst thing that component can do once somebody else is driving
it.** A compromised container that can read a secret is an incident; one that can silently *replace*
that secret is an incident that survives everything you do about it — you restart the app, the app
fetches the attacker's value, and the vault's audit log says the write was authorised, because it was.
`Key Vault Secrets User` is `get` and `list` on secret values and nothing else, and the resource label
says it: `secrets_read`, beside a `secrets_write` that the application is not in. **The application
never writes**, and the file reads that way without a comment explaining it.

Note what this pair does *not* buy, so nobody leaves thinking it did: because the scope is the vault,
`Key Vault Secrets User` reads every secret in it. Today that is one secret belonging to this service,
so read-everything and read-mine are the same grant — step 3 said which signal changes that, and
per-secret scope is where it goes when it does. **Least privilege on the verbs, deferred on the
scope**, and only one of those two is worth paying for now.

The mirror of that is `Key Vault Secrets Officer` on the group: full control of secret values, no
control of the vault itself. Nobody in this system needs to change the vault's access model, delete
it, or manage its keys and certificates, so nobody has `Key Vault Administrator`. The two roles
together are a sentence about who does what, which is exactly what one role would have erased.

**`principal_type` is not decoration.** Without it Azure looks the principal up in Entra to find out
what it is, and a service principal created seconds earlier may not have replicated yet — the
classic intermittent `PrincipalNotFound` on a fresh `apply`. Telling it what to expect skips the
lookup. Cheap fix for a failure that only shows up on the first apply, which is the apply a student
is watching.

**What is *not* needed here, and is worth pointing at:** the pipeline's service principal gets no
role on this vault. It creates the vault and the role assignments — it already creates `acr_pull`, so
the permission to do that exists as of episode 8 — but it never reads the secret and never writes
one. **A deploy that cannot see the secret it is deploying is the goal**, and it comes for free from
having put the value in by hand.

---

## Step 5 — The vault's address is not a secret

The application has to be told which vault to ask. That value is a URI, it identifies a resource
rather than granting access to one, and it belongs in plain configuration.

`infra/main.tf`, in the web app's `app_settings` map. Already there:

```hcl
  app_settings = {
    WEBSITES_PORT              = "8080"
    ASPNETCORE_ENVIRONMENT     = "Production"
    ConnectionStrings__Catalog = "Host=${azurerm_postgresql_flexible_server.catalog.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.catalog.name};Username=${local.catalog_app_name};SSL Mode=Require"
  }
```

Becomes:

```hcl
  app_settings = {
    WEBSITES_PORT              = "8080"
    ASPNETCORE_ENVIRONMENT     = "Production"
    ConnectionStrings__Catalog = "Host=${azurerm_postgresql_flexible_server.catalog.fqdn};Port=5432;Database=${azurerm_postgresql_flexible_server_database.catalog.name};Username=${local.catalog_app_name};SSL Mode=Require"
    KeyVault__Uri              = azurerm_key_vault.main.vault_uri
  }
```

**One line, and no comment on it**, which is the rare case where that is the right call: the setting
says which vault to ask, not what is in it, and **a vault URI with no role assignment is a 403.** There
is nothing to warn the next reader about, so there is nothing to write.

**This is the same shape as the connection string above it and that parallel is the lesson.** Episode
18's connection string names a host, a database and a username, and carries no password, because the
password is a token fetched at runtime by a managed identity. This setting names a vault and carries
no secret, because the secret is fetched at runtime by the same managed identity. Two different
Azure services, one pattern: **configuration says where, identity says who, and the credential never
exists as a value anybody can copy.**

And the third appearance of that pattern is the one that makes it stick — say the count out loud.
Episode 8: the registry, via `AcrPull`. Episode 18: Postgres, via an Entra token. Episode 26: the
vault, via `Key Vault Secrets User`. Nothing about the third one is new, which is the point of having
done the first two.

### The pipeline needs the new variable too

The `deploy` job applies Terraform, so it needs a value for `secret_admin_object_id` — `terraform.tfvars`
is for local applies, and the workflow passes ids as environment variables.

`.github/workflows/deploy.yml`, in the `deploy` job's `env` block. Already there:

```yaml
      TF_VAR_postgres_admin_object_id: ${{ vars.POSTGRES_ADMIN_OBJECT_ID }}
      TF_VAR_postgres_admin_name: ${{ vars.POSTGRES_ADMIN_NAME }}
```

Paste below them:

```yaml
      TF_VAR_secret_admin_object_id: ${{ vars.SECRET_ADMIN_OBJECT_ID }}
```

Then add the repository variable, which is the one piece of this episode that lives in GitHub's
settings rather than in the repository:

```bash
gh variable set SECRET_ADMIN_OBJECT_ID --body "<the object id from step 2>"
```

**A `vars` context entry, not `secrets`** — and that distinction is the small idea of this step. An
object id is not a secret; it identifies a group, it appears in `terraform.tfvars` in this very
commit, and putting it in `secrets` would make it invisible in logs for no benefit while teaching
students that everything that looks like a GUID is dangerous. The one genuine secret in this episode
is never going near GitHub at all.

---

## Step 6 — Apply, then put the key in by hand

Apply from the laptop, so the vault and both role assignments exist:

```bash
cd infra
terraform init -input=false
terraform apply
```

Read the plan on camera: one vault, two role assignments, one app setting changing. The web app is
modified in place — no replacement, no downtime that was not already there.

Then the secret. This is the only manual step in the episode and it is manual **on purpose**:

```bash
az keyvault secret set \
  --vault-name kv-brickshare-dev \
  --name Rebrickable--ApiKey \
  --value "<your key>"
```

### The two things to say while that runs

**`--` becomes `:`.** Key Vault secret names allow letters, digits and hyphens — no colons — so the
configuration provider maps a double hyphen onto the configuration separator. `Rebrickable--ApiKey`
in the vault *is* `Rebrickable:ApiKey` in `IConfiguration`, which is the key `RebrickableOptions`
already binds to and the key that is already in your user secrets. Nothing in C# changes. The claim
episode 25 made — a different mechanism per environment, the same key from the code's point of view —
is this line, and it is nice that it is this small.

**The value is not in Terraform state, and you can prove it.** No `azurerm_key_vault_secret`
resource exists, which is not an omission:

```bash
terraform show -json | grep -ci "<the first eight characters of your key>"
```

Zero. Compare with the alternative this episode exists to reject. Had the key gone into
`app_settings`, it would now be:

- **in Terraform state**, in a storage account, in plaintext — state has no concept of a sensitive
  value, only of one it declines to print;
- **in the portal**, readable by anyone with Contributor on the resource group, which is a role
  people get for reasons that have nothing to do with this key;
- **in a `terraform plan` diff**, on every pull request, in CI logs that are readable by anyone who
  can read the repository.

An `azurerm_key_vault_secret` resource would have removed the second and third and kept the first,
which is why it is not here either. **A secret managed by Terraform is a secret in Terraform state.**
The hand-run command is the whole mechanism by which the value exists in exactly one place.

The cost, stated plainly so nobody discovers it later: this step is not in the pipeline, so a
from-scratch environment is `terraform apply` **plus** one documented command. Episode 18 already
established that precedent with its Postgres role bootstrap, and the reasoning is the same — the
thing Terraform cannot do without being handed the secret is the thing a human does once.

---

## Step 7 — The configuration provider

Four lines of C# and a package. Everything up to here was infrastructure; this is the part that makes
the application read it.

`Directory.Packages.props`, in the single `ItemGroup`. Already there:

```xml
    <PackageVersion Include="Azure.Identity" Version="1.21.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
```

Paste between them — the list is alphabetical and stays that way:

```xml
    <PackageVersion Include="Azure.Extensions.AspNetCore.Configuration.Secrets" Version="1.5.2" />
```

`src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj`, in the `PackageReference` group.
Already there:

```xml
    <PackageReference Include="Azure.Identity" />
    <PackageReference Include="FluentValidation" />
```

Paste above the first of those — no version attribute, because central package management owns
versions:

```xml
    <PackageReference Include="Azure.Extensions.AspNetCore.Configuration.Secrets" />
```

`src/Catalog/BrickShare.Catalog.Api/Program.cs`. Already there, at the top of the file body:

```csharp
var builder = WebApplication.CreateBuilder(args);

// FluentValidation names errors after C# properties. The wire is camelCase, so the two have to be
// reconciled somewhere, and this is the only place FluentValidation offers.
```

Paste between them:

```csharp
/*
 Azure only, and driven by whether the setting exists rather than by which environment this is.
 With no KeyVault:Uri the provider is never added, so a laptop keeps reading `dotnet user-secrets`
 and Compose keeps reading the environment — episode 25, step 6, unchanged.
*/
if (builder.Configuration["KeyVault:Uri"] is { Length: > 0 } keyVaultUri)
{
    // Added last, so it wins. Nothing in appsettings.json sets Rebrickable:ApiKey today, and this
    // ordering is what keeps the answer obvious if anything ever does.
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}
```

No new `using`. `AddAzureKeyVault` is an extension on `IConfigurationBuilder` in
`Microsoft.Extensions.Configuration`, which the Web SDK's implicit usings already cover, and
`Azure.Identity` is on line 5 of this file since episode 18 — adding either directive fails the build
on `IDE0005`.

### Three decisions inside four lines

**`if (setting exists)`, not `if (!builder.Environment.IsDevelopment())`.** The environment check is
the version most examples show, and it is worse in a way that costs real time. The integration tests
boot the API through `CatalogApiFactory` in `Production`-shaped configuration; Compose runs the
container with `ASPNETCORE_ENVIRONMENT=Development` for reasons that have nothing to do with vaults.
Branch on the environment name and both of those have to be argued with. **Branch on the presence of
the configuration the provider needs, and the condition is the truth rather than a proxy for it** —
an instance with a vault URI uses a vault; one without, does not.

**Before `builder.Build()`, and that is not stylistic.** `RebrickableOptions` is bound when the
options are first resolved, and `ValidateOnStart` forces that during host startup. A configuration
source added after `Build()` is a source that does not exist as far as any of that is concerned.
Configuration is assembled, then frozen, then read — showing that order once here saves an episode's
worth of confusion later.

**`DefaultAzureCredential`, third appearance, same reasoning as the first two.** On App Service it
finds the system-assigned identity from the environment; on a laptop it finds your `az login`, which
is why step 6's role assignment on the *group* matters and why you can run the app against the real
vault if you ever want to. The same class, unchanged, resolving a different credential in each place
— which is the single most useful thing `Azure.Identity` does and the reason episode 18 introduced it
rather than a managed-identity-specific type.

### The alternative worth naming **(cuttable)**

App Service can do this with no package and no code. Put `@Microsoft.KeyVault(SecretUri=https://…)`
in an application setting and the platform resolves it before the container starts; the app sees a
plain environment variable and knows nothing about Key Vault.

It is genuinely good, and it is not what this course uses, for one reason that outranks the
convenience: **it is App Service's feature, not .NET's.** `docs/course-plan/` has this system moving
onto Container Apps and Azure Functions, and the configuration provider is the mechanism that works
identically in all three, in a console app, and in a test. Teaching the portable one first and
mentioning the platform shortcut is the right order; the reverse strands students the first time they
move a workload.

Two smaller strikes, worth a sentence each: the reference is resolved once at container start, so it
has the same rotation story as the provider with none of the knobs, and a broken reference surfaces as
an empty setting rather than an error, which turns a misconfiguration into a `null`.

---

## Step 8 — Green: deploy it and watch it come up

Push the branch, merge it, and watch the pipeline. Then the same two commands step 1 opened with:

```bash
curl -i https://app-brickshare-catalog-dev.azurewebsites.net/health/live

az webapp log tail \
  --name app-brickshare-catalog-dev \
  --resource-group rg-brickshare-dev
```

`200`, and a log with no `OptionsValidationException` in it. **That `200` is the assertion this whole
episode was building**, and it is worth being explicit about the chain it proves, because no single
step of it is visible on screen:

App Service started the container with a system-assigned identity → `DefaultAzureCredential` found
that identity → `Key Vault Secrets User` let it call the vault named in `KeyVault__Uri` →
`Rebrickable--ApiKey` arrived as `Rebrickable:ApiKey` → `RebrickableOptions` bound and validated →
the host finished starting → the liveness probe answers.

Break any one of those and the app is down, with a startup error naming which one. **There is no
degraded middle state**, and that is the shape `ValidateOnStart` bought back in episode 25.

`/health/ready` is the second call, and it should also be `200` — Postgres has not moved. Worth
making it, because it is the difference between "the process started" and "the instance can serve
traffic", and this episode changed something that sits in front of both.

### What is not proved yet, and why that is fine

**No request has reached Rebrickable.** Nothing in the API calls the client — episode 25 built it
with no caller on purpose, and episode 27 is what gives it one. So the deployed proof available today
is that **the key was read**, not that it works, and the two should not be conflated on camera.

The key's correctness is already covered where it belongs: locally, against the real service, with
the manual `GET` from episode 25's verification table. If that call worked with the value in your
user secrets, and `az keyvault secret set` received the same value, the deployment has it.

### The new coupling, said out loud

Key Vault is now on the startup path. An unreachable vault is an instance that cannot boot — the
hardest possible dependency, considerably harder than anything else in this service.

Compare that with the decision episode 25 made forty minutes ago: Rebrickable gets **no readiness
check**, because an outage there must not evict an instance that can still browse the catalog and
register stock. Those two look inconsistent and are not, and the reconciliation is one line:
**a dependency you cannot start without belongs at startup; a dependency you can work around belongs
behind a feature, not a probe.** Without a key the API cannot serve *any* authenticated call to
Rebrickable and cannot be repaired by retrying. With Rebrickable down, five of six endpoints are
fine.

The cost is real — a Key Vault outage during a scale-out event means new instances do not come up —
and it is the accepted one, because the alternative is an instance running with no key and finding
out per-request.

---

## Step 9 — Rotation, and the close call this episode declines

The secret is read once, during startup. Rotate the key in Rebrickable's dashboard, set the new value
in the vault, and **the running app keeps using the old one until it restarts.**

The library has an answer:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUri),
    new DefaultAzureCredential(),
    new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromMinutes(5) });
```

**Not taken, and this is a judgement call rather than an obvious one**, so it gets the trade-off
rather than a verdict.

Against it: this key changes when a human decides to change it, perhaps once a year. Reloading buys
a shorter window between "new value in the vault" and "app using it", and pays for it with a vault
call every five minutes forever, from every instance, and with a configuration graph that can change
under a running request. The operational procedure without it is `az webapp restart`, which is one
command and which somebody rotating a key is already at a keyboard for.

For it, and this is the part worth remembering: **the calculation inverts completely for a credential
that expires on its own.** A certificate, a SAS token, a rotating storage key — anything with an
expiry rather than a rotation event — cannot be handled by "restart when you change it", because
nobody changes it. Reload, or a client that fetches per-use, stops being overhead and becomes the
only correct design.

The rule that generalises: **choose based on who initiates the change.** A human rotating on purpose
can restart the app. A clock rotating on a schedule cannot be asked to.

---

## What this episode is not

**No Key Vault for local development.** User secrets stay exactly as episode 25 left them. The
alternative is a vault every student provisions before they can run the app, or — worse — one shared
vault holding one shared key, which is a production secret on thirty laptops. Two mechanisms, one
configuration key, and the code does not know which it got.

**No secret in Terraform.** Covered in step 6, and the single most likely thing to be "improved" by
the next person to read `main.tf`. The comment in the file has to carry that, because the absence of
a resource explains nothing on its own.

**No private endpoint, and no vault firewall.** The vault is reachable from the public internet and
protected by RBAC alone — the same posture, and the same deferral, as the Postgres server in episode
18. `docs/architecture/catalog.md` wants both locked down; both arrive together in the hardening pass,
because a private endpoint costs a VNet, a subnet, private DNS and an App Service integration, and
that is an episode about networking rather than about secrets.

**No customer-managed keys, no HSM.** `standard` rather than `premium`. Nothing in BrickShare has a
compliance requirement that asks for a key you own protecting a key you rent, and adding one because
the option exists is precisely the demo-architecture failure `CLAUDE.md` guards against.

**No second secret.** Stripe's key and anything else this service needs are later episodes, and each
costs one `az keyvault secret set` and nothing else — no Terraform, no code, no role assignment.
**That is the payoff for the work in this episode** and it is the reason to do it properly once.

**And no second boundary**, which is the related thing not to confuse with it. A second secret is free.
A secret belonging to a *different service* is the one that reopens step 3's decision, because that is
when one vault holding everything stops being a simplification and starts being a grant nobody
intended.

**No health check for the vault.** Tempting, and wrong for the same reason it is unnecessary: an
instance that could not read the vault never started, so a readiness check on Key Vault can only ever
report healthy. A probe that cannot fail is a probe worth deleting.

## Verification

| Check | Expected |
| --- | --- |
| `dotnet build` | 0 warnings |
| `dotnet test` | Green, with no test file opened this episode — `Every_request_carries_the_api_key` included |
| `dotnet run`, then a real Rebrickable `GET` | Works, from user secrets, with no vault involved |
| `docker compose up` | Works, from `.env`, with no vault involved |
| `cd infra && terraform fmt -check && terraform validate` | Clean |
| `terraform plan` immediately after the apply | No changes — the role assignments do not drift |
| `terraform show -json \| grep -ci "<first 8 chars of the key>"` | `0` |
| `az keyvault secret list --vault-name kv-brickshare-dev --query "[].name" -o tsv` | Exactly `Rebrickable--ApiKey` |
| `az role assignment list --scope "$(az keyvault show -n kv-brickshare-dev --query id -o tsv)" -o table` | Two entries: Secrets User, Secrets Officer. No Administrator |
| `az webapp config appsettings list -g rg-brickshare-dev -n app-brickshare-catalog-dev` | A vault URI. No key |
| Deployed `/health/live` and `/health/ready` | `200` and `200` |
| `git grep -i "<first 8 chars of the key>"` | Nothing |
| Delete the secret, `az webapp restart`, watch the log | `OptionsValidationException` again — the step 1 failure, on purpose. Put it back |

That last row is the one to actually run. **A control you have never seen fire is a control you are
guessing about**, and it takes ninety seconds to stop guessing.

## Next

[Episode 27 — The lookup that owns the facts](catalog-api.md#episode-27--the-lookup-that-owns-the-facts):
the Rebrickable client finally gets a caller — `POST /catalog/lookups`, which asks Rebrickable
about a set number, stores what it said, and hands back a prefilled draft.

The reason it exists is an authorization bug that reads as an API design choice. `POST
/catalog/sets` accepts nine fields and five of them are LEGO's facts rather than the shop's, so
anyone who can call it can invent a product — a four-piece Titanic that passes every validator
episode 22 wrote, because every one of those values is well-formed. Storing the facts server-side
at lookup time makes **the server the only source of the facts it stores**.

The question that generalises out of it — *whose data is this?*, asked of every field in every
request body — is one of the most transferable ideas in the course. Episode 27 builds the half
that fetches; [episode 28](catalog-api.md#episode-28--the-request-body-that-loses-its-facts) is
the half that takes the five fields away, and it costs one extra endpoint.

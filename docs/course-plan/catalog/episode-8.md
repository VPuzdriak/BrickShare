# Episode 8 — A registry of our own

← [Course plan](catalog-api.md) · Previous: [Episode 7 — Terraform: recreate it declaratively](episode-7.md)

Episodes 6 and 7 both pulled the catalog image from **Docker Hub**, and that was always
scaffolding. This episode replaces it with an **Azure Container Registry** that the App Service
reads using its own managed identity — a private image, and not one credential anywhere in the
system to store, rotate or leak.

**Done when** the same three URLs answer, served from an image nobody outside the subscription
can pull, and `az webapp config appsettings list` shows there is no registry password in the
app's configuration because there never was one.

Like episode 7, everything here is a change to the single `infra/main.tf` built in that episode.
No new files, no repository code changes.

## Before recording

Nothing new to install. Terraform and the Azure CLI from episode 7, Docker from episode 5, and
the `infra/main.tf` that episode 7 left applied and working.

Confirm the starting point before rolling:

```bash
cd infra
terraform plan     # No changes. Your infrastructure matches the configuration.
```

If that prints anything other than no changes, fix it before adding to it. Episode 7's whole
closing argument was that state and reality agree; building on top of a drifted state teaches
the opposite of that.

## Why Docker Hub stops being enough

The governing rule of this course is that no Azure service appears because the syllabus wants a
module for it. So before creating anything, the honest question: what specifically does Docker
Hub fail at, right now, that a registry in Azure does not?

Four answers, strongest first.

**1. The next episode has to push, and pushing needs a credential.** This is the reason the
switch lands *here* and not somewhere more convenient. Episode 9's pipeline builds an image and
pushes it. Pushing to Docker Hub from GitHub Actions means a Docker Hub personal access token,
stored as a GitHub secret — which is precisely the kind of thing episode 9's OIDC lesson exists
to eliminate. Push to ACR instead and the *same* federated credential that authorises
`terraform apply` authorises the push. Doing the registry first is what lets the next episode be
secretless end to end rather than secretless-except-for-this-one-token.

**2. Private, with nothing to store.** A private Docker Hub repository forces App Service to
hold a registry username and password in its application settings — a real secret, in
configuration, which `docs/architecture/catalog.md` refuses in as many words: *managed identity
throughout, no secrets in configuration.* The alternative is what episodes 6 and 7 actually did:
keep the repository public, and avoid the secret by publishing the artifact to the world. ACR
with a managed identity is the only option on the table that is both private and credential-
free.

**3. Anonymous pull rate limits are real and they fail badly.** App Service pulls the image on
every restart, every scale-out and every slot swap, from shared Azure egress addresses. When a
rate limit hits, the symptom is a container that intermittently fails to start — which looks
exactly like an application bug, in the environment where debugging is hardest. A failure mode
that misdirects is worse than one that is merely inconvenient.

**4. Colocation.** Same region as the app: less pull latency, no cross-internet egress. **Say
plainly that this one is small.** It is real, it is the reason most people cite first, and on
its own it would not justify moving anything.

**The counterpoint, said out loud:** for a genuinely public image that no machine pushes to,
Docker Hub is a perfectly good answer and there is nothing to fix. The trigger is *private,
plus pushed by automation*. Both of those arrive in the next two episodes, which is why this one
exists.

## The target shape

Still one file. `infra/main.tf` gains one resource and one role assignment, the web app gains an
`identity` block and two changed lines — and the file **loses its only variable**.

Five resources in one file is still comfortably the right size. The two triggers episode 7
named for splitting it — a second service wanting to reuse this shape, or the file genuinely
getting hard to read — have not fired.

---

## Step 1 — Add the registry, and apply it on its own

Add to `infra/main.tf`:

```hcl
resource "azurerm_container_registry" "main" {
  name                = "crbrickshare"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
}
```

**The name has no hyphens, and that is not a style choice.** Container registry names are
alphanumeric only, 5–50 characters — `cr-brickshare-dev`, which is what the naming convention in
every other line of this file would produce, is rejected outright. Worth stopping on for ten
seconds, because it is the kind of thing that reads as a typo when someone copies the pattern.
Like the storage account in episode 7, the name is also globally unique across all of Azure; if
`crbrickshare` is taken, pick another and change the one literal.

**`sku = "Basic"`** — 10 GiB of storage, which is a great deal more than one API image needs.
Standard buys throughput and storage; Premium buys geo-replication and, relevantly,
**private endpoints**, which is what the network posture recommended in
`docs/architecture/catalog.md` would eventually want. That is a cost decision deferred on
purpose, not an oversight — say so rather than leaving Basic looking like a default nobody
thought about.

**`admin_enabled = false` is written down even though it is already the default.** The admin
account is a username and password that works from anywhere on the internet — exactly the thing
this episode exists to not have. Writing the default explicitly means that turning it on later
is a visible line in a diff someone has to justify, rather than a portal toggle nobody sees.

Now apply just this:

```bash
terraform plan
```

`1 to add`. The web app, the plan and the resource group are untouched.

**This is worth a beat on camera.** Every `plan` so far in this course has been a from-nothing
create — three resources, all new. This is the first time `plan` does the thing that actually
makes it valuable: shows a *change* against infrastructure that already exists and is serving
traffic, and shows that the change is exactly one resource and touches nothing else. Episode 7
claimed that was the point of the tool. This is where that claim gets cashed.

```bash
terraform apply
```

## Step 2 — Push the image into it

```bash
az acr login --name crbrickshare
```

**Say this, it is the first of two credential-free moments in this episode:** "No `docker login`,
no password prompt, nothing pasted. `az acr login` uses the Azure CLI session that has been
logged in since episode 7 and hands Docker a short-lived token. The credential exists for
minutes and never touches a file."

Then build and push, from the repository root:

```bash
docker build --platform linux/amd64 \
  -f src/Catalog/BrickShare.Catalog.Api/Dockerfile \
  -t crbrickshare.azurecr.io/brickshare-catalog-api:episode-8 \
  .

docker push crbrickshare.azurecr.io/brickshare-catalog-api:episode-8
```

`--platform linux/amd64` is here for the same reason it is in episode 6, and deserves the same
one-line reminder rather than a silent retype: App Service Linux plans run amd64 only, a build
on Apple Silicon produces arm64 by default, and the symptom is `Container exited with exit code
255 during startup` with nothing in the application logs to explain it.

Confirm it landed:

```bash
az acr repository show-tags --name crbrickshare --repository brickshare-catalog-api -o table
```

**Worth mentioning, not worth switching to: `az acr build`.**

```bash
az acr build --registry crbrickshare \
  --image brickshare-catalog-api:episode-8 \
  --file src/Catalog/BrickShare.Catalog.Api/Dockerfile .
```

One command, builds in Azure on amd64 hardware, sidesteps the platform flag entirely, and does
not need Docker running locally at all. It is genuinely useful and worth knowing. It stays an
aside because the next episode builds the image in CI and pushes it, and a cloud build would
hide the exact step that pipeline is about to automate — the same reason this course has no
`azd` and no Aspire.

## Step 3 — Point the web app at it, with no credential

Three changes to the existing `azurerm_linux_web_app.catalog`, one new resource, and one
deletion.

**Delete `variable "docker_image"` entirely.** Episode 7 argued that it earned its place as the
one value genuinely personal to whoever was following along — a Docker Hub username nobody else
shares. That justification is gone: it is our registry, our repository name, our tag, and every
one of those is now a literal like every other name in the file.

**Say why the tag is not becoming a variable to replace it:** "The obvious move is to swap one
variable for another and make the tag configurable. Don't. Nothing varies it yet. The pipeline
in the next episode is what will genuinely need to pass a tag in, and that is the episode where
it becomes a variable — one episode later, for a reason that exists, instead of now for a reason
we are anticipating." Pleasant side effect: `plan` and `apply` stop prompting for input.

The web app, with the changed lines:

```hcl
resource "azurerm_linux_web_app" "catalog" {
  name                = "app-brickshare-catalog-dev"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.catalog.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    container_registry_use_managed_identity = true

    application_stack {
      docker_image_name   = "brickshare-catalog-api:episode-8"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }

  app_settings = {
    WEBSITES_PORT          = "8080"
    ASPNETCORE_ENVIRONMENT = "Production"
  }
}
```

And the grant:

```hcl
resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.catalog.identity[0].principal_id
}
```

### The four things worth explaining here

**`identity { type = "SystemAssigned" }` — one line, and Azure creates a service principal that
belongs to this web app.** It is created with the app and deleted with it. Nothing to manage,
nothing to clean up, nothing else can use it.

**`AcrPull` is the same idea as episode 7's `Storage Blob Data Contributor`, and the callback is
worth making explicitly.** Episode 7 ended with a 403 because creating a storage account grants
no access to the blobs inside it — control plane and data plane are separate, and nothing
bridges them automatically. This is that pattern again: the web app and the registry live in the
same resource group in the same subscription, and that grants exactly nothing. `AcrPull`,
assigned explicitly, is what lets one read the other. **Say it as a pattern, not a quirk** —
students will meet it a third time with Key Vault and a fourth with Postgres, and recognising
the shape is worth more than memorising three role names.

Same caveat as episode 7's bootstrap: creating role assignments requires `Owner` or
`User Access Administrator` on the subscription. On a personal or training subscription this is
rarely the blocker.

**Why system-assigned rather than user-assigned, and what would change that.** There is one
consumer, no deployment slots and no database. The identity has nothing to outlive and nothing
else to attach to, so the simpler of the two options is the right one — the same reasoning that
kept this file to one `main.tf` and the solution to one project.

Two concrete things will later argue for a user-assigned identity, and both are worth naming now
so the switch reads as planned rather than as a correction:

- **Deployment slots** (the final episode). Every slot gets its own system-assigned principal,
  so every role assignment has to be duplicated per slot. A user-assigned identity is shared —
  one grant covers all of them.
- **A database role bound to a specific principal.** Entra authentication to Postgres creates a role
  inside the database tied to the identity's object id, and a system-assigned identity is destroyed
  and recreated with the resource that owns it. Replace the web app and the role is left pointing at
  a principal that no longer exists — the app cannot log in, and nothing in `terraform plan` says
  why. A user-assigned identity survives the app being recreated.

Neither is a problem yet, and neither is fixed the moment it first appears: Postgres arrives in
episode 18 and takes the system-assigned identity as it stands, with that hazard named and its
trigger written down. The switch happens once, at the end of this module, where deployment slots
force it anyway — and it is its own worthwhile episode about why Azure has two kinds of managed
identity in the first place.

**The propagation window — say this before applying, not after it breaks.**

```bash
terraform plan     # 1 to add, 1 to change
terraform apply
```

Terraform can finish updating the web app before the `AcrPull` grant has propagated, so the
first pull may fail and the container may not start. That is expected and it is not a mistake in
the configuration. Wait a minute, then:

```bash
az webapp restart --name app-brickshare-catalog-dev --resource-group rg-brickshare-dev
```

The log stream from episode 6 is the place to watch it recover — the pull succeeds and the
familiar `Application started. Press Ctrl+C to shut down.` follows. **Getting ahead of this is
worth the thirty seconds it costs**; a viewer who hits an unexplained failed start here will
assume they typed something wrong and go looking in the wrong place.

## Step 4 — Verify, and prove the claim

```bash
terraform output web_app_url
```

Hit `/`, `/health/live` and `/health/ready`. Same three responses as every episode since 3 —
now served from an image that nobody outside this subscription can pull.

**Then the beat this whole episode was built for:**

```bash
az webapp config appsettings list \
  --name app-brickshare-catalog-dev \
  --resource-group rg-brickshare-dev \
  -o table
```

Two settings. `WEBSITES_PORT` and `ASPNETCORE_ENVIRONMENT`. No `DOCKER_REGISTRY_SERVER_URL`, no
`DOCKER_REGISTRY_SERVER_USERNAME`, no `DOCKER_REGISTRY_SERVER_PASSWORD` — the three settings
that a private Docker Hub repository, or an ACR with `admin_enabled = true`, would have put
there.

**Say it while the empty output is on screen:** "The image is private, the app can read it, and
there is no credential in this system. Nothing to rotate, nothing to leak, nothing to paste into
a chat window by accident. This is what *managed identity throughout, no secrets in
configuration* actually looks like when it is done rather than written down — and it is the same
move we will make again for Key Vault and for Postgres."

```bash
terraform plan
```

`No changes.` — the same bar episode 7 set, and the actual "Done when" for this episode.

## What this episode is not

**No Premium SKU, so no private endpoint on the registry.** The registry's own network access is
still public — authenticated, but reachable. Closing that needs Premium and the VNet work that
`docs/architecture/catalog.md` describes as a recommendation with an honestly-stated cost. It is
deferred with the rest of the network posture, not forgotten.

**No retention policy, no tag immutability, no vulnerability scanning.** All three are real and
all three want a registry holding more than one image before they mean anything. A retention
policy over a single tag is a rule with nothing to apply to.

**No user-assigned identity** — the two triggers that would earn one are named in Step 3.

**No Docker Hub cleanup.** The public image from episodes 6 and 7 can stay; it costs nothing and
deleting it proves nothing. What matters is that Azure no longer reads from it.

## Verification

```bash
terraform fmt -check
terraform validate
```

Both run without touching a subscription. The real check needs Azure: `plan` reads `1 to add`
after Step 1 and `1 to add, 1 to change` after Step 3, the three URLs answer, the app settings
list shows two entries and no registry credential, and the final `plan` reports no changes.

## Next

[Episode 9 — GitHub Actions: the loop closes](catalog-api.md): build, test, image, push, deploy,
on every push to `main` — pushing into the registry created here, with OIDC federated
credentials so no Azure secret and no registry token is ever stored in GitHub.

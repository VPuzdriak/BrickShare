# Episode 9 — GitHub Actions: the loop closes

← [Course plan](catalog-api.md) · Previous: [Episode 8 — A registry of our own](episode-8.md)

Every deployment in this course so far has ended with a human typing `terraform apply`. This
episode ends that. A push to `main` builds, tests, images, pushes and deploys — and **no Azure
credential is stored in GitHub at any point**, because none is ever created.

**Done when** a commit reaches Azure without anyone touching a terminal.

**This is the milestone the whole ordering was built around.** From here on every episode ends
with a push that deploys itself, and nothing later in this course has to stop and fix
deployment.

## Before recording

- Episodes 7 and 8 complete: `infra/main.tf` applies cleanly, the registry exists, and the app
  is running from it.
- A GitHub repository with the code pushed to `main`.
- The Azure CLI, logged in with an account that can create app registrations and role
  assignments.

Two names appear throughout and are the ones from earlier episodes — substitute if different:
the registry `crbrickshare`, and the repository `VPuzdriak/BrickShare`.

**Order matters this episode.** Steps 1 and 2 must be finished before the workflow is pushed.
A push before then produces a red run for reasons that have nothing to do with the workflow, and
debugging that on camera teaches nothing.

## Why OIDC, and why it is worth an episode

The default in most tutorials is one command:

```
az ad sp create-for-rbac --sdk-auth
```

— then paste the JSON blob it prints into a repository secret. It works, it takes two minutes,
and it is the reason so many production Azure subscriptions are one leaked repository setting
away from a bad day.

Look at what that credential actually is. It is **long-lived** — usually until someone notices
it in an audit, which is to say never. It gets **copied at least once** on its way into GitHub,
through a clipboard and often a chat window. It is **shared**, because the second pipeline that
needs Azure access reuses it rather than making another. And **nobody rotates it**, because
rotating it means finding every place it was pasted.

A federated credential inverts every one of those properties. GitHub mints a token that says
*this repository, this branch, this run*. Azure is configured in advance to trust exactly that
statement and nothing else. The token lasts minutes, it is useless anywhere but the workflow it
was issued to, and — the part worth saying slowly — **it is never stored, so there is nothing to
leak and nothing to rotate.**

**Say this plainly:** "This is the highest-leverage habit in the entire course. Everything else
we teach makes your code better. This one decides whether a compromised laptop or a careless
screen share costs you a subscription."

---

## Step 1 — Bootstrap the identity GitHub will use

The same shape of problem as episode 7's state backend, and worth naming as such: **the pipeline
cannot create the credential it authenticates with.** Something comes first, imperatively, once.

### The app registration

```bash
APP_ID=$(az ad app create --display-name "github-brickshare-catalog" --query appId -o tsv)
az ad sp create --id "$APP_ID"
SP_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
```

**Point to land while this runs:** no `--password`, no `create-for-rbac`, no secret produced at
any point. Nothing in this step generates a value that needs protecting, which is why nothing in
this step needs to be done off camera.

### The federated credential

```bash
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:VPuzdriak@20270523/BrickShare@1322339990:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

**Walk the `subject` string on camera, character by character.** It is the entire trust
boundary:

- `repo:VPuzdriak/BrickShare` — this repository. A fork has a different owner, so a fork cannot
  use it.
- `ref:refs/heads/main` — this branch. A push to any other branch produces a token Azure will
  not accept.

A pull request produces `pull_request` rather than a branch ref, and a tag produces
`refs/tags/...` — neither matches, so neither can deploy. Each of those needs its own federated
credential, added deliberately.

**Say it:** "'Only `main` deploys' is usually a convention people agree to and then break at
2 a.m. Here it is a fact about what Azure will accept. There is no way to be careless with it,
because carelessness produces a token that simply does not work."

### Four role assignments

The identity exists and can prove who it is. It still cannot do anything.

```bash
SUB_ID=$(az account show --query id -o tsv)

# Manage the infrastructure Terraform describes
az role assignment create \
  --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Contributor" \
  --scope "/subscriptions/$SUB_ID"

# Read and write the Terraform state blob
az role assignment create \
  --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" \
  --scope "$(az storage account show --name stbricksharetfstate \
              --resource-group rg-brickshare-tfstate --query id -o tsv)"

# Push images
az role assignment create \
  --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "AcrPush" \
  --scope "$(az acr show --name crbrickshare --query id -o tsv)"

# Manage the AcrPull assignment that Terraform owns
az role assignment create \
  --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" \
  --scope "/subscriptions/$SUB_ID/resourceGroups/rg-brickshare-dev"
```

**Use `--assignee-object-id` with `--assignee-principal-type`, not `--assignee`.** The
convenient form looks the principal up by name in Entra, which races directory propagation on a
principal created thirty seconds ago and fails intermittently — the worst kind of failure to hit
while recording.

**The second one is the third appearance of a pattern, and that is worth pointing out.** Episode
7 ended with a 403 because creating a storage account grants nothing inside it. Episode 8
granted `AcrPull` because sharing a subscription with a registry grants nothing either. Here it
is again, for a different identity on the same storage account. **Control plane and data plane
are separate everywhere in Azure**, and by the third time it should stop being surprising.

**Two things worth being honest about rather than glossing over:**

- **Subscription-scoped `Contributor` is broader than anyone would like.** It is required because
  Terraform manages `azurerm_resource_group.main` itself, and the permission to create or delete
  a resource group cannot be granted at resource-group scope — there is no group yet to scope it
  to. The tighter version is to take the resource group out of Terraform's hands entirely,
  create it once by hand alongside the state account, and scope `Contributor` to it. That costs
  one more imperative bootstrap step and one less resource Terraform describes. **A production
  team would almost certainly take that trade**; this course keeps the resource group in
  Terraform because seeing the whole environment in one file is worth more to a learner than the
  narrower grant, and saying so is better than pretending the broad grant is fine.
- **`Role Based Access Control Administrator` sounds alarming and is the restrained choice.**
  Terraform owns `azurerm_role_assignment.acr_pull` from episode 8, and a pipeline that cannot
  recreate what it manages is not really managing it. Day to day only *reading* that assignment
  is needed, which `Contributor` already allows — this matters the first time it is created or
  any time it drifts. It is scoped to one resource group, and it is the narrow alternative to
  handing the pipeline `Owner`.

## Step 2 — Tell GitHub who to be

**Settings → Secrets and variables → Actions → Variables → New repository variable**, three
times:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | `echo $APP_ID` |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |

**Then click the Secrets tab and leave it on screen.** It is empty, and it stays empty for the
rest of the course.

**Say it while it is up:** "Three values, none of them secret. These are the *name* of an
identity, not the ability to be it — knowing my client ID gets you exactly as far as knowing my
username does. They are in the tab that is not encrypted because encrypting them would be
theatre. And that empty page next to it is the actual deliverable of this episode."

## Step 3 — Make the tag a variable again

Episode 8 deleted this file's last variable and promised the tag would become one "in the next
episode, when the pipeline needs to pass one in." That is now.

In `infra/main.tf`:

```hcl
variable "image_tag" {
  description = "Container image tag to deploy — the commit SHA the pipeline built."
  type        = string
}
```

and the image reference becomes:

```hcl
docker_image_name = "brickshare-catalog-api:${var.image_tag}"
```

**No default, and that is deliberate.** A default is a fallback that silently deploys a stale
image the first time someone forgets to pass one. A deployment that refuses to run is an
inconvenience; a deployment that quietly ships the wrong artifact is an outage nobody is looking
for.

**Say what this changes about who deploys:** "Until now `terraform apply` was something we ran.
From this episode on, the pipeline owns it and running it from a laptop is the exception. That
is what 'the pipeline is the definition of done' actually means — not that the pipeline exists,
but that it is the only path. Anything applied locally from here is drift, and the next push
will reconcile it away."

## Step 4 — The workflow

`.github/workflows/deploy.yml`. Build it up job by job on camera rather than pasting it whole —
the shape is the lesson.

```yaml
name: Deploy catalog API

on:
  push:
    branches: [main]
  workflow_dispatch:

# id-token: write is what makes OIDC possible — it lets the job request a short-lived
# token from GitHub describing which repository and branch is running. It is not a
# default, and without it every Azure step below fails before it starts.
permissions:
  contents: read
  id-token: write

# Two pushes in quick succession would otherwise run two `terraform apply` calls against
# one state file. The blob lease prevents corruption; this prevents the second run dying
# on it.
concurrency:
  group: deploy-main
  cancel-in-progress: false

env:
  REGISTRY: crbrickshare
  IMAGE_NAME: brickshare-catalog-api

jobs:
  # CI: is this commit good?
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release

  # Build the artifact once. Everything downstream refers to this tag, never rebuilds it.
  image:
    needs: build-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      # Uses the token azure/login just obtained. No docker login, no registry password.
      - run: az acr login --name ${{ env.REGISTRY }}

      # No --platform flag: the runner is already amd64. That flag existed to work around
      # building on an Apple Silicon laptop, which is not a problem CI has.
      - name: Build and push
        run: |
          docker build \
            -f src/Catalog/BrickShare.Catalog.Api/Dockerfile \
            -t ${{ env.REGISTRY }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }} \
            .
          docker push ${{ env.REGISTRY }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }}

  # CD: make this commit live. Terraform owns the image field, so the deployment is a
  # variable change and nothing else.
  deploy:
    needs: image
    runs-on: ubuntu-latest
    env:
      ARM_USE_OIDC: true
      ARM_CLIENT_ID: ${{ vars.AZURE_CLIENT_ID }}
      ARM_TENANT_ID: ${{ vars.AZURE_TENANT_ID }}
      ARM_SUBSCRIPTION_ID: ${{ vars.AZURE_SUBSCRIPTION_ID }}
    defaults:
      run:
        working-directory: infra
    steps:
      - uses: actions/checkout@v4

      - uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: 1.15.8

      - run: terraform init -input=false

      - run: terraform apply -auto-approve -input=false -var="image_tag=${{ github.sha }}"

      - run: echo "Deployed $(terraform output -raw web_app_url)" >> "$GITHUB_STEP_SUMMARY"
```

### The beats, block by block

**`permissions: id-token: write`** is not a default, and forgetting it is the single most common
first-run failure. Without it the job cannot request a token, and every Azure step fails before
it does anything. Name it here rather than burying it in a troubleshooting section.

**`concurrency`** — two pushes in quick succession means two `terraform apply` calls against one
state file. The state blob's lease prevents corruption; this prevents the second run failing on
the lease. Not exciting, and the alternative is a red build caused by nothing but timing.

**`needs:` is where CI ends and CD begins**, and it is worth pointing at on screen rather than
defining abstractly. `build-test` answers *is this commit good* — it compiles and the tests pass,
and it would run identically if nobody ever deployed. `deploy` answers *make this commit live*.
The `image` job in the middle belongs to neither and to both: it produces the artifact that CI
approved and CD promotes.

**`${{ github.sha }}`, never `latest`.** With `latest`, "what is running in production right
now" has no answer you can trust, and rollback means rebuilding and hoping. With a commit SHA,
the running container points at exactly one commit — you can diff it, blame it, and redeploy the
previous one by name. This is also what *built once, promoted* means concretely: the `deploy`
job never rebuilds anything, it names the tag `image` already pushed.

**No `--platform linux/amd64`.** Episodes 6 and 8 both needed it and it is gone here. Worth ten
seconds: that was never an Azure problem, it was an Apple-Silicon-laptop problem, and the runner
is amd64 already. CI removed the entire category.

**`az acr login` with no password, again.** Same move as episode 8, except the token now comes
from a credential the workflow was handed rather than one a human logged in for. Four steps in
this episode touch Azure and none of them present a password.

**The double compile, admitted rather than hidden.** `build-test` compiles the code, then the
Dockerfile compiles it again inside the image. That is genuinely wasteful. The alternative —
build the image first, then run the tests inside it — is a legitimate design that trades away
the fast, clear feedback of a plain `dotnet test` failure. **Say that it is a real cost and that
nothing here is slow enough yet to pay for fixing it.** A course that pretends its example is
optimal teaches students not to look.

## Step 5 — Push, and watch it deploy

```bash
git add .github/workflows/deploy.yml infra/main.tf
git commit -m "Deploy the catalog API from GitHub Actions"
git push
```

Watch the three jobs go green in order. Open the deploy job's summary for the URL, and hit `/`,
`/health/live` and `/health/ready` — the same three responses as every episode since 3.

**Close on the contrast, because it is the point of the last eight episodes:** "Every deployment
in this course until now happened because a person ran a command. This one happened because a
commit exists. That is the entire difference, and everything we build from here lands the same
way."

## Failure modes worth pre-empting

Quoted verbatim, so they are recognisable rather than mysterious:

| Symptom | Cause |
| --- | --- |
| `Unable to get ACTIONS_ID_TOKEN_REQUEST_URL` | `permissions: id-token: write` is missing from the workflow |
| `AADSTS70021: No matching federated identity record found for the given subject` | The `subject` does not match — wrong repository capitalisation, wrong branch, or the run was triggered by a pull request or a tag |
| `403 ... AuthorizationPermissionMismatch` on `terraform init` | The service principal is missing `Storage Blob Data Contributor` — the same 403 as episode 7, now for a different identity |
| `terraform apply` proposing changes unrelated to the tag | Local drift: something was applied from a laptop that the pipeline is now reconciling away |
| `AuthorizationFailed` on the `azurerm_role_assignment` | `Role Based Access Control Administrator` missing, or not yet propagated — RBAC can take a minute or two |

## What this episode is not

**No environments, approvals, deployment slots, smoke tests or rollback.** All of that is
episode 30, which exists to make this pipeline *safe* rather than merely *working*. Shipping
straight to a live app on every push is exactly right for a course environment with one
consumer, and would be reckless with real customers — the difference is worth stating out loud
now so nobody copies this workflow into a job.

**No plan-on-pull-request.** Genuinely valuable, and it wants the quality gates from episodes 10
and 11 to exist first, so a pull request check has something to enforce beyond "it compiled."

**No dev-to-prod promotion.** There is one environment. *Built once and promoted* is
demonstrated here by the deploy job reusing the image job's tag, not by a second environment that
does not exist yet.

**No NuGet or Docker layer caching.** Both would make this faster and both would add moving parts
to the episode that introduces the pipeline. The build is under a couple of minutes; speed is not
the problem being solved.

## Verification

The pipeline is its own verification, which is the point — but three things are worth checking
explicitly on camera:

1. **The Secrets page is empty.** Not "contains only what it needs" — empty.
2. **The deployed image tag is a commit SHA**, visible in the portal's Deployment Center or via
   `az webapp config container show --name app-brickshare-catalog-dev --resource-group
   rg-brickshare-dev`. Match it against the commit that triggered the run.
3. **A second push deploys again with no manual step.** One green run could be luck; two is a
   loop.

## Next

Episode 10 — Consistency and strictness: `.editorconfig`, `Directory.Build.props` and
warnings-as-errors. The pipeline exists now, so a rule introduced there fails somebody's push
from the moment it lands — which is the whole reason the quality gates waited until after this
episode rather than coming before it.

# Episode 6 — The Azure portal, by hand — then delete it all

← [Course plan](catalog-api.md) · Previous: [Episode 5 — One image, run locally](episode-5.md)

No repository changes. This episode's only artifacts are what viewers understand and a
resource group that exists for the length of the recording, then doesn't.

**Done when** the episode 5 image is running on App Service, its logs and settings have been
shown on camera, and the resource group has been deleted with the deletion visible.

This document is a **shot-by-shot script** — what to click, what to say, and why each thing
gets shown before episode 7 replaces all of it with Terraform.

## Before recording

- An Azure subscription, logged into the portal.
- A Docker Hub account. The episode 5 image needs to live somewhere the portal can pull it
  from, and creating an Azure Container Registry here would be a fourth resource this episode
  never names — Docker Hub keeps the resource list exactly what the course plan says it is:
  **a resource group, a plan, a web app.**
- The episode 5 image, built locally (`docker compose build` from the repo root).

### Get the image somewhere pullable

Off camera, or as a quick cold open — this is prerequisite plumbing, not the lesson. **Build
for `linux/amd64` explicitly** — don't tag and push whatever `docker compose build` produced
in episode 5:

```bash
docker build --platform linux/amd64 \
  -f src/Catalog/BrickShare.Catalog.Api/Dockerfile \
  -t <your-dockerhub-username>/brickshare-catalog-api:episode-6 \
  .
docker login
docker push <your-dockerhub-username>/brickshare-catalog-api:episode-6
```

**Say, on the `--platform` flag — this one's worth explaining rather than typing silently:**
"Azure App Service Linux plans run **amd64** only. `docker compose build` in episode 5 built
for whatever CPU is under this machine — arm64 if this is being recorded on an Apple Silicon
Mac, amd64 on an Intel Mac, Windows or Linux box. On amd64 hardware that image would happen to
already be right; on Apple Silicon it would be arm64, and Azure can't run that binary at all.
It's not a slow start or a missed setting — the container process fails before it does
anything, in about a second, because the CPU instructions themselves don't match the host.
That's `ContainerStartupFailure`, exit code 255, in the App Service logs: the fastest, most
opaque-looking failure in this whole episode, and it's just an architecture mismatch.
Hardcoding `--platform linux/amd64` here means this command is correct regardless of what
machine recorded it."

**This also sharpens a claim from episode 5, rather than repeating it unchanged.** Episode 5's
whole case for multi-stage Docker builds was dev/prod parity — the same image running
everywhere. That's still true of the Dockerfile and the source: same recipe, same layers,
nothing rewritten for Azure. It stops being true **byte-for-byte** the moment the recording
machine and Azure don't share a CPU architecture. Worth saying precisely rather than letting
the stronger version stand: parity here means the same build produces the right artifact for
wherever it's asked to run, not that the bytes pushed are identical to the bytes that ran
locally on a different chip.

## The names used on camera

| Resource | Name | Why this name |
| --- | --- | --- |
| Resource group | `rg-brickshare-portal-demo` | `-demo` is doing real work: it should look, at a glance, obviously temporary. Nobody watching should wonder whether this is the resource group Terraform expects to find in episode 7 — it is not, and the name says so before anyone has to explain it. |
| App Service Plan | `plan-brickshare-demo` | Same reasoning. |
| Web App | `app-brickshare-catalog-demo` | Same reasoning. |

Episode 7 chooses its own names as part of designing the Terraform module. Nothing here is
meant to be reused.

---

## Part 1 — Create the resource group

**Portal → Resource groups → Create.**

- Subscription: whichever the course uses.
- Resource group name: `rg-brickshare-portal-demo`.
- Region: pick one and **say it out loud** — every resource created after this inherits it
  unless overridden, and that's worth students hearing stated rather than left implicit.

**Say:** "A resource group isn't a server and isn't a service — it's a folder. Everything
we create in the next ten minutes goes in this one folder, which means deleting the folder
deletes everything in it. That's not a side effect, that's the reason it's structured this
way, and it's why the episode ends the way it does."

Click **Review + create → Create**. Wait for the notification.

---

## Part 2 — Create the App Service Plan, on its own, first

**Portal → App Service Plans → Create.**

- Resource group: `rg-brickshare-portal-demo`.
- Name: `plan-brickshare-demo`.
- Operating System: **Linux.**
- Region: same as the resource group.
- Pricing plan: **Basic B1.**

**Say, while on the pricing tier picker:** "The Free tier is tempting and it's the wrong
choice here, for a specific reason: F1 doesn't support custom Docker containers on Linux App
Service. Basic B1 is the cheapest tier that does. This isn't a course opinion about
production sizing — it's a hard requirement for what we're about to do."

**This is the moment to teach the distinction the whole episode is really about:**

**Say:** "Stop here before creating anything else. What did we just create? Not a website.
Not our API. We rented **a machine** — CPU, memory, a Linux kernel it's running on — and gave
it a name. Nothing is deployed to it yet. It has no idea our container exists. That's the
whole distinction: **the plan is the machine you're renting; the app is what runs on it.** One
plan can hold several apps, and every app on it shares that machine's CPU and memory. We're
about to prove that by putting two apps on this one plan."

Click **Review + create → Create**. Wait for the notification.

---

## Part 3 — Create the Web App, on that plan, pointed at the container

**Portal → App Services → Create → Web App.**

**Basics tab:**
- Resource group: `rg-brickshare-portal-demo`.
- Name: `app-brickshare-catalog-demo`. (This becomes part of the URL —
  `app-brickshare-catalog-demo.azurewebsites.net` — point that out when it appears.)
- Publish: **Container.**
- Operating System: **Linux.**
- Region: same as before.
- App Service Plan: select the **existing** `plan-brickshare-demo`. Don't let it create a new
  one — the whole point of Part 2 was building this one first.

**Container tab:**
- Image source: **Docker Hub.**
- Access type: Public (assuming a public repo — say so, and mention that a private one needs
  credentials here, which is exactly the kind of secret episode 9's "no secrets in
  configuration" habit and episode 8's OIDC setup exist to keep out of a repo).
- Image and tag: `<your-dockerhub-username>/brickshare-catalog-api:episode-6`.
- **Port: `8080`.** Defaults to `80` — change it here, now, before moving on.

**Say, and slow down on this one:** "This Port field is the one to actually stop on. Every
other field on this tab is obviously required — you can't skip the image reference. This one
looks optional, defaults to `80`, and nothing about the form complains if you leave it there.
Our container listens on `8080` — that's `ASPNETCORE_HTTP_PORTS=8080`, baked into the base
image, `EXPOSE 8080` in the Dockerfile documenting it. Leave this field at `80` and the
container will start completely normally — the logs will show `Application started`, nothing
looks wrong at the app level — but the platform spends the next 230 seconds probing a port
nothing is listening on, gives up, and every request times out or comes back `504`. That's the
single most likely reason this episode goes wrong, and the fix is this one field, right here,
before **Review + create** — not something to discover afterward from a confusing timeout."

Click **Review + create → Create**. This step takes a minute or two — a natural place for a
cut, but let the first pull happen live at least once so viewers see it isn't instant.

---

## Part 4 — Watch it come up, and prove it's the same app

Once deployment finishes, click **Browse**, or go straight to the three URLs episodes 3 and 5
already exercised:

```
https://app-brickshare-catalog-demo.azurewebsites.net/
https://app-brickshare-catalog-demo.azurewebsites.net/health/live
https://app-brickshare-catalog-demo.azurewebsites.net/health/ready
```

**Say:** "Same three responses episode 5 got from `docker compose up` on a laptop. Nothing
about the app changed to get here — only where the container is running."

### If it doesn't come up: fixing a missed port field after the fact

Show this even if the Container tab's Port field got set correctly the first time — it's the
recovery path for the single most likely mistake in this episode, and it's worth having on
camera once.

If Port was left at `80` back in Part 3, the container itself starts fine — Log stream shows
`Application started` like nothing's wrong — but every request times out, and eventually the
platform gives up on the container entirely:

```
Container did not respond to startup probe on port 80 within the expected time limit of 230s.
Port mismatch detected: the container is listening on port 8080 but the platform is probing
port 80. Set the WEBSITES_PORT app setting to match the port your application listens on.
```

**Say, if this comes up:** "Recognise this. This is the Port field from Part 3, just showing
up somewhere else. The container is fine — it's telling you exactly what's wrong: it's
listening on 8080, the platform is checking 80. Fixable without recreating anything."

**Portal → the Web App → Settings → Environment variables (Application settings).**

Add a setting named exactly `WEBSITES_PORT` (plural "WEBSITES" — `WEBSITE_PORT` is a real,
easy typo that silently does nothing), value `8080`. This is the same setting the Container
tab's Port field writes for you at creation time, just reachable after the fact.

**Then click Save at the top of the whole Configuration page — not just OK in the flyout —
and confirm the restart prompt.** This is the second place people get stuck: adding the
setting via **+ New application setting** and clicking **OK** only stages it. The list looks
identical whether it's staged or saved, right up until the page is reloaded. Skip the
top-level Save and the exact same probe-timeout error reappears, looking exactly like the
setting was never added at all.

**Say:** "Our container's Dockerfile sets `ASPNETCORE_HTTP_PORTS=8080` and `EXPOSE 8080` —
that's what tells Kestrel and Docker what port matters, and it's what the Container tab's Port
field and this `WEBSITES_PORT` setting both exist to tell the platform. Two names for the same
knob: one set at creation, one set here, after the fact."

While in this blade, also point at where `ASPNETCORE_ENVIRONMENT` would go if it were set —
it isn't, so the app is running in **Production**, same as `docker-compose.yml`'s comment from
episode 5 predicted it would be.

### Log stream

**Portal → the Web App → Monitoring → Log stream.**

**Say:** "This is the container pull, and then Kestrel starting — `Now listening on:
http://+:8080`, `Application started`. Identical lines to what showed up in the terminal in
episode 3, and in `docker compose up`'s output in episode 5. Same app, same startup, third
place it's run."

---

## Part 5 — Prove one plan holds many apps (optional, but the payoff of Part 2)

If there's time, this is the beat that makes "one plan, many apps" concrete instead of
asserted.

**Portal → App Services → Create → Web App**, same **Basics** flow, but:
- Name: `app-brickshare-second-demo`.
- App Service Plan: the **same** `plan-brickshare-demo`.
- Any container works here — reusing the same image is fine, or the platform's own quickstart
  image.

Once it's up, open `plan-brickshare-demo` in the portal and look at its **Apps** blade —
**two apps now listed, one plan.**

**Say:** "Both of these are running on the machine we rented in Part 2. Scale that plan up and
both apps get more CPU. Scale it down and both feel it. This is why 'which plan is this app
on' is a real production question and not trivia — apps on the same plan share fate."

Delete `app-brickshare-second-demo` immediately after this beat (**Delete**, confirm the name).
It was only ever there to make the point.

---

## Part 6 — Delete the resource group

**Portal → Resource groups → `rg-brickshare-portal-demo` → Delete resource group.**

Type the resource group's name to confirm. Watch the deletion progress. Once it's gone,
navigate back to **Resource groups** and show it's no longer in the list.

**Say, plainly, to close the episode:** "Everything from here forward is created by Terraform.
If we left this resource group sitting next to what Terraform creates in episode 7, we'd have
a resource Terraform doesn't know about and can't manage — and a hand-made resource sitting
next to a Terraform-managed one is exactly how a state file starts lying about what's really
out there. So it goes. Before you let `terraform apply` create anything in the next episode,
you should be able to recognise every single resource it's about to make — and now you can,
because you just made them yourself."

---

## Recording checklist

A shot list for the editor, matched to the parts above:

- [ ] Resource group created — name and region spoken on camera
- [ ] App Service Plan created — **pause here**, the plan-vs-app explanation
- [ ] Pricing tier picker shown with F1 vs B1 called out
- [ ] Web App created, container tab showing the Docker Hub image reference
- [ ] **Port field set to `8080` on the Container tab — the moment to slow down for**
- [ ] First container pull, live, not cut
- [ ] All three URLs (`/`, `/health/live`, `/health/ready`) hit and shown
- [ ] `WEBSITES_PORT` recovery path shown (even if not needed — say so), including the
      top-level Save, not just the flyout's OK
- [ ] Log stream open, showing the same Kestrel startup lines as episodes 3 and 5
- [ ] *(optional)* second app on the same plan, then the Plan's Apps blade showing both
- [ ] *(optional)* second app deleted
- [ ] Resource group deletion, confirmed by name, shown completing
- [ ] Resource group absent from the list, on camera, as proof

## What this episode is not

No Terraform, no CLI, no infrastructure-as-code of any kind — that's episode 7, and the whole
value of this episode is contrast: a viewer who has clicked through this by hand feels episode
7's `terraform plan` output completely differently than one who hasn't.

No app settings beyond `WEBSITES_PORT`, no custom domain, no scaling rules, no deployment
slots. Those either don't exist yet for a reason (see the architecture doc for what Terraform
will eventually configure) or belong to a specific later episode — slots are episode 29,
health-probe wiring to `/health/ready` returns properly in episode 7 once it's declared in
code rather than clicked.

## Verification

There's no `dotnet build` for this episode. The check is narrower and stricter: could someone
who only watched this recording look at episode 7's Terraform plan output and recognise every
resource type in it before `apply` runs? If yes, the episode did its job.

## Next

[Episode 7 — Terraform: recreate it declaratively](catalog-api.md): the same resource group,
plan and web app, written as HCL — and the remote-state bootstrap problem that comes with
writing infrastructure this way.

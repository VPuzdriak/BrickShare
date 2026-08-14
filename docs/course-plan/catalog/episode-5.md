# Episode 5 — One image, run locally

← [Course plan](catalog-api.md) · Previous: [Episode 4 — The first test](episode-4.md)

A multi-stage `Dockerfile`, a `.dockerignore`, and a `docker-compose.yml` with one service.

**Done when** `docker compose up` serves the same endpoints `dotnet run` did.

## The idea this episode teaches

Up to now, "the app" has meant source code that `dotnet run` compiles and starts. From here
on, **the app is the container image** — the same image runs on a laptop today and on App
Service in episode 6. Dev/prod parity isn't a goal to aim for; it's a property that falls out
of testing and deploying the identical artifact, and it's why the Dockerfile is written before
Azure ever enters the picture.

## The Dockerfile

```dockerfile
# syntax=docker/dockerfile:1

# --- build ---
# The SDK image carries the whole toolchain (~800MB) and never ships. Only its output does.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, and copy nothing but the project file to do it. Docker caches each layer by
# its inputs: as long as this .csproj is unchanged, this layer — and the NuGet restore it
# triggers — is reused on every rebuild, however much application code just changed.
COPY src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj src/Catalog/BrickShare.Catalog.Api/
RUN dotnet restore src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj

# Now the rest of the source. This layer invalidates on every code change, which is fine —
# it's cheap. Restore, the expensive step, already happened above and stays cached.
COPY src/Catalog/BrickShare.Catalog.Api/ src/Catalog/BrickShare.Catalog.Api/
RUN dotnet publish src/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# --- final ---
# The aspnet image has the ASP.NET Core runtime only — no compiler, no SDK, no source.
# Roughly a third the size of the build image, and it's the one that actually ships.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The base image creates an unprivileged "app" user (uid 1654, via $APP_UID) but runs as
# root until asked not to. A process compromised through the network gets that user's
# privileges — root by default is a container escape away from the host, "app" is not.
USER $APP_UID

COPY --from=build /app/publish .

# ASPNETCORE_HTTP_PORTS=8080 is already set by the base image; Kestrel listens on it without
# further configuration. EXPOSE documents that for anyone reading the file — it does not
# publish the port itself, docker-compose.yml's `ports:` does that.
EXPOSE 8080

ENTRYPOINT ["dotnet", "BrickShare.Catalog.Api.dll"]
```

### Two stages, and why the split is the whole point

A single-stage build — `FROM sdk`, compile, run — works and ships the compiler, the source
tree and every restore artifact in the image that reaches production. The **multi-stage**
build compiles in one image and copies only the *output* — a handful of DLLs — into a second,
much smaller image that never had a compiler in it.

Measured on this project:

| Image | Size |
| --- | --- |
| `dotnet/sdk:10.0` (build stage) | 944 MB |
| `dotnet/aspnet:10.0` (final stage, base) | 262 MB |
| `brickshare-catalog-api` (final, published) | 262 MB |

The published app adds only a few DLLs to the runtime base — too small to move the number at
this resolution. The size that matters is the one that never ships: **944 MB of compiler and
SDK stays in the build stage and is discarded**, because `COPY --from=build` only takes
`/app/publish`, not the stage it came from.

### Layer order is a deliberate caching decision, not file-copying convenience

`COPY *.csproj` then `dotnet restore` then `COPY` everything else then `dotnet publish` looks
like it copies twice for no reason. It doesn't: Docker caches a layer by its inputs, and the
`.csproj` changes far less often than the source does. Restore — the step that hits NuGet and
can take real time — stays cached across every rebuild that only touched a `.cs` file. Copy
everything up front instead, and every source edit invalidates restore too.

This matters more once there's more than one project. When episode 12 adds a `Domain` project,
this same layer-ordering pattern is what keeps the whole chain fast — copy every `.csproj`
first, restore once, then copy source.

### The non-root user is not decoration

`docker run --rm mcr.microsoft.com/dotnet/aspnet:10.0 whoami` answers `root`. The base image
ships an unprivileged `app` user (`$APP_UID`, uid 1654) but doesn't switch to it — that's an
opt-in, and skipping it is exactly how "official base image" gets mistaken for "already
secure." One line, `USER $APP_UID`, is the whole fix. Verified on this build:

```
$ docker compose exec catalog-api whoami
app
```

An ASP.NET Core process has no reason to hold root inside its own container, and a process
that's been exploited through the network inherits whatever user it's running as. Root in a
container is one escape away from root on the host; `app` is not.

## `.dockerignore`

```
**/bin/
**/obj/
**/.vs/
**/.idea/
.git/
.github/

tests/
docs/
*.md

global.json
```

The Dockerfile only ever `COPY`s files it names — `tests/` was never going to end up in the
image regardless. What `.dockerignore` controls is the **build context upload**: everything in
the repo that isn't excluded gets sent to the Docker daemon *before* any `COPY` filtering
happens, on every build. Measured here, with the ignore file in place:

```
transferring context: 582B
```

Without it, that number is the size of `tests/obj/`, `.git/`, and every markdown file in
`docs/` — megabytes, on every single build, for bytes that were always going to be discarded.

**`global.json` is excluded on purpose, not by oversight.** It pins the SDK for the `dotnet`
CLI running on a developer's machine. Inside the container, the `FROM mcr.microsoft.com/dotnet/sdk:10.0`
line **is** the SDK pin — there's exactly one SDK installed, so a `global.json` copied in would
have nothing to choose between. Two pinning mechanisms, one for each environment, each doing
the same job in the place that needs it.

## `docker-compose.yml`

```yaml
services:
  catalog-api:
    build:
      context: .
      dockerfile: src/Catalog/BrickShare.Catalog.Api/Dockerfile
    ports:
      - "5080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
```

**Port 5080 on the host** is the same port `launchSettings.json` uses for `dotnet run` (episode
2). A student doesn't learn a second URL for the container — `http://localhost:5080/` works
either way, and that's deliberate, not a coincidence of both defaulting to the same number.
**Port 8080 in the container** is `ASPNETCORE_HTTP_PORTS`, set by the base image; nothing in
this project's code sets it.

**`ASPNETCORE_ENVIRONMENT: Development` is explicit and, right now, inert** — nothing branches
on environment yet. It's set anyway because this file describes the local inner loop, and
Azure (episode 6 on) will run the same image with no environment variable set, which means
**Production** by default. Naming the difference now, while it changes nothing, means the day
it does change something, it's expected rather than discovered.

### What this episode does not add: a Compose healthcheck

The natural next line is a `healthcheck:` block calling `/health/live`. It doesn't appear here,
and the reason is concrete: the `aspnet:10.0` runtime image ships neither `curl` nor `wget`
(checked directly — `which curl` and `which wget` both return nothing), so a container-level
healthcheck would mean installing one, which grows the "small runtime base" this episode just
spent its whole first half building. Trading that away for a healthcheck with nothing yet
depending on it isn't worth it. `depends_on: condition: service_healthy` becomes relevant in
episode 15, when Postgres joins with an official image that has healthcheck tooling built in —
and it's Postgres's own health that a compose-level check will need to express, not this
service's.

## Verified end to end

```
$ docker compose build
...
 Image brickshare-catalog-api Built

$ docker compose up -d
...
 Container brickshare-catalog-api-1 Started

$ curl http://localhost:5080/
{"service":"BrickShare Catalog API"}
$ curl http://localhost:5080/health/live
Healthy
$ curl http://localhost:5080/health/ready
Healthy

$ docker compose exec catalog-api whoami
app
```

Same three responses episode 3 verified with `dotnet run`, from the container instead.

## Verification

```bash
docker compose build
docker compose up -d

curl http://localhost:5080/               # {"service":"BrickShare Catalog API"}
curl http://localhost:5080/health/live    # Healthy
curl http://localhost:5080/health/ready   # Healthy

docker compose exec catalog-api whoami    # app, not root

docker compose down
```

## Next

[Episode 6 — The Azure portal, by hand — then delete it all](episode-6.md): this same image,
pushed and run on App Service, clicked together first so the building blocks are visible
before anything generates them.

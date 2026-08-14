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

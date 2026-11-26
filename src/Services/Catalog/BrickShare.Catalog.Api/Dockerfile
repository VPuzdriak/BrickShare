FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BuildingBlocks/BrickShare.Auth/BrickShare.Auth.csproj src/BuildingBlocks/BrickShare.Auth/
COPY src/BuildingBlocks/BrickShare.SharedKernel/BrickShare.SharedKernel.csproj src/BuildingBlocks/BrickShare.SharedKernel/
COPY src/Services/Catalog/BrickShare.Catalog.Domain/BrickShare.Catalog.Domain.csproj src/Services/Catalog/BrickShare.Catalog.Domain/
COPY src/Services/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj src/Services/Catalog/BrickShare.Catalog.Api/
RUN dotnet restore src/Services/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Catalog/ src/Services/Catalog/
RUN dotnet publish src/Services/Catalog/BrickShare.Catalog.Api/BrickShare.Catalog.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "BrickShare.Catalog.Api.dll"]

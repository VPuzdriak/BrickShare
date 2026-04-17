FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Rent/src/BrickShare.Rent.Api/BrickShare.Rent.Api.csproj", "Rent/src/BrickShare.Rent.Api/"]
RUN dotnet restore "Rent/src/BrickShare.Rent.Api/BrickShare.Rent.Api.csproj"
COPY . .
WORKDIR "/src/Rent/src/BrickShare.Rent.Api"
RUN dotnet build "./BrickShare.Rent.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BrickShare.Rent.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BrickShare.Rent.Api.dll"]

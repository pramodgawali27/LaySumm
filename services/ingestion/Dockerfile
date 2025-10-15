# Simplified container definition for the ingestion service.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY services/ingestion/src/Ingestion.Api/Ingestion.Api.csproj ./services/ingestion/src/Ingestion.Api/
COPY pkg/platform/Platform.Api/Platform.Api.csproj ./pkg/platform/Platform.Api/
RUN dotnet restore services/ingestion/src/Ingestion.Api/Ingestion.Api.csproj
COPY services/ingestion/ ./services/ingestion/
COPY pkg/platform/ ./pkg/platform/
RUN dotnet publish services/ingestion/src/Ingestion.Api/Ingestion.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ingestion.Api.dll"]

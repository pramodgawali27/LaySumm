# Simplified container definition for the Summarizer A3 (RAG) service.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY services/summarizer-a3/src/SummarizerA3.Api/SummarizerA3.Api.csproj ./services/summarizer-a3/src/SummarizerA3.Api/
COPY pkg/platform/Platform.Api/Platform.Api.csproj ./pkg/platform/Platform.Api/
RUN dotnet restore services/summarizer-a3/src/SummarizerA3.Api/SummarizerA3.Api.csproj
COPY services/summarizer-a3/ ./services/summarizer-a3/
COPY pkg/platform/ ./pkg/platform/
RUN dotnet publish services/summarizer-a3/src/SummarizerA3.Api/SummarizerA3.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SummarizerA3.Api.dll"]

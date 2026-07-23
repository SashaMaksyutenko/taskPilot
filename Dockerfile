# Multi-stage build for the Taskpilot.API ASP.NET Core application.
# Build context is the repository root (see docker-compose.yml).

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first so this layer is cached unless a project file changes. The API
# references Taskpilot.Contracts and Taskpilot.Integrations, so those projects must be
# present before the restore too.
COPY src/Taskpilot.Contracts/Taskpilot.Contracts.csproj src/Taskpilot.Contracts/
COPY src/Taskpilot.Integrations/Taskpilot.Integrations.csproj src/Taskpilot.Integrations/
COPY src/Taskpilot.API/Taskpilot.API.csproj src/Taskpilot.API/
RUN dotnet restore src/Taskpilot.API/Taskpilot.API.csproj

# Copy the remaining source (all referenced projects) and publish a Release build.
COPY src/Taskpilot.Contracts/ src/Taskpilot.Contracts/
COPY src/Taskpilot.Integrations/ src/Taskpilot.Integrations/
COPY src/Taskpilot.API/ src/Taskpilot.API/
RUN dotnet publish src/Taskpilot.API/Taskpilot.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Listen on 8080 by default (docker-compose), but honour the PORT variable that managed
# hosts such as Railway inject — they route traffic to that port only. The shell form of
# ENTRYPOINT is required so ${PORT} is expanded at runtime rather than baked in at build.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet Taskpilot.API.dll"]

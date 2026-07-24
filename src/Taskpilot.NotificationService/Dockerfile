# Multi-stage build for the Taskpilot.NotificationService worker.
# Build context is the repository root (see docker-compose.yml).

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached unless a project file changes). The worker references the shared
# Contracts and Integrations projects, so both must be present before the restore.
COPY src/Taskpilot.Contracts/Taskpilot.Contracts.csproj src/Taskpilot.Contracts/
COPY src/Taskpilot.Integrations/Taskpilot.Integrations.csproj src/Taskpilot.Integrations/
COPY src/Taskpilot.NotificationService/Taskpilot.NotificationService.csproj src/Taskpilot.NotificationService/
RUN dotnet restore src/Taskpilot.NotificationService/Taskpilot.NotificationService.csproj

# Copy the remaining source (all three projects) and publish a Release build.
COPY src/Taskpilot.Contracts/ src/Taskpilot.Contracts/
COPY src/Taskpilot.Integrations/ src/Taskpilot.Integrations/
COPY src/Taskpilot.NotificationService/ src/Taskpilot.NotificationService/
RUN dotnet publish src/Taskpilot.NotificationService/Taskpilot.NotificationService.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
# A background worker: no web server, so the smaller runtime image (not aspnet) is enough.
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Taskpilot.NotificationService.dll"]

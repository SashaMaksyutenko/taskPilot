# Multi-stage build for the Taskpilot.API ASP.NET Core application.
# Build context is the repository root (see docker-compose.yml).

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first so this layer is cached unless the project file changes.
COPY src/Taskpilot.API/Taskpilot.API.csproj src/Taskpilot.API/
RUN dotnet restore src/Taskpilot.API/Taskpilot.API.csproj

# Copy the remaining source and publish a Release build.
COPY src/Taskpilot.API/ src/Taskpilot.API/
RUN dotnet publish src/Taskpilot.API/Taskpilot.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Listen on port 8080 inside the container (configuration comes from env vars).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Taskpilot.API.dll"]

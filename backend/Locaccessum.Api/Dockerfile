# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the full solution (projects reference each other).
# Directory.Build.props lives at the repo root and supplies TargetFramework/
# Nullable/TreatWarningsAsErrors/etc. to every project, so it must be copied
# in alongside the solution/project files, before the rest of the sources.
COPY Directory.Build.props ./
COPY backend/Locaccessum.sln backend/
COPY backend/Locaccessum.Domain/Locaccessum.Domain.csproj backend/Locaccessum.Domain/
COPY backend/Locaccessum.Infrastructure/Locaccessum.Infrastructure.csproj backend/Locaccessum.Infrastructure/
COPY backend/Locaccessum.Api/Locaccessum.Api.csproj backend/Locaccessum.Api/
COPY backend/Locaccessum.Tests/Locaccessum.Tests.csproj backend/Locaccessum.Tests/
RUN dotnet restore backend/Locaccessum.Api/Locaccessum.Api.csproj

# Copy the rest of the sources and publish.
COPY backend/ backend/
RUN dotnet publish backend/Locaccessum.Api/Locaccessum.Api.csproj -c Release -o /app --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as the non-root user shipped in the base image.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Locaccessum.Api.dll"]

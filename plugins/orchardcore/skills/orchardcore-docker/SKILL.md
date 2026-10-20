---
name: orchardcore-docker
description: Skill for containerizing Orchard Core with Docker. Covers multi-stage and multi-platform Dockerfiles, dockerignore configuration, docker compose with multiple database providers, persistent App_Data and temporary file volumes, configuring Orchard Core 4 through environment variables with the renamed configuration sections, scaling containers with shared media storage, data protection keys, Redis and SignalR backplanes, HTTPS in containers, non-root images, environment-specific targeting, image optimization, and CI/CD. Use this skill when requests mention Orchard Core Docker, containerizing Orchard Core, Dockerfile, Dockerfile-CI, TARGETOS, docker compose, container environment variables, ORCHARD_APP_DATA, temporary file storage in containers, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include OrchardCore.Cms.Web, OrchardCore.Cms.Web.dll, and docker-compose.yml.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Docker - Prompt Templates

## Containerize Orchard Core with Docker

You are an Orchard Core expert. Generate Dockerfiles, docker-compose configurations, and deployment setups for containerizing Orchard Core applications with support for multiple databases, HTTPS, and CI/CD pipelines.

### Guidelines

- Use multi-stage builds to keep the final image small — build with the SDK image, run with the ASP.NET runtime image.
- Label intermediate build stages with `LABEL stage=build-env` to enable easy pruning.
- Always include a `.dockerignore` file to exclude `App_Data/`, `bin/`, and `obj/` directories.
- Use `docker-compose` to orchestrate Orchard Core with database services (SQL Server, MySQL, PostgreSQL).
- For HTTPS in containers, mount ASP.NET Core developer certificates or use production certificates.
- Prune intermediate images after building to reclaim disk space.
- Target the appropriate .NET base images for your environment and framework version.
- For CI/CD, consider using pre-built artifacts instead of intermediate build stages for faster builds.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Multi-Stage Dockerfile

Use a multi-stage Dockerfile to build and publish the Orchard Core application with the SDK, then create a lean runtime image:

```dockerfile
# TARGETARCH and TARGETOS are set automatically when --platform is provided.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
ARG TARGETOS
LABEL stage=build-env
WORKDIR /source

# .dockerignore excludes App_Data and binaries from these.
COPY ./src ./src
COPY Directory.Build.props .
COPY Directory.Packages.props .

RUN dotnet publish src/OrchardCore.Cms.Web/OrchardCore.Cms.Web.csproj -c Release -o /app --framework net10.0 /p:RunAnalyzers=false

# Pick the runtime image for the target OS.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-nanoserver-ltsc2025 AS build_windows
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS build_linux
FROM build_${TARGETOS} AS aspnet

EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
WORKDIR /app
COPY --from=build-env /app/ .
ENTRYPOINT ["dotnet", "OrchardCore.Cms.Web.dll"]
```

This is the Dockerfile of the Orchard Core repository. Copy
`Directory.Build.props` and `Directory.Packages.props` (central package
management) when your solution has them, or the restore fails. Build a specific
platform with `docker buildx build --platform linux/amd64 -t orchardcore-app .`.

### Custom Application Dockerfile

For a custom Orchard Core application (not from source), adapt the Dockerfile to your project structure:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
LABEL stage=build-env
WORKDIR /app

# Copy solution, build props and project files first for layer caching.
# Adjust to the files your solution has (.sln or .slnx, Directory.*.props).
COPY *.slnx Directory.Build.props Directory.Packages.props ./
COPY src/MyOrchardApp.Web/MyOrchardApp.Web.csproj ./src/MyOrchardApp.Web/
RUN dotnet restore src/MyOrchardApp.Web/MyOrchardApp.Web.csproj

# Copy full source and publish
COPY . .
RUN dotnet publish src/MyOrchardApp.Web -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
WORKDIR /app
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "MyOrchardApp.Web.dll"]
```

### Dockerignore File

Create a `.dockerignore` file at the solution root to exclude unnecessary files from the Docker build context:

```
# Ignore all
**

# Include source for building
!./src/*

# Ignore App_Data
**/App_Data/

# Ignore build artifacts
**/[b|B]in/
**/[O|o]bj/
```

### Building and Running with Docker

```bash
# Navigate to the folder containing the Dockerfile
cd /orchardcore

# Build the image
docker build -t orchardcore-app .

# Run the container on port 80
docker run -p 80:80 orchardcore-app

# Build with intermediate image cleanup
docker build -t orchardcore-app --rm .

# Prune intermediate build images
docker image prune -f --filter label=stage=build-env
```

### Docker Compose with Multiple Databases

Use `docker-compose.yml` to run Orchard Core alongside SQL Server, MySQL, and PostgreSQL services:

```yaml
services:
    web:
        build:
            context: .
            dockerfile: Dockerfile
        ports:
            - "5009:80"
        depends_on:
            - sqlserver
            - mysql
            - postgresql

    sqlserver:
        image: "mcr.microsoft.com/mssql/server"
        environment:
            SA_PASSWORD: "P@ssw0rd!123456"
            ACCEPT_EULA: "Y"

    mysql:
        image: mysql:latest
        restart: always
        environment:
            MYSQL_DATABASE: 'orchardcore_database'
            MYSQL_USER: 'orchardcore_user'
            MYSQL_PASSWORD: 'orchardcore_password'
            MYSQL_ROOT_PASSWORD: 'root_password'
        ports:
            - '3306:3306'
        expose:
            - '3306'
        volumes:
            - mysql-data:/var/lib/mysql

    postgresql:
        image: postgres:latest
        volumes:
            - postgresql-data:/var/lib/postgresql/data
        ports:
            - 5432:5432
        environment:
            POSTGRES_USER: orchardcore_user
            POSTGRES_PASSWORD: orchardcore_password
            POSTGRES_DB: orchardcore_database

volumes:
    mysql-data:
    postgresql-data:
```

### Docker Compose Commands

```bash
# Build images if not already built
docker-compose build

# Prune intermediate build images
docker image prune -f --filter label=stage=build-env

# Start all containers
docker-compose up

# Start in detached mode
docker-compose up -d

# Stop and remove containers
docker-compose down

# Rebuild and restart
docker-compose up --build
```

### Docker Compose with SQLite Only

A minimal `docker-compose.yml` for SQLite (no external database required):

```yaml
services:
    web:
        build:
            context: .
            dockerfile: Dockerfile
        ports:
            - "8080:80"
        volumes:
            - app-data:/app/App_Data

volumes:
    app-data:
```

`App_Data` holds tenant settings, SQLite databases, the local media store and
the default Data Protection keys, so it must be a persistent volume.

### Configuring Orchard Core with Environment Variables

Orchard Core 4.0 groups configuration sections by service, without the
`OrchardCore_` prefix, which keeps environment variable names short:

```yaml
services:
    web:
        image: orchardcore-app
        environment:
            ASPNETCORE_ENVIRONMENT: Production
            OrchardCore__Media__MaxFileSize: "104857600"
            OrchardCore__Media__Azure__ConnectionString: "${MEDIA_BLOB_CONNECTION}"
            OrchardCore__Media__Azure__ContainerName: "media"
            OrchardCore__DataProtection__Azure__ConnectionString: "${DP_BLOB_CONNECTION}"
            OrchardCore__Redis__Configuration: "redis:6379,abortConnect=false"
            OrchardCore__TempDirectory__Path: "/app/data/temp"
        volumes:
            - app-data:/app/App_Data
            - temp-data:/app/data/temp

volumes:
    app-data:
    temp-data:
```

- Old names such as `OrchardCore__OrchardCore_Media_Azure__ConnectionString` still work but are deprecated; the new name wins when both are set.
- `OrchardCore__TempDirectory__Path` moves chunked and TUS uploads and deployment and recipe archives off the container's small temp volume (`MediaOptions.TusTempPath` no longer exists). See `orchardcore-temp-file-storage`.
- `ORCHARD_APP_DATA` relocates the whole `App_Data` folder (for example to a mounted volume); do not use absolute paths in `Media:AssetsPath`.
- Pass secrets through the orchestrator's secret store or an `.env` file rather than hard-coding them. Tenant auto setup values follow the same pattern (`OrchardCore__AutoSetup__...`, see `orchardcore-auto-setup`).

### Running Several Containers

Container instances do not share local disks, so a scaled-out deployment needs:

| Concern | Use |
|---|---|
| Media files | `OrchardCore.Media.Azure.Storage` or `OrchardCore.Media.AmazonS3` (`orchardcore-azure-media`, `orchardcore-amazon-s3-media`) |
| Data Protection keys | `OrchardCore.DataProtection.Azure` or `OrchardCore.Redis.DataProtection` |
| Cache invalidation, locks | `OrchardCore.Redis.Bus`, `OrchardCore.Redis.Cache`, `OrchardCore.Redis.Lock` |
| Media Gallery real-time updates | `OrchardCore.SignalR.Redis` or `OrchardCore.SignalR.Azure` |
| TUS resumable uploads | Shared `TempDirectory:Path` volume (NFS, Azure Files, EFS), cloud media storage, or sticky sessions |
| Tenant settings | `AddDatabaseShellsConfiguration()` or `AddAzureShellsConfiguration()` (see `orchardcore-shells`), or a shared `App_Data` volume |

```yaml
services:
    web:
        image: orchardcore-app
        deploy:
            replicas: 3
        environment:
            OrchardCore__Redis__Configuration: "redis:6379,abortConnect=false"
            OrchardCore__TempDirectory__Path: "/mnt/shared/temp"
        volumes:
            - shared-temp:/mnt/shared/temp

    redis:
        image: redis:7
        command: ["redis-server", "--appendonly", "yes"]
        volumes:
            - redis-data:/data

volumes:
    shared-temp:
    redis-data:
```

Enable `OrchardCore.Redis`, `OrchardCore.Redis.Cache`, `OrchardCore.Redis.Bus`,
`OrchardCore.Redis.Lock`, `OrchardCore.Redis.DataProtection` and
`OrchardCore.SignalR.Redis` in the tenant (see `orchardcore-redis` and
`orchardcore-media-gallery`). Redis must persist data (AOF or RDB) because it
holds the Data Protection keys.

### HTTPS in Containers

For HTTPS deployment in Docker containers, refer to the ASP.NET Core documentation for hosting with HTTPS. Common approaches include:

1. **Development certificates**: Mount ASP.NET Core dev certificates into the container.
2. **Production certificates**: Mount a PFX certificate and configure Kestrel.
3. **Reverse proxy**: Terminate TLS at a reverse proxy (e.g., Nginx, Traefik) in front of the container.

Example with a PFX certificate:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
EXPOSE 80
EXPOSE 443
ENV ASPNETCORE_URLS="https://+:443;http://+:80"
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=YourCertPassword
WORKDIR /app
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "MyOrchardApp.Web.dll"]
```

Run with certificate volume:

```bash
docker run -p 443:443 -p 80:80 \
  -v ${HOME}/.aspnet/https:/https:ro \
  orchardcore-app
```

### Environment-Specific Targeting

Target specific .NET base images for different environments:

| Environment | SDK Image | Runtime Image |
|-------------|-----------|---------------|
| Linux x64 | `mcr.microsoft.com/dotnet/sdk:10.0` | `mcr.microsoft.com/dotnet/aspnet:10.0` |
| Linux Alpine | `mcr.microsoft.com/dotnet/sdk:10.0-alpine` | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` |
| Windows | `mcr.microsoft.com/dotnet/sdk:10.0-nanoserver-ltsc2025` | `mcr.microsoft.com/dotnet/aspnet:10.0-nanoserver-ltsc2025` |

Alpine-based images produce the smallest containers but may have compatibility
differences. Image resizing uses NetVips; its native libvips binaries come from
the `NetVips.Native` package, so add the matching `NetVips.Native.<rid>` package
to the web project when you publish for a runtime it does not cover.

### Non-Root Containers

The .NET 8+ images include an `app` user. To run without root, listen on an
unprivileged port and make the volumes writable by that user:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
WORKDIR /app
COPY --from=build-env /app/publish .
RUN mkdir -p /app/App_Data && chown -R $APP_UID /app/App_Data
USER $APP_UID
ENTRYPOINT ["dotnet", "MyOrchardApp.Web.dll"]
```

### CI/CD Considerations

For CI/CD pipelines (e.g., GitHub Actions), the application is typically built on the CI host rather than inside the Docker image. Use a simplified Dockerfile that copies pre-built artifacts:

```dockerfile
# A prerequisite is a published application in .build/release
FROM --platform=$BUILDPLATFORM golang:alpine AS build
ARG TARGETOS

FROM mcr.microsoft.com/dotnet/aspnet:10.0-nanoserver-ltsc2025 AS build_windows
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS build_linux
FROM build_${TARGETOS} AS aspnet

EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
WORKDIR /app
COPY .build/release /app
ENTRYPOINT ["dotnet", "OrchardCore.Cms.Web.dll"]
```

This mirrors the repository's `Dockerfile-CI`; the small `golang:alpine` stage
only lets BuildKit resolve `TARGETOS`. A single-platform pipeline can simply use
`FROM mcr.microsoft.com/dotnet/aspnet:10.0` and `COPY ./publish .`.

This avoids the intermediate build stage and produces faster image builds in the pipeline.

### Image Cleanup Best Practices

Intermediate build images accumulate disk space over time. Always prune them after building:

```bash
# Prune only Orchard Core build stage images
docker image prune -f --filter label=stage=build-env

# Prune all dangling images
docker image prune -f

# Full system cleanup (use with caution)
docker system prune -f
```

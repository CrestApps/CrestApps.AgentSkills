---
name: orchardcore-docker
description: Skill for containerizing Orchard Core with Docker. Covers Dockerfile creation with multi-stage builds, dockerignore configuration, docker-compose setup for multiple database providers, HTTPS deployment in containers, environment-specific targeting, image optimization, and CI/CD considerations.
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
# Build stage using .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
LABEL stage=build-env
WORKDIR /app

# Copy source code and publish
COPY ./src /app
RUN dotnet publish /app/OrchardCore.Cms.Web -c Release -o ./build/release

# Runtime stage using ASP.NET runtime only
FROM mcr.microsoft.com/dotnet/aspnet:10.0
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
WORKDIR /app
COPY --from=build-env /app/build/release .
ENTRYPOINT ["dotnet", "OrchardCore.Cms.Web.dll"]
```

### Custom Application Dockerfile

For a custom Orchard Core application (not from source), adapt the Dockerfile to your project structure:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
LABEL stage=build-env
WORKDIR /app

# Copy solution and project files first for layer caching
COPY *.sln .
COPY src/**/*.csproj ./src/
RUN dotnet restore

# Copy full source and publish
COPY . .
RUN dotnet publish src/MyOrchardApp.Web -c Release -o /app/publish

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
version: '3.3'
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
version: '3.3'
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
| Windows | `mcr.microsoft.com/dotnet/sdk:10.0-nanoserver-ltsc2022` | `mcr.microsoft.com/dotnet/aspnet:10.0-nanoserver-ltsc2022` |

Alpine-based images produce the smallest containers but may have compatibility differences.

### CI/CD Considerations

For CI/CD pipelines (e.g., GitHub Actions), the application is typically built on the CI host rather than inside the Docker image. Use a simplified Dockerfile that copies pre-built artifacts:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
WORKDIR /app
COPY ./publish .
ENTRYPOINT ["dotnet", "MyOrchardApp.Web.dll"]
```

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

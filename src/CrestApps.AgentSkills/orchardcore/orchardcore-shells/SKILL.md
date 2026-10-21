---
name: orchardcore-shells
description: Skill for Orchard Core shells and shell configuration providers. Covers ShellSettings, ShellContext, ShellScope, IShellHost and IShellSettingsManager, per-tenant dependency injection, running code in a shell scope with deferred tasks, reloading and releasing shells, Azure Blob shell configuration for cloud deployments (including Azure Storage accounts with Hierarchical Namespace, ADLS Gen2), database shell configuration for multi-instance scenarios, the OrchardCore Shells Azure and Shells Database configuration sections, migration from file-based to cloud/database providers, and environment-specific settings. Use this skill when requests mention Orchard Core Shells, ShellScope, running code in a tenant, Azure Blob or Database Shell Configuration, UseHierarchicalNamespace, or related troubleshooting. Strong matches include OrchardCore.Shells.Azure, AddAzureShellsConfiguration, AddDatabaseShellsConfiguration, BlobShellStorageOptions, DatabaseShellsStorageOptions, ShellScope, IShellHost, tenants.json.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Shells Configuration - Prompt Templates

## Configure Shell Providers for Multi-Tenant Environments

You are an Orchard Core expert. Generate configuration and code for Azure Blob shell storage, database shell storage, migration from file-based providers, and environment-specific shell settings.

### Guidelines

- By default, Orchard Core stores `tenants.json` and per-tenant `appsettings.json` in the `App_Data` folder.
- For stateless multi-instance deployments, use Azure Blob or Database shell configuration providers.
- Shell providers enable multiple hosts to share and mutate tenant configuration in a central store.
- Azure Blob provider stores files in a blob container hierarchy similar to `App_Data`.
- Database provider stores all tenant configuration as a single JSON document in a supported database.
- Both providers support `MigrateFromFiles` to automatically migrate existing `App_Data` configuration.
- Shell providers are configured in the startup project, not in individual tenant settings, under `OrchardCore:Shells:Azure` or `OrchardCore:Shells:Database`. The 3.x names `OrchardCore_Shells_Azure` and `OrchardCore_Shells_Database` still work but are deprecated.
- Use environment-based conditional registration to disable providers in development.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Azure Blob Shell Configuration

#### NuGet Package

Add the `OrchardCore.Shells.Azure` package to the web project:

```xml
<PackageReference Include="OrchardCore.Shells.Azure" Version="3.*" />
```

#### Configuration in appsettings.json

```json
{
  "OrchardCore": {
    "Shells": {
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net",
        "ContainerName": "orchardcore-shells",
        "BasePath": "",
        "MigrateFromFiles": true
      }
    }
  }
}
```

| Setting | Description |
|---|---|
| `ConnectionString` | Azure Storage account connection string |
| `ContainerName` | Name of the blob container for shell configuration |
| `BasePath` | Optional subdirectory path inside the container |
| `MigrateFromFiles` | When `true`, migrates existing `App_Data` config to blob storage |
| `UseHierarchicalNamespace` | Optional `true`/`false` to force Gen2 (HNS) or Gen1 behavior when account-level detection is unavailable, e.g. with a container-scoped SAS token. Omit to auto-detect |

#### Azure Storage with Hierarchical Namespace (ADLS Gen2)

Orchard Core 4.0 supports storage accounts with Hierarchical Namespace enabled as well as flat-namespace accounts. Before a namespace-sensitive operation the store probes the account (`GetAccountInfo`) to detect HNS. On HNS accounts, directories are real and moves are atomic through the Data Lake APIs; the DFS endpoint is derived from the connection string, so no extra setting is needed. Set `UseHierarchicalNamespace` only when detection cannot run.

#### Enable in Startup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .AddAzureShellsConfiguration();

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

#### Azure Blob Container Hierarchy

The blob container mirrors the `App_Data` structure:

```
orchardcore-shells/
├── tenants.json
├── appsettings.json              (optional root config)
├── appsettings.Production.json   (optional environment config)
└── Sites/
    ├── Default/
    │   └── appsettings.json
    ├── TenantA/
    │   └── appsettings.json
    └── TenantB/
        └── appsettings.json
```

> **Note:** The blob container must be created before the application starts. Individual tenant directories do not support environment-specific `appsettings.{Environment}.json` files, but the root level does.

### Database Shell Configuration

#### Configuration in appsettings.json

```json
{
  "OrchardCore": {
    "Shells": {
      "Database": {
        "DatabaseProvider": "SqlConnection",
        "ConnectionString": "Server=db-server;Database=OrchardShells;Trusted_Connection=True;",
        "TablePrefix": "",
        "MigrateFromFiles": true
      }
    }
  }
}
```

| Setting | Description |
|---|---|
| `DatabaseProvider` | Database engine (`SqlConnection`, `Sqlite`, `Postgres`, `MySql`) |
| `ConnectionString` | Connection string for the shell configuration database |
| `TablePrefix` | Optional table prefix for the shell configuration table |
| `Schema` | Optional database schema, for providers that support schemas |
| `MigrateFromFiles` | When `true`, migrates existing `App_Data` config to the database |

> **Note:** The Database Shells provider does not support root-level `appsettings.json` or `appsettings.{Environment}.json` files.

#### Enable in Startup

`AddDatabaseShellsConfiguration()` ships with the core `OrchardCore.Infrastructure` package, so no extra package is needed with `OrchardCore.Application.Cms.Targets`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .AddDatabaseShellsConfiguration();

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

### Migration from File-Based Configuration

Both providers support the `MigrateFromFiles` option to assist migrating from an existing `App_Data` configuration:

1. Set `MigrateFromFiles` to `true` in the provider configuration.
2. On startup, the provider checks if configuration exists for each tenant in the target store.
3. If no configuration exists, it reads from `App_Data` and writes to the target store.
4. Once migrated, the `App_Data` files can be removed (but keep as backup).

```json
{
  "OrchardCore": {
    "Shells": {
      "Database": {
        "DatabaseProvider": "Postgres",
        "ConnectionString": "Host=db-server;Database=orchardshells;Username=orcharduser;Password=P@ss;",
        "MigrateFromFiles": true
      }
    }
  }
}
```

### Environment-Specific Provider Registration

Disable the shell provider in development to use local `App_Data` files, and enable it in production:

```csharp
var builder = WebApplication.CreateBuilder(args);

var orchardBuilder = builder.Services.AddOrchardCms();

if (!builder.Environment.IsDevelopment())
{
    orchardBuilder.AddDatabaseShellsConfiguration();
}

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

### Environment-Specific with Azure Blob

```csharp
var builder = WebApplication.CreateBuilder(args);

var orchardBuilder = builder.Services.AddOrchardCms();

if (builder.Environment.IsProduction())
{
    orchardBuilder.AddAzureShellsConfiguration();
}

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

### Using Environment Variables for Configuration

Store shell provider settings in environment variables to avoid committing secrets:

#### Azure Blob via Environment Variables

```
OrchardCore__Shells__Azure__ConnectionString=DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net
OrchardCore__Shells__Azure__ContainerName=orchardcore-shells
OrchardCore__Shells__Azure__MigrateFromFiles=true
```

#### Database via Environment Variables

```
OrchardCore__Shells__Database__DatabaseProvider=SqlConnection
OrchardCore__Shells__Database__ConnectionString=Server=db-server;Database=OrchardShells;Trusted_Connection=True;
OrchardCore__Shells__Database__MigrateFromFiles=true
```

### Multi-Instance Deployment Pattern

For load-balanced or horizontally scaled deployments where multiple app instances need to share tenant configuration:

```json
{
  "OrchardCore": {
    "Shells": {
      "Database": {
        "DatabaseProvider": "SqlConnection",
        "ConnectionString": "Server=central-db;Database=OrchardShells;User Id=shelluser;Password=ShellP@ss;",
        "TablePrefix": "Shell",
        "MigrateFromFiles": false
      }
    }
  }
}
```

This ensures all instances read and write tenant configuration from the same central database, enabling:
- Tenant creation on any instance propagates to all instances.
- Tenant settings changes are immediately available cluster-wide.
- No reliance on local file system for configuration state.

### Choosing Between Azure Blob and Database

| Criteria | Azure Blob | Database |
|---|---|---|
| Root `appsettings.json` support | ✅ Yes | ❌ No |
| Environment-specific root config | ✅ Yes | ❌ No |
| Per-tenant file management | ✅ Independent files | Single JSON document |
| Best for | Azure-hosted deployments | Any database-backed deployment |
| Independent tenant config access | ✅ Direct blob access | Via application only |

### Keeping Tenant States in Sync

Shared shell configuration stores tenant settings centrally, but each instance still holds its own running shells. Enable `OrchardCore.Tenants.Distributed` on the Default tenant, with a distributed cache such as `OrchardCore.Redis.Cache`, so that tenant creation, setup, and release on one instance are applied on the others (see `orchardcore-redis` and `orchardcore-tenants`).

## Shells, Contexts and Scopes

Every tenant is a shell: its own settings, enabled features, DI container and request pipeline inside the one process.

| Type (namespace `OrchardCore.Environment.Shell`) | Role |
|------|------|
| `ShellSettings` | A tenant's identity and settings: `Name`, `State`, `RequestUrlHost`, `RequestUrlPrefix`, `TenantId`, `VersionId`, the `this[key]` bag (`DatabaseProvider`, `TablePrefix`, ...) and `ShellConfiguration`. Inject it to know the current tenant. |
| `ShellContext` | A built tenant: settings, blueprint (enabled features) and `ServiceProvider`. |
| `ShellScope` (`.Scope`) | A unit of work in a tenant; `ShellScope.Current`, `ShellScope.Services` and `ShellScope.Context` are the ambient values. |
| `IShellHost` | Builds, lists, reloads, releases and removes shells. |
| `IShellSettingsManager` | Loads, saves and removes settings in the configured shell store (files, Azure Blob or database, above). |

### Where tenant settings come from

`IShellSettingsManager` layers the startup project configuration (`appsettings.json`, environment variables, ...), then `App_Data/appsettings.json`, then `tenants.json` and the per-tenant `App_Data/Sites/{tenant}/appsettings.json` (whose sections are not nested under `OrchardCore`). The section names are covered in `orchardcore-configuration`.

### Per-tenant dependency injection

`ShellContainerFactory` builds each tenant's `IServiceProvider` as a child of the application services and runs every enabled feature's `Startup.ConfigureServices` for that tenant. A singleton registered in a module is therefore one instance per tenant. Resolving from the application root provider gives host-level services only; resolve tenant services through a shell scope.

### Running code in a scope

```csharp
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

var shellScope = await shellHost.GetScopeAsync("Tenant1");

await shellScope.UsingAsync(async scope =>
{
    var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();

    // Runs in a new scope after this one is disposed.
    ShellScope.AddDeferredTask(async deferredScope =>
    {
        var logger = deferredScope.ServiceProvider.GetRequiredService<ILogger<Startup>>();
        logger.LogInformation("Finished work in {Tenant}.", deferredScope.ShellContext.Settings.Name);
    });
});
```

- `UsingAsync(execute, activateShell: true)` activates the shell if needed (tenant events run), executes in an isolated async flow, then disposes the scope. `UsingServiceScopeAsync` is the service-scope-only variant without shell activation or tenant events.
- `IShellHost.TryGetScopeAsync(name)` returns `(scope, success)` instead of throwing for an unknown tenant. From inside a tenant, `ShellScope.UsingChildScopeAsync(...)` or `ShellScope.CreateChildScopeAsync(...)` open a scope on the same or another tenant.
- `ShellScope.AddDeferredTask`, `RegisterBeforeDispose`, `AddDeferredSignal` and `AddExceptionHandler` hook into the scope lifetime. Deferred tasks are how Orchard Core runs work after a request commits (indexing, tenant reloads).
- `ShellScope.Get`, `Set`, `GetOrCreate` and the `*Feature<T>` helpers keep per-scope ambient state.

### Reloading and releasing shells

| Member | Effect |
|--------|--------|
| `IShellHost.UpdateShellSettingsAsync(settings)` | Stamps a new `VersionId`, saves the settings and reloads the shell. |
| `IShellHost.ReloadShellContextAsync(settings)` | Reloads the settings and rebuilds the shell for later requests; current requests finish on the old one. |
| `IShellHost.ReleaseShellContextAsync(settings)` | Releases the shell so the next request builds a new one with the same settings. |
| `IShellReleaseManager.RequestRelease()` | Asks for a release of the current tenant at the end of the request (used after settings that are read only at startup change). |
| `ReloadAllShellContextsAsync()` / `ReleaseAllShellContextsAsync()` | Extensions acting on every shell. |

Enable `OrchardCore.Tenants.Distributed` to propagate reloads and releases to other instances (see Keeping Tenant States in Sync above).

### Upgrading from 3.x

- Rename `OrchardCore:OrchardCore_Shells_Azure` to `OrchardCore:Shells:Azure` and `OrchardCore:OrchardCore_Shells_Database` to `OrchardCore:Shells:Database` (environment variables `OrchardCore__Shells__Azure__*` and `OrchardCore__Shells__Database__*`). When both names define a value, the new name wins.
- Azure Shells Configuration now works on HNS (ADLS Gen2) storage accounts; add `UseHierarchicalNamespace` only when auto-detection fails.

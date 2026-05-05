---
name: orchardcore-configuration
description: Skill for configuring Orchard Core applications. Covers IShellConfiguration, configuration sources hierarchy, tenant pre-configuration and post-configuration, environment variable overrides, appsettings.json structure, IOptions patterns, and deployment configuration. Use this skill when requests mention Orchard Core Configuration, Configure Orchard Core Applications, Configuration Sources Hierarchy, Basic Module Configuration in appsettings.json, Tenant Pre-Configuration, Tenant Post-Configuration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with IShellConfiguration, IOptions, IConfiguration, DatabaseProvider, CreateBuilder, GetRequiredService, OrchardCore, appsettings.json, OrchardCore_ModuleName. It also helps with Tenant Pre-Configuration, Tenant Post-Configuration, Global Tenant Database Configuration, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Configuration - Prompt Templates

## Configure Orchard Core Applications

You are an Orchard Core expert. Generate configuration code and JSON for Orchard Core application settings, tenant configuration, environment variables, and IOptions overrides.

### Guidelines

- Orchard Core extends ASP.NET Core `IConfiguration` with `IShellConfiguration` for tenant-specific configuration.
- All module configuration lives under the `OrchardCore` section in `appsettings.json`.
- Module keys use underscores: `OrchardCore_ModuleName` (e.g., `OrchardCore_Media`).
- Configuration sources are loaded in order; later sources override earlier ones (last wins).
- Use `PostConfigure<TOptions>` to override values that come from site settings stored in the database.
- Use `Configure<TOptions>` when the module does not use site settings.
- Environment variables use double-underscore `__` as the hierarchy separator.
- Tenant-level `appsettings.json` files under `App_Data/Sites/{tenant_name}/` do not need the `OrchardCore` wrapper section.
- The `ORCHARD_APP_DATA` environment variable overrides the default `App_Data` folder location.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Configuration Sources Hierarchy

Configuration is loaded in the following order (last wins):

1. Startup project `appsettings.json`
2. Startup project `appsettings.{Environment}.json`
3. User Secrets (Development environment only)
4. Environment Variables
5. Command Line Arguments
6. `App_Data/appsettings.json` (global tenant config)
7. `App_Data/Sites/{tenant_name}/appsettings.json` (per-tenant config)

### Basic Module Configuration in appsettings.json

All module configuration is nested under the `OrchardCore` section:

```json
{
  "OrchardCore": {
    "OrchardCore_Media": {
      "MaxFileSize": 104857600
    },
    "OrchardCore_Email": {
      "DefaultSender": "noreply@example.com"
    }
  }
}
```

### Tenant Pre-Configuration

Pre-configure a tenant before it is created. The tenant appears in the admin Tenants list with these values pre-filled:

```json
{
  "OrchardCore": {
    "MyTenant": {
      "State": "Uninitialized",
      "RequestUrlPrefix": "mytenant",
      "ConnectionString": "Server=localhost;Database=MyTenantDb;Trusted_Connection=True;",
      "DatabaseProvider": "SqlConnection"
    }
  }
}
```

### Tenant Post-Configuration

Override module settings for a specific tenant after it has been set up:

```json
{
  "OrchardCore": {
    "Default": {
      "OrchardCore_Media": {
        "MaxFileSize": 209715200
      }
    }
  }
}
```

### Global Tenant Database Configuration

Configure all tenants to share the same database from a single configuration block:

```json
{
  "OrchardCore": {
    "ConnectionString": "Server=localhost;Database=SharedOrchardDb;Trusted_Connection=True;",
    "DatabaseProvider": "SqlConnection",
    "Default": {
      "State": "Uninitialized",
      "TablePrefix": "Default"
    }
  }
}
```

> **Note:** Each tenant must use a unique `TablePrefix` when sharing a database. Configure the Default tenant's prefix here; other tenants get their prefix at setup time.

### Per-Tenant Configuration File

Tenant-specific files at `App_Data/Sites/{tenant_name}/appsettings.json` omit the `OrchardCore` wrapper:

```json
{
  "OrchardCore_Media": {
    "MaxFileSize": 52428800
  }
}
```

### Environment Variable Overrides

Use double-underscore `__` as the hierarchy separator:

```
OrchardCore__OrchardCore_Media__MaxFileSize=104857600

OrchardCore__Default__OrchardCore_Media__MaxFileSize=209715200

OrchardCore__MyTenant__OrchardCore_Media__MaxFileSize=52428800
```

> **Note:** Use `_` (not `.`) as the module name separator (e.g., `OrchardCore_Media`) for Linux compatibility.

### Overriding IOptions from Startup

Use `PostConfigure` to override values populated from site settings:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .ConfigureServices(tenantServices =>
        tenantServices.PostConfigure<ResourceOptions>(options =>
        {
            options.UseCdn = true;
        }));

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

### Using IShellConfiguration in PostConfigure

Read values from the tenant-aware configuration hierarchy:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .ConfigureServices((tenantServices, serviceProvider) =>
    {
        var shellConfiguration = serviceProvider.GetRequiredService<IShellConfiguration>();

        tenantServices.PostConfigure<ResourceOptions>(options =>
        {
            options.UseCdn = shellConfiguration.GetValue<bool>("OrchardCore_Resources:UseCdn");
        });
    });

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

### Custom App_Data Location

Set the `ORCHARD_APP_DATA` environment variable to change the data folder:

```
ORCHARD_APP_DATA=D:\OrchardData\App_Data
```

Supported path formats:
- Relative: `./App_Data`
- Absolute: `/path/from/root`
- Fully qualified: `D:\Path\To\App_Data`

### Azure Deployment Configuration

Azure App Settings are treated as environment variables. Use double-underscore `__` for hierarchy:

| Azure App Setting | Value |
|---|---|
| `OrchardCore__OrchardCore_Media__MaxFileSize` | `104857600` |
| `OrchardCore__Default__OrchardCore_Email__DefaultSender` | `noreply@example.com` |

For CI/CD pipelines, use JSON path transformations to inject secrets from Azure Key Vault or pipeline variables into `appsettings.json`.

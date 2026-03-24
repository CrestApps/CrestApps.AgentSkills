---
name: orchardcore-auto-setup
description: Skill for configuring automatic setup in Orchard Core. Covers AutoSetup configuration for unattended installations, multi-tenant auto-provisioning, JSON configuration parameters, environment variable overrides, distributed locking with Redis, feature profiles, and headless deployment automation.
---

# Orchard Core Auto Setup - Prompt Templates

## Configure Automatic Setup for Orchard Core

You are an Orchard Core expert. Generate configuration for unattended installations, multi-tenant auto-provisioning, environment variable overrides, distributed locking, and headless deployment automation.

### Guidelines

- The `OrchardCore.AutoSetup` module automatically installs the application and tenants on the first request.
- AutoSetup must be explicitly enabled via `AddSetupFeatures("OrchardCore.AutoSetup")` in the startup project.
- The `Tenants` array must contain a root tenant with `ShellName` set to `Default`.
- Each tenant is installed on demand when its first request arrives.
- If `AutoSetupPath` is provided, that path must be requested to trigger installation.
- Use user secrets or environment variables to avoid committing sensitive credentials.
- For multi-instance deployments sharing a database, enable `OrchardCore.Redis.Lock` for atomic setup.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Auto Setup

Add the AutoSetup feature in the startup project's `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .AddSetupFeatures("OrchardCore.AutoSetup");

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

### Configuration Parameters

| Parameter | Description |
|---|---|
| `AutoSetupPath` | URL path to trigger setup. If empty, setup triggers on first request |
| `Tenants` | Array of tenant configurations to install |

#### Tenant Parameters

| Parameter | Description |
|---|---|
| `ShellName` | Technical tenant name (use `Default` for the root tenant) |
| `SiteName` | Display name of the site |
| `SiteTimeZone` | IANA time zone identifier (e.g., `America/New_York`) |
| `AdminUsername` | Super user username |
| `AdminEmail` | Super user email address |
| `AdminPassword` | Super user password |
| `DatabaseProvider` | Database provider (`Sqlite`, `SqlConnection`, `Postgres`, `MySql`) |
| `DatabaseConnectionString` | Connection string (empty for Sqlite) |
| `DatabaseTablePrefix` | Table prefix for shared database scenarios |
| `RecipeName` | Setup recipe name (`Blog`, `Agency`, `SaaS`, `Blank`) |
| `RequestUrlHost` | Tenant host URL |
| `RequestUrlPrefix` | Tenant URL prefix |
| `FeatureProfile` | Feature profile name (requires Feature Profiles feature) |

### Single-Tenant Auto Setup

Configure the default tenant with Sqlite and the Blog recipe:

```json
{
  "OrchardCore": {
    "OrchardCore_AutoSetup": {
      "AutoSetupPath": "",
      "Tenants": [
        {
          "ShellName": "Default",
          "SiteName": "My Orchard Site",
          "SiteTimeZone": "America/New_York",
          "AdminUsername": "admin",
          "AdminEmail": "admin@example.com",
          "AdminPassword": "P@ssw0rd123!",
          "DatabaseProvider": "Sqlite",
          "DatabaseConnectionString": "",
          "DatabaseTablePrefix": "",
          "RecipeName": "Blog"
        }
      ]
    }
  }
}
```

### Multi-Tenant Auto Setup

Configure the default tenant and additional tenants:

```json
{
  "OrchardCore": {
    "OrchardCore_AutoSetup": {
      "AutoSetupPath": "",
      "Tenants": [
        {
          "ShellName": "Default",
          "SiteName": "Main Site",
          "SiteTimeZone": "Europe/Amsterdam",
          "AdminUsername": "admin",
          "AdminEmail": "admin@example.com",
          "AdminPassword": "OrchardCoreRules1!",
          "DatabaseProvider": "Sqlite",
          "DatabaseConnectionString": "",
          "DatabaseTablePrefix": "",
          "RecipeName": "SaaS"
        },
        {
          "ShellName": "ClientTenant",
          "SiteName": "Client Portal",
          "SiteTimeZone": "Europe/Amsterdam",
          "AdminUsername": "clientadmin",
          "AdminEmail": "client@example.com",
          "AdminPassword": "ClientP@ss1!",
          "DatabaseProvider": "Sqlite",
          "DatabaseConnectionString": "",
          "DatabaseTablePrefix": "client",
          "RecipeName": "Agency",
          "RequestUrlHost": "",
          "RequestUrlPrefix": "client",
          "FeatureProfile": "client-profile"
        }
      ]
    }
  }
}
```

### Environment Variable Configuration

Use environment variables to avoid committing credentials. Variables use `__` as the hierarchy separator and array indices for tenant entries:

```
OrchardCore__OrchardCore_AutoSetup__AutoSetupPath=

OrchardCore__OrchardCore_AutoSetup__Tenants__0__ShellName=Default
OrchardCore__OrchardCore_AutoSetup__Tenants__0__SiteName=My Site
OrchardCore__OrchardCore_AutoSetup__Tenants__0__SiteTimeZone=America/New_York
OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminUsername=admin
OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminEmail=admin@example.com
OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminPassword=P@ssw0rd123!
OrchardCore__OrchardCore_AutoSetup__Tenants__0__DatabaseProvider=Sqlite
OrchardCore__OrchardCore_AutoSetup__Tenants__0__DatabaseConnectionString=
OrchardCore__OrchardCore_AutoSetup__Tenants__0__DatabaseTablePrefix=
OrchardCore__OrchardCore_AutoSetup__Tenants__0__RecipeName=Blog
```

For additional tenants, increment the index:

```
OrchardCore__OrchardCore_AutoSetup__Tenants__1__ShellName=SecondTenant
OrchardCore__OrchardCore_AutoSetup__Tenants__1__SiteName=Second Site
OrchardCore__OrchardCore_AutoSetup__Tenants__1__RequestUrlPrefix=second
```

### User Secrets Configuration

Store sensitive setup data in user secrets during local development:

```bash
cd src/MyOrchardApp
dotnet user-secrets init
dotnet user-secrets set "OrchardCore:OrchardCore_AutoSetup:Tenants:0:ShellName" "Default"
dotnet user-secrets set "OrchardCore:OrchardCore_AutoSetup:Tenants:0:SiteName" "Dev Site"
dotnet user-secrets set "OrchardCore:OrchardCore_AutoSetup:Tenants:0:AdminUsername" "admin"
dotnet user-secrets set "OrchardCore:OrchardCore_AutoSetup:Tenants:0:AdminEmail" "admin@example.com"
dotnet user-secrets set "OrchardCore:OrchardCore_AutoSetup:Tenants:0:AdminPassword" "DevP@ssw0rd1!"
dotnet user-secrets set "OrchardCore:OrchardCore_AutoSetup:Tenants:0:DatabaseProvider" "Sqlite"
dotnet user-secrets set "OrchardCore:OrchardCore_AutoSetup:Tenants:0:RecipeName" "Blog"
```

### Distributed Lock with Redis

For multi-instance deployments where multiple Orchard Core hosts share the same database, enable Redis-based distributed locking to prevent concurrent setup:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .AddSetupFeatures("OrchardCore.Redis.Lock", "OrchardCore.AutoSetup");

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

Configure the Redis connection string:

```json
{
  "OrchardCore": {
    "OrchardCore_Redis": {
      "Configuration": "redis-server:6379,allowAdmin=true"
    }
  }
}
```

Or via environment variable:

```
OrchardCore__OrchardCore_Redis__Configuration=redis-server:6379,allowAdmin=true
```

### Distributed Lock Options

| Parameter | Description | Default |
|---|---|---|
| `LockTimeout` | Timeout in milliseconds to acquire the lock | `60000` (60s) |
| `LockExpiration` | Expiration in milliseconds of the lock | `60000` (60s) |

Configure via environment variables or `appsettings.json`:

```
OrchardCore__OrchardCore_AutoSetup__LockOptions__LockTimeout=10000
OrchardCore__OrchardCore_AutoSetup__LockOptions__LockExpiration=10000
```

### Custom AutoSetup Path

Use a custom path to control when setup triggers:

```json
{
  "OrchardCore": {
    "OrchardCore_AutoSetup": {
      "AutoSetupPath": "/autosetup",
      "Tenants": [
        {
          "ShellName": "Default",
          "SiteName": "My Site",
          "SiteTimeZone": "UTC",
          "AdminUsername": "admin",
          "AdminEmail": "admin@example.com",
          "AdminPassword": "P@ssw0rd123!",
          "DatabaseProvider": "Sqlite",
          "RecipeName": "Blog"
        }
      ]
    }
  }
}
```

Trigger installation by visiting:
- `/autosetup` — installs the Default tenant
- `/tenant-prefix/autosetup` — installs a tenant with that URL prefix

### SQL Server Multi-Tenant Auto Setup

```json
{
  "OrchardCore": {
    "OrchardCore_AutoSetup": {
      "AutoSetupPath": "",
      "Tenants": [
        {
          "ShellName": "Default",
          "SiteName": "Production Site",
          "SiteTimeZone": "America/New_York",
          "AdminUsername": "admin",
          "AdminEmail": "admin@example.com",
          "AdminPassword": "Pr0dP@ssw0rd!",
          "DatabaseProvider": "SqlConnection",
          "DatabaseConnectionString": "Server=db-server;Database=OrchardMain;User Id=sa;Password=DbP@ss;",
          "DatabaseTablePrefix": "Main",
          "RecipeName": "SaaS"
        },
        {
          "ShellName": "ClientA",
          "SiteName": "Client A Portal",
          "SiteTimeZone": "America/Chicago",
          "AdminUsername": "clienta",
          "AdminEmail": "admin@clienta.com",
          "AdminPassword": "Cl1entAP@ss!",
          "DatabaseProvider": "SqlConnection",
          "DatabaseConnectionString": "Server=db-server;Database=OrchardMain;User Id=sa;Password=DbP@ss;",
          "DatabaseTablePrefix": "ClientA",
          "RecipeName": "Agency",
          "RequestUrlPrefix": "clienta"
        }
      ]
    }
  }
}
```

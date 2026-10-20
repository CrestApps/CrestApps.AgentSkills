---
name: orchardcore-configuration
description: Skill for configuring Orchard Core applications. Covers IShellConfiguration, configuration sources hierarchy, the Orchard Core 4.0 section names grouped by service (Email Smtp, Media Azure, Search Elasticsearch) and the deprecated OrchardCore_ prefixed names, environment variable and Azure Key Vault naming, ConfigurationSchema.json IntelliSense for appsettings.json, GetSectionCompat for module authors, tenant pre-configuration and post-configuration, shared tenant databases, TempDirectory, ORCHARD_APP_DATA, PostConfigure overrides, and live IOptionsMonitor options. Use this skill when requests mention Orchard Core Configuration, appsettings.json sections, renaming OrchardCore_ sections, configuration environment variables, or closely related Orchard Core setup, upgrade, or troubleshooting work. Strong matches include IShellConfiguration, IOptionsMonitor, IOptionsUpdateNotifier, GetSectionCompat, ConfigurationSchema.json, JsonSchemaSegment, DatabaseProvider, TablePrefixPattern, ITempDirectoryProvider.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Configuration - Prompt Templates

## Configure Orchard Core Applications

You are an Orchard Core expert. Generate configuration JSON, environment variables, and C# code for Orchard Core application settings, tenant configuration, and `IOptions` overrides.

### Guidelines

- Orchard Core extends ASP.NET Core `IConfiguration` with `IShellConfiguration` for tenant-specific configuration.
- All module configuration lives under the root `OrchardCore` section.
- In Orchard Core 4.0, sections are **grouped by service with a nested section per provider** and no `OrchardCore_` prefix, e.g. `OrchardCore:Email:Smtp`, `OrchardCore:Media:Azure`, `OrchardCore:Search:Elasticsearch`. Always generate the new names.
- The previous `OrchardCore_*` names still work but are deprecated. When both are present, the new name wins, and an array under the new name replaces the old array.
- Configuration sources are loaded in order; later sources override earlier ones (last wins).
- Environment variables use double underscore `__` as the hierarchy separator; Azure Key Vault secrets use `--`.
- Tenant-level `App_Data/Sites/{tenant}/appsettings.json` files omit the `OrchardCore` wrapper section.
- Never name a tenant like a configuration section (`Media`, `Email`, `Search`, ...): tenant sections share the `OrchardCore:{TenantName}` path.
- Use `PostConfigure<TOptions>` to override values that come from site settings stored in the database; use `Configure<TOptions>` when the module does not use site settings.
- Read options that are built from site settings through `IOptionsMonitor<TOptions>.CurrentValue`; they refresh without a shell release in 4.0.
- Write temporary files through `ITempDirectoryProvider` (configured by `OrchardCore:TempDirectory:Path`), never `Path.GetTempPath()`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier, except view models.

### Configuration Sources Hierarchy

Configuration is loaded in the following order (last wins):

1. Startup project `appsettings.json`
2. Startup project `appsettings.{Environment}.json`
3. User Secrets (Development environment only)
4. Environment variables (including Azure App Settings)
5. Command line arguments
6. `App_Data/appsettings.json` (global tenant configuration, `IShellConfiguration` only)
7. `App_Data/Sites/{tenant_name}/appsettings.json` (per-tenant configuration, `IShellConfiguration` only)

### Configuration Section Names (Orchard Core 4.0)

Common sections, all nested under `OrchardCore`:

| Area | Section |
| --- | --- |
| Admin | `Admin`, `Admin:List` |
| Email | `Email:Smtp`, `Email:Azure` |
| SMS | `Sms:Azure` |
| Media | `Media`, `Media:Azure`, `Media:Azure:ImageCache`, `Media:AmazonS3`, `Media:AmazonS3:ImageCache`, `Media:Slugify` |
| Search | `Search:Elasticsearch`, `Search:AzureAISearch` |
| Authentication | `Authentication:GitHub`, `Authentication:Google`, `Authentication:AzureAD`, `Authentication:MicrosoftAccount`, `Facebook`, `X` |
| Data | `Data:Sqlite`, `Data:TableOptions`, `YesSql`, `Documents` |
| Hosting | `Shells:Azure`, `Shells:Database`, `KeyVault:Azure`, `DataProtection:Azure`, `Redis`, `BackgroundService`, `TempDirectory` |
| Other | `Tenants`, `Users`, `Roles`, `Security`, `Setup`, `AutoSetup`, `Liquid`, `Markdown`, `Resources`, `ReverseProxy`, `HealthChecks`, `DynamicCache`, `Workflows`, `Apis:GraphQL`, `ReCaptcha`, `Navigation`, `Notifications`, `ContentTypes`, `Localization:CultureOptions`, `ContentLocalization:CulturePickerOptions`, `Antivirus:ClamAV` |

See `references/configuration-section-names.md` for the full previous → new rename table, environment variable and Key Vault before/after examples.

```json
{
  "OrchardCore": {
    "Email": {
      "Smtp": {
        "DefaultSender": "site@example.com"
      }
    },
    "Media": {
      "MaxFileSize": 104857600,
      "Azure": {
        "ConnectionString": "...",
        "ContainerName": "media",
        "ImageCache": {
          "ContainerName": "imagecache"
        }
      }
    },
    "Search": {
      "Elasticsearch": {
        "Url": "http://localhost:9200"
      }
    },
    "BackgroundService": {
      "ShellWarmup": true,
      "PollingTime": "00:01:00",
      "MinimumIdleTime": "00:00:10"
    }
  }
}
```

### Upgrading from 3.x

- Rename every `OrchardCore_*` section using the rename table, e.g. `OrchardCore_Email_Smtp` → `Email:Smtp`, `OrchardCore_Media_Azure` → `Media:Azure`, `OrchardCore_Elasticsearch` → `Search:Elasticsearch`, `OrchardCore_GitHub` → `Authentication:GitHub`, `OrchardCore_BackgroundService` → `BackgroundService`.
- Rename environment variables: `OrchardCore__OrchardCore_Media_Azure__ConnectionString` → `OrchardCore__Media__Azure__ConnectionString`.
- Rename Key Vault secrets: `OrchardCore--OrchardCore---Media---Azure--ConnectionString` → `OrchardCore--Media--Azure--ConnectionString`.
- Mixed old and new names keep working during a rolling migration, but values under the new name take precedence and arrays are replaced, not merged.
- Remove `MediaOptions.TusTempPath` usages; resumable uploads now use `OrchardCore:TempDirectory:Path`.
- The 4.0 `OrchardCore.Cms.Web` template `appsettings.json` already uses the new names and is a good reference.

### IntelliSense and Validation of appsettings.json

Orchard Core packages that read a configuration section ship a `ConfigurationSchema.json` file declared as a JSON schema segment. It flows to referencing projects (for example through `OrchardCore.Application.Cms.Targets`), so Visual Studio provides IntelliSense, descriptions, known values (database providers, setup recipes, time zones, cultures, AWS regions, security headers), and flags deprecated section names in:

- `appsettings.json` and `appsettings.{Environment}.json`
- `App_Data/appsettings.json` and `App_Data/appsettings.{Environment}.json`

Tenant files under `App_Data/Sites/{tenant}/` get no IntelliSense (their sections are not nested in `OrchardCore`), and Visual Studio Code does not support JSON schema segments yet.

A module can describe its own sections the same way: put a `ConfigurationSchema.json` at the package root describing properties under `OrchardCore`, and declare it in `buildTransitive/{PackageId}.targets`:

```xml
<Project>
  <ItemGroup>
    <JsonSchemaSegment Include="$(MSBuildThisFileDirectory)..\ConfigurationSchema.json" FilePathPattern="appsettings\..*json" />
  </ItemGroup>
</Project>
```

```json
{
  "type": "object",
  "properties": {
    "OrchardCore": {
      "type": "object",
      "properties": {
        "MyService": {
          "type": "object",
          "description": "Settings of My Service.",
          "properties": {
            "ApiKey": {
              "type": "string",
              "description": "The API key. Provide it through user secrets, environment variables or a key vault."
            }
          }
        },
        "MyCompany_MyService": {
          "type": "object",
          "deprecated": true,
          "deprecationMessage": "The 'MyCompany_MyService' section is deprecated. Use 'OrchardCore:MyService' instead.",
          "doNotSuggest": true
        }
      }
    }
  }
}
```

### Naming Sections in Your Own Modules

Follow the same convention: no `OrchardCore_` prefix, grouped by service, e.g. `OrchardCore:MyService:MyProvider` (environment variable `OrchardCore__MyService__MyProvider__MySetting`).

To rename an existing section without breaking deployments, bind through `GetSectionCompat(key, params legacyKeys)` from `OrchardCore.Environment.Shell.Configuration`. It returns a section merged key by key with its legacy sections: the new key wins, then legacy keys in the order given; arrays in a higher-priority section replace lower ones; underscore legacy keys also resolve their dot-notation form.

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace MyCompany.MyService;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 'MyCompany_MyService' is deprecated, use 'MyService' instead.
        services.Configure<MyServiceOptions>(
            _shellConfiguration.GetSectionCompat("MyService", "MyCompany_MyService"));
    }
}
```

The single-argument `GetSectionCompat(key)` overload only adds the old dot-notation fallback and should not be used for new code.

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
      "Media": {
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
    "Tenants": {
      "RequireTablePrefix": true,
      "TablePrefixPattern": "{{ ShellSettings.Name }}",
      "SchemaPattern": "dbo"
    },
    "Default": {
      "State": "Uninitialized",
      "TablePrefix": "Default"
    }
  }
}
```

- `Default:TablePrefix` isolates the Default tenant's tables only; it does not generate prefixes for tenants created later.
- `Tenants:RequireTablePrefix` requires a prefix whenever a tenant is created (or an uninitialized one edited) with a provider that supports prefixes.
- `Tenants:TablePrefixPattern` and `Tenants:SchemaPattern` are Fluid templates with `ShellSettings` in scope; when set, the generated value replaces any posted manual value.
- Prefixes and schemas are validated as SQL identifiers (letters, digits, underscores, starting with a letter or underscore).
- When both `DatabaseProvider` and `ConnectionString` are configured at the root, Setup and Tenants treat them as fixed and hide them. Setting only `DatabaseProvider` does not lock the database fields.
- The built-in SQLite provider does not share one `.db` file through the root `ConnectionString`; this pattern targets SQL Server, MySQL, and PostgreSQL.

### Per-Tenant Configuration File

Tenant-specific files at `App_Data/Sites/{tenant_name}/appsettings.json` omit the `OrchardCore` wrapper. They are written during setup, so environment-specific variants are not supported:

```json
{
  "Media": {
    "MaxFileSize": 52428800
  }
}
```

### Environment Variable Overrides

```text
OrchardCore__Media__MaxFileSize=104857600
OrchardCore__Default__Media__MaxFileSize=209715200
OrchardCore__MyTenant__Media__MaxFileSize=52428800
OrchardCore__Email__Smtp__DefaultSender=noreply@example.com
OrchardCore__Media__Azure__ConnectionString=DefaultEndpointsProtocol=https;...
```

The 4.0 names keep variable names short, which matters for hosts that limit their length.

### Azure Key Vault Secret Names

With `AddOrchardCoreAzureKeyVault()` (configured under `OrchardCore:KeyVault:Azure`), secret names map `--` to `:` and `---` to `_`:

| Secret name | Configuration key |
| --- | --- |
| `OrchardCore--Media--Azure--ConnectionString` | `OrchardCore:Media:Azure:ConnectionString` |
| `OrchardCore--Shells--Database--ConnectionString` | `OrchardCore:Shells:Database:ConnectionString` |

### Temporary File Storage

Orchard Core writes in-progress chunked and TUS uploads, deployment and recipe import/export archives, and tenant recipe uploads under a tenant-scoped sub-directory of `OrchardCore:TempDirectory:Path` (default: the OS temp directory). Point it at a mounted file share, not object storage:

```json
{
  "OrchardCore": {
    "TempDirectory": {
      "Path": "/mnt/shared/temp"
    }
  }
}
```

Module code should inject `ITempDirectoryProvider` (`GetRootDirectory()`, `GetTempFileName(extension)`, `CreateTempSubdirectory(prefix)`) instead of calling `Path.GetTempPath()`. See `orchardcore-temp-file-storage` for details.

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

The admin settings screen still shows the stored site settings value, so tell administrators when a value is overridden from code.

### Using IShellConfiguration in PostConfigure

Read values from the tenant-aware configuration hierarchy (prefer `IShellConfiguration` over `IConfiguration` so `App_Data` and tenant files are included):

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    .ConfigureServices((tenantServices, serviceProvider) =>
    {
        var shellConfiguration = serviceProvider.GetRequiredService<IShellConfiguration>();

        tenantServices.PostConfigure<ResourceOptions>(options =>
        {
            options.UseCdn = shellConfiguration.GetValue<bool>("Resources:UseCdn");
        });
    });

var app = builder.Build();
app.UseStaticFiles();
app.UseOrchardCore();
app.Run();
```

### Live Tenant Options with IOptionsMonitor

Orchard Core 4.0 adds a post-commit, signal-backed refresh pipeline for options built from mutable tenant state (site settings, documents). Built-in examples: `EmailOptions`, `EmailProviderOptions`, `AzureEmailOptions`, `SmtpOptions`, `ShapeRenderingOptions`, `ReCaptchaSettings`, `RegistrationOptions`, `ExternalLoginOptions`, `AzureAISearchDefaultOptions`, `SmsProviderOptions`, `AzureSmsOptions`, `TwilioOptions`, `AdminListOptions`.

- Inject `IOptionsMonitor<TOptions>` and read `CurrentValue` (or `Get(name)`) at the point of use; do not cache the value in a field.
- Opt an options type in with `services.AddSignalOptionsChangeTokenSource<TOptions>()`.
- After a settings editor changes the source data, call `IOptionsUpdateNotifier.RequestUpdate<TOptions>()`. The invalidation is dispatched only after the YesSql session commits successfully; there is no API to cancel a request.
- For named options, register `SignalOptionsChangeTokenSource<TOptions>` with the name and call `RequestUpdate<TOptions>(name)`.
- In multi-node deployments use a distributed `ISignal` (for example the Redis Bus feature) so every node refreshes.
- Keep `IOptions<TOptions>` for values that are immutable for the shell lifetime (startup code, static configuration).

See `orchardcore-site-settings` for a complete settings-backed options example.

### Custom App_Data Location

Set the `ORCHARD_APP_DATA` environment variable to change the data folder (created if missing):

```text
ORCHARD_APP_DATA=D:\OrchardData\App_Data
```

Supported path formats: relative (`./App_Data`), absolute (`/path/from/root`), or fully qualified (`D:\Path\To\App_Data`).

### Azure Deployment Configuration

Azure App Settings are treated as environment variables:

| Azure App Setting | Value |
|---|---|
| `OrchardCore__Media__MaxFileSize` | `104857600` |
| `OrchardCore__Default__Email__Smtp__DefaultSender` | `noreply@example.com` |

For CI/CD pipelines, use JSON path transformations to inject secrets from pipeline variables or Azure Key Vault into `appsettings.json`. The `IShellConfiguration` files in `App_Data` can also be stored in Azure Blob Storage or the database (see `orchardcore-shells`).

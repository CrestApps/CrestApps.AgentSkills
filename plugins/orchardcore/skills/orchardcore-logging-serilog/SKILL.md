---
name: orchardcore-logging-serilog
description: Skill for configuring Serilog logging in Orchard Core. Covers the OrchardCore.Logging.Serilog package reference in the web project, appsettings.json Serilog configuration with Console and File sinks, Program.cs initialization with UseSerilog or UseSerilogWeb, tenant-aware logging with UseSerilogTenantNameLogging and TenantName enrichment, output templates, per-environment configuration, and replacing the default NLog setup of the Orchard Core templates. Use this skill when requests mention Orchard Core Serilog Logging, Serilog Configuration in appsettings.json, Output Template Tokens, Minimum Level Overrides, tenant name in logs, or closely related Orchard Core setup or troubleshooting work. Strong matches include OrchardCore.Logging.Serilog, UseSerilogWeb, UseSerilogTenantNameLogging, FromLogContext, SourceContext, TenantName, ReadFrom.Configuration(), UseNLogHost.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Serilog Logging - Prompt Templates

## Configure Serilog Structured Logging

You are an Orchard Core expert. Generate configuration and code for Serilog integration including structured logging, Console and File sinks, tenant-aware enrichment, and output template customization.

### Guidelines

- Add a `PackageReference` to `OrchardCore.Logging.Serilog` in the **web (startup) project**. It is a host-level library, not a module: there is no feature to enable and no recipe step.
- The Orchard Core templates use NLog (`builder.Host.UseNLogHost()`); remove that call (and the `OrchardCore.Logging.NLog` reference) when switching to Serilog.
- Configure Serilog in `appsettings.json` under the `Serilog` section.
- Initialize Serilog in `Program.cs` using `UseSerilog()` and `ReadFrom.Configuration()`.
- Always call `Enrich.FromLogContext()` to enable log context enrichment.
- Call `UseSerilogTenantNameLogging()` in the Orchard Core pipeline to add `TenantName` to the log context.
- Clear default logging providers before configuring Serilog to prevent duplicate log entries.
- Use the `{TenantName}` token in output templates to include the tenant name in log entries.
- Use the `{RequestId}` token to correlate log entries across a single HTTP request.
- Use `restrictedToMinimumLevel` on individual sinks to control per-sink log verbosity.
- The Console sink is useful for development; the File sink with rolling intervals is recommended for production.
- All C# classes must use the `sealed` modifier.

### Package Reference

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="OrchardCore.Application.Cms.Targets" Version="3.*" />
    <PackageReference Include="OrchardCore.Logging.Serilog" Version="3.*" />
  </ItemGroup>
</Project>
```

Add the sink packages you configure (for example `Serilog.Sinks.Console`, `Serilog.Sinks.File`) if they are not already brought in transitively.

### Serilog Configuration in appsettings.json

Configure Console and File sinks with minimum levels, output templates, and rolling intervals:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Default": "Warning",
        "Microsoft": "Error",
        "System": "Error"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "{Timestamp:HH:mm:ss}|{TenantName}|{RequestId}|{SourceContext}|{Level:u3}|{Message:lj}{NewLine}{Exception}",
          "restrictedToMinimumLevel": "Information"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "App_Data/logs/orchard-log.txt",
          "rollingInterval": "Day",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.ffff}|{TenantName}|{RequestId}|{SourceContext}|{Level:u3}|{Message:lj}{NewLine}{Exception}",
          "restrictedToMinimumLevel": "Warning"
        }
      }
    ]
  }
}
```

### Output Template Tokens

| Token | Description |
|-------|-------------|
| `{Timestamp:format}` | Log entry timestamp in the specified format |
| `{TenantName}` | Name of the Orchard Core tenant processing the request |
| `{RequestId}` | Unique ID for correlating log entries within a single HTTP request |
| `{SourceContext}` | Fully qualified name of the class that generated the log entry |
| `{Level:u3}` | Log level as a 3-character uppercase string (e.g., `INF`, `WRN`, `ERR`) |
| `{Message:lj}` | The log message with JSON literals inline |
| `{NewLine}` | Platform-appropriate line break |
| `{Exception}` | Exception details, if present |

### Minimum Level Overrides

Use `Override` to control log verbosity per namespace. This reduces noise from framework internals:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Error",
        "System": "Error",
        "OrchardCore": "Warning"
      }
    }
  }
}
```

### Program.cs Serilog Initialization

Configure Serilog in `Program.cs` by clearing default providers and reading from configuration:

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host
    .ConfigureLogging(logging => logging.ClearProviders())
    .UseSerilog((hostingContext, configBuilder) =>
    {
        configBuilder
            .ReadFrom.Configuration(hostingContext.Configuration)
            .Enrich.FromLogContext();
    });

builder.Services.AddOrchardCms();

var app = builder.Build();

app.UseStaticFiles();
app.UseOrchardCore(c => c.UseSerilogTenantNameLogging());

app.Run();
```

`OrchardCore.Logging` also provides `builder.Host.UseSerilogWeb()`, a shortcut for `UseSerilog()` with `ReadFrom.Configuration()` and `Enrich.FromLogContext()`:

```csharp
using OrchardCore.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseSerilogWeb();

builder.Services.AddOrchardCms();

var app = builder.Build();

app.UseStaticFiles();
app.UseOrchardCore(c => c.UseSerilogTenantNameLogging());

app.Run();
```

`UseSerilogTenantNameLogging()` must be called inside `UseOrchardCore(...)` so the middleware runs in the tenant pipeline where the tenant name is known.

### Tenant-Aware Logging

The `UseSerilogTenantNameLogging()` middleware enriches every log entry with the current tenant's name. This is critical for multi-tenant deployments where logs from different tenants are written to the same output.

The `{TenantName}` token in the output template renders the tenant name:

```
14:32:05|Default|0HN4K5...|MyApp.Services.ProductService|INF|Product created: Widget
14:32:06|Tenant2|0HN4K5...|MyApp.Services.ProductService|INF|Product created: Gadget
```

### File Sink with Rolling Interval

For production, use the File sink with daily rolling to manage log file sizes:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "App_Data/logs/orchard-log.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 31,
          "fileSizeLimitBytes": 104857600,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.ffff}|{TenantName}|{RequestId}|{SourceContext}|{Level:u3}|{Message:lj}{NewLine}{Exception}",
          "restrictedToMinimumLevel": "Warning"
        }
      }
    ]
  }
}
```

| File Sink Property | Description |
|--------------------|-------------|
| `path` | Base file path for log output |
| `rollingInterval` | How often to roll to a new file (`Day`, `Hour`, `Month`) |
| `retainedFileCountLimit` | Maximum number of rolled log files to retain |
| `fileSizeLimitBytes` | Maximum size of a single log file before rolling |
| `restrictedToMinimumLevel` | Minimum log level for this sink |

### Development vs Production Configuration

Use environment-specific `appsettings.{Environment}.json` files to vary logging:

**appsettings.Development.json** — verbose console output:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:HH:mm:ss}|{TenantName}|{Level:u3}|{SourceContext}|{Message:lj}{NewLine}{Exception}",
          "restrictedToMinimumLevel": "Debug"
        }
      }
    ]
  }
}
```

**appsettings.Production.json** — file-only, warnings and above:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft": "Error",
        "System": "Error"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "App_Data/logs/orchard-log.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 14,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.ffff}|{TenantName}|{RequestId}|{SourceContext}|{Level:u3}|{Message:lj}{NewLine}{Exception}",
          "restrictedToMinimumLevel": "Warning"
        }
      }
    ]
  }
}
```

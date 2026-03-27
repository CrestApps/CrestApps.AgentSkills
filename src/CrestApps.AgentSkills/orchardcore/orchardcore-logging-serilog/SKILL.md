---
name: orchardcore-logging-serilog
description: Skill for configuring Serilog logging in Orchard Core. Covers Serilog integration setup, appsettings.json logging configuration with Console and File sinks, Program.cs Serilog initialization, tenant-aware logging with TenantName enrichment, and output template customization. Use this skill when requests mention Orchard Core Serilog Logging, Configure Serilog Structured Logging, Enabling the Feature, Serilog Configuration in appsettings.json, Output Template Tokens, Minimum Level Overrides, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Logging.Serilog, FromLogContext, SourceContext, CreateBuilder, ProductService, appsettings.json, Program.cs, UseSerilog(), ReadFrom.Configuration(). It also helps with Output Template Tokens, Minimum Level Overrides, Program.cs Serilog Initialization, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Serilog Logging - Prompt Templates

## Configure Serilog Structured Logging

You are an Orchard Core expert. Generate configuration and code for Serilog integration including structured logging, Console and File sinks, tenant-aware enrichment, and output template customization.

### Guidelines

- Add a reference to `OrchardCore.Logging.Serilog` to enable Serilog integration.
- Configure Serilog in `appsettings.json` under the `Serilog` section.
- Initialize Serilog in `Program.cs` using `UseSerilog()` and `ReadFrom.Configuration()`.
- Always call `Enrich.FromLogContext()` to enable log context enrichment.
- Call `UseSerilogTenantNameLogging()` in the Orchard Core pipeline to add `TenantName` to the log context.
- Clear default logging providers before configuring Serilog to prevent duplicate log entries.
- Use the `{TenantName}` token in output templates to include the tenant name in log entries.
- Use the `{RequestId}` token to correlate log entries across a single HTTP request.
- Use `restrictedToMinimumLevel` on individual sinks to control per-sink log verbosity.
- The Console sink is useful for development; the File sink with rolling intervals is recommended for production.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling the Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Logging.Serilog"
      ],
      "disable": []
    }
  ]
}
```

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

app.UseOrchardCore(c => c.UseSerilogTenantNameLogging());

app.Run();
```

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

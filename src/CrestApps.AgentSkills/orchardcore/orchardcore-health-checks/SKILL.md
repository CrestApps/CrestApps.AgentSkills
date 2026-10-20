---
name: orchardcore-health-checks
description: Skill for exposing and extending Orchard Core tenant health checks. Covers HealthChecksOptions, the OrchardCore HealthChecks configuration section, health endpoint configuration, compact and detailed status codes, rate limiting probes, IHealthCheck registration, detailed JSON responses, IHealthChecksResponseWriter customization, and ASP.NET Core health check integration. Use this skill when requests mention Orchard Core Health Checks, health endpoints, liveness probes, IHealthCheck, HealthChecksOptions, Kubernetes probes, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.HealthChecks, HealthChecksOptions, IHealthChecksResponseWriter, DefaultHealthChecksResponseWriter, HealthCheckOptions, HealthReport, and Microsoft.Extensions.Diagnostics.HealthChecks. It also helps with feature recipes, tenant configuration, custom checks, and the code patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Health Checks - Prompt Templates

## Expose Tenant Health Checks

You are an Orchard Core expert. Generate health endpoint configuration and ASP.NET Core health check integrations for Orchard Core.

### Guidelines

- Enable the `OrchardCore.HealthChecks` feature. The module has no declared feature dependencies.
- The feature registers ASP.NET Core health check services with `services.AddHealthChecks()`.
- By default, the health endpoint is `/health/live`, relative to the tenant host and URL prefix. Configure it per tenant through the `OrchardCore:HealthChecks` configuration section (formerly `OrchardCore_HealthChecks`).
- The feature has no admin UI or site settings, and it registers no check itself. With no checks registered, the endpoint reports `Healthy`. `OrchardCore.Redis` and `OrchardCore.Sms` contribute checks when they are enabled with Health Checks.
- The endpoint does not require authentication and runs every registered check; the module does not filter by tag.
- Set `ShowDetails` to `false` for a minimal standard health response. Set it to `true` only for protected operational endpoints that may expose dependency information.
- With details enabled, the module uses `IHealthChecksResponseWriter` and returns JSON with overall status, duration, and each check's name, status, and description.
- With `ShowDetails: false`, the standard ASP.NET Core response returns `200` for Healthy and Degraded and `503` for Unhealthy. With details enabled, every status returns `200`, so monitors must parse the JSON `Status`. Both modes disable response caching.
- The route and writer are chosen when the tenant pipeline is built; reload the tenant or restart after changing the settings.
- The endpoint is mapped through endpoint routing, so an `Endpoint` policy from `orchardcore-rate-limits` on the health path can throttle probes.
- Implement `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck` for custom dependencies and register it using `IHealthChecksBuilder`.
- Keep health checks fast, cancellation-aware, and free of side effects.
- All recipe JSON must be wrapped in the root `{ "steps": [...] }` format.
- All C# classes must use the `sealed` modifier, except View Models.

### Enabling Health Checks

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.HealthChecks"
      ],
      "disable": []
    }
  ]
}
```

### Tenant Configuration

The module binds `HealthChecksOptions` from `OrchardCore:HealthChecks`.

```json
{
  "OrchardCore": {
    "HealthChecks": {
      "Url": "/health/live",
      "ShowDetails": false
    }
  }
}
```

```text
OrchardCore__HealthChecks__Url=/health/live
OrchardCore__CustomerA__HealthChecks__Url=/health/ready
```

The second variable targets only the `CustomerA` tenant.

| Option | Default | Description |
|---|---|---|
| `Url` | `/health/live` | Tenant health endpoint path. |
| `ShowDetails` | `false` | Enables the detailed JSON response writer. |

Use the shell or tenant configuration source appropriate to the hosting model so the settings apply to the intended tenant.

### Registering a Custom Health Check

Register custom checks in a module startup class. The Health Checks feature already calls `AddHealthChecks`; calling it again returns the same builder for adding checks. Use `[RequireFeatures("OrchardCore.HealthChecks")]` so the registration loads only when the endpoint exists, or declare the feature as a dependency.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<InventoryHealthCheck>("inventory");
    }
}
```

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MyModule;

public sealed class InventoryHealthCheck : IHealthCheck
{
    private readonly IInventoryClient _inventoryClient;

    public InventoryHealthCheck(IInventoryClient inventoryClient)
    {
        _inventoryClient = inventoryClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isAvailable = await _inventoryClient.IsAvailableAsync(cancellationToken);

        return isAvailable
            ? HealthCheckResult.Healthy("Inventory service is reachable.")
            : HealthCheckResult.Unhealthy("Inventory service is unavailable.");
    }
}
```

### Detailed Response Format

When `ShowDetails` is true, `DefaultHealthChecksResponseWriter` produces JSON shaped like:

```json
{
  "Status": "Healthy",
  "Duration": "00:00:00.0123456",
  "HealthChecks": [
    {
      "Name": "inventory",
      "Status": "Healthy",
      "Description": "Inventory service is reachable."
    }
  ]
}
```

The response writer uses `HealthReport.TotalDuration` and every `HealthReportEntry` from the health report.

### Replacing the Response Writer

Register a custom scoped `IHealthChecksResponseWriter` only when the detailed contract must change.

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.HealthChecks.Services;

namespace MyModule;

public sealed class ProbeResponseWriter : IHealthChecksResponseWriter
{
    public async Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
        });
    }
}
```

```csharp
services.Replace(ServiceDescriptor.Scoped<IHealthChecksResponseWriter, ProbeResponseWriter>());
```

The writer is only used when `ShowDetails` is `true`; compact mode keeps the ASP.NET Core default writer.

### Probe Design

- Use `/health/live` for a lightweight process liveness check.
- Add dependency checks deliberately for readiness-style monitoring.
- Do not expose detailed dependency names and descriptions on a public internet endpoint.
- Set probe timeouts in the hosting platform and honor the `CancellationToken` in custom checks.
- Do not use a health check to perform migrations, warm caches, or repair data.
- The endpoint is anonymous. Restrict it with network policy or a reverse proxy when it should not be public, especially with `ShowDetails` enabled.
- For separate liveness and readiness semantics, map additional filtered endpoints in module code; this endpoint always runs every check.

### Troubleshooting

| Symptom | Check |
|---|---|
| Endpoint is missing (`404`) | Enable `OrchardCore.HealthChecks` for the tenant and include the tenant host and URL prefix. |
| Wrong endpoint path | Check `OrchardCore:HealthChecks:Url` in tenant configuration, then reload the tenant. |
| JSON details are absent | Set `ShowDetails` to true. |
| Unhealthy response has HTTP 200 | This is expected with detailed output; evaluate the JSON `Status`. |
| Custom check never runs | Verify it is registered with `AddHealthChecks().AddCheck` in a feature enabled on the same tenant. |
| Probe returns `429` | A Rate Limits policy (global or endpoint) is throttling the probe; exclude or raise its budget. |

### Upgrading from 3.x

- Rename the `OrchardCore_HealthChecks` section to `OrchardCore:HealthChecks` (environment variable `OrchardCore__HealthChecks__Url`). The old name still works but is deprecated.
- In a tenant `App_Data/Sites/{TenantName}/appsettings.json`, use a root `HealthChecks` section.

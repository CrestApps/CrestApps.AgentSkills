---
name: orchardcore-opentelemetry
description: Skill for collecting Orchard Core 4.0 traces, metrics and logs with OpenTelemetry. Covers the OrchardCore.OpenTelemetry package, AddOpenTelemetry() and HasOtlpEndpoint(), the OrchardCore OpenTelemetry configuration section, OTEL_EXPORTER_OTLP values, per-tenant tagging, resource attributes, the OrchardCore activity source and meter, slow shape spans, collecting YesSql, Npgsql, Elasticsearch, MailKit and Azure SDK telemetry, AddOrchardCoreInstrumentation() for Aspire, exporters, and emitting telemetry from a module. Use this skill when requests mention Orchard Core OpenTelemetry, OTLP, traces, spans, metrics, distributed tracing, Aspire dashboard, Grafana, Prometheus, Azure Monitor, per-tenant telemetry, slow shapes, or N+1 queries in production. Strong matches include OrchardCore.OpenTelemetry, AddOpenTelemetry, AddOrchardCoreInstrumentation, OrchardCoreOpenTelemetryOptions, OrchardCoreTelemetry, DisplayManagementDiagnostics, and OTEL_EXPORTER_OTLP_ENDPOINT.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core OpenTelemetry - Prompt Templates

## Collect Traces, Metrics and Logs

You are an Orchard Core expert. Generate configuration and code that exports Orchard Core telemetry with OpenTelemetry over OTLP.

### Guidelines

- `OrchardCore.OpenTelemetry` (new in 4.0) is an application-level package, not a tenant feature: there is nothing to enable in a recipe or in the admin. It is configured once for the process, and the telemetry of every tenant is tagged with its name.
- Reference the package in the web (startup) project and call `AddOpenTelemetry()` on the Orchard Core builder. It is not part of `OrchardCore.Application.Cms.Targets`; only the repository's `OrchardCore.Cms.Web` sample references it.
- `AddOpenTelemetry()` collects telemetry even when nothing exports it. When OTLP is the only exporter, guard the call with `builder.Configuration.HasOtlpEndpoint()`.
- Telemetry is exported over OTLP once an endpoint is set in `OrchardCore:OpenTelemetry:Otlp:Endpoint` or in the standard `OTEL_EXPORTER_OTLP_ENDPOINT` value. The `Otlp` section takes precedence over the `OTEL_*` values.
- Calling `AddOpenTelemetry()` twice is ignored, so exporters are never registered twice.
- When the app already configures OpenTelemetry (for example Aspire `ServiceDefaults`), do not call `AddOpenTelemetry()` on the Orchard Core builder; add `AddOrchardCoreInstrumentation()` to the existing tracing, metrics and logging builders, before the exporters.
- Libraries emit their own telemetry and OpenTelemetry only listens to the names it is given: add `YesSql`, `Npgsql`, `Elastic.Transport`, `MailKit.Net.SmtpClient`, `Azure.*` and so on to `Tracing:Sources` / `Metrics:Meters`.
- With many tenants, set `Metrics:IncludeTenantName` to `false` to limit the number of HTTP metric series. Spans and logs keep the tenant tag.
- An `Otlp:Endpoint` that is not an absolute HTTP or HTTPS URI stops the application from starting.
- All C# classes in samples are `sealed`, except view models.

### Package Reference

```xml
<PackageReference Include="OrchardCore.OpenTelemetry" Version="4.*" />
```

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseNLogHost();

var orchardCore = builder.Services.AddOrchardCms();

// Only collect when something exports it, e.g. OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317.
if (builder.Configuration.HasOtlpEndpoint())
{
    orchardCore.AddOpenTelemetry();
}

var app = builder.Build();

app.UseStaticFiles();
app.UseOrchardCore();

app.Run();
```

Add other instrumentations or exporters with the optional callback, which receives the `OpenTelemetryBuilder`:

```csharp
builder.Services
    .AddOrchardCms()
    .AddOpenTelemetry(openTelemetry => openTelemetry
        .WithTracing(tracing => tracing.AddSqlClientInstrumentation()));
```

`AddOpenTelemetry()` and `AddOrchardCoreInstrumentation()` live in the `Microsoft.Extensions.DependencyInjection` and `OpenTelemetry` namespaces; `HasOtlpEndpoint()` extends `IConfiguration` in `Microsoft.Extensions.Configuration`.

### Configuration

Options bind to `OrchardCoreOpenTelemetryOptions` from the application-level `OrchardCore:OpenTelemetry` section (not a tenant section):

```json
{
  "OrchardCore": {
    "OpenTelemetry": {
      "Enabled": true,
      "ServiceName": "MyCms",
      "ServiceInstanceId": "web-1",
      "Tracing": {
        "Enabled": true,
        "SamplingRatio": 0.25,
        "Sources": [ "MyCompany.MyModule", "YesSql" ],
        "SlowShapeThreshold": "00:00:00.050"
      },
      "Metrics": {
        "Enabled": true,
        "IncludeTenantName": true,
        "Meters": [ "MyCompany.MyModule", "YesSql" ]
      },
      "Logging": {
        "Enabled": true
      },
      "Otlp": {
        "Endpoint": "http://localhost:4317",
        "Protocol": "Grpc"
      }
    }
  }
}
```

| Setting | Default | Notes |
|---|---|---|
| `Enabled` | `true` | Turns collection and export off. |
| `ServiceName` | Application name | `service.name` resource attribute. |
| `ServiceInstanceId` | Machine name + process id | `service.instance.id`, distinguishes web-farm nodes. |
| `Tracing:SamplingRatio` | All sampled (or `OTEL_TRACES_SAMPLER`) | `0` to `1`; a sampled parent always samples its children. |
| `Tracing:Sources` | `[]` | Extra `ActivitySource` names; a trailing `*` matches a prefix. |
| `Tracing:SlowShapeThreshold` | `00:00:00.050` | Shapes slower than this get a `Render shape {type}` span; `00:00:00` traces every shape. |
| `Metrics:IncludeTenantName` | `true` | Tags `http.server.request.duration` with the tenant. |
| `Metrics:Meters` | `[]` | Extra `Meter` names; a trailing `*` matches a prefix. |
| `Logging:Enabled` | `true` | The levels of the `Logging` section still apply. |
| `Otlp:Endpoint` | `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://host:4317` for gRPC, `http://host:4318` for HTTP. |
| `Otlp:Protocol` | `OTEL_EXPORTER_OTLP_PROTOCOL`, else `Grpc` | `Grpc` or `HttpProtobuf`. |

Standard values are also honored: `OTEL_EXPORTER_OTLP_ENDPOINT`, `_PROTOCOL`, `_HEADERS`, `_TIMEOUT`, `_COMPRESSION` (and their `_TRACES_`, `_METRICS_`, `_LOGS_` variants, which win; a per-signal endpoint is used as is, and only signals with an endpoint are exported), `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE`, `OTEL_SDK_DISABLED`, `OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` (both win over the section), and the batch settings `OTEL_BSP_*` / `OTEL_BLRP_*` (environment variables only).

### What Orchard Core Emits

- **Resource attributes**: `service.name`, `service.version`, `service.instance.id`, `host.name`, `process.pid`, `process.runtime.name`, `process.runtime.version`, `deployment.environment.name`, `orchardcore.version`.
- **Spans** from the `OrchardCore` activity source: `Build tenant container`, `Build tenant pipeline`, `Activate tenant`, `Run background task {task}` (root of its own trace), `Execute recipe {recipe}`, `Execute recipe step {step}`, and `Render shape {type}` for slow shapes (a child of the request). Failures set the `Error` status, `error.type` and an `exception` event.
- **Metrics** from the `OrchardCore` meter: `orchardcore.tenants` (gauge by `orchardcore.tenant.state`), `orchardcore.tenants.loaded`, `orchardcore.tenant.container.duration`, `orchardcore.tenant.pipeline.duration`, `orchardcore.tenant.activation.duration`, `orchardcore.tenant.releases`, `orchardcore.background_task.duration`, and `orchardcore.shape.cache.lookups` (`hit`/`miss`, with Dynamic Cache enabled).
- **Also collected**: ASP.NET Core and HttpClient spans, the ASP.NET Core, Kestrel, routing, rate limiting, authentication/authorization/identity, `System.Net.Http` and `System.Runtime` meters, and `ILogger` logs with scopes, next to NLog or Serilog.
- **Tenant tagging**: every span started in a tenant scope (including other libraries' spans), tenant log records and HTTP server metrics carry `orchardcore.tenant.name`.

### Collect Library and Database Telemetry

```json
{
  "OrchardCore": {
    "OpenTelemetry": {
      "Tracing": {
        "Sources": [ "YesSql", "Npgsql", "Elastic.Transport", "MailKit.Net.SmtpClient", "Azure.*" ]
      },
      "Metrics": {
        "Meters": [ "YesSql", "Npgsql" ]
      }
    }
  }
}
```

- YesSql reports a span per command and per session flush, `db.client.operation.duration`, and the `yessql.session.commands` / `yessql.session.documents` histograms (many commands per session reveals an N+1 pattern). Its telemetry is tagged with the tenant of the store.
- SQL text is added to YesSql spans only with the YesSql option `IncludeQueryTextInTelemetry` (`OrchardCore:YesSql:IncludeQueryTextInTelemetry`). Npgsql spans always include it.
- Commands run directly on a connection, such as SQL queries of the Queries module, are not reported by YesSql.
- Collecting both the `YesSql` and `Npgsql` meters counts each command twice; drop the driver instrument with a view:

```csharp
using OpenTelemetry.Metrics;
using YesSql;

builder.Services
    .AddOrchardCms()
    .AddOpenTelemetry(openTelemetry => openTelemetry
        .WithMetrics(metrics => metrics.AddView(instrument =>
            YesSqlTelemetry.IsDuplicateDriverInstrument(instrument) ? MetricStreamConfiguration.Drop : null)));
```

A library that emits nothing itself (for example the SQL Server client) needs an instrumentation package added through the `AddOpenTelemetry()` callback.

### Investigate Slow Pages

Add the `OrchardCore.DisplayManagement` source and meter (constants `DisplayManagementDiagnostics.ActivitySourceName` and `MeterName`) to get a nested `Render shape {type}` span for every shape, with `orchardcore.shape.type` and `orchardcore.shape.binding` (the template or shape method), plus `orchardcore.display.shapes_rendered` and the `orchardcore.display.shape.duration` histogram:

```json
{
  "OrchardCore": {
    "OpenTelemetry": {
      "Tracing": { "Sources": [ "OrchardCore.DisplayManagement", "YesSql" ] },
      "Metrics": { "Meters": [ "OrchardCore.DisplayManagement" ] }
    }
  }
}
```

Find the slowest shape types in the histogram, open a slow request's `Render shape` spans, compare a shape's time with its children's, and read the nested YesSql spans to tell one slow query from many small ones. For a quick per-request view in development, see `orchardcore-mini-profiler`.

### Use an Existing OpenTelemetry Setup (Aspire)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOrchardCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddOrchardCoreInstrumentation())
    .WithLogging(logging => logging.AddOrchardCoreInstrumentation());
```

Add the Orchard Core instrumentation before the exporters so telemetry is tagged with the tenant first.

### Emit Telemetry from a Module

`OrchardCoreTelemetry` (namespace `OrchardCore.Telemetry`, package `OrchardCore.Abstractions`) exposes the shared `ActivitySource` and `Meter`, which only depend on `System.Diagnostics`:

```csharp
using OrchardCore.Telemetry;

public sealed class ProductImporter
{
    public async Task ImportAsync()
    {
        using var activity = OrchardCoreTelemetry.ActivitySource.StartActivity("Import products");

        try
        {
            await ImportProductsAsync();
        }
        catch (Exception ex)
        {
            // Sets the Error status, error.type and an exception event; accepts a null activity.
            OrchardCoreTelemetry.RecordException(activity, ex);

            throw;
        }
    }

    private Task ImportProductsAsync() => Task.CompletedTask;
}
```

Attribute and instrument names are constants of `OrchardCoreTelemetryConstants` (for example `OrchardCoreTelemetryConstants.Attributes.TenantName`). A module can instead use its own `ActivitySource` or `Meter` and add its name to `Tracing:Sources` or `Metrics:Meters`. Without OpenTelemetry, watch the meter with `dotnet-counters monitor --counters OrchardCore,System.Runtime` (the `orchardcore.tenants` gauges are only observed once OpenTelemetry metrics are configured).

See [references/opentelemetry-examples.md](references/opentelemetry-examples.md) for local Aspire and Grafana setups and backend queries.

# OpenTelemetry Examples

## Aspire Dashboard (Local)

The Orchard Core repository's `OrchardCore.AspireHost` project runs `OrchardCore.Cms.Web` with the Aspire dashboard and sets its OTLP endpoint. For your own app, run the standalone dashboard and point the app at it:

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run
```

## Grafana LGTM (Local)

```bash
docker run --rm -d -p 3000:3000 -p 4317:4317 -p 4318:4318 --name lgtm grafana/otel-lgtm
```

Run the app with `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`, open `http://localhost:3000`, and use **Explore**.

## Grafana Cloud

```bash
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-{region}.grafana.net/otlp
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic%20{Base64 of instanceId:token}
```

## Per-Signal Endpoints

Only signals with an endpoint are exported, so traces alone can be sent somewhere:

```bash
OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=http://tempo:4318/v1/traces
OTEL_EXPORTER_OTLP_TRACES_PROTOCOL=http/protobuf
```

## Environment Variables for the Section

```bash
OrchardCore__OpenTelemetry__ServiceName=MyCms
OrchardCore__OpenTelemetry__Tracing__SamplingRatio=0.1
OrchardCore__OpenTelemetry__Tracing__Sources__0=YesSql
OrchardCore__OpenTelemetry__Metrics__IncludeTenantName=false
OrchardCore__OpenTelemetry__Otlp__Endpoint=http://collector:4318
OrchardCore__OpenTelemetry__Otlp__Protocol=HttpProtobuf
```

## Prometheus / Mimir Queries

Metric names become snake case with a unit suffix (`http.server.request.duration` becomes `http_server_request_duration_seconds`), `orchardcore.tenant.name` becomes the `orchardcore_tenant_name` label, and `service.name` / `service.instance.id` become `job` / `instance`.

| Question | Query |
|---|---|
| Slowest shapes (with the `OrchardCore.DisplayManagement` meter) | `topk(10, histogram_quantile(0.95, sum by (le, orchardcore_shape_type) (rate(orchardcore_display_shape_duration_seconds_bucket[5m]))))` |
| p95 latency per tenant | `histogram_quantile(0.95, sum by (le, orchardcore_tenant_name) (rate(http_server_request_duration_seconds_bucket[5m])))` |
| Memory per instance | `sum by (instance) (dotnet_process_memory_working_set_bytes)` |
| Tenants running the most commands (with the `YesSql` meter) | `topk(10, sum by (orchardcore_tenant_name) (rate(db_client_operation_duration_seconds_count[5m])))` |

## Tempo TraceQL

```text
{ span.orchardcore.tenant.name = "Default" && duration > 500ms }
{ name =~ "Render shape.*" }
{ span.db.system.name != nil && duration > 100ms }
```

## Custom Meter in a Module

```csharp
using System.Diagnostics.Metrics;
using OrchardCore.Telemetry;

public sealed class OrderMetrics
{
    private static readonly Counter<long> s_ordersPlaced =
        OrchardCoreTelemetry.Meter.CreateCounter<long>("mycompany.orders.placed", "{order}", "Number of placed orders.");

    public void OrderPlaced(string tenantName)
        => s_ordersPlaced.Add(1, new KeyValuePair<string, object>(OrchardCoreTelemetryConstants.Attributes.TenantName, tenantName));
}
```

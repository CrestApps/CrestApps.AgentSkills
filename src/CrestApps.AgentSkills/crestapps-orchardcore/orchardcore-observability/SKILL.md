---
name: orchardcore-observability
description: Skill for OpenTelemetry traces, metrics and logs of the CrestApps Orchard Core modules. Covers turning collection on with Orchard Core AddOpenTelemetry and HasOtlpEndpoint, the CrestApps.OrchardCore.* and CrestApps.Core* sources and meters, AICompletionService gen_ai telemetry, IncludeTenantName and the orchardcore.tenant.name tag, the naming, outcome and banned-tag rules, the shared helpers in CrestApps.OrchardCore.Diagnostics (TelemetryScope, BackgroundTaskTelemetry, MeasuredTaskRun, DeferredWork, RetryTelemetry, TraceContextPropagation, CrestAppsEventIds), the meter of each area, and instrumenting a custom module. Use this skill when requests mention CrestApps telemetry, OTLP, the Aspire dashboard, a missing CrestApps metric or span, background task health, or adding instruments to a module built on CrestApps.OrchardCore. Strong matches include CrestAppsOrchardCoreTelemetry, TelemetryScope, MeasuredTaskRun and crestapps.background_task.outcomes.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps Orchard Core Observability

You are an Orchard Core expert. Help the user collect and extend the OpenTelemetry telemetry of the CrestApps.OrchardCore modules.

The modules emit traces, metrics and logs through `System.Diagnostics` only: no OpenTelemetry package is referenced by a module, and nothing is recorded until a listener subscribes. Each area has its own meter and activity source named `CrestApps.OrchardCore.<Area>`. CrestApps.Core reports under `CrestApps.Core` and `CrestApps.Core.*` (see `crestapps-core-observability`), and Orchard Core under `OrchardCore` (see `orchardcore-opentelemetry`).

## Guidelines

- Collection is a host decision. There is no feature to enable: the startup project calls Orchard Core's `AddOpenTelemetry()` and adds the CrestApps sources and meters.
- The modules instrument their own operations only (a call reaching an agent, a report run, an import, a checkout). They never wrap a single HTTP or SDK call, YesSql queries, Orchard Core locks, recipes, file stores or SignalR connections; those libraries report their own telemetry.
- Metric tags are bounded. Never put ids, phone numbers, user names, URLs, hosts, file names, SQL or free-text names on a metric. Ids go on spans and logs; phone numbers, transcripts and SIP URIs go nowhere.
- A failed span records the exception type and stack trace, never the message.
- Use the shared helpers for new instruments so names, outcomes, buckets and the tenant tag stay consistent.
- All C# classes must use the `sealed` modifier, except View Models.

## Turning it on

Install `OrchardCore.OpenTelemetry` (Orchard Core brings every OpenTelemetry package) in the web/startup project:

```csharp
using CrestApps.Core;
using CrestApps.OrchardCore.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Follow Orchard Core's option for tagging metrics with the tenant (default true).
CrestAppsOrchardCoreTelemetry.IncludeTenantName =
    builder.Configuration.GetValue("OrchardCore:OpenTelemetry:Metrics:IncludeTenantName", true);

// True when OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_{SIGNAL}_ENDPOINT
// or OrchardCore:OpenTelemetry:Otlp:Endpoint is set.
var exportTelemetry = builder.Configuration.HasOtlpEndpoint();

// 'X.*' does not match 'X', so both are listed.
string[] ownSources =
[
    CrestAppsTelemetry.SourceName,
    CrestAppsTelemetry.SourceName + ".*",
    CrestAppsOrchardCoreTelemetry.SourceNameWildcard,
    "AICompletionService",
];

builder.Services.AddOrchardCms(orchardCoreBuilder =>
{
    if (exportTelemetry)
    {
        orchardCoreBuilder.AddOpenTelemetry(openTelemetry => openTelemetry
            .WithTracing(tracing => tracing.AddSource(ownSources))
            .WithMetrics(metrics => metrics.AddMeter(ownSources)));
    }
});
```

A host configured through settings lists the same names:

```json
{
  "OrchardCore": {
    "OpenTelemetry": {
      "Tracing": { "Sources": [ "CrestApps.OrchardCore.*", "CrestApps.Core", "CrestApps.Core.*", "AICompletionService" ] },
      "Metrics": { "Meters": [ "CrestApps.OrchardCore.*", "CrestApps.Core", "CrestApps.Core.*", "AICompletionService" ], "IncludeTenantName": true }
    }
  }
}
```

- `AICompletionService` carries the AI clients' `gen_ai.*` spans and metrics only while **Enable OpenTelemetry** is on under **Settings → Artificial Intelligence** (General card).
- Other libraries (OpenAI SDK, SignalR, MCP and A2A SDKs, Npgsql, Polly) are not collected by default; add their names to `OrchardCore:OpenTelemetry:Tracing:Sources` or `Metrics:Meters`.
- Locally, the Aspire AppHost sets `OTEL_EXPORTER_OTLP_ENDPOINT`. Without it, run the standalone Aspire dashboard (`mcr.microsoft.com/dotnet/aspire-dashboard`, OTLP on `4317`) and set `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` and `OTEL_SERVICE_NAME`.
- Turn `IncludeTenantName` off to limit metric series on hosts with many tenants. Spans and logs carry the tenant either way.

## Rules every instrument follows

| Rule | Value |
| --- | --- |
| Meter and source | `CrestApps.OrchardCore.<Area>` (`CrestAppsOrchardCoreTelemetry.CreateMeter(area)`, `CreateActivitySource(area)`) |
| Instrument names | `crestapps.<area>.<thing>`; spans `crestapps.<area>.<operation>` |
| Units | UCUM: `s`, `By`, `{row}`, `{item}`; durations in seconds |
| `outcome` | `ok`, `error`, `skipped`, `cancelled`, `timeout`, `blocked` (plus bounded area values); `unknown` when the operation ended without saying |
| Failures | The count of a duration histogram with `outcome=error`, not a separate counter; `error` also carries `error.type` |
| Banned metric tags | `CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys` (ids, phone numbers, users, URLs, hosts, lock keys, file names, SQL, free-text names) |
| Tenant | `orchardcore.tenant.name` on metrics when `IncludeTenantName` is on |
| Async hops | Work that crosses a queue, an outbox or the webhook inbox keeps the queuing request's `traceparent` and starts a span **linked** to it, not a child |
| Logs | `[LoggerMessage]` with event ids from `CrestAppsEventIds` (10000 and up; CrestApps.Core uses ids below 10000) |

Some counters recorded inside a unit of work (contact center queues and offers, messaging, inventory holds, orders, subscriptions) can count twice when a write conflict retries the work; treat them as approximate.

## Shared platform instruments

Meter and source `CrestApps.OrchardCore.Platform`:

| Instrument | What it measures |
| --- | --- |
| `crestapps.background_task.outcomes` | Background task runs that did not end `ok` (`error`, `skipped`, `skipped_lease`, `cancelled`, `timeout`, `budget_exceeded`), including failures a task logged and swallowed |
| `crestapps.background_task.items` | Items a run processed, by `result` |
| `crestapps.background_task.last_success` | Unix time of the last run without an error; alert when it falls behind |
| `crestapps.background_task.lock.utilization` | Run duration divided by the task's lock expiration; near 1 the lock can expire mid-run |
| `crestapps.deferred_task.duration` | Deferred tasks queued with `DeferredWork.Add` (span `crestapps.deferred.<name>`) |
| `crestapps.retry.attempts` | Retries of the solution's own retry loops (`area`, `reason`, `attempt` capped at 5) |
| `crestapps.background.pacer.gate_wait`, `.pause` | Time bulk batches wait for, and hold, the shared pacing gate |

Orchard Core already records `orchardcore.background_task.duration` and a span for every background task run.

## Instrumenting a module

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

public static class InvoiceSyncTelemetry
{
    public static readonly Meter Meter = CrestAppsOrchardCoreTelemetry.CreateMeter("InvoiceSync");

    public static readonly ActivitySource Source = CrestAppsOrchardCoreTelemetry.CreateActivitySource("InvoiceSync");

    public static readonly Histogram<double> SyncDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.invoicesync.sync.duration",
        "One synchronization of invoices with the ERP.");
}

public sealed class InvoiceSyncService
{
    public async Task SyncAsync(string batchId, string provider, CancellationToken cancellationToken)
    {
        using var telemetry = TelemetryScope.Start(
            InvoiceSyncTelemetry.Source,
            "crestapps.invoicesync.sync",
            InvoiceSyncTelemetry.SyncDuration,
            new TagList { { CrestAppsOrchardCoreTelemetry.ProviderTag, provider } });

        // Ids are span tags only.
        telemetry.SetSpanTag("crestapps.invoicesync.batch.id", batchId);

        try
        {
            // ... the work ...
            telemetry.Succeed();
        }
        catch (Exception ex)
        {
            // Sets outcome error/cancelled/timeout, adds error.type, records the exception type on the span.
            telemetry.Fail(ex);

            throw;
        }
    }
}
```

`TelemetryScope` records the duration with `outcome` and the tenant tag when disposed. `AddMetricTag` adds a bounded tag to both the metric and the span, `SetSpanTag` to the span only, `SetOutcome` sets a bounded area value, `Fail(reason)` fails without an exception. `TelemetryScope.StartLinked(source, name, duration, traceParent)` starts a span linked to queued work.

Background tasks and deferred work:

```csharp
public sealed class InvoiceSyncBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => SyncDueAsync(serviceProvider, run, cancellationToken));

    private static async Task SyncDueAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var processed = 0;

        // ... the work ...

        if (processed == 0)
        {
            run.Skip();

            return;
        }

        run.AddItems(processed);
    }
}
```

- `this.RunAsync(...)` (`BackgroundTaskTelemetry`) feeds the platform background task instruments and reads the lock expiration from the task's `[BackgroundTask]` attribute. Call `run.ReportError(ex)` for an error you catch and swallow so the run is not counted as healthy; `run.SkipForLease()` when another node holds the work; `run.BudgetExceeded()` when the run stopped at its time or item budget.
- `DeferredWork.Add("invoice_sync", async scope => { ... })` replaces `ShellScope.AddDeferredTask` when the deferred work should be measured and linked to the request.
- `TraceContextPropagation.Capture()` returns the current `traceparent` to store with queued work; `TraceContextPropagation.StartLinkedActivity(source, name, traceParent)` starts the span that handles it later.
- `RetryTelemetry.RecordRetry(area, reason, attempt)` counts one retry of your own loop.
- `ExternalCallTelemetry.GetStatusClass(statusCode)` turns an HTTP status into a bounded `status_class`.

## Where each area reports

See [Meters by area](references/meters-by-area.md) for the meter of every module and its main instruments. Module skills with a monitoring section (Contact Center, Telephony, Report Builder, Data Connectors, Webhooks) give the details in context.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No CrestApps spans or metrics | `HasOtlpEndpoint()` is false (no OTLP endpoint set), or `CrestApps.OrchardCore.*` is missing from the sources and meters |
| No `gen_ai.*` telemetry | `AICompletionService` not added, or **Enable OpenTelemetry** is off in the AI settings |
| `crestapps.core.ai.usage.*` empty | The **AI Usage Analytics** feature is off; only `gen_ai.client.token.usage` is available without it |
| Too many metric series | Turn off `OrchardCore:OpenTelemetry:Metrics:IncludeTenantName`; check a custom instrument does not tag ids |
| Old dashboards empty | Renamed instruments: report metrics use `outcome` instead of `crestapps.report.outcome` and no longer carry report, query or data set ids; `contactcenter.outbox.*` and `asterisk.realtime.*` became `crestapps.contactcenter.outbox.*` and `crestapps.asterisk.realtime.*` |
| Queued work shows as a separate trace | Expected: outbox, inbox and import work start linked spans; follow the span link |

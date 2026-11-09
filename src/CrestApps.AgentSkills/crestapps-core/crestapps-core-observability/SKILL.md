---
name: crestapps-core-observability
description: Skill for OpenTelemetry observability in CrestApps.Core 2.0. Covers the CrestApps.Core ActivitySource and Meter defined in CrestAppsTelemetry and AITelemetry, CrestApps.Core.* sub-area meters such as CrestApps.Core.Templates, the AICompletionService gen_ai spans and metrics from Microsoft.Extensions.AI, wiring a host with AddCrestAppsOpenTelemetry from the CrestApps.Core.OpenTelemetry package and UseOtlpExporter, OTEL_EXPORTER_OTLP_ENDPOINT and logs with scopes, the EnableOpenTelemetry and EnableSensitiveData AI options and the OpenAI.Experimental.EnableOpenTelemetry switch, the tag cardinality and sensitive data policies, log event id ranges, checking a trace in the Aspire dashboard, adding a crestapps.core instrument or span with CreateDurationHistogram and StartLinkedActivity, and testing with MeterListener and ActivityListener. Use it when collecting CrestApps.Core traces, metrics or logs, instrumenting framework code, or troubleshooting missing spans and metrics.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Observability

Every CrestApps.Core package reports through one `ActivitySource` and one `Meter`, both named `CrestApps.Core` (`CrestAppsTelemetry.ActivitySource` and `CrestAppsTelemetry.Meter`). They use `System.Diagnostics` only, so the framework packages take no OpenTelemetry dependency. Nothing is recorded until a listener subscribes. The `CrestApps.Core.OpenTelemetry` package connects them to the OpenTelemetry SDK.

## Guidelines

- Reference `CrestApps.Core.OpenTelemetry` in the host and call `AddCrestAppsOpenTelemetry()` on the OpenTelemetry builder. It registers source and meter names only. It adds no exporter, no ASP.NET Core or HttpClient instrumentation, and no runtime meter.
- The `CrestApps.Core` spans and metrics do not depend on any option. They are recorded whenever a listener subscribes. The `EnableOpenTelemetry` AI option only adds the Microsoft.Extensions.AI `gen_ai.*` telemetry, and `EnableSensitiveData` only adds prompt and response content to it.
- Instrument names are a public contract. Dashboards and alerts depend on them, so never rename one.
- Put ids on spans and in log scopes. Never put them on a metric tag.
- The instrument catalogue grows with each release, so this skill names the main instruments only. The complete list, with the tags of each instrument, is on the [Observability page](https://core.crestapps.com/docs/core/observability) (`src/CrestApps.Core.Docs/docs/core/observability.md` in the CrestApps.Core repository). Read it before you chart, alert on or add a name.

## Registration

```xml
<ItemGroup>
  <PackageReference Include="CrestApps.Core.OpenTelemetry" />
  <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
  <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
  <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
</ItemGroup>
```

```csharp
using CrestApps.Core.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .AddCrestAppsOpenTelemetry()
    .UseOtlpExporter();

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});
```

- `UseOtlpExporter()` sends traces, metrics and logs to `OTEL_EXPORTER_OTLP_ENDPOINT`, and `OTEL_SERVICE_NAME` names the service. The Aspire AppHost sets both for every project it runs. When OTLP is the only exporter, skip the block while `OTEL_EXPORTER_OTLP_ENDPOINT` is empty, as the sample hosts do.
- With `IncludeScopes`, each log line of a chat turn carries the `SessionId` and `ProfileId` scope properties (`InteractionId` for a Chat Interaction), next to its trace id.
- Add the OpenTelemetry log provider after any `builder.Logging.ClearProviders()` call, or that call removes it. It runs next to NLog or Serilog.
- A host that already configures OpenTelemetry, for example through Aspire ServiceDefaults, also calls `builder.Services.AddOpenTelemetry().AddCrestAppsOpenTelemetry()`. `AddOpenTelemetry()` is safe to call more than once. A host can instead register the same names with `AddSource` and `AddMeter`.

## What Is Emitted

| Source or meter | Contents |
|---|---|
| `CrestApps.Core` | The framework spans and metrics. Names and tags are in `CrestAppsTelemetry` (`CrestApps.Core.Abstractions`) and `AITelemetry` (`CrestApps.Core.AI.Abstractions`) |
| `CrestApps.Core.*` | Meters of standalone libraries, such as `CrestApps.Core.Templates` (`TemplateTelemetry.MeterName`) with `crestapps.core.templates.render.duration` |
| `AICompletionService` | The `gen_ai.*` spans and metrics of the Microsoft.Extensions.AI clients while `EnableOpenTelemetry` is on. The constant is `CrestAppsTelemetry.AIClientSourceName` |
| `OpenAI.*` | The OpenAI SDK's own `gen_ai.*` telemetry, which the Azure OpenAI chat-completions client reports through |
| `Microsoft.AspNetCore.SignalR.Server`, `Microsoft.AspNetCore.Http.Connections` | The hub invocation a chat turn starts from (spans) and the active connections (meter) |
| `Experimental.ModelContextProtocol`, `A2A`, `A2A.AspNetCore` | MCP and A2A SDK client and server operations |
| `Npgsql` | PostgreSQL commands, when the pgvector store is used |
| `Polly` | Retries, timeouts and open circuits of resilience pipelines that have a name and telemetry configured (meter only) |

`AddCrestAppsOpenTelemetry()` registers all of these names.

### Spans

One chat turn is one trace. The `gen_ai` spans appear while `EnableOpenTelemetry` is on:

```text
crestapps.chat.turn                     the chat hub received a message
├── crestapps.orchestrate.context       the orchestration context is built
│   └── crestapps.rag.preemptive        one per preemptive retrieval handler
│       └── crestapps.index.search      one per index query
├── crestapps.guardrail.check           when the profile has guardrails
└── crestapps.orchestrate               the orchestrator reply, also for sub-agents
    ├── crestapps.orchestrate.plan      when the default orchestrator plans its tools
    ├── chat {model}                    gen_ai, one per provider round trip
    └── execute_tool {tool}             gen_ai, one per tool call
        └── crestapps.tool.invoke       the traced tool invoker, with tool.name and tool.source
```

Other entry points start their own trace: `crestapps.realtime.session` (a voice conversation), `crestapps.ingest.run` (a file source or crawler run), `crestapps.ingest.process` (one uploaded document), `crestapps.mcp.connect` (an MCP connection and handshake) and `crestapps.background.cycle` (one pass of a periodic service). Queued work runs in `crestapps.background.item`, a new trace with a link back to the request that queued it.

Spans carry the ids that help find a trace: `crestapps.session.id`, `crestapps.profile.id` and `crestapps.data_source.id` (`AITelemetry.SessionIdAttribute`, `ProfileIdAttribute` and `DataSourceIdAttribute`).

### Metrics by Area

Durations are `Histogram<double>` instruments in seconds with advised bucket boundaries. The `outcome` tag is `ok`, `error`, `cancelled` or `blocked`, and some instruments add `partial` or `empty`.

| Area | Main instruments (tags) |
|---|---|
| Orchestration and completions | `crestapps.core.ai.orchestration.duration` and `crestapps.core.ai.orchestration.time_to_first_token` (`orchestrator`, `outcome`), `crestapps.core.ai.completion.time_to_first_token` and `crestapps.core.ai.completion.errors` (`client`, `error.type`), `crestapps.core.ai.planning.duration` |
| Tools and handlers | `crestapps.core.ai.tool.duration` (`tool.source`, `outcome`), `crestapps.core.ai.tools.scoped_count`, `crestapps.core.ai.handler.errors` (`handler`) |
| Safety and limits | `crestapps.core.ai.security.prompt_checks` (`decision`, `direction`), `crestapps.core.ai.guardrail.duration`, `crestapps.core.ai.ratelimit.rejections` (`limiter`), `crestapps.core.ai.resilience.retries`, `crestapps.core.ai.resilience.stream_failures` |
| Retrieval and ingestion | `crestapps.core.ai.retrieval.duration` (`mode`, `outcome`), `crestapps.core.ai.retrieval.fallbacks`, `crestapps.core.ai.embedding.fallbacks`, `crestapps.core.ai.ingestion.duration`, `crestapps.core.ai.ingestion.run.items` (`result`), `crestapps.core.ai.webcrawler.crawl.duration` |
| Chat, voice and MCP | `crestapps.core.chat.turn.duration`, `crestapps.core.realtime.session.duration` (`end_reason`), `crestapps.core.realtime.sessions.active`, `crestapps.core.realtime.first_audio_latency`, `crestapps.core.mcp.client.connect.duration` (`transport`, `outcome`) |
| Usage | `crestapps.core.ai.usage.requests`, `crestapps.core.ai.usage.tokens` (adds `token.type`) and `crestapps.core.ai.usage.duration` (`operation`, `client`, `deployment`, `purpose`, `streaming`), only with `AddCoreAIUsageTracking()` |
| Framework | `crestapps.core.index.operation.duration` (`provider`, `operation`, `outcome`), `crestapps.core.store.commit.duration` (`store`), `crestapps.core.queue.depth` and `crestapps.core.queue.wait.duration` (`queue`), `crestapps.core.background.item.duration` and `crestapps.core.background.cycle.duration` (`service`) |

While `EnableOpenTelemetry` is on, Microsoft.Extensions.AI adds `gen_ai.client.operation.duration` and `gen_ai.client.token.usage` under `AICompletionService`.

## AI Options

The two switches are on `DefaultAIOptions`. The framework registers a scoped `DefaultAIOptions` that applies a site's `GeneralAIOptions` overrides, and framework code takes that scoped instance.

| Option | Default | Effect |
|---|---|---|
| `EnableOpenTelemetry` | `false` | Adds `UseOpenTelemetry(...)` with the `AICompletionService` name to the OpenAI, Azure AI Inference and Ollama completion clients, the Azure OpenAI Responses path, the Claude orchestrator, and the embedding, image, speech-to-text, text-to-speech and realtime clients from `IAIClientFactory`. Each chat round trip gets a `chat {model}` span and each tool call an `execute_tool` span |
| `EnableSensitiveData` | `false` | Adds prompts, responses and tool arguments to those `gen_ai.*` spans. They can contain personal data. It applies only while `EnableOpenTelemetry` is on |

```csharp
using CrestApps.Core.AI.Models;

builder.Services.Configure<DefaultAIOptions>(options =>
{
    options.EnableOpenTelemetry = true;
});
```

- A site value applies only with its flag: `GeneralAIOptions.OverrideEnableOpenTelemetry` with `EnableOpenTelemetry`, and `OverrideEnableSensitiveData` with `EnableSensitiveData`. A settings page that saves a value without its flag changes nothing.
- The Azure OpenAI chat-completions client calls the OpenAI SDK directly, so `EnableOpenTelemetry` does not reach it. Its `gen_ai.*` telemetry comes from the SDK after the host calls `AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true)` at startup. `AddCrestAppsOpenTelemetry()` collects the `OpenAI.*` names but does not set the switch.
- A chat client from `IAIClientFactory.CreateChatClientAsync` gets no `gen_ai` spans by itself. Add `UseOpenTelemetry` last, so it sits below the tool loop and each round trip and each tool call gets its own span:

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;

var chatClient = await clientFactory.CreateChatClientAsync(deployment, builder =>
{
    builder.UseFunctionInvocation(loggerFactory, invocation => invocation.UseTextToolResults());

    if (defaultOptions.EnableOpenTelemetry)
    {
        builder.UseOpenTelemetry(loggerFactory, CrestAppsTelemetry.AIClientSourceName, telemetry => telemetry.EnableSensitiveData = defaultOptions.EnableSensitiveData);
    }
});
```

`UseTextToolResults()` sends each tool call through `AIToolInvocation`, the traced invoker that records `crestapps.tool.invoke` and `crestapps.core.ai.tool.duration`. A custom tool loop calls `AIToolInvocation.InvokeAsync(function, arguments, cancellationToken)` itself. A tool that returns its failure to the model as text calls `AIToolInvocation.ReportFailure()`, so the call still counts as an error.

## Policies

### Tag Cardinality

Each tag value multiplies the series a collector stores. Tag metrics with bounded values only: the provider or client, the operation, the purpose, the outcome, the direction, the orchestrator and other small sets. Never tag a metric with:

- session, user, interaction, connection, profile or item ids;
- URLs, hosts, file names or index names;
- MCP or A2A tool names, which a remote server chooses;
- prompt text, responses, or the part of a prompt that a security rule matched.

Put an id on the span (`activity?.SetTag(AITelemetry.SessionIdAttribute, sessionId)`) and in the log scope. A tool name goes on the span as `tool.name`, never on `crestapps.core.ai.tool.duration`.

### Sensitive Data

- The framework's own spans, metrics and log scopes never carry prompt, response or tool-argument text, whatever the options say. Keep it that way in new framework code.
- Content reaches telemetry only through `EnableSensitiveData`. It is off by default, and it adds prompts, responses and tool arguments to the Microsoft.Extensions.AI `gen_ai.*` spans only.

### Log Event Ids

Failures that the pipeline logs and then swallows, so that the request can continue, have their own event ids and a counter. Alert on them without matching message text.

| Range | Area | Examples |
|---|---|---|
| 1000s | A2A | `A2ALog` |
| 2000s | AI pipeline | 2001 completion failed, 2002 planning failed, 2003 handler skipped, 2004 guardrail check failed, 2101 and 2102 security audit of input and output |
| 4000s | Document processing and ingestion | 4001 a document could not be read, 4002 embeddings failed |
| 5000s | Retrieval | 5001 hierarchical retrieval fell back to chunks |
| 9000s | Sample hosts | The MVC and Blazor samples' own messages |

A new message in framework code uses `[LoggerMessage]` source generation with the next free id in its area's range, next to the area's existing log class.

## Verify in the Aspire Dashboard

1. Run `dotnet run --project src/Startup/CrestApps.Core.Aspire.AppHost` and open the dashboard link it prints. The AppHost sets `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME` for both sample hosts.
2. Without the AppHost, start the dashboard alone:

   ```bash
   docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
   ```

   Set `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` and `OTEL_SERVICE_NAME=MvcWeb` for the host. Open `http://localhost:18888` with the login link from the container output.
3. In the sample's admin **Settings** page, turn on **Enable OpenTelemetry** and save, to add the `gen_ai` spans.
4. Send a chat message on a profile that has a tool or a data source.
5. Under **Traces**, open the trace that contains `crestapps.chat.turn` and compare it with the tree above. Under **Metrics**, look for `crestapps.core.*` and `gen_ai.*`. Under **Structured logs**, filter by the trace id and check `SessionId`, `ProfileId` and the event ids.

Without a collector, `dotnet-counters monitor -n <process name> --counters CrestApps.Core` shows the metrics live.

## Add an Instrument or Span

These rules apply to code in CrestApps.Core. Application code uses its own meter and source names, or records into an existing instrument with the documented tags.

- Declare the instrument in its area's catalogue: `CrestAppsTelemetry` for framework services, `AITelemetry` for the AI pipeline. A standalone library that does not reference those abstractions creates its own `CrestApps.Core.<Area>` meter, like `TemplateTelemetry`. `AddCrestAppsOpenTelemetry()` collects it through `CrestApps.Core.*`.
- Name an instrument `crestapps.core.<area>.<thing>`, lowercase, with `_` between the words of one part (`time_to_first_token`). Name a span `crestapps.<area>.<operation>`, without `core` (`crestapps.tool.invoke`).
- Record a duration in seconds on a histogram from `CrestAppsTelemetry.CreateDurationHistogram(name, description)`, which sets the `s` unit and the bucket advice. Pass `CrestAppsTelemetry.LongDurationBucketBoundaries` for work that takes minutes. Give a counter a unit in braces, such as `{error}`.
- Tag with the constants on `CrestAppsTelemetry` (`OutcomeTag`, `ErrorTypeTag`, `ProviderTag`, `OperationTag`, `ServiceTag`, `QueueTag`, `StoreTag`) and `AITelemetry` (`ClientTag`, `ReasonTag`, `ModeTag`, `PurposeTag` and the others). Add a new constant next to them instead of a string literal. Set `outcome` from `OutcomeOk`, `OutcomeBlocked` or `CrestAppsTelemetry.GetOutcome(exception)`.
- Start a span with `CrestAppsTelemetry.ActivitySource.StartActivity(...)` and use `?.` on it, because it is `null` when nothing listens.
- Add the name to the Observability page, the 2.0.0 changelog and the expected names in `CrestAppsTelemetryTests`.

```csharp
// In AITelemetry, next to the other instruments.
public static readonly Histogram<double> SummaryDuration = CrestAppsTelemetry.CreateDurationHistogram(
    "crestapps.core.ai.summary.duration",
    "The duration of summarizing one conversation.");
```

```csharp
using var activity = CrestAppsTelemetry.ActivitySource.StartActivity("crestapps.chat.summarize");
activity?.SetTag(AITelemetry.SessionIdAttribute, sessionId);

var startTimestamp = Stopwatch.GetTimestamp();
var outcome = CrestAppsTelemetry.OutcomeOk;

try
{
    return await CreateSummaryAsync(sessionId, cancellationToken);
}
catch (Exception ex)
{
    outcome = CrestAppsTelemetry.GetOutcome(ex);
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message).AddException(ex);

    throw;
}
finally
{
    AITelemetry.SummaryDuration.Record(
        Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
        new KeyValuePair<string, object>(CrestAppsTelemetry.OutcomeTag, outcome));
}
```

The complete service and its test are in [observability-examples.md](references/observability-examples.md).

### Queued Work

The request that queued an item has usually ended when a worker picks it up. Capture its context when the item is queued, and start the worker's span with `StartLinkedActivity`. The span starts a new trace with a link back, not a child span:

```csharp
// On the work item, set when it is created.
public ActivityContext QueuedContext { get; } = Activity.Current?.Context ?? default;

public long QueuedTimestamp { get; } = Stopwatch.GetTimestamp();
```

```csharp
var queueTag = new KeyValuePair<string, object>(CrestAppsTelemetry.QueueTag, "reports");

// When the item is written to the channel.
CrestAppsTelemetry.QueueDepth.Add(1, queueTag);

// When the worker reads it.
CrestAppsTelemetry.QueueDepth.Add(-1, queueTag);
CrestAppsTelemetry.QueueWaitDuration.Record(Stopwatch.GetElapsedTime(workItem.QueuedTimestamp).TotalSeconds, queueTag);

using var activity = CrestAppsTelemetry.StartLinkedActivity("crestapps.background.item", workItem.QueuedContext);
```

Record the item on `CrestAppsTelemetry.BackgroundItemDuration` with `service`, `work_item_type` and `outcome`, and each pass of a periodic service on `BackgroundCycleDuration`.

### Async Iterators

An async iterator resumes with its consumer's `Activity.Current`, not with the span it started. Restore the span after each `yield return`, or the spans started for the next item get the consumer's span as their parent:

```csharp
using var activity = CrestAppsTelemetry.ActivitySource.StartActivity("crestapps.example.stream");

await foreach (var update in updates.WithCancellation(cancellationToken))
{
    yield return update;

    if (activity is not null)
    {
        Activity.Current = activity;
    }
}
```

Record the duration in a `finally` block. It also runs when the consumer stops early.

## Test Telemetry

Listen with `MeterListener` and `ActivityListener`. No OpenTelemetry SDK is needed. The CrestApps.Core tests share a `TelemetryCollector` in `tests/CrestApps.Core.Tests/Core/Telemetry`, and [observability-examples.md](references/observability-examples.md) has a complete test.

```csharp
var measurements = new ConcurrentQueue<string>();
var activities = new ConcurrentQueue<Activity>();

using var meterListener = new MeterListener
{
    InstrumentPublished = (instrument, listener) =>
    {
        if (instrument.Meter.Name == CrestAppsTelemetry.SourceName)
        {
            listener.EnableMeasurementEvents(instrument);
        }
    },
};

meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => measurements.Enqueue(instrument.Name));
meterListener.Start();

using var activityListener = new ActivityListener
{
    ShouldListenTo = source => source.Name == CrestAppsTelemetry.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activities.Enqueue,
};

ActivitySource.AddActivityListener(activityListener);
```

- The meter and the source are static, and every test in the process shares them while tests run in parallel. Match a value that only your test uses, such as a new id or provider name, and never assert an exact count of an instrument that other tests also record.
- `StartActivity` returns `null` unless a listener samples the span, so the code under test must use `activity?.`.
- A library meter such as `CrestApps.Core.Templates` has its own name. Match it in `InstrumentPublished` as well.
- In CrestApps.Core, `CrestAppsTelemetryTests` checks that every documented instrument is published, and that every duration is a `Histogram<double>` in seconds with bucket advice.

## Troubleshooting

- **No `crestapps.*` spans or `crestapps.core.*` metrics**: the names are not registered, or nothing exports them. Call `AddCrestAppsOpenTelemetry()` and add an exporter. `UseOtlpExporter()` needs `OTEL_EXPORTER_OTLP_ENDPOINT`.
- **No `gen_ai` spans**: `EnableOpenTelemetry` is off, or a site saved it without `OverrideEnableOpenTelemetry`. For Azure OpenAI chat completions, set the `OpenAI.Experimental.EnableOpenTelemetry` switch. A chat client from `CreateChatClientAsync` needs its own `UseOpenTelemetry`.
- **The `gen_ai` spans have no prompts**: `EnableSensitiveData` is off, which is the default.
- **A span sits beside the span that should contain it**: an async iterator did not restore `Activity.Current` after `yield return`.
- **Queued work is in another trace**: this is by design. Follow the link on `crestapps.background.item`.
- **Log lines have no `SessionId`**: `IncludeScopes` is off, or the OpenTelemetry log provider was added before `ClearProviders()`.
- **Token totals look doubled**: a chart adds `crestapps.core.ai.usage.tokens` to `gen_ai.client.token.usage`, or adds every `token.type`. See `crestapps-core-ai-usage-metering`.

## Upgrading Within 2.0 Previews

- `EnableOpenTelemetry` now covers more than chat: the embedding, image, speech and realtime clients, the Claude orchestrator and the Azure OpenAI Responses path. The `gen_ai` source keeps the name `AICompletionService`, so an existing `AddSource("AICompletionService")` keeps working.
- `ProviderAICompletionClient<TProvider>` and `ClaudeOrchestrator` take the scoped `DefaultAIOptions` instead of `IOptions<DefaultAIOptions>`, so a site's settings reach them. Only code that constructs them directly changes.
- A settings page saves `OverrideEnableOpenTelemetry` and `OverrideEnableSensitiveData` with the values. Before, the sample hosts' **Enable OpenTelemetry** switch had no effect.

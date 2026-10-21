---
name: crestapps-core-ai-usage-metering
description: Skill for AI usage metering in CrestApps.Core 2.0. Covers how every client created by IAIClientFactory (chat, embedding, image, speech-to-text, text-to-speech and realtime) records one AICompletionUsageRecord per provider request, enabling it with GeneralAIOptions EnableAIUsageTracking, the recorded units (cached, reasoning and audio tokens, audio duration, characters, images) and AIUsageOperationTypes, labeling requests by category and purpose with UseUsageLabels, AIUsageScope, AICompletionContextKeys and AIUsagePurposes, metering clients built outside the factory with AIUsageMetering Meter, observing records with IAICompletionUsageObserver, storing them with IAICompletionUsageStore, and building per-model, per-category and per-purpose reports with AIUsageReport. Use it when pricing or attributing AI usage, building a usage dashboard, or troubleshooting missing or mislabeled usage records.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Usage Metering

Every client `IAIClientFactory` returns meters its own requests by wrapping the provider's client and reading the provider-agnostic `UsageDetails` on each response. That covers every provider, including custom `IAIClientProvider` implementations, with no provider code.

## Guidelines

- Turn metering on with `GeneralAIOptions.EnableAIUsageTracking = true`. It is off by default.
- A record is written per request sent to the provider, not per user turn. Planning, query rewrites, vision calls, each tool-calling round trip, each embedding batch and each realtime response are separate records, so the request count equals billed requests.
- Metering sits below every middleware you add, so `UseFunctionInvocation()` yields one record per round trip and a cached response yields none.
- Label your own requests with `UseUsageLabels(...)` on the client pipeline, or an `AIUsageScope` around a call into a service that creates the client for you. Labels never reach the provider.
- A client you build without the factory is not metered until you wrap it with `AIUsageMetering.Meter(...)`.

## Enable Metering and Storage

```csharp
using CrestApps.Core.AI.Models;

builder.Services.Configure<GeneralAIOptions>(options =>
{
    options.EnableAIUsageTracking = true;
});
```

Each record goes to every registered `IAICompletionUsageObserver`. The default `IAICompletionUsageService` is one of them and saves through `IAICompletionUsageStore`. The YesSql chat-session stores (`AddCoreAIChatSessionStoresYesSql()`, which calls `AddCoreAICompletionUsageStoresYesSql()`) and the EntityCore chat-session stores (`AddCoreAIChatSessionStoresEntityCore()`) register a store. A record whose scope was already disposed when the response finished is dropped rather than failing the answer.

## What a Record Holds

| Property | Meaning |
|---|---|
| `OperationType` | `AIUsageOperationTypes.Chat`, `Embedding`, `Image`, `SpeechToText`, `TextToSpeech`, `Realtime` or `RealtimeTranscription` |
| `ClientName`, `ConnectionName`, `DeploymentName`, `ModelName` | Where the request went; `ModelName` is what the provider reported, else the deployment |
| `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` | Tokens in and out |
| `CachedInputTokenCount`, `ReasoningTokenCount` | Discounted cached input; reasoning output tokens |
| `InputAudioTokenCount`, `OutputAudioTokenCount` | Audio tokens, billed at their own rate |
| `AudioDurationMs`, `CharacterCount`, `ImageCount` | Duration-, character- and image-billed units |
| `ContextType`, `Purpose` | Category of feature and reason for the request |
| `SessionId`, `ProfileId`, `InteractionId`, `UserId`, `VisitorId`, `ResponseLatencyMs`, `IsStreaming` | Attribution and timing |

A provider that measures a non-token unit better reports it in `UsageDetails.AdditionalCounts` under `AIUsageAdditionalCounts.AudioDurationMs`, `CharacterCount` or `ImageCount`.

## Labeling Requests

`ContextType` is the feature category (`AIChatSession` and `ChatInteraction` for the built-in chats; any other feature names its own). `Purpose` is why the request was made. The framework uses `AIUsagePurposes`: `Conversation`, `Planning`, `SearchQueryGeneration`, `Retrieval`, `Indexing`, `TitleGeneration`, `SubAgent`, `TabularProcessing`, `DataExtraction`, `PostSessionProcessing`, `ImageAnalysis`, `DocumentAnalysis`, `ImageGeneration`, `ChartGeneration`, `MetadataExtraction`, `Transcription`, `SpeechSynthesis`, `VoiceConversation`.

When your code creates the client:

```csharp
using CrestApps.Core.AI.Completions; // UseUsageLabels
using CrestApps.Core.AI.Resilience; // UseDefaultResilience

var chatClient = await clientFactory.CreateChatClientAsync(deployment, builder => builder
    .UseDefaultResilience()
    .UseUsageLabels(contextType: "Leads", purpose: "LeadScoring"));
```

`UseUsageLabels` exists on the chat, embedding, image, speech-to-text, text-to-speech and realtime builders. `CreateRealtimeClientAsync` takes no pipeline callback, so build on the returned client:

```csharp
var realtimeClient = (await clientFactory.CreateRealtimeClientAsync(deployment))
    .AsBuilder()
    .UseUsageLabels(contextType: "Support", purpose: AIUsagePurposes.VoiceConversation)
    .Build();
```

When a service creates the client for you, wrap the call in a scope:

```csharp
using CrestApps.Core.AI.Completions;

using var usageScope = AIUsageScope.Begin(contextType: "Leads", purpose: "LeadScoring");

var response = await completionService.CompleteAsync(deployment, messages, context, cancellationToken);
```

Scopes nest and a `null` label inherits from the enclosing scope. A scope flows with the async context, so it does not reliably survive `yield return` inside an async iterator; label streaming code with `UseUsageLabels` or per-request keys instead.

A single request can carry labels in `ChatOptions.AdditionalProperties` or `AICompletionContext.AdditionalProperties` under `AICompletionContextKeys.UsageContextType` and `UsagePurpose`. Resolution order: the request's value, the client's `UseUsageLabels`, the current `AIUsageScope`, the request's `AICompletionContextKeys.DefaultUsagePurpose`, then the chat session or interaction category.

## Metering a Client Built Outside the Factory

Wrap the provider client before adding any middleware, so every round trip is recorded:

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Services;
using Microsoft.Extensions.AI;

IChatClient metered = AIUsageMetering.Meter(
    providerClient,
    clientName: "MyProvider",
    connectionName: null,
    deploymentName: "model-id",
    serviceProvider,
    defaultPurpose: AIUsagePurposes.Conversation);

var client = new ChatClientBuilder(metered)
    .UseFunctionInvocation()
    .Build(serviceProvider);
```

`AIUsageMetering.Meter` has overloads for `IChatClient` and `IEmbeddingGenerator<string, Embedding<float>>`. The Claude orchestrator uses it for its Anthropic client. The Copilot orchestrator's SDK-managed requests are not metered.

## Observing Records

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Logging;

public sealed class UsageBudgetObserver : IAICompletionUsageObserver
{
    private readonly ILogger<UsageBudgetObserver> _logger;

    public UsageBudgetObserver(ILogger<UsageBudgetObserver> logger)
    {
        _logger = logger;
    }

    public Task UsageRecordedAsync(AICompletionUsageRecord record, CancellationToken cancellationToken = default)
    {
        if (record.TotalTokenCount > 50_000)
        {
            _logger.LogWarning(
                "Large {OperationType} request on {Deployment} for {Purpose}: {Tokens} tokens.",
                record.OperationType,
                record.DeploymentName,
                record.Purpose,
                record.TotalTokenCount);
        }

        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddScoped<IAICompletionUsageObserver, UsageBudgetObserver>();
```

Keep observers fast and non-throwing; they run as each response finishes.

## Reports

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Services;

var records = await usageService.GetAsync(startUtc, endUtc, cancellationToken);

var perModel = AIUsageReport.Build(records, AIUsageReportGrouping.Model);
var perPurpose = AIUsageReport.Build(records, AIUsageReportGrouping.ModelAndPurpose);
var perCategory = AIUsageReport.Build(records, AIUsageReportGrouping.ModelAndCategory);
```

Every grouping keeps client, model and operation type apart because each combination is priced separately. `AIUsageReportRow` sums `RequestCount`, every token, audio, character and image total, and reports `AverageResponseLatencyMs`. Records written before 2.0 have no operation type and report as chat requests.

To turn usage into cost, multiply each unit by its meter's price for an estimate, or split each meter's billed cost in proportion to the units each group used in the same period. A single blended per-model price misprices groups with a different input and output mix.

## Provider Notes

- `AzureOpenAICompletionClient` records each tool-calling round trip and keeps cached, reasoning and audio tokens.
- The Azure realtime protocol reads usage on `response.done` and on completed input transcriptions, including duration-billed transcription.
- Azure AI Speech reports recognized speech start and end, which gives speech-to-text its duration. Trailing silence is not counted.
- A cascaded realtime deployment is not wrapped itself; each leg meters its own usage.

## Upgrading from 1.x

- Usage was previously recorded only for chat completions and once per response. Expect more records per turn now; that is the correct billed count.
- `IAIClientFactory.CreateRealtimeClientAsync` returns a metering wrapper. Code that compared the result with a provider instance should call `GetService` instead.
- Stored records need no migration; the new values live on the record document.

---
name: crestapps-core-ai-usage-metering
description: Skill for AI usage metering in CrestApps.Core 2.0. Covers opting in with AddCoreAIUsageTracking or AddUsageTracking, how every client created by IAIClientFactory (chat, embedding, image, speech-to-text, text-to-speech and realtime) records one AICompletionUsageRecord per provider request, the recorded units (cached, cache-write, reasoning, audio and image tokens, web searches, audio duration and bytes, characters, voices) and AIUsageOperationTypes, labeling requests with UseUsageLabels, AIUsageScope and AIUsagePurposes, metering clients built outside the factory with AIUsageMetering Meter, IAICompletionUsageObserver and IAICompletionUsageStore, AIUsageReport and AIAudioUsageReport, and per-deployment prices with AIDeploymentPricing, AIUsageMeters and AIUsageCostEstimator. Use it when pricing or attributing AI usage, estimating cost, building a usage or audio usage dashboard, or troubleshooting missing usage records.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Usage Metering

Every client `IAIClientFactory` returns meters its own requests by wrapping the provider's client and reading the provider-agnostic `UsageDetails` on each response. That covers every provider, including custom `IAIClientProvider` implementations, with no provider code.

## Guidelines

- Metering is opt-in by registration: call `services.AddCoreAIUsageTracking()` or `.AddUsageTracking()` on the AI suite builder. Without it, and without an `IAICompletionUsageObserver` of your own, nothing is metered and `IAICompletionUsageService` is not registered.
- A record is written per request sent to the provider, not per user turn. Planning, query rewrites, vision calls, each tool-calling round trip, each embedding batch and each realtime response are separate records, so the request count equals billed requests.
- Metering sits below every middleware you add, so `UseFunctionInvocation()` yields one record per round trip and a cached response yields none.
- Label your own requests with `UseUsageLabels(...)` on the client pipeline, or an `AIUsageScope` around a call into a service that creates the client for you. Labels never reach the provider.
- A client you build without the factory is not metered until you wrap it with `AIUsageMetering.Meter(...)`.

## Enable Metering and Storage

```csharp
using CrestApps.Core.AI;

builder.Services.AddCoreAIUsageTracking();

// or, on the AI suite builder
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddUsageTracking()));
```

Metering follows registration: a client is wrapped only when an `IAICompletionUsageObserver` is registered (checked through `IServiceProviderIsService`, so nothing is constructed to find out). `AddCoreAIUsageTracking()` registers the default `IAICompletionUsageService`, which is itself an observer. A host can register its own observer instead of, or alongside, it.

Each record goes to every registered `IAICompletionUsageObserver`. The default `IAICompletionUsageService` saves through `IAICompletionUsageStore`. The YesSql chat-session stores (`AddCoreAIChatSessionStoresYesSql()`, which calls `AddCoreAICompletionUsageStoresYesSql()`) and the EntityCore chat-session stores (`AddCoreAIChatSessionStoresEntityCore()`) register a store. A record whose scope was already disposed when the response finished is dropped rather than failing the answer.

## What a Record Holds

| Property | Meaning |
|---|---|
| `OperationType` | `AIUsageOperationTypes.Chat`, `Embedding`, `Image`, `SpeechToText`, `TextToSpeech`, `Realtime` or `RealtimeTranscription` |
| `ClientName`, `ConnectionName`, `DeploymentName`, `ModelName` | Where the request went; `ModelName` is what the provider reported, else the deployment |
| `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` | Tokens in and out |
| `CachedInputTokenCount`, `ReasoningTokenCount` | Discounted cached input; reasoning output tokens |
| `InputAudioTokenCount`, `OutputAudioTokenCount` | Audio tokens, billed at their own rate |
| `CacheWriteInputTokenCount`, `CachedInputAudioTokenCount` | Tokens written to the prompt cache (Anthropic bills a premium); cached audio input |
| `InputImageTokenCount`, `OutputImageTokenCount` | Image tokens in and out |
| `AcceptedPredictionTokenCount`, `RejectedPredictionTokenCount` | Predicted-output tokens; rejected ones are still billed |
| `WebSearchCount`, `WebFetchCount` | Server-side web searches and fetches |
| `AdditionalCounts` | Any other count the provider reported, by its own name |
| `AudioDurationMs`, `CharacterCount`, `ImageCount` | Duration-, character- and image-billed units; `AudioDurationMs` also holds generated TTS length when known |
| `AudioStartMs`, `AudioInputBytes`, `AudioOutputBytes`, `SegmentCount` | Start of recognized speech, audio sizes, transcription segments |
| `AudioSampleRate`, `AudioFormat`, `VoiceId`, `Language`, `TargetLanguage`, `Speed`, `Pitch` | What an STT or TTS request asked for |
| `ContextType`, `Purpose` | Category of feature and reason for the request |
| `SessionId`, `ProfileId`, `InteractionId`, `UserId`, `VisitorId`, `ResponseLatencyMs`, `IsStreaming` | Attribution and timing |

Input and output token counts include the cached, cache-write, audio and image tokens; those fields say how many were of each kind. A provider that measures a non-token unit better reports it in `UsageDetails.AdditionalCounts` under `AIUsageAdditionalCounts.AudioDurationMs`, `CharacterCount` or `ImageCount`. The adapters' own names are read too: Anthropic's `CacheCreationInputTokens`, `WebSearchRequests` and `WebFetchRequests`, and OpenAI's audio, image and predicted-output token details. A TTS model that reports only tokens has its output tokens recorded as audio output tokens, and an image model's as image output tokens.

## Labeling Requests

`ContextType` is the feature category (`AIChatSession` and `ChatInteraction` for the built-in chats; any other feature names its own). `Purpose` is why the request was made. The framework uses `AIUsagePurposes`: `Conversation`, `Planning`, `SearchQueryGeneration`, `Retrieval`, `Indexing`, `TitleGeneration`, `SubAgent`, `TabularProcessing`, `DataExtraction`, `PostSessionProcessing`, `ImageAnalysis`, `DocumentAnalysis`, `ImageGeneration`, `ChartGeneration`, `MetadataExtraction`, `Transcription`, `SpeechSynthesis`, `VoiceConversation`, `Guardrail`, `PersonalDataRedaction`, `ConversationSummarization`.

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

## Usage Metrics

`AddCoreAIUsageTracking()` also registers an observer that emits each record as OpenTelemetry metrics on the `CrestApps.Core` meter, for every client kind:

| Instrument | Tags |
|---|---|
| `crestapps.core.ai.usage.requests` | `operation`, `client`, `deployment`, `purpose`, `streaming` |
| `crestapps.core.ai.usage.tokens` | The same, plus `token.type`: `input`, `output`, `cached`, `reasoning` or `audio` |
| `crestapps.core.ai.usage.duration` | The same as requests; the response latency in seconds |

- Session, user and profile ids stay in the stored records. They never reach a metric.
- `input` and `output` already include the `cached`, `reasoning` and `audio` tokens. A total is `input` plus `output`; never add up every `token.type`.
- While `EnableOpenTelemetry` is on, `gen_ai.client.token.usage` counts the same chat tokens again. Chart one of the two, never their sum: the usage metrics cover every client kind and carry the purpose, and `gen_ai.client.token.usage` follows the OpenTelemetry conventions that other tools read.
- A host that registers only its own observer, without `AddCoreAIUsageTracking()`, gets no usage metrics.
- The metrics carry no prices. Estimate cost from the stored records with the reports below.

See `crestapps-core-observability` for collecting them.

## Reports

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Services;

var records = await usageService.GetAsync(startUtc, endUtc, cancellationToken);

var perModel = AIUsageReport.Build(records, AIUsageReportGrouping.Model);
var perPurpose = AIUsageReport.Build(records, AIUsageReportGrouping.ModelAndPurpose);
var perCategory = AIUsageReport.Build(records, AIUsageReportGrouping.ModelAndCategory);
```

Every grouping keeps client, model and operation type apart because each combination is priced separately. `AIUsageReportRow` sums `RequestCount`, every token, web search, audio, character and image total and `AdditionalCounts`, and reports `AverageResponseLatencyMs`. Records written before 2.0 have no operation type and report as chat requests.

### Audio Usage

`AIAudioUsageReport.Build` reports speech-to-text and text-to-speech requests only:

```csharp
var perModel = AIAudioUsageReport.Build(records, AIAudioUsageReportGrouping.Model);
var perVoice = AIAudioUsageReport.Build(records, AIAudioUsageReportGrouping.ModelAndVoice);
var perLanguage = AIAudioUsageReport.Build(records, AIAudioUsageReportGrouping.ModelAndLanguage);
```

`AIAudioUsageReportRow` exposes `AudioHours`, `AudioDurationMs`, `SpeechDurationMs`, `RequestCount`, `RequestsWithDurationCount`, `CharacterCount`, `AudioInputBytes`, `AudioOutputBytes`, `SegmentCount` and tokens. TTS length is known only when the provider reports it or the audio is WAV, so a row whose `RequestsWithDurationCount` is below `RequestCount` is a minimum.

## Deployment Pricing and Estimated Cost

A deployment can carry its own prices in `AIDeploymentPricing` (`Currency`, default `USD`, and `Prices`). Each `AIDeploymentPrice` is an `Amount` for `Per` base units of one `AIUsageMeters` meter:

| Meter | Base unit |
|---|---|
| `InputTokens`, `CachedInputTokens`, `CacheWriteInputTokens`, `OutputTokens` | token |
| `InputAudioTokens`, `CachedInputAudioTokens`, `OutputAudioTokens`, `InputImageTokens`, `OutputImageTokens` | token |
| `AudioSeconds` | second of audio |
| `Characters`, `Images`, `Requests`, `WebSearches`, `WebFetches` | one each |
| `AdditionalCount` | the provider count named by `CountName` |

- Cached, cache-write, audio and image tokens are taken out of the input or output count and billed at their own price only when the deployment prices that meter; otherwise they stay at the token price.
- Several prices for one meter add up.
- `AboveInputTokens` makes a long-context price: a request with more input tokens pays it instead of the lower tier.
- `Increment` rounds each request's quantity up, such as `15` for audio billed in 15-second steps.

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;

deployment.SetPricing("USD",
[
    new AIDeploymentPrice { Meter = AIUsageMeters.InputTokens, Amount = 2.5m, Per = 1_000_000 },
    new AIDeploymentPrice { Meter = AIUsageMeters.CachedInputTokens, Amount = 1.25m, Per = 1_000_000 },
    new AIDeploymentPrice { Meter = AIUsageMeters.OutputTokens, Amount = 10m, Per = 1_000_000 },
]);

var pricing = deployment.GetPricing();
```

`SetPricing` drops incomplete prices and removes the pricing when none is left. A configured deployment declares it under `Properties`:

```json
{
  "Name": "gpt-4o",
  "Properties": {
    "AIDeploymentPricing": {
      "Currency": "USD",
      "Prices": [
        { "Meter": "InputTokens", "Amount": 2.5, "Per": 1000000 },
        { "Meter": "OutputTokens", "Amount": 10, "Per": 1000000 }
      ]
    }
  }
}
```

Pass pricing to a report and each row gains `Currency` and `EstimatedCost`. Requests are priced by `DeploymentName`; deployments in different currencies, or without a price, land in separate rows:

```csharp
using CrestApps.Core.AI.Services;

var pricingByDeployment = await deploymentStore.GetPricingByDeploymentAsync(cancellationToken);

var perModel = AIUsageReport.Build(records, AIUsageReportGrouping.Model, pricingByDeployment);
var audio = AIAudioUsageReport.Build(records, AIAudioUsageReportGrouping.Model, pricingByDeployment);

decimal? oneRecord = AIUsageCostEstimator.Estimate(record, AIUsageCostEstimator.GetPricing(pricingByDeployment, record));
decimal? allRecords = AIUsageCostEstimator.Estimate(records, pricingByDeployment);
```

`GetPricingByDeploymentAsync` is an `IAIDeploymentStore` extension declared on `AIUsageCostEstimator`. To match an actual bill, including discounts and credits, split each meter's billed cost in proportion to the units each group used in the same period; a single blended per-model price misprices groups with a different input and output mix.

## Provider Notes

- `AzureOpenAICompletionClient` records each tool-calling round trip and keeps cached, reasoning and audio tokens.
- The Azure realtime protocol reads usage on `response.done` and on completed input transcriptions, including duration-billed transcription.
- Azure AI Speech reports recognized speech start and end, which gives speech-to-text its duration. Trailing silence is not counted.
- A cascaded realtime deployment is not wrapped itself; each leg meters its own usage.

## Upgrading Within 2.0 Previews

- **Tracking is opted into by registration.** `GeneralAIOptions.EnableAIUsageTracking` and `GeneralAISettings.EnableAIUsageTracking` are `[Obsolete]` and ignored. Call `AddCoreAIUsageTracking()` (or `.AddUsageTracking()`) in any host that had the option on. `GeneralAISettings.EnableAIUsageTracking` stays readable so a migration can turn a stored `true` into that registration.
- Without the registration, the factory returns unmetered clients, `AIUsageMetering.Meter` returns the client it was given, and `IAICompletionUsageService` cannot be resolved.
- New usage-record fields need no store migration; older records read them as zero or empty.

## Upgrading from 1.x

- Usage was previously recorded only for chat completions and once per response. Expect more records per turn now; that is the correct billed count.
- `IAIClientFactory.CreateRealtimeClientAsync` returns a metering wrapper. Code that compared the result with a provider instance should call `GetService` instead.
- Stored records need no migration; the new values live on the record document.

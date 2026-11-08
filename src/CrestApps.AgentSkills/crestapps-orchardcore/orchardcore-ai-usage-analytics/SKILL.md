---
name: orchardcore-ai-usage-analytics
description: Skill for the AI Usage Analytics feature and AI usage metering in CrestApps.OrchardCore 3.0. Covers the CrestApps.OrchardCore.AI.UsageAnalytics feature and its Reports-based AI Usage Analytics report, the ViewChatAnalytics permission, the Metered usage, Audio usage, Completions and Voice tables, estimated cost from per-deployment prices (the Pricing card, AIDeploymentPricing, AIUsageMeters and the Pricing section of the AIDeployment recipe and deployment steps), carrier prices on the Costs card and AICallMetrics.EstimateCost, the CompletionUsage and VoiceUsage Report Builder data sets, AICompletionUsageRecord units, usage categories and purposes, UseUsageLabels and AIUsageScope, IAICompletionUsageService, IAIVoiceSessionSummaryStore, and upgrading from the EnableAIUsageTracking setting. Use this skill when requests mention AI usage analytics, AI token usage, AI billing or cost, deployment prices, metered usage, usage labels, or tracking which feature spent AI tokens.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Usage Analytics

Every AI client the site creates through `IAIClientFactory` meters its own requests, whatever the
provider and whatever the kind (chat, embeddings, images, speech-to-text, text-to-speech, realtime), while
the **AI Usage Analytics** feature is enabled. Its report shows those records per model, category and
purpose, audio usage, chat completions and automated voice calls, with an estimated cost from the prices set
on each deployment.

## Setup

| Item | Value |
| --- | --- |
| Feature | AI Usage Analytics, `CrestApps.OrchardCore.AI.UsageAnalytics` (in the `CrestApps.OrchardCore.AI.Chat` package; depends on `CrestApps.OrchardCore.AI.Chat` and `CrestApps.OrchardCore.Reports`) |
| Report | **Reports > Billing & Usage > AI Usage Analytics** (`/Admin/Reports/view/ai-usage-analytics`, `AIUsageAnalyticsReport`) |
| Permission | `ViewChatAnalytics` (granted to Administrator by default) |
| Exports | CSV, Excel with **Reports (OpenXml)**, PDF with **Reports (PDF)** |

Usage is recorded only while the feature is on (its startup calls `AddCoreAIUsageTracking()`); nothing used
before it was enabled can be reported. There is no tracking checkbox any more.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.UsageAnalytics",
        "CrestApps.OrchardCore.AI.Chat.Analytics"
      ],
      "disable": []
    }
  ]
}
```

`CrestApps.OrchardCore.AI.Chat.Analytics` (AI Chat Session Analytics) registers the usage and voice
summary index tables and the store that persists voice session summaries; without it automated calls leave
no voice summary and the Report Builder data sets below are not listed.

## What the report shows

| Section | Contents | Group by |
| --- | --- | --- |
| **Metered usage** | One row per request sent to a provider: planning, query rewriting, each tool-calling step, image descriptions and each embedding batch count separately. Cached responses add nothing. **Est. cost**, **Cache writes** and **Other usage** columns, and the estimated total per currency above the table | **Model**, **Model and category**, **Model and purpose** |
| **Audio usage** | Speech-to-text and text-to-speech: requests, hours, speech hours, characters, audio in and out, segments, tokens, **Est. cost**. An asterisk on hours means some requests reported no length, so the figure is a minimum | **Model**, **Model and voice**, **Model and language** |
| **Completions** | Chat completions that belong to a chat session or chat interaction: calls, sessions, interactions, tokens, average latency, **Est. cost** | **User and model**, **Model**, **Deployment**, **AI profile**, **Connection** |
| **Voice** | One summary per automated AI call: calls, AI, caller and silence minutes, average call, handoff rate, barge-ins, text and audio tokens, answer and model latency | **Deployment (model)**, **AI profile**, **Campaign**, **Channel**, **Engine**, **Day** |

Filters are a **Date range** (site time zone; the report starts on **All Time** through
`IReport.DefaultDateRange`) and an optional **AI profile**; requests that belong to no profile, such as
indexing, drop out while a profile is selected.

Metered units on each `AICompletionUsageRecord`: `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`,
`CachedInputTokenCount`, `ReasoningTokenCount`, `InputAudioTokenCount`, `OutputAudioTokenCount`,
`AudioDurationMs`, `CharacterCount`, `ImageCount`, `CacheWriteInputTokenCount`, `CachedInputAudioTokenCount`,
`InputImageTokenCount`, `OutputImageTokenCount`, `RejectedPredictionTokenCount`, `WebSearchCount`,
`WebFetchCount`, plus `OperationType` (`AIUsageOperationTypes`: `Chat`, `Embedding`, `Image`, `SpeechToText`,
`TextToSpeech`, `Realtime`, `RealtimeTranscription`), `ContextType` (category), `Purpose`, `ProfileId`,
`SessionId`, `InteractionId`, `ClientName`, `ConnectionName`, `DeploymentName` and `ModelName`. Speech
requests also keep the sample rate, audio format, speed, pitch, voice and language.

## Categories and purposes

| Category (`ContextType`) | Source |
| --- | --- |
| Chat sessions, Chat interactions | Set by the chat surfaces |
| `Sms`, `Voice`, `Api`, `Workflow`, `A2A` | `AIUsageCategories` in `CrestApps.OrchardCore.AI.Core` (`A2A` is `AIUsageCategories.AgentToAgent`) |

Purposes come from CrestApps.Core `AIUsagePurposes` (`Conversation`, `Planning`, `SearchQueryGeneration`,
`Retrieval`, `Indexing`, `TitleGeneration`, `SubAgent`, `DataExtraction`, `PostSessionProcessing`,
`ImageAnalysis`, `DocumentAnalysis`, `ImageGeneration`, `Transcription`, `SpeechSynthesis`,
`VoiceConversation` and others) and from `AIUsageFeaturePurposes` in `CrestApps.OrchardCore.AI.Core`
(`ConversationConclusion`, `HandoffSummary`, `ReplyDecision`, `ReEngagement`, `Utility`,
`ProfileBuilderCoaching`, `ProfileBuilderReplay`).

## Label your module's requests

Requests are metered without any code. Labels only say what they were for, so they can be grouped.

### On a client you create

```csharp
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using Microsoft.Extensions.AI;

public sealed class LeadScoringService
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;

    public LeadScoringService(IAIDeploymentManager deploymentManager, IAIClientFactory clientFactory)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
    }

    public async Task<string> ScoreAsync(string leadSummary, CancellationToken cancellationToken)
    {
        var deployment = await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Utility,
            cancellationToken: cancellationToken);

        if (deployment is null)
        {
            return null;
        }

        var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(contextType: "Leads", purpose: "LeadScoring"));

        var response = await client.GetResponseAsync(leadSummary, cancellationToken: cancellationToken);

        return response.Text;
    }
}
```

`UseUsageLabels(contextType, purpose)` exists on the chat, embedding, image, speech-to-text,
text-to-speech and realtime client builders. A `null` label falls back to the request or scope.

### Around a service that creates the client

```csharp
using var usageScope = AIUsageScope.Begin(contextType: "Leads", purpose: "LeadScoring");

var completion = await completionService.CompleteAsync(deployment, messages, context);
```

### On one request

```csharp
completionContext.AdditionalProperties[AICompletionContextKeys.UsageContextType] = AIUsageCategories.Workflow;
completionContext.AdditionalProperties[AICompletionContextKeys.UsagePurpose] = "LeadScoring";
```

Precedence is per-request keys, then client labels, then the current `AIUsageScope`. Labels travel on
each request, so prefer client labels for streamed responses: a scope begun inside an async iterator does
not flow to the code that reads it.

## Read usage in code

```csharp
var records = await usageService.GetAsync(startDateUtc, endDateUtc, cancellationToken);
var voiceCalls = await voiceSessionStore.GetAsync(startDateUtc, endDateUtc, cancellationToken);
```

`IAICompletionUsageService` is in `CrestApps.Core.AI.Completions` and `IAIVoiceSessionSummaryStore` in
`CrestApps.OrchardCore.AI.Core.Services`. Records are stored in the AI collection and are not aged out
automatically; plan your own retention if volume matters.

## Voice figures

Voice durations are measured from call audio and provider voice events, not from transcripts.
Turn-based calls cannot measure caller speech, so caller minutes, silence, barge-ins and the turn detection
wait are stored as unknown for them and excluded from those averages. Answer latency runs from the caller's
voice stopping to the assistant's first audio (realtime) or from the line being taken to speech starting
(turn-based); each call keeps the median, 90th and 95th percentiles and the longest. Outcomes are
`HandedToAgent`, `Transferred`, `CompletedByAI`, `Voicemail`, `NoAnswer`, `CallerHungUp` and `Failed`. The
review that concludes a call runs on the call's chat session, so its tokens count with the call.

## Deployment pricing and estimated cost

Each deployment carries its prices on a **Pricing** card (**Artificial Intelligence > Deployments > Edit**),
stored as CrestApps.Core's `AIDeploymentPricing`: a `Currency` (default `USD`) and `Prices`, each an
`AIDeploymentPrice` with `Meter`, `Amount`, `Per` (the base units the amount is for), optional `CountName`
(for `AdditionalCount`), `AboveInputTokens` (a long-context price that replaces the regular one above that
input size) and `Increment` (round each request up, such as `15` seconds).

| Meter (`AIUsageMeters`) | Base unit | Typical `Per` |
| --- | --- | --- |
| `InputTokens`, `CachedInputTokens`, `CacheWriteInputTokens`, `OutputTokens` | token | `1000000` |
| `InputAudioTokens`, `CachedInputAudioTokens`, `OutputAudioTokens`, `InputImageTokens`, `OutputImageTokens` | token | `1000000` |
| `AudioSeconds` | second | `60` or `3600` |
| `Characters` | character | `1000000` |
| `Images`, `Requests` | image, request | `1` |
| `WebSearches`, `WebFetches` | search, fetch | `1000` |
| `AdditionalCount` | the count named in `CountName` | any |

Cached, cache-write, audio and image tokens are part of the token counts; each is billed at its own price
only when the deployment has one, otherwise at the token price. Prices for the same meter add up. Requests
to a deployment without prices have no cost.

The `AIDeployment` recipe step imports a `Pricing` section per deployment (also read from
`Properties.AIDeploymentPricing`), and the **AI Deployments** deployment step exports it. Omit `Pricing` to
keep a deployment's prices, or set it to `null` to remove them.

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Pricing": {
            "Currency": "USD",
            "Prices": [
              { "Meter": "InputTokens", "Amount": 2.5, "Per": 1000000 },
              { "Meter": "CachedInputTokens", "Amount": 1.25, "Per": 1000000 },
              { "Meter": "OutputTokens", "Amount": 10, "Per": 1000000 }
            ]
          }
        }
      ]
    }
  ]
}
```

The **Costs** card of **Settings > Artificial Intelligence** keeps only the carrier's prices
(`AICostSettings`: `Currency`, `TelephonyPerMinute`, `SmsPerMessage`). Each AI voice call summary is priced
when the call ends: the call's minutes at the carrier price plus its tokens or minutes at the answering
deployment's prices, through `AICallMetrics.EstimateCost(summary, carrier, pricing, textsSent)`, which
returns `(decimal? Cost, string Currency)`. When the two currencies differ, only the AI part is kept.

List price times units is an estimate. To match an invoice, take each billing meter's cost for a period
and split it in proportion to the units each row used on that meter in the same period; discounts and
committed capacity are then included.

## Report Builder data sets

With **Data Pipelines Core** and **AI Chat Session Analytics** enabled, the `AIChat` data source (permission
`ViewChatAnalytics`) adds these data sets beside `ChatSessions` and `ChatSessionMetrics`:

| Data set | Rows | Notable fields |
| --- | --- | --- |
| `CompletionUsage` (AI completion usage) | One per metered request | `OperationType`, `ContextType`, `Purpose`, `Model`, `DeploymentName`, every unit count, `AudioMinutes`, `EstimatedCost`, `CostCurrency` |
| `VoiceUsage` (AI voice usage) | One per AI voice session summary | talk time, turns, tokens, `ResponseLatencyP50Ms`, `ResponseLatencyP90Ms`, `ResponseLatencyP95Ms`, `ResponseLatencyMaxMs`, `ModelLatencyP50Ms`, `DetectorWaitP50Ms`, `EstimatedCost`, `CostCurrency` |

For cost per model, group `CompletionUsage` by `Model` and `CostCurrency` and add `EstimatedCost` with the
**Sum** aggregate; for models without deployment prices, add a calculated field that multiplies each unit by
your own price. Count distinct `ActivityId` values of `VoiceUsage` to count calls. See
`orchardcore-report-builder`.

## Upgrading

- 2.x recorded only chat completions. 3.0 meters every request kind, so token totals rise and the
  **Chat completions** table counts chat completions only; embeddings and voice requests are in
  **Metered usage**. Metering every client needs the matching CrestApps.Core release.
- The **Enable AI usage tracking** checkbox and `EnableAIUsageTracking` in the `GeneralAISettings` recipe
  schema are gone. Enable `CrestApps.OrchardCore.AI.UsageAnalytics` instead; a site that had the setting on
  gets the feature enabled once at startup (`AIUsageAnalyticsFeatureMigrations`). The report and its menu
  entry come with that feature, not with AI Chat, and the old `/Admin/AI/UsageAnalytics/Index` page is now
  the Reports page `/Admin/Reports/view/ai-usage-analytics`.
- The **AI deployments** price text box of the **Costs** card, `AICostSettings.Deployments` and
  `CrestApps.OrchardCore.AI.Core.Models.AIDeploymentPrice` are removed. Prices live on each deployment's
  **Pricing** card; prices already entered are moved onto the matching deployments once at startup
  (`AIDeploymentPricingMigrations`). `AICallMetrics.EstimateCost` now takes the carrier prices and the
  deployment's `AIDeploymentPricing` and returns the cost with its currency.

## Related skills

- `orchardcore-ai` for AI settings, deployments and `IAIClientFactory`.
- `orchardcore-ai-chat-analytics` for per-session conversation metrics and the AI chat reports.
- `orchardcore-ai-model-capabilities` for resolving the deployments being metered.
- `orchardcore-reports` for the Reports framework the report runs on.
- `orchardcore-report-builder` for custom usage and cost reports.
- `crestapps-core-ai-usage-metering` for the framework metering APIs.

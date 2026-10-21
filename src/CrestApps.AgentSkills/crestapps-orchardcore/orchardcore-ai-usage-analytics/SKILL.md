---
name: orchardcore-ai-usage-analytics
description: Skill for the AI Usage Analytics report and AI usage metering in CrestApps.OrchardCore 3.0. Covers the Reports, Billing and Usage, AI Usage Analytics page in the CrestApps.OrchardCore.AI.Chat feature, the ViewChatAnalytics permission, the EnableAIUsageTracking setting in GeneralAISettings, the Metered usage, Completions and Voice tables and their grouping, AICompletionUsageRecord units (cached input, reasoning, audio tokens, audio minutes, characters, images), usage categories and purposes, labelling requests with UseUsageLabels on IAIClientFactory builders or with AIUsageScope.Begin, AICompletionContextKeys.UsageContextType and UsagePurpose, AIUsageCategories, AIUsagePurposes and AIUsageFeaturePurposes, IAICompletionUsageService, AI voice session summaries and IAIVoiceSessionSummaryStore, and turning usage into cost. Use this skill when requests mention AI usage analytics, AI token usage, AI billing, metered usage, usage labels, AI cost reporting, or tracking which feature spent AI tokens.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Usage Analytics

Every AI client the site creates through `IAIClientFactory` meters its own requests, whatever the
provider and whatever the kind (chat, embeddings, images, speech-to-text, text-to-speech, realtime). The
**AI Usage Analytics** page reports those records per model, category and purpose, plus chat completions
and automated voice calls.

## Setup

| Item | Value |
| --- | --- |
| Feature | AI Chat, `CrestApps.OrchardCore.AI.Chat` |
| Page | **Reports > Billing & Usage > AI Usage Analytics** (`/Admin/AI/UsageAnalytics/Index`) |
| Permission | `ViewChatAnalytics` (granted to Administrator by default) |
| Switch | **Enable AI usage tracking** under **Settings > Artificial Intelligence > General** |

Nothing is recorded while tracking is off, and nothing recorded before it was turned on can be reported.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Chat.Analytics"
      ],
      "disable": []
    },
    {
      "name": "Settings",
      "GeneralAISettings": {
        "EnableAIUsageTracking": true
      }
    }
  ]
}
```

`CrestApps.OrchardCore.AI.Chat.Analytics` (AI Chat Session Analytics) registers the usage and voice
summary index tables and the store that persists voice session summaries; without it automated calls leave
no voice summary. The tracking switch is read through `IOptionsMonitor<GeneralAIOptions>`, so it applies
without a tenant reload.

## What the page shows

| Section | Contents | Group by |
| --- | --- | --- |
| **Metered usage** | One row per request sent to a provider: planning, query rewriting, each tool-calling step, image descriptions and each embedding batch count separately. Cached responses add nothing | **Model**, **Model and category**, **Model and purpose** |
| **Completions** | Chat completions that belong to a chat session or chat interaction: calls, sessions, interactions, tokens, average latency | **User and model**, **Model**, **Deployment**, **AI profile**, **Connection** |
| **Voice** | One summary per automated AI call: calls, AI, caller and silence minutes, average call, handoff rate, barge-ins, text and audio tokens | **Deployment (model)**, **AI profile**, **Campaign**, **Channel**, **Engine**, **Day** |

Filters are a **Date range** (site time zone) and an optional **AI profile**; requests that belong to no
profile, such as indexing, drop out while a profile is selected.

Metered units on each `AICompletionUsageRecord`: `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`,
`CachedInputTokenCount`, `ReasoningTokenCount`, `InputAudioTokenCount`, `OutputAudioTokenCount`,
`AudioDurationMs`, `CharacterCount`, `ImageCount`, plus `OperationType` (`AIUsageOperationTypes`: `Chat`,
`Embedding`, `Image`, `SpeechToText`, `TextToSpeech`, `Realtime`, `RealtimeTranscription`), `ContextType`
(category), `Purpose`, `ProfileId`, `SessionId`, `InteractionId`, `ClientName`, `ConnectionName`,
`DeploymentName` and `ModelName`.

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
Turn-based calls cannot measure caller speech, so caller minutes, silence and barge-ins are stored as
unknown for them and excluded from those averages. Outcomes are `HandedToAgent`, `CompletedByAI`,
`Voicemail`, `NoAnswer`, `CallerHungUp` and `Failed`. The review that concludes a call runs on the call's
chat session, so its tokens count with the call. Audio tokens appear once the realtime provider reports them.

## Turning usage into cost

List price times units is an estimate. To match an invoice, take each billing meter's cost for a period
and split it in proportion to the units each row used on that meter in the same period; discounts and
committed capacity are then included.

## Upgrading from 2.x

- Only chat completions used to be recorded. 3.0 meters every request kind, so token totals rise and the
  **Chat completions** table now counts chat completions only; embeddings and voice requests are in
  **Metered usage**.
- Metering every client needs the matching CrestApps.Core release; no module code changes are required.
- Read `GeneralAIOptions.EnableAIUsageTracking` through `IOptionsMonitor<GeneralAIOptions>`.

## Related skills

- `orchardcore-ai` for AI settings and `IAIClientFactory`.
- `orchardcore-ai-chat-analytics` for per-session conversation metrics and the AI chat reports.
- `orchardcore-ai-model-capabilities` for resolving the deployments being metered.
- `orchardcore-reports` for the Reports menu the page sits in.

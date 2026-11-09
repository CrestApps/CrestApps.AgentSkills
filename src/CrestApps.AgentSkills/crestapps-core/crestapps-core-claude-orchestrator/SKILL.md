---
name: crestapps-core-claude-orchestrator
description: Skill for the Claude orchestrator in CrestApps.Core 2.0, a named IOrchestrator ("anthropic") built on the official Anthropic C# SDK. Covers AddClaudeOrchestrator and AddCoreAIClaudeOrchestrator registration, binding ClaudeOptions (ApiKey, BaseUrl, DefaultModel, EnablePromptCaching) yourself because the package reads no configuration section, the ClaudeOptionsValidator and the requirement that both ApiKey and DefaultModel are set, per-profile and per-interaction overrides with ClaudeSessionMetadata ClaudeModel and ClaudeEffortLevel, ClaudeClientService model discovery, resolving it with IOrchestratorResolver, sending scoped tools through IToolMaterializer and the function-invocation loop, Anthropic prompt caching breakpoints, and Anthropic usage metering through AIUsageMetering. Use it when running AI profiles or chat interactions on Anthropic Claude models, or when Claude reports it is not configured.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Claude Orchestrator

`CrestApps.Core.AI.Claude` adds `ClaudeOrchestrator`, an `IOrchestrator` named `"anthropic"` (title **Claude**). It talks to Anthropic through the official `Anthropic` NuGet package, so there is no CLI or external executable to install.

## Guidelines

- Use it when a profile or chat interaction should run on Anthropic models with the CrestApps tool pipeline. Claude does not use AI connections or deployments; the model comes from `ClaudeSessionMetadata.ClaudeModel` or `ClaudeOptions.DefaultModel`.
- The package does not bind configuration. Bind `ClaudeOptions` from your own section or site settings.
- Both `ApiKey` and `DefaultModel` must be set, or every turn answers "Claude is not configured and cannot be used until it has been configured." A per-profile model override does not replace the default-model requirement.
- Deployment model parameters such as `reasoningEffort` do not apply; Claude uses `ClaudeEffortLevel` on the session metadata.

## Registration

```csharp
using CrestApps.Core.AI.Claude;
using CrestApps.Core.AI.Claude.Models;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddClaudeOrchestrator()
    )
);

builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Claude"));
```

Raw form: `builder.Services.AddCoreAIClaudeOrchestrator();` after `AddCoreAIServices()` and `AddCoreAIOrchestration()`.

```json
{
  "Claude": {
    "ApiKey": "sk-ant-...",
    "BaseUrl": "https://api.anthropic.com",
    "DefaultModel": "claude-sonnet-4-6",
    "EnablePromptCaching": true
  }
}
```

`ClaudeOptionsValidator` (an `IValidateOptions<ClaudeOptions>`) fails when `ApiKey` is empty. Keep the key in user secrets or a key vault.

## Registered Services

| Service | Implementation |
|---|---|
| `IOrchestrator` | `ClaudeOrchestrator` (`ClaudeOrchestrator.OrchestratorName = "anthropic"`) |
| `ClaudeClientService` | Creates SDK clients and lists models from the Anthropic Models API |
| `IChatInteractionSettingsHandler` | `ClaudeChatInteractionSettingsHandler` persists chat interaction overrides |
| `IOrchestrationContextBuilderHandler` | `ClaudeOrchestrationContextHandler` copies `ClaudeSessionMetadata` into `OrchestrationContext.Properties` |

## Use It

```csharp
using CrestApps.Core.AI.Claude.Models;
using CrestApps.Core.AI.Claude.Services;

profile.OrchestratorName = ClaudeOrchestrator.OrchestratorName;

profile.Put(new ClaudeSessionMetadata
{
    ClaudeModel = "claude-opus-4-5",
    EffortLevel = ClaudeEffortLevel.Medium,
});

await profileManager.UpdateAsync(profile);
```

Or resolve it directly: `var orchestrator = resolver.Resolve(ClaudeOrchestrator.OrchestratorName);`.

`ClaudeEffortLevel` is `None`, `Low`, `Medium` or `High`; anything other than `None` is sent as the `reasoning_effort` additional property.

## How a Turn Runs

1. The model is `ClaudeSessionMetadata.ClaudeModel`, else `ClaudeOptions.DefaultModel`.
2. Tools come from `IToolRegistry.GetAllAsync(context.CompletionContext)`, the same scoped registry the default orchestrator uses, and are materialized into `ChatOptions.Tools` through `IToolMaterializer`. For a Chat Interaction the listable tools are re-checked against the current user (`IUserAccessor`) and denied ones are left out with a warning; AI sessions run the profile's tools as configured. `DisableTools` sends none.
3. The Anthropic chat client is wrapped with `AIUsageMetering.Meter(..., AIUsagePurposes.Conversation)` before `UseFunctionInvocation`, so every round trip is a usage record with client name `anthropic`. With `DefaultAIOptions.EnableOpenTelemetry` on, `UseOpenTelemetry` sits below the tool loop, so every round trip also gets a `gen_ai` span (see `crestapps-core-observability`).
4. Temperature, top-p, penalties and max tokens come from the completion context (a `null` value is not sent); the iteration cap from `DefaultAIOptions.MaximumIterationsPerRequest` (default 30).
5. A provider failure ends the stream with "An unexpected error occurred while communicating with Anthropic. Please try again." and is logged.

## Prompt Caching

With `ClaudeOptions.EnablePromptCaching` on (the default), each request marks ephemeral cache breakpoints on the last function tool, the end of the system prompt and the last block of the latest message. Later turns, and later round trips of the tool-calling loop within five minutes, read that prefix at the cache-read rate. Three of Anthropic's four breakpoints are used, and marked messages and tools are copies, so the caller's history is never changed. A prefix shorter than the model's minimum cacheable length is sent uncached without an error. Cache reads are metered as `CachedInputTokenCount` and cache writes as `CacheWriteInputTokenCount`. Turn it off only for workloads that never repeat a prefix within five minutes, since a cache write costs more than an uncached request.

## Model Discovery

`ClaudeClientService.CreateClient()` returns a configured `AnthropicClient`, and `ListModelsAsync(cancellationToken)` returns `ClaudeModelInfo` entries for the API key so editors can offer a picker; fall back to free text when discovery is unavailable. The MVC and Blazor sample hosts wire Claude into settings, AI profiles, profile templates and chat interactions.

## Troubleshooting

- **"Claude is not configured..."**: `ClaudeOptions.ApiKey` or `DefaultModel` is empty, usually because nothing bound the options. Set a default model even when every profile overrides it.
- **No usage records**: register usage tracking with `AddCoreAIUsageTracking()` or `.AddUsageTracking()` (see `crestapps-core-ai-usage-metering`).
- **"Claude is configured, but no default model or override has been selected."**: the model resolved to empty.
- **Claude never calls tools**: before the scoped-tools fix the orchestrator sent no tools at all; update to a current 2.0 preview.

## Upgrading Within 2.0 Previews

- The orchestrator now sends the scoped tools, so profile, interaction, system and MCP tools are called on Claude. `ClaudeOrchestrator` takes `IToolMaterializer` and `IUserAccessor` as new constructor parameters; code that constructs it directly passes them.
- Prompt caching is on by default. Set `EnablePromptCaching` to `false` to restore uncached requests.
- `ClaudeOrchestrator` takes the scoped `DefaultAIOptions` instead of `IOptions<DefaultAIOptions>`, so a site's **Enable OpenTelemetry** setting reaches it. Code that constructs it directly passes a `DefaultAIOptions`.

## Upgrading from 1.x

- Anthropic usage is now metered through `AIUsageMetering.Meter`.
- Deployments and their capabilities do not affect Claude; nothing changes for the removed deployment purpose.

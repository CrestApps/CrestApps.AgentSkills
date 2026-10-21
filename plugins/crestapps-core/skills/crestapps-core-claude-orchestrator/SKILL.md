---
name: crestapps-core-claude-orchestrator
description: Skill for the Claude orchestrator in CrestApps.Core 2.0, a named IOrchestrator ("anthropic") built on the official Anthropic C# SDK. Covers AddClaudeOrchestrator and AddCoreAIClaudeOrchestrator registration, binding ClaudeOptions (ApiKey, BaseUrl, DefaultModel) yourself because the package reads no configuration section, the ClaudeOptionsValidator and the requirement that both ApiKey and DefaultModel are set, per-profile and per-interaction overrides with ClaudeSessionMetadata ClaudeModel and ClaudeEffortLevel, ClaudeClientService model discovery, resolving it with IOrchestratorResolver, tool scoping and function invocation, and Anthropic usage metering through AIUsageMetering. Use it when running AI profiles or chat interactions on Anthropic Claude models, or when Claude reports it is not configured.
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
    "DefaultModel": "claude-sonnet-4-6"
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
2. Tools come from `IToolRegistry.GetAllAsync(context.CompletionContext)`, the same scoped registry the default orchestrator uses.
3. The Anthropic chat client is wrapped with `AIUsageMetering.Meter(..., AIUsagePurposes.Conversation)` before `UseFunctionInvocation`, so every round trip is a usage record with client name `anthropic`.
4. Temperature, top-p, penalties and max tokens come from the completion context; the iteration cap from `DefaultAIOptions.MaximumIterationsPerRequest`.
5. A provider failure ends the stream with "An unexpected error occurred while communicating with Anthropic. Please try again." and is logged.

## Model Discovery

`ClaudeClientService.CreateClient()` returns a configured `AnthropicClient`, and `ListModelsAsync(cancellationToken)` returns `ClaudeModelInfo` entries for the API key so editors can offer a picker; fall back to free text when discovery is unavailable. The MVC and Blazor sample hosts wire Claude into settings, AI profiles, profile templates and chat interactions.

## Troubleshooting

- **"Claude is not configured..."**: `ClaudeOptions.ApiKey` or `DefaultModel` is empty, usually because nothing bound the options. Set a default model even when every profile overrides it.
- **No usage records**: turn on `GeneralAIOptions.EnableAIUsageTracking` (see `crestapps-core-ai-usage-metering`).

## Upgrading from 1.x

- Anthropic usage is now metered through `AIUsageMetering.Meter`.
- Deployments and their capabilities do not affect Claude; nothing changes for the removed deployment purpose.

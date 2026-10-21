---
name: crestapps-core-ai-runtime
description: Skill for the provider-agnostic AI runtime in CrestApps.Core 2.0. Covers AddCoreAIServices and AddAISuite registration, provider connections and configured deployments under CrestApps AI Connections and Deployments, declaring deployment Features instead of the removed Purpose or Type, resolving deployments through slots with IAIDeploymentManager ResolveSlotAsync, GetAllBySlotAsync, ResolveUtilityOrDefaultAsync and ResolveSlotOrThrowAsync, DefaultAIDeploymentSettings site defaults, IAIClientFactory chat, embedding, image, speech and realtime clients, IAICompletionService and IAICompletionContextBuilder, custom IAICompletionClient providers, and upgrading 1.x code that used AIDeploymentPurpose. Use it when wiring AI services into an ASP.NET Core host, picking a deployment in code, or troubleshooting deployments that do not resolve.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Runtime

The runtime is the provider-agnostic layer every other AI feature builds on. A **connection** holds credentials and an endpoint, a **deployment** names a model on a connection and declares what that model can do, and a **slot** says what the installation uses a deployment for.

## Guidelines

- Prefer `AddCrestAppsCore(...).AddAISuite(...)` for application composition. Use `AddCoreAIServices()` only when the host composes raw service registrations itself.
- Program against `IAIClientFactory`, `IAICompletionService`, `IAICompletionContextBuilder` and `IAIDeploymentManager`. Never create a provider SDK client in business code.
- Every configured deployment declares its model capabilities through `Features`. A configured entry with no features and no legacy purpose is skipped at startup with the warning "declares no model capabilities".
- Pick deployments through slots (`AIDeploymentSlotNames`) and `ResolveSlotAsync(...)`. There is no deployment purpose or type in 2.0.
- Use `crestapps-core-ai-model-capabilities` for registering features, parameters and slots, `crestapps-core-ai-usage-metering` for usage records, and the provider skills (`crestapps-core-openai`, `crestapps-core-azure-openai`, `crestapps-core-ollama`, `crestapps-core-azure-ai-inference`) for provider-specific settings.

## Registration

```csharp
using CrestApps.Core.AI;
using CrestApps.Core.AI.OpenAI;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);
```

`AddAISuite(...)` calls `AddCoreServices()`, `AddCoreAIServices()` and `AddCoreAIOrchestration()`. Without the builder:

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIOpenAI();
```

`AddCoreAIServices()` also chains `AddCoreAITemplating()` and `AddCoreAIDeploymentCapabilities()` (features, parameters and slots). `IAIDeploymentManager` is registered by `AddCoreAIOrchestration()`, so register orchestration whenever code resolves deployments.

## Connections and Deployments

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "openai",
          "ClientName": "OpenAI",
          "ApiKey": "sk-..."
        }
      ],
      "Deployments": [
        {
          "ClientName": "OpenAI",
          "Name": "gpt-5-chat",
          "ModelName": "gpt-5",
          "ConnectionName": "openai",
          "Features": [ "textGeneration", "toolCalling", "streaming", "reasoning" ],
          "Parameters": {
            "reasoningEffort": { "AllowedValues": [ "Low", "Medium", "High" ], "DefaultValue": "Medium" }
          }
        },
        {
          "ClientName": "OpenAI",
          "Name": "embeddings",
          "ModelName": "text-embedding-3-large",
          "ConnectionName": "openai",
          "Features": [ "textEmbedding" ]
        }
      ]
    }
  }
}
```

- `Features` and `Parameters` on the entry are shorthand for `Properties:AIDeploymentMetadata:Features` and `...:Parameters`. Both forms work and merge.
- Configured deployments are read-only in admin UIs; stored deployments come from the YesSql or EntityCore stores and are merged with configuration through `IAIDeploymentStore`.
- A connection name that differs from another only by case is a duplicate. The first definition wins and the second is logged as a warning.

## Resolving a Deployment

| Slot (`AIDeploymentSlotNames`) | Required feature | Site default (`DefaultAIDeploymentSettings`) |
|---|---|---|
| `Chat` | `textGeneration` (excludes `realtime`) | `DefaultChatDeploymentName` |
| `Utility` | `textGeneration` (excludes `realtime`), falls back to `Chat` | `DefaultUtilityDeploymentName` |
| `Embedding` | `textEmbedding` | `DefaultEmbeddingDeploymentName` |
| `Image` | `imageOutput` | `DefaultImageDeploymentName` |
| `Vision` | `imageInput` | `DefaultVisionDeploymentName` |
| `SpeechToText` | `speechToText` | `DefaultSpeechToTextDeploymentName` |
| `TextToSpeech` | `textToSpeech` | `DefaultTextToSpeechDeploymentName` |
| `Realtime` | `realtime` | `DefaultRealtimeDeploymentName` |

Resolution walks one chain: the explicit name, the slot's site default, the fallback slot's explicit name and default, then the first capable deployment.

```csharp
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;

// Named deployment if it qualifies, otherwise the site default, otherwise the first capable one.
var chat = await deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: "gpt-5-chat");

// Background work: explicit utility, site utility, explicit chat, site chat, then first text-capable.
var utility = await deploymentManager.ResolveUtilityOrDefaultAsync(
    utilityDeploymentName: profile.UtilityDeploymentName,
    chatDeploymentName: profile.ChatDeploymentName);

// Throws InvalidOperationException when nothing can fill the slot.
var embedding = await deploymentManager.ResolveSlotOrThrowAsync(AIDeploymentSlotNames.Embedding);

// Everything eligible for a picker.
var candidates = await deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Chat);
```

`DefaultAIDeploymentSettings` is a plain options class. The framework does not bind it from configuration; configure it from your own section or site settings:

```csharp
builder.Services.Configure<DefaultAIDeploymentSettings>(settings =>
{
    settings.DefaultChatDeploymentName = "gpt-5-chat";
    settings.DefaultUtilityDeploymentName = "gpt-5-mini";
    settings.DefaultEmbeddingDeploymentName = "embeddings";
});
```

## Key Runtime Services

| Service | Use |
|---|---|
| `IAIClientFactory` | Create `IChatClient`, `IEmbeddingGenerator`, `IImageGenerator`, `ISpeechToTextClient`, `ITextToSpeechClient` and `IRealtimeClient` for a deployment. Every overload except realtime accepts an `Action<...Builder>` pipeline callback, and the factory owns the final `Build(serviceProvider)` |
| `IAICompletionService` | `CompleteAsync` / `CompleteStreamingAsync(deployment, messages, context, cancellationToken)` without the orchestration loop |
| `IAICompletionContextBuilder` | `BuildAsync(resource, configure, cancellationToken)` runs the context handlers for a profile, interaction or other resource |
| `IAIDeploymentManager` | Slot resolution and deployment CRUD |
| `IAIDeploymentCapabilityService` | What a deployment's model declares |
| `IAIProviderConnectionStore` / `IAIDeploymentStore` | Merged configuration and store views |

Every client the factory creates is metered, and every chat client is wrapped in `CapabilityEnforcingChatClient`, which strips tools, JSON response formats and reasoning options the deployment does not declare. `CreateRealtimeClientAsync` returns a metering wrapper; use `GetService` to reach the provider client.

## Example Completion Service

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

public sealed class QuestionService
{
    private readonly IAICompletionService _completionService;
    private readonly IAIDeploymentManager _deploymentManager;

    public QuestionService(
        IAICompletionService completionService,
        IAIDeploymentManager deploymentManager)
    {
        _completionService = completionService;
        _deploymentManager = deploymentManager;
    }

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Chat,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("No chat deployment is configured.");

        var context = new AICompletionContext
        {
            ChatDeploymentName = deployment.Name,
            SystemMessage = "You are a helpful assistant.",
            DisableTools = true,
        };

        var response = await _completionService.CompleteAsync(
            deployment,
            [new ChatMessage(ChatRole.User, question)],
            context,
            cancellationToken);

        return response.Text;
    }
}
```

To reuse a profile's prompts, tools, persona and model parameters, build the context from the profile instead: `await completionContextBuilder.BuildAsync(profile, cancellationToken: cancellationToken)`.

## Direct Clients

```csharp
var chatClient = await clientFactory.CreateChatClientAsync(deployment, builder => builder
    .UseDefaultResilience()
    .UseUsageLabels(contextType: "Leads", purpose: "LeadScoring"));

var embeddingDeployment = await deploymentManager.ResolveSlotOrThrowAsync(AIDeploymentSlotNames.Embedding);
var generator = await clientFactory.CreateEmbeddingGeneratorAsync(embeddingDeployment);
```

When you finish a builder yourself, call `Build(serviceProvider)`, never `Build()`.

## Custom Providers

A provider that creates Microsoft.Extensions.AI clients implements `IAIClientProvider` (`CanHandle(clientName)` plus `GetChatClientAsync`, `GetEmbeddingGeneratorAsync`, `GetRealtimeClientAsync` and the other client getters for a connection), so `IAIClientFactory`, capability enforcement and metering apply to it with no extra code. A provider that owns the whole completion loop implements `IAICompletionClient` (`ClientName`, `CompleteAsync`, `CompleteStreamingAsync`) and registers it with `AddCoreAICompletionClient<TClient>(clientName, configure)`. Deriving from `AICompletionServiceBase` gives `ResolveDeploymentAsync(slotName, providerName, deploymentName)` and `ResolveRequestDeploymentAsync(context, providerName)`, which honors `AICompletionContext.IsUtilityCompletion`.

## Troubleshooting

- **`ResolveSlotAsync` returns `null`**: no deployment declares the slot's required feature, or the only candidate declares the excluded `realtime` feature for `Chat` or `Utility`.
- **A configured deployment is missing**: the entry declares no `Features`, its `ClientName` is unknown, or one of its values could not be read (each bad entry is now skipped on its own with an error naming it).
- **Tools are never called**: the deployment declares metadata without `toolCalling`, so capability enforcement removes them and logs a warning.

## Upgrading from 1.x

- `AIDeploymentPurpose`, `AIDeploymentPurposeExtensions`, `AIDeploymentType`, `AIDeployment.Purpose`, `AIDeployment.Type`, `SupportsPurpose`, `SupportsType` and `CanServeTextCompletion()` are removed.
- `ResolveOrDefaultAsync(AIDeploymentPurpose.X, ...)` becomes `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)`. `GetByPurposeAsync`, `GetAllByPurposeAsync`, `GetByTypeAsync`, `GetAllByTypeAsync` and the purpose overloads of `GetDefaultAsync` become `GetAllBySlotAsync`. `ResolveAsync(purpose, ...)` becomes `ResolveSlotOrThrowAsync(slotName, ...)`.
- `AIDeploymentConfigurationEntry.Purpose` and `.Type` become `LegacyPurposes`. Replace `"Purpose"` or `"Type"` keys in configuration with `Features`.
- Stored and configured deployments that still carry `Purpose`, `Capability` or `Type` are projected onto capabilities when read, so no data migration is needed. The projection maps `Chat`/`Utility` to `textGeneration` only, so declare `toolCalling` and `streaming` yourself or tools are stripped.
- `AICompletionServiceBase.ResolveDeploymentAsync` takes a slot name instead of a purpose.
- `ChatMode.Realtime` and `AIProfile.RealtimeDeploymentName` are removed; a realtime model is a deployment that declares `realtime`.

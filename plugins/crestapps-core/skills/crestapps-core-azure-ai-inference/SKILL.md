---
name: crestapps-core-azure-ai-inference
description: Skill for using Azure AI Inference, Azure AI Foundry serverless models, and GitHub Models in CrestApps.Core 2.0. Covers AddAzureAIInference and AddCoreAIAzureAIInference registration, connections with the AzureAIInference client name, Endpoint, AuthenticationType (Default, ApiKey, ManagedIdentity) and IdentityId, configured chat and embedding deployments that declare model capabilities through Features and Parameters instead of the removed Purpose or Type, deployment slots resolved with ResolveSlotAsync, per-model capability differences such as tool calling, unsupported image, speech and realtime clients, and upgrading from 1.x. Use it when connecting a CrestApps.Core host to GitHub Models or an Azure AI model catalog endpoint, comparing model families behind one endpoint, or troubleshooting authentication, skipped deployments, or tools that never run.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Azure AI Inference

The `CrestApps.Core.AI.AzureAIInference` package uses the `Azure.AI.Inference` SDK to reach one endpoint that serves many model families (OpenAI, Meta Llama, Mistral, Cohere, and others). It is the provider for GitHub Models and for serverless model deployments in Azure AI Foundry.

## Guidelines

- Use it for multi-model evaluation, GitHub Models prototypes, and serverless Azure model catalog deployments.
- Keep the endpoint and credentials on the connection; keep the model name and its capabilities on the deployment.
- Always set `AuthenticationType`. When it is empty the provider uses `DefaultAzureCredential`, even if `ApiKey` is set. A GitHub token needs `"AuthenticationType": "ApiKey"`.
- Only chat and embeddings are supported. Image generation, speech-to-text, text-to-speech and realtime throw `NotSupportedException`; use `crestapps-core-openai` or `crestapps-core-azure-openai` for those.
- Declare features per model. Tool calling, vision and structured outputs vary across the catalog; declare only what the chosen model supports.

## Registration

```csharp
using CrestApps.Core.AI.AzureAIInference;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddAzureAIInference()
    )
);
```

Without the suite builder, register the core and orchestration services as well; `AddCoreAIAzureAIInference()` alone adds only the provider:

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIAzureAIInference();
```

It registers `AzureAIInferenceClientProvider`, the `ProviderAICompletionClient<AzureAIInferenceClientMarker>` completion client, a connection source, and a deployment provider for `AzureAIInferenceConstants.ClientName` (`"AzureAIInference"`).

## Configuration

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "github-models",
          "ClientName": "AzureAIInference",
          "Endpoint": "https://models.inference.ai.azure.com",
          "AuthenticationType": "ApiKey",
          "ApiKey": "ghp_your-token-with-models-read"
        },
        {
          "Name": "foundry",
          "ClientName": "AzureAIInference",
          "Endpoint": "https://your-project.inference.ai.azure.com",
          "AuthenticationType": "ManagedIdentity"
        }
      ],
      "Deployments": [
        {
          "ClientName": "AzureAIInference",
          "ConnectionName": "github-models",
          "Name": "gpt-4o-mini",
          "Features": [ "textGeneration", "toolCalling", "streaming" ]
        },
        {
          "ClientName": "AzureAIInference",
          "ConnectionName": "github-models",
          "Name": "llama-3.3-70b",
          "ModelName": "Llama-3.3-70B-Instruct",
          "Features": [ "textGeneration", "streaming" ]
        },
        {
          "ClientName": "AzureAIInference",
          "ConnectionName": "github-models",
          "Name": "text-embedding-3-small",
          "Features": [ "textEmbedding" ]
        }
      ]
    }
  }
}
```

| `AuthenticationType` | Credential |
|---|---|
| `Default` or empty | `DefaultAzureCredential` |
| `ApiKey` | `AzureKeyCredential(ApiKey)` (GitHub token or Azure key) |
| `ManagedIdentity` | `ManagedIdentityCredential`, user-assigned when `IdentityId` is set |

- `ModelName` is the catalog model name sent to the endpoint and falls back to `Name`.
- A configured deployment with no `Features` (and no legacy `Purpose`/`Type`) is skipped with "declares no model capabilities".
- `Features` and `Parameters` on the entry are shorthand for `Properties:AIDeploymentMetadata:*` and keep environment variable names short.
- A connection may instead declare `ChatDeploymentName` (read-only deployment with `textGeneration`, `toolCalling`, `streaming`) or `EmbeddingDeploymentName` (`textEmbedding`). Do not use the connection shorthand for a model that cannot call tools: it always declares `toolCalling`.

## Resolve Through Slots

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace MyApp.Services;

public sealed class ModelComparisonService
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionService _completionService;

    public ModelComparisonService(IAIDeploymentManager deploymentManager, IAICompletionService completionService)
    {
        _deploymentManager = deploymentManager;
        _completionService = completionService;
    }

    public async Task<IReadOnlyDictionary<string, string>> CompareAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var candidates = await _deploymentManager.GetAllBySlotAsync(
            AIDeploymentSlotNames.Chat,
            clientName: "AzureAIInference",
            cancellationToken: cancellationToken);

        foreach (var deployment in candidates)
        {
            var response = await _completionService.CompleteAsync(
                deployment,
                [new ChatMessage(ChatRole.User, prompt)],
                new AICompletionContext { DisableTools = true },
                cancellationToken);

            results[deployment.Name] = response.Text;
        }

        return results;
    }
}
```

`GetAllBySlotAsync(AIDeploymentSlotNames.Chat, ...)` returns only deployments that declare `textGeneration` and are not `realtime`. Use `ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName)` to pick one, with fallback to the site default in `DefaultAIDeploymentSettings.DefaultChatDeploymentName`.

## Troubleshooting

- **401 or credential errors with a GitHub token**: `AuthenticationType` is not `ApiKey`.
- **The model says it will call a tool but nothing runs**: the deployment does not declare `toolCalling`, or the model does not support tools. Check the model card and declare features accordingly.
- **`NotSupportedException` for images or speech**: those clients do not exist for this provider; register a deployment on another provider and resolve it through the matching slot.
- **GitHub Models rate limits**: the free tier is for experimentation; move production traffic to an Azure AI Foundry endpoint.

## Upgrading from 1.x

- Replace `"Type": "Chat"` or `"Purpose": "Chat"` with `"Features": [ ... ]`. Legacy `Purpose`, `Capability` and `Type` values still load, but the projection is narrow: `Chat` or `Utility` becomes `textGeneration` only (not when `realtime` is declared), and `Embedding`, `Image`, `Vision`, `SpeechToText` and `TextToSpeech` become `textEmbedding`, `imageOutput`, `imageInput`, `speechToText` and `textToSpeech`. A 1.x chat entry therefore loses tool calling and streaming until `toolCalling` and `streaming` are declared.
- Replace `ResolveOrDefaultAsync(AIDeploymentPurpose.X, ...)` with `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)` and purpose lookups with `GetAllBySlotAsync`.
- Check that every connection that authenticates with a key or GitHub token sets `"AuthenticationType": "ApiKey"`.

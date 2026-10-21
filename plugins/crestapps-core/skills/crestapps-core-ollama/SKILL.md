---
name: crestapps-core-ollama
description: Skill for running local and self-hosted Ollama models in CrestApps.Core 2.0. Covers AddOllama and AddCoreAIOllama registration, connections with the Ollama client name and a required Endpoint, configured chat and embedding deployments that declare model capabilities through Features instead of the removed Purpose or Type, declaring toolCalling only for models that support tools, deployment slots resolved with ResolveSlotAsync, Docker and model pulling, unsupported image, speech and realtime clients, and upgrading from 1.x. Use it for local development, offline or air-gapped hosts, and privacy-sensitive workloads, or when troubleshooting missing endpoints, skipped deployments, tool calls that never happen, or slow local responses.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Ollama

The `CrestApps.Core.AI.Ollama` package uses `OllamaApiClient` (OllamaSharp) to talk to a local or self-hosted Ollama server. No API key is involved; data stays on your infrastructure.

## Guidelines

- Use Ollama for local development, offline work, and privacy-sensitive scenarios.
- The connection's `Endpoint` is required. There is no built-in default; a connection without one fails when a client is created.
- Capabilities are model-dependent. Declare `toolCalling` only for models that support tools (for example `llama3.1`, `llama3.2`, `qwen2.5`, `mistral`), and `imageInput` only for multimodal models such as `llava`.
- Only chat and embeddings are supported. Image generation, speech-to-text, text-to-speech and realtime are not; put those on another provider and resolve them through their slots.
- Pull a model before configuring a deployment that names it.

## Registration

```csharp
using CrestApps.Core.AI.Ollama;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOllama()
    )
);
```

Raw registration:

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIOllama();
```

It registers `OllamaAIClientProvider`, the `ProviderAICompletionClient<OllamaClientMarker>` completion client, a connection source, and a deployment provider for `OllamaConstants.ClientName` (`"Ollama"`).

## Configuration

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "local-ollama",
          "ClientName": "Ollama",
          "Endpoint": "http://localhost:11434"
        }
      ],
      "Deployments": [
        {
          "ClientName": "Ollama",
          "ConnectionName": "local-ollama",
          "Name": "llama3.1",
          "Features": [ "textGeneration", "toolCalling", "streaming" ]
        },
        {
          "ClientName": "Ollama",
          "ConnectionName": "local-ollama",
          "Name": "phi3",
          "Features": [ "textGeneration", "streaming" ]
        },
        {
          "ClientName": "Ollama",
          "ConnectionName": "local-ollama",
          "Name": "nomic-embed-text",
          "Features": [ "textEmbedding" ]
        }
      ]
    }
  }
}
```

- `Name` (or `ModelName`, which falls back to `Name`) must match the pulled Ollama model tag.
- A configured deployment with no `Features` and no legacy `Purpose`/`Type` is skipped with the warning "declares no model capabilities".
- The connection shorthand `ChatDeploymentName` always declares `textGeneration`, `toolCalling` and `streaming`. Use an explicit `Deployments` entry for a model that cannot call tools, otherwise the model receives tools it cannot use.
- `UtilityDeploymentName` (`textGeneration`, `streaming`) and `EmbeddingDeploymentName` (`textEmbedding`) work the same way.

Point the slot defaults at the local models:

```csharp
using CrestApps.Core.AI.Models;

builder.Services.Configure<DefaultAIDeploymentSettings>(settings =>
{
    settings.DefaultChatDeploymentName = "llama3.1";
    settings.DefaultUtilityDeploymentName = "phi3";
    settings.DefaultEmbeddingDeploymentName = "nomic-embed-text";
});
```

```csharp
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;

namespace MyApp.Services;

public sealed class LocalModelSelector
{
    private readonly IAIDeploymentManager _deploymentManager;

    public LocalModelSelector(IAIDeploymentManager deploymentManager)
    {
        _deploymentManager = deploymentManager;
    }

    public ValueTask<AIDeployment> GetUtilityModelAsync(CancellationToken cancellationToken = default)
        => _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Utility,
            clientName: "Ollama",
            cancellationToken: cancellationToken);
}
```

The `utility` slot falls back to the `chat` slot, so background work (titles, extraction, query rewriting) runs on the chat model when no utility model is set.

## Running Ollama

```bash
# CPU
docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama

# NVIDIA GPU
docker run -d --gpus=all -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama

docker exec -it ollama ollama pull llama3.1
docker exec -it ollama ollama pull nomic-embed-text
curl http://localhost:11434/api/tags
```

The CrestApps.Core repository's Aspire AppHost (`src/Startup/CrestApps.Core.Aspire.AppHost`) can start Ollama alongside the sample hosts.

## Troubleshooting

- **`Endpoint` errors when the first chat runs**: the connection has no `Endpoint`. Add `http://localhost:11434` or the server URL.
- **Deployment missing from pickers**: no `Features`, an unknown `ClientName`, or the slot requires a capability the entry does not declare.
- **The model talks about calling a tool and stops**: either the model does not support tools, or `toolCalling` is not declared and tools were stripped. Pick a tool-capable model and declare it.
- **Model not found**: pull the exact tag named by `ModelName`.
- **Slow or timed-out responses**: local throughput depends on hardware; use a GPU or a smaller model, and keep the utility slot on a small model.

## Upgrading from 1.x

- Replace `"Purpose": "Chat"` with `"Features": [ "textGeneration", "streaming" ]`, adding `toolCalling` only for tool-capable models. Legacy `Purpose`, `Capability` and `Type` values still load, but the projection is narrow: `Chat` or `Utility` becomes `textGeneration` only (not when `realtime` is declared), and `Embedding`, `Image`, `Vision`, `SpeechToText` and `TextToSpeech` become `textEmbedding`, `imageOutput`, `imageInput`, `speechToText` and `textToSpeech`. A 1.x chat entry therefore loses tool calling and streaming until `toolCalling` and `streaming` are declared.
- Replace `ResolveOrDefaultAsync(AIDeploymentPurpose.X, ...)` with `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)` and purpose lookups with `GetAllBySlotAsync`.

---
name: crestapps-core-openai
description: Skill for using OpenAI and OpenAI-compatible endpoints in CrestApps.Core 2.0. Covers AddOpenAI and AddCoreAIOpenAI registration, connections under CrestApps AI Connections with ApiKey, Endpoint and the ChatApi setting (Auto, ChatCompletions, Responses) that sends reasoning requests with tools to the Responses API, configured deployments that declare model capabilities through Features and Parameters instead of the removed Purpose or Type, connection-declared deployment names, deployment slots resolved with ResolveSlotAsync, text-to-speech voices and raw PCM output for cascaded realtime, gpt-realtime speech-to-speech deployments with OpenAIRealtimeVoices, and upgrading from 1.x. Use it when wiring OpenAI, OpenRouter, xAI or another OpenAI-compatible server into a CrestApps.Core host, or when troubleshooting skipped deployments, stripped tools, or reasoning-effort errors.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core OpenAI

The `CrestApps.Core.AI.OpenAI` package connects CrestApps.Core to `api.openai.com` and to any server that speaks the OpenAI API. The connection holds credentials and the endpoint; each deployment names a model and declares what that model can do.

## Guidelines

- Register only the providers the host uses. OpenAI is the simplest first provider because it needs only an API key.
- Put the API key, endpoint and `ChatApi` on the connection. Put the model name and its capabilities on the deployment.
- Every configured deployment must declare its model capabilities (`Features`). A deployment entry with no features and no legacy `Purpose`/`Type` is skipped at startup with the warning "declares no model capabilities".
- Pick deployments through slots (`AIDeploymentSlotNames`) and `IAIDeploymentManager.ResolveSlotAsync(...)`. Never hard-code a provider client in business code.
- Use `crestapps-core-ai-runtime` for the provider-agnostic runtime, `crestapps-core-chat` for completion loops, and `crestapps-core-realtime-voice` for voice sessions.

## Registration

```csharp
using CrestApps.Core.AI.OpenAI;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
    )
);
```

`AddAISuite(...)` already calls `AddCoreServices()`, `AddCoreAIServices()` and `AddCoreAIOrchestration()`. Without the builder, register them yourself:

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIOpenAI();
```

`AddCoreAIOpenAI()` registers:

| Service | Implementation |
|---|---|
| `IAIClientProvider` | `OpenAIClientProvider` (chat, embeddings, images, speech-to-text, text-to-speech, realtime) |
| `IAICompletionClient` for `"OpenAI"` | `ProviderAICompletionClient<OpenAIClientMarker>` |
| `IAIProviderConnectionHandler` | `OpenAIConnectionHandler` (unprotects a stored `OpenAIConnectionMetadata.ApiKey`) |
| Connection source and deployment provider | Client name `OpenAIConstants.ClientName` (`"OpenAI"`) |

## Connection and Deployments

Connections are read from `CrestApps:AI:Connections` (or `CrestApps:AI:Providers:OpenAI:Connections`) and deployments from `CrestApps:AI:Deployments`. A configured deployment is read-only in the admin UI.

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
          "ConnectionName": "openai",
          "Name": "gpt-4.1",
          "ModelName": "gpt-4.1",
          "Features": [ "textGeneration", "toolCalling", "streaming", "imageInput" ]
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Name": "gpt-5-reasoning",
          "ModelName": "gpt-5",
          "Features": [ "textGeneration", "toolCalling", "streaming", "reasoning" ],
          "Parameters": {
            "reasoningEffort": {
              "AllowedValues": [ "None", "Low", "Medium", "High" ],
              "DefaultValue": "Low"
            }
          }
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Name": "text-embedding-3-small",
          "Features": [ "textEmbedding" ]
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Name": "gpt-4o-transcribe",
          "Features": [ "speechToText" ]
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Name": "gpt-4o-mini-tts",
          "Features": [ "textToSpeech" ]
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Name": "gpt-realtime",
          "Features": [ "realtime" ]
        }
      ]
    }
  }
}
```

- `Name` is the lookup name; `ModelName` is the model sent to OpenAI and falls back to `Name`.
- `Features` and `Parameters` on the entry are shorthand for `Properties:AIDeploymentMetadata:Features` and `Properties:AIDeploymentMetadata:Parameters`. Both forms merge, and the shorthand keeps environment variable names short enough for Azure App Service (`CrestApps__AI__Deployments__0__Features__0=textGeneration`).
- `textGeneration` is opt-out only for a deployment with no metadata at all. Once `Features` is declared, list `textGeneration` for every text model. A speech-to-speech-only model declares `realtime` and omits `textGeneration`.
- Declare `toolCalling` and `streaming` on chat models. Without `toolCalling` every tool is stripped from the request; without `streaming` a streaming call is completed as one response.
- Feature names are the `AIDeploymentFeatureNames` constants: `textGeneration`, `toolCalling`, `structuredOutputs`, `streaming`, `reasoning`, `imageInput`, `imageOutput`, `audioInput`, `audioOutput`, `videoInput`, `videoOutput`, `realtime`, `textEmbedding`, `speechToText`, `textToSpeech`.

### Deployments declared on the connection

A connection can name its models directly instead. Each name becomes a read-only deployment with fixed capabilities:

| Connection key | Capabilities declared |
|---|---|
| `ChatDeploymentName` (or `DeploymentName`) | `textGeneration`, `toolCalling`, `streaming` |
| `UtilityDeploymentName` | `textGeneration`, `streaming` |
| `EmbeddingDeploymentName` | `textEmbedding` |
| `ImagesDeploymentName` | `imageOutput` |
| `SpeechToTextDeploymentName` | `speechToText` |
| `TextToSpeechDeploymentName` | `textToSpeech` |

Use the explicit `Deployments` list when a model needs `reasoning`, `imageInput`, `realtime`, or parameters.

## Slots and Site Defaults

A slot is what the installation uses a deployment for. The built-in slots are `chat`, `utility`, `embedding`, `image`, `vision`, `speechToText`, `textToSpeech` and `realtime`. Fill a slot default through `DefaultAIDeploymentSettings`, then resolve it:

```csharp
using CrestApps.Core.AI.Models;

builder.Services.Configure<DefaultAIDeploymentSettings>(settings =>
{
    settings.DefaultChatDeploymentName = "gpt-4.1";
    settings.DefaultUtilityDeploymentName = "gpt-4.1";
    settings.DefaultEmbeddingDeploymentName = "text-embedding-3-small";
    settings.DefaultSpeechToTextDeploymentName = "gpt-4o-transcribe";
    settings.DefaultTextToSpeechDeploymentName = "gpt-4o-mini-tts";
    settings.DefaultRealtimeDeploymentName = "gpt-realtime";
});
```

```csharp
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace MyApp.Services;

public sealed class EmbeddingService
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;

    public EmbeddingService(IAIDeploymentManager deploymentManager, IAIClientFactory clientFactory)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
    }

    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Embedding,
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("No deployment declares the textEmbedding capability.");

        var generator = await _clientFactory.CreateEmbeddingGeneratorAsync(deployment);
        var embeddings = await generator.GenerateAsync([text], cancellationToken: cancellationToken);

        return embeddings[0].Vector;
    }
}
```

Resolution order is the explicit name, the slot's site default, the fallback slot (only `utility` falls back to `chat`), and finally the first deployment that declares the slot's capability. `GetAllBySlotAsync(slotName)` lists every candidate, which is what settings pickers use. `ResolveSlotOrThrowAsync(...)` throws instead of returning `null`.

## Chat API and Reasoning Models

Reasoning models such as `gpt-6-luna` reject function tools combined with a reasoning effort on `/v1/chat/completions`. The connection's `ChatApi` setting (`OpenAIChatApis`) decides where such a request goes:

| `ChatApi` | Behavior |
|---|---|
| `Auto` (default) | On OpenAI's own endpoint (no `Endpoint`, `api.openai.com`, or a regional host such as `eu.api.openai.com`), a request with function tools and a reasoning effort other than `None` goes to the Responses API. Any other endpoint uses Chat Completions only. |
| `ChatCompletions` | Every request uses Chat Completions. |
| `Responses` | Tools plus reasoning go to `/v1/responses` on any endpoint. Use it for compatible servers such as OpenRouter or xAI. |

Values are case-insensitive; an unknown value logs a warning and acts as `Auto`. Responses requests are sent with `store: false`. A reasoning effort reaches the provider only when the deployment declares `reasoning` and the `reasoningEffort` parameter; without the parameter no effort is sent and Chat Completions rejects the tools. A host configured only from environment variables can set `Parameters:reasoningEffort:DefaultValue=None` for models that refuse tools unless the effort is `none`.

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "openrouter",
          "ClientName": "OpenAI",
          "Endpoint": "https://openrouter.ai/api/v1",
          "ApiKey": "...",
          "ChatApi": "Responses"
        }
      ]
    }
  }
}
```

## Speech and Realtime

- **Text-to-speech** (`IAIClientFactory.CreateTextToSpeechClientAsync`) streams for `gpt-4o-mini-tts`; `tts-1` and `tts-1-hd` answer in one piece. `ISpeechVoiceResolver` returns `OpenAITextToSpeechVoices.For(model)`: thirteen voices for `gpt-4o-mini-tts`, nine for `tts-1`/`tts-1-hd`.
- OpenAI produces raw PCM only at 24 kHz. Ask for `pcm` or `Pcm<rate>`; `Pcm24000` passes through and any other rate (`Pcm8000`, `Pcm16000`) is requested at 24 kHz and resampled while streaming, so OpenAI can be the speaking leg of a cascaded realtime deployment at any output rate.
- `TextToSpeechOptions.Speed` is clamped to 0.25 to 4. A speaking style passed as the `TextToSpeechOptionsProperties.Instructions` additional property is sent as `instructions`, except to `tts-1` and `tts-1-hd`.
- **Realtime** uses `OpenAIRealtimeClient` from `Microsoft.Extensions.AI.OpenAI`. Declare the `realtime` feature so the deployment fills the `realtime` slot. Voices come from `IRealtimeVoiceResolver` and are `OpenAIRealtimeVoices.All` (`alloy`, `ash`, `ballad`, `cedar`, `coral`, `echo`, `marin`, `sage`, `shimmer`, `verse`). With no voice chosen the provider's own default voice is used.
- Realtime sessions transcribe the user with a speech-to-text deployment on the same provider (for example `gpt-4o-transcribe`); add one or the user's turns are not transcribed. See `crestapps-core-realtime-voice`.

## OpenAI-Compatible Servers

Set `Endpoint` to the server's base URL (for example Groq, Mistral, Together AI, Fireworks, DeepSeek, OpenRouter, vLLM, LocalAI). Declare only the features the hosted model really has; many servers lack tool calling, structured outputs, or `/v1/responses`. For Anthropic's native API prefer `crestapps-core-claude-orchestrator`.

## Troubleshooting

- **Deployment missing from pickers or `ResolveSlotAsync` returns `null`**: the entry has no `Features`, its `ClientName` is missing or unknown, or it declares a capability other than the slot requires.
- **The model describes a tool call but never calls it**: the deployment does not declare `toolCalling`; capability enforcement removed the tools (a warning is logged).
- **HTTP 400 about tools and reasoning effort**: declare `reasoning` and `reasoningEffort`, or leave `ChatApi` at `Auto`/`Responses` so the request uses the Responses API.
- **Two connections whose names differ only by case**: the first definition wins and the second is logged as a warning. Rename one.

## Upgrading from 1.x

- Replace `"Purpose": "Chat"` or `"Type": "Chat"` with `"Features": [ "textGeneration", "toolCalling", "streaming" ]`. Legacy `Purpose`, `Capability` and `Type` values still load, but the projection is narrow: `Chat` or `Utility` becomes `textGeneration` only (not when `realtime` is declared), and `Embedding`, `Image`, `Vision`, `SpeechToText` and `TextToSpeech` become `textEmbedding`, `imageOutput`, `imageInput`, `speechToText` and `textToSpeech`. A 1.x chat entry therefore loses tool calling and streaming until `toolCalling` and `streaming` are declared.
- Replace `ResolveOrDefaultAsync(AIDeploymentPurpose.X, ...)` with `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)`, and `GetByPurposeAsync`/`GetAllByPurposeAsync` with `GetAllBySlotAsync`. `AIDeploymentPurpose`, `AIDeploymentType`, `SupportsPurpose` and `CanServeTextCompletion()` are removed.
- A realtime model is now a deployment with the `realtime` feature. `ChatMode.Realtime` and `AIProfile.RealtimeDeploymentName` are gone.
- The OpenAI provider now supports text-to-speech and voice listing; earlier builds failed with "The provider 'OpenAI' does not support text-to-speech".

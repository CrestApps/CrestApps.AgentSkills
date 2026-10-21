---
name: crestapps-core-azure-openai
description: Skill for using Azure OpenAI and Azure AI Speech in CrestApps.Core 2.0. Covers AddAzureOpenAI and AddCoreAIAzureOpenAI registration, Azure connections with Endpoint, AuthenticationType (Default, ApiKey, ManagedIdentity), IdentityId and the ChatApi setting, the CrestApps AI AzureClient retry and logging options, configured deployments that declare model capabilities through Features and Parameters instead of the removed Purpose or Type, contained-connection AzureSpeech deployments for speech-to-text and text-to-speech with selectable audio formats and speed, deployment slots resolved with ResolveSlotAsync, reasoning requests with tools routed to the Responses endpoint, gpt-realtime speech-to-speech over the GA realtime WebSocket endpoint, and upgrading from 1.x. Use it when wiring Azure OpenAI into a CrestApps.Core host, choosing API key or managed identity authentication, or troubleshooting DefaultAzureCredential failures, DeploymentNotFound errors, skipped deployments, or reasoning-effort errors.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Azure OpenAI

The `CrestApps.Core.AI.OpenAI.Azure` package connects CrestApps.Core to Azure OpenAI (client name `"Azure"`) and to Azure AI Speech (client name `"AzureSpeech"`). Use it for enterprise workloads that need regional data residency, managed identity, or private networking.

## Guidelines

- Keep the endpoint and credentials on the connection; keep the Azure deployment name and its capabilities on the deployment.
- Always set `AuthenticationType` on an Azure connection. When it is empty the provider uses `DefaultAzureCredential`, even if an `ApiKey` is present.
- Every configured deployment must declare `Features`. An entry with no features and no legacy `Purpose`/`Type` is skipped with the warning "declares no model capabilities".
- Resolve deployments through slots with `IAIDeploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.X, ...)`.
- See `crestapps-core-openai` for the shared `ChatApi`, feature, and slot details, and `crestapps-core-realtime-voice` for voice sessions.

## Registration

```csharp
using CrestApps.Core.AI.OpenAI.Azure;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddAzureOpenAI()
    )
);
```

Without the suite builder, register the core and orchestration services too; `AddCoreAIAzureOpenAI()` alone adds only the provider:

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIAzureOpenAI();
```

`AddCoreAIAzureOpenAI()` registers `AzureOpenAIClientProvider` (chat, embeddings, images, speech-to-text, realtime), `AzureSpeechClientProvider` (Azure AI Speech speech-to-text and text-to-speech), `AzureOpenAIConnectionHandler`, the `AzureOpenAICompletionClient` completion client for `"Azure"`, `AzureClientOptions` bound from `CrestApps:AI:AzureClient`, and two deployment providers: `"Azure"` and the contained-connection `"AzureSpeech"`.

## Connection

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "azure-production",
          "ClientName": "Azure",
          "Endpoint": "https://my-resource.openai.azure.com/",
          "AuthenticationType": "ApiKey",
          "ApiKey": "your-api-key"
        },
        {
          "Name": "azure-managed",
          "ClientName": "Azure",
          "Endpoint": "https://my-resource.openai.azure.com/",
          "AuthenticationType": "ManagedIdentity",
          "IdentityId": "user-assigned-client-id"
        }
      ]
    }
  }
}
```

| `AuthenticationType` | Credential used |
|---|---|
| `Default` or empty | `DefaultAzureCredential` (environment, workload identity, managed identity, Azure CLI, Visual Studio) |
| `ApiKey` | `ApiKeyCredential(ApiKey)` |
| `ManagedIdentity` | `ManagedIdentityCredential`, system-assigned when `IdentityId` is empty, otherwise the user-assigned client ID |

For managed identity, grant the hosting identity the `Cognitive Services OpenAI User` role on the resource. `"AzureOpenAI"` is accepted as an alias of the `"Azure"` client name.

### Shared Azure SDK options

`CrestApps:AI:AzureClient` binds `AzureClientOptions` for every Azure OpenAI connection:

```json
{
  "CrestApps": {
    "AI": {
      "AzureClient": {
        "EnableDefaultRetryPolicy": true,
        "MaxRetryAttempts": 5,
        "RateLimitRetryDelay": "00:00:01",
        "BackoffType": "Exponential",
        "UseJitter": true,
        "MaxRetryDelay": "00:00:32",
        "EnableLogging": false,
        "EnableMessageLogging": false,
        "EnableMessageContentLogging": false
      }
    }
  }
}
```

## Deployments

```json
{
  "CrestApps": {
    "AI": {
      "Deployments": [
        {
          "ClientName": "Azure",
          "ConnectionName": "azure-production",
          "Name": "gpt-4o",
          "ModelName": "my-gpt-4o-deployment",
          "Features": [ "textGeneration", "toolCalling", "streaming", "imageInput" ]
        },
        {
          "ClientName": "Azure",
          "ConnectionName": "azure-production",
          "Name": "gpt-5-reasoning",
          "ModelName": "gpt-5",
          "Features": [ "textGeneration", "toolCalling", "streaming", "reasoning" ],
          "Parameters": {
            "reasoningEffort": { "DefaultValue": "None" }
          }
        },
        {
          "ClientName": "Azure",
          "ConnectionName": "azure-production",
          "Name": "text-embedding-3-large",
          "Features": [ "textEmbedding" ]
        },
        {
          "ClientName": "Azure",
          "ConnectionName": "azure-production",
          "Name": "gpt-4o-transcribe",
          "Features": [ "speechToText" ]
        },
        {
          "ClientName": "Azure",
          "ConnectionName": "azure-production",
          "Name": "gpt-realtime",
          "Features": [ "realtime" ]
        },
        {
          "ClientName": "AzureSpeech",
          "Name": "azure-tts",
          "Endpoint": "https://eastus.tts.speech.microsoft.com",
          "AuthenticationType": "ApiKey",
          "ApiKey": "your-speech-key",
          "Features": [ "textToSpeech" ]
        },
        {
          "ClientName": "AzureSpeech",
          "Name": "azure-stt",
          "Endpoint": "https://eastus.stt.speech.microsoft.com",
          "AuthenticationType": "ApiKey",
          "ApiKey": "your-speech-key",
          "Features": [ "speechToText" ]
        }
      ]
    }
  }
}
```

- `ModelName` is the Azure deployment name used in the request URL; it does not have to match the model. It falls back to `Name`.
- `AzureSpeech` deployments carry their own connection: top-level `Endpoint`, `AuthenticationType`, `ApiKey` and `IdentityId` become deployment properties, so no `ConnectionName` is needed.
- Declare `toolCalling` and `streaming` on chat deployments; undeclared features are enforced away (tools stripped, streaming completed as one response).
- A connection can also declare `ChatDeploymentName`, `UtilityDeploymentName`, `EmbeddingDeploymentName`, `ImagesDeploymentName`, `SpeechToTextDeploymentName` or `TextToSpeechDeploymentName`; each becomes a read-only deployment with fixed capabilities (chat gets `textGeneration`, `toolCalling` and `streaming`).

Fill slot defaults with `DefaultAIDeploymentSettings` (`DefaultChatDeploymentName`, `DefaultUtilityDeploymentName`, `DefaultEmbeddingDeploymentName`, `DefaultImageDeploymentName`, `DefaultVisionDeploymentName`, `DefaultSpeechToTextDeploymentName`, `DefaultTextToSpeechDeploymentName`, `DefaultRealtimeDeploymentName`) and resolve them:

```csharp
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace MyApp.Services;

#pragma warning disable MEAI001 // Microsoft.Extensions.AI speech APIs are evaluation-only.
public sealed class SpeechSynthesisService
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;

    public SpeechSynthesisService(IAIDeploymentManager deploymentManager, IAIClientFactory clientFactory)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
    }

    public async Task<TextToSpeechResponse> SpeakAsync(string text, string voice, CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentManager.ResolveSlotOrThrowAsync(AIDeploymentSlotNames.TextToSpeech);
        var client = await _clientFactory.CreateTextToSpeechClientAsync(deployment);

        return await client.GetAudioAsync(
            text,
            new TextToSpeechOptions
            {
                VoiceId = voice,
                AudioFormat = "Pcm24000",
                Speed = 1.1f,
            },
            cancellationToken);
    }
}
#pragma warning restore MEAI001
```

## Azure AI Speech Formats and Speed

`AzureSpeechServiceTextToSpeechClient` honors `TextToSpeechOptions.AudioFormat` and reports the matching media type. Names are case-insensitive: `Pcm8000`, `Pcm16000`, `Pcm24000`, `Pcm48000` (raw PCM, `audio/L16`), `Riff8000` through `Riff48000` (WAV), `Mp3`, `Mp3_16000`, `Mp3_24000`, `Mp3_48000`, or a Speech SDK format name. MP3 at 16 kHz is the default. Raw PCM is what lets Azure Speech be the speaking leg of a cascaded realtime deployment. `TextToSpeechOptions.Speed` is applied as SSML `prosody rate` within 0.5 to 2 when a voice is named. Azure Speech has no realtime client, so it cannot be a cascade's speech-to-text leg.

## Responses API and Realtime

- A chat request with function tools and a reasoning effort other than `None` goes to `/openai/v1/responses` (with `store: false`); every other request stays on Chat Completions. Set the connection's `ChatApi` to `ChatCompletions` to turn this off, for example in a region without the Responses API. The routing uses a temporary built-in client (`CrestApps.Core.AI.OpenAI.Azure.Responses`) because the pinned `Azure.AI.OpenAI` SDK throws `MissingMethodException` on its own Responses client.
- A reasoning effort is sent only when the deployment declares `reasoning` and `reasoningEffort`. `None` is sent as `"none"`; `ExtraHigh` is clamped to `High` with a log entry.
- Realtime deployments connect to `/openai/v1/realtime` through a temporary WebSocket transport (`CrestApps.Core.AI.OpenAI.Azure.Realtime`) that supports API keys and Microsoft Entra ID. Voices are `OpenAIRealtimeVoices.All`. Add a speech-to-text deployment (for example `gpt-4o-transcribe`) on the same Azure provider so the user's realtime turns are transcribed.

## Troubleshooting

- **`CredentialUnavailableException` or 401 with an API key configured**: `AuthenticationType` is missing, so `DefaultAzureCredential` was used. Set `"AuthenticationType": "ApiKey"`.
- **`DeploymentNotFound`**: the `ModelName` does not exist on the resource the connection points at. Failed completions log the deployment name, model name, connection name, and endpoint host.
- **HTTP 400 `unsupported_value` for reasoning effort**: declare the allowed `reasoningEffort` values the model accepts and pick a valid default.
- **Two connections whose names differ only by case** (for example under `CrestApps:AI:Connections` and `CrestApps:AI:Providers:Azure:Connections`): the first wins and the second is logged as a warning.

## Upgrading from 1.x

- Replace `"Type": "Chat"` / `"Purpose": "Chat"` with `Features`. Legacy `Purpose`, `Capability` and `Type` values still load, but the projection is narrow: `Chat` or `Utility` becomes `textGeneration` only (not when `realtime` is declared), and `Embedding`, `Image`, `Vision`, `SpeechToText` and `TextToSpeech` become `textEmbedding`, `imageOutput`, `imageInput`, `speechToText` and `textToSpeech`. A 1.x chat entry therefore loses tool calling and streaming until `toolCalling` and `streaming` are declared.
- Replace `ResolveOrDefaultAsync(AIDeploymentPurpose.X, ...)` with `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)` and `GetByPurposeAsync`/`GetAllByPurposeAsync` with `GetAllBySlotAsync`.
- `AzureOpenAICompletionClient` now takes an `IAIClientFactory` constructor argument; code that constructs it directly must pass one.
- A realtime model is a deployment that declares `realtime`; `ChatMode.Realtime` and `AIProfile.RealtimeDeploymentName` are removed.

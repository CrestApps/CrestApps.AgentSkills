---
name: orchardcore-ai-providers
description: Skill for the CrestApps.OrchardCore 3.0 AI provider modules. Covers CrestApps.OrchardCore.OpenAI (OpenAI and OpenAI-compatible services such as DeepSeek, Gemini and vLLM), CrestApps.OrchardCore.OpenAI.Azure (Azure OpenAI plus contained AzureSpeech deployments and GStreamer), CrestApps.OrchardCore.AzureAIInference (GitHub Models), CrestApps.OrchardCore.Ollama, the built-in CascadedRealtime deployment provider, provider connections and AIDeployment recipes with AIDeploymentMetadata capabilities, the AzureClient retry and logging options, how the Claude and Copilot orchestrators differ from providers, and writing a custom provider with AIClientProviderBase, IAIClientMarker, ProviderAICompletionClient, AddCoreAICompletionClient, AddCoreAIDeploymentProvider and AddCoreAIConnectionSource. Use this skill when requests mention AI providers, OpenAI setup, Azure OpenAI setup, Azure AI Speech, GitHub Models, Ollama, OpenAI-compatible endpoints, provider deployments, or adding a new AI provider to Orchard Core.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Providers

A provider module teaches the CrestApps AI suite to talk to one AI platform: it creates chat clients,
embedding generators, image generators and speech clients, registers a connection source and a deployment
provider, and contributes its connection editor. Configure a **connection** (credentials), then
**deployments** (models with declared capabilities), then point profiles or site defaults at deployments.

## Guidelines

- Install provider packages in the web/startup project and enable the provider feature; it enables
  `CrestApps.OrchardCore.AI`. Enable `CrestApps.OrchardCore.AI.ConnectionManagement` for the connection UI
  and the `AIProviderConnections` recipe step.
- Declare each deployment's capabilities under `Properties.AIDeploymentMetadata.Features`. There is no
  `Purpose` and no `IsDefault`; defaults are site settings (see `orchardcore-ai-model-capabilities`).
- Use the OpenAI provider for any OpenAI-compatible API.
- Keep secrets in user secrets, environment variables or Key Vault. Prefer managed identity on Azure.

## Providers

| Provider | Feature ID | `ClientName` | Notes |
| --- | --- | --- | --- |
| OpenAI | `CrestApps.OrchardCore.OpenAI` | `OpenAI` | OpenAI and any OpenAI-compatible endpoint |
| Azure OpenAI | `CrestApps.OrchardCore.OpenAI.Azure` | `Azure` | Also registers the contained `AzureSpeech` deployment provider (**Azure AI Services**) |
| Azure AI Inference | `CrestApps.OrchardCore.AzureAIInference` | `AzureAIInference` | GitHub Models and Azure AI model inference endpoints |
| Ollama | `CrestApps.OrchardCore.Ollama` | `Ollama` | Local models; no API key |
| Cascaded Realtime | built into `CrestApps.OrchardCore.AI` | `CascadedRealtime` | Chains speech-to-text, chat and text-to-speech deployments; see `orchardcore-ai-realtime-voice` |

Connection metadata per provider and the `AIProviderConnections` recipe are in `orchardcore-ai-connections`.

## OpenAI

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Connections": [
          { "Name": "openai-cloud", "ClientName": "OpenAI", "ApiKey": "<secret>" },
          { "Name": "deepseek", "ClientName": "OpenAI", "Endpoint": "https://api.deepseek.com/v1", "ApiKey": "<secret>" }
        ],
        "Deployments": [
          {
            "Name": "chat-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "gpt-4o",
            "Properties": { "AIDeploymentMetadata": { "Features": [ "textGeneration", "toolCalling", "streaming", "structuredOutputs" ] } }
          },
          {
            "Name": "embedding-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "text-embedding-3-large",
            "Properties": { "AIDeploymentMetadata": { "Features": [ "textEmbedding" ] } }
          },
          {
            "Name": "image-default",
            "ClientName": "OpenAI",
            "ConnectionName": "openai-cloud",
            "ModelName": "dall-e-3",
            "Properties": { "AIDeploymentMetadata": { "Features": [ "imageOutput" ] } }
          },
          {
            "Name": "deepseek-reasoner",
            "ClientName": "OpenAI",
            "ConnectionName": "deepseek",
            "ModelName": "deepseek-reasoner",
            "Properties": { "AIDeploymentMetadata": { "Features": [ "textGeneration", "reasoning", "streaming" ] } }
          }
        ]
      }
    }
  }
}
```

A Gemini connection typically uses `https://generativelanguage.googleapis.com/v1` as `Endpoint`.

## Azure OpenAI

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Source": "Azure",
          "Name": "azure-openai",
          "DisplayText": "Azure OpenAI",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://my-resource.openai.azure.com/",
              "AuthenticationType": "ManagedIdentity"
            }
          }
        }
      ]
    },
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "chat-deployment",
          "ClientName": "Azure",
          "ConnectionName": "azure-openai",
          "ModelName": "gpt-4.1",
          "Properties": { "AIDeploymentMetadata": { "Features": [ "textGeneration", "toolCalling", "streaming" ] } }
        },
        {
          "Name": "embedding-deployment",
          "ClientName": "Azure",
          "ConnectionName": "azure-openai",
          "ModelName": "text-embedding-3-small",
          "Properties": { "AIDeploymentMetadata": { "Features": [ "textEmbedding" ] } }
        }
      ]
    }
  ]
}
```

`AuthenticationType` is `Default`, `ManagedIdentity` (optional `IdentityId` for a user-assigned identity)
or `ApiKey` (requires `ApiKey`).

### Azure client options

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureClient": {
          "MaxRetryAttempts": 3,
          "EnableLogging": false
        }
      }
    }
  }
}
```

Keys: `EnableDefaultRetryPolicy` (`true`), `MaxRetryAttempts` (`5`), `RateLimitRetryDelay` (`00:00:01`),
`MaxRetryDelay` (`00:00:32`), `BackoffType` (`Exponential`), `UseJitter` (`true`), `EnableLogging`,
`EnableMessageLogging`, `EnableMessageContentLogging` (all `false`).

### Azure AI Speech deployments

`AzureSpeech` deployments carry their own connection (no `ConnectionName`). In the admin, choose
**Azure AI Services** under **Add Deployment** and tick **Speech to text (transcription)**.

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "speech-to-text",
          "ClientName": "AzureSpeech",
          "Endpoint": "https://eastus.api.cognitive.microsoft.com/",
          "AuthenticationType": "ApiKey",
          "ApiKey": "{{SpeechApiKey}}",
          "Properties": { "AIDeploymentMetadata": { "Features": [ "speechToText" ] } }
        }
      ]
    }
  ]
}
```

Then set **Default Speech-to-Text Deployment** (`DefaultSpeechToTextDeploymentName`). The Azure Speech SDK
needs **GStreamer** on every host to decode the compressed audio browsers send (OGG/Opus, WebM/Opus, MP3,
FLAC); without it requests fail with `SPXERR_GSTREAMER_NOT_FOUND_ERROR` (`0x29`). In Docker install
`libgstreamer1.0-0` and the `gstreamer1.0-plugins-base/good/bad/ugly` packages before the app layer.

## Azure AI Inference (GitHub Models)

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Connections": [
          {
            "Name": "github-models",
            "ClientName": "AzureAIInference",
            "Endpoint": "https://models.inference.ai.azure.com",
            "AuthenticationType": "ApiKey",
            "ApiKey": "<github token>"
          }
        ],
        "Deployments": [
          {
            "Name": "phi-chat",
            "ClientName": "AzureAIInference",
            "ConnectionName": "github-models",
            "ModelName": "Phi-3-medium-4k-instruct",
            "Properties": { "AIDeploymentMetadata": { "Features": [ "textGeneration", "streaming" ] } }
          }
        ]
      }
    }
  }
}
```

## Ollama

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Connections": [
          { "Name": "local-ollama", "ClientName": "Ollama", "Endpoint": "http://localhost:11434" }
        ],
        "Deployments": [
          {
            "Name": "local-chat",
            "ClientName": "Ollama",
            "ConnectionName": "local-ollama",
            "ModelName": "deepseek-v2:16b",
            "Properties": { "AIDeploymentMetadata": { "Features": [ "textGeneration", "toolCalling", "streaming" ] } }
          }
        ]
      }
    }
  }
}
```

In a recipe, put the Ollama endpoint in `Properties.Endpoint` (see `orchardcore-ai-connections`). Declare
`toolCalling` only for models that support it. The repository's Aspire app host
(`CrestApps.Aspire.AppHost`) starts Ollama and adds its connection through environment variables.

## Orchestrators are not providers

`CrestApps.OrchardCore.AI.Chat.Claude` and `CrestApps.OrchardCore.AI.Chat.Copilot` replace the chat
orchestrator instead of adding a connection. They are configured under **Settings > Artificial
Intelligence** or `OrchardCore:CrestApps:AI:Claude` / `OrchardCore:CrestApps:AI:Copilot`, choose their own
models, and hide the deployment fields on a profile. See `orchardcore-ai-chat-claude` and
`orchardcore-ai-chat-copilot`.

## Write a custom provider

```csharp
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

public readonly struct AcmeClientMarker : IAIClientMarker
{
    public static string ClientName => "Acme";
}

public sealed class AcmeClientProvider : AIClientProviderBase
{
    public AcmeClientProvider(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    protected override string GetProviderName() => AcmeClientMarker.ClientName;

    protected override IChatClient GetChatClient(AIProviderConnectionEntry connection, string deploymentName)
        => new AcmeChatClient(connection.GetEndpoint(), connection.GetApiKey(), deploymentName);

    protected override IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(AIProviderConnectionEntry connection, string deploymentName)
        => throw new NotSupportedException();

#pragma warning disable MEAI001
    protected override IImageGenerator GetImageGenerator(AIProviderConnectionEntry connection, string deploymentName)
        => throw new NotSupportedException();

    protected override ISpeechToTextClient GetSpeechToTextClient(AIProviderConnectionEntry connection, string deploymentName)
        => throw new NotSupportedException();
#pragma warning restore MEAI001
}

public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAIClientProvider, AcmeClientProvider>();

        services.AddCoreAICompletionClient<ProviderAICompletionClient<AcmeClientMarker>>(AcmeClientMarker.ClientName, o =>
        {
            o.DisplayName = S["Acme"];
            o.Description = S["Provides AI profiles using Acme."];
        });

        services.AddCoreAIDeploymentProvider(AcmeClientMarker.ClientName, o =>
        {
            o.DisplayName = S["Acme"];
            o.Description = S["Acme model deployments."];
        });

        services.AddCoreAIConnectionSource(AcmeClientMarker.ClientName, o =>
        {
            o.DisplayName = S["Acme"];
            o.Description = S["Connects to the Acme AI API."];
        });
    }
}
```

- `AcmeChatClient` stands for your `IChatClient` implementation.
- The connection entry is a dictionary of the connection's `Properties` plus values added by
  `IAIProviderConnectionHandler.Initializing` (use a handler to decrypt a stored API key with the
  `AIProviderConnection` data protector). `GetEndpoint()` and `GetApiKey()` read the `Endpoint` and
  `ApiKey` keys.
- Override `GetTextToSpeechClientAsync`, `GetSpeechVoicesAsync`, `GetRealtimeClientAsync` and
  `GetRealtimeVoicesAsync` when the platform supports them.
- Set `UseContainedConnection = true` on the deployment provider for deployments that carry their own
  endpoint, like `AzureSpeech`.
- Register capabilities the platform adds with `AddAIDeploymentFeature` (see
  `orchardcore-ai-model-capabilities`).
- For the admin connection editor, add an `AIProviderConnection` display driver in a startup marked
  `[RequireFeatures("CrestApps.OrchardCore.AI.ConnectionManagement")]`, as the shipped providers do.
- Every client created through `IAIClientFactory` is metered automatically (`orchardcore-ai-usage-analytics`).

## Upgrading from 2.x

- Replace `"Purpose"` on deployments with `Properties.AIDeploymentMetadata.Features`; legacy values are
  still projected on read (`Chat`/`Utility` become `textGeneration`, `toolCalling`, `streaming`).
- Remove `"IsDefault"` from deployment recipes and set `DefaultAIDeploymentSettings` instead.
- Recipe arrays are `Connections` and `Deployments` (PascalCase, as in the published schemas).
- The Azure client section is `OrchardCore:CrestApps:AI:AzureClient` with flat keys such as
  `MaxRetryAttempts`; there is no nested `Retry` object.
- Custom providers implement `IAIClientProvider` (`CanHandle`, `GetChatClientAsync(AIProviderConnectionEntry, string)`, ...)
  or derive from `AIClientProviderBase`; the `CreateChatClientAsync(AIProviderConnection, string)` shape
  and `AddAIProfile<TClient>` registration are gone.

## Related skills

- `orchardcore-ai-connections` for connection recipes and the admin UI.
- `orchardcore-ai-model-capabilities` for capabilities, parameters and slots.
- `orchardcore-ai` for site defaults, profiles and resolving deployments in code.
- `crestapps-core-openai`, `crestapps-core-azure-openai`, `crestapps-core-azure-ai-inference`,
  `crestapps-core-ollama` for the framework provider packages.

# Orchard Core AI Chat Interactions Practical Examples

## Recipe: Chat Interactions with Document Retrieval (Azure AI Search)

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.AzureAI",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml",
        "CrestApps.OrchardCore.OpenAI"
      ]
    },
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Source": "OpenAI",
          "Name": "openai",
          "DisplayText": "OpenAI",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "{{YourApiKey}}"
            }
          }
        }
      ]
    },
    {
      "name": "AIDeployment",
      "Deployments": [
        {
          "Name": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "toolCalling", "streaming" ]
            }
          }
        },
        {
          "Name": "text-embedding-3-small",
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textEmbedding" ]
            }
          }
        }
      ]
    }
  ]
}
```

`CrestApps.OrchardCore.AI.Documents.AzureAI` enables `OrchardCore.AzureAI` and `OrchardCore.Indexing` for you. After running the recipe:

1. Configure the Azure AI Search connection for `OrchardCore.AzureAI`.
2. In **Search → Indexing**, create an **AI Documents** index that uses `text-embedding-3-small`.
3. In **Settings → Artificial Intelligence**, select the index in the document settings and turn on **Allow document uploads in chat interactions**.
4. Open **Artificial Intelligence → Chat Interactions**, click **New Chat**, and upload a file on the **Knowledge** panel.

## Recipe: Chat Interactions with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.Elasticsearch",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.OpenAI"
      ]
    }
  ]
}
```

## Configuration: Chat, Utility, Embedding and Image Deployments

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
          {
            "Name": "gpt-4o",
            "ClientName": "OpenAI",
            "ConnectionName": "openai",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textGeneration", "toolCalling", "streaming" ]
              }
            }
          },
          {
            "Name": "gpt-4o-mini",
            "ClientName": "OpenAI",
            "ConnectionName": "openai",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textGeneration", "toolCalling", "streaming" ]
              }
            }
          },
          {
            "Name": "text-embedding-3-small",
            "ClientName": "OpenAI",
            "ConnectionName": "openai",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textEmbedding" ]
              }
            }
          },
          {
            "Name": "dall-e-3",
            "ClientName": "OpenAI",
            "ConnectionName": "openai",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "imageOutput" ]
              }
            }
          }
        ]
      }
    }
  }
}
```

Configuration only declares what each model can do. Assign the chat, utility, embedding and image slots under **Settings → Artificial Intelligence → Default Deployments**, or pick deployments on each interaction.

## Recipe: Site-Wide Conversation Mode for Interactions

```json
{
  "steps": [
    {
      "name": "Settings",
      "ChatInteractionChatModeSettings": {
        "ChatMode": "Conversation",
        "EnableTextToSpeechPlayback": true
      }
    }
  ]
}
```

Conversation mode is carried by the interaction's conversation deployment, the site's default realtime deployment, or the first realtime-capable deployment; otherwise by the speech-to-text plus text-to-speech cascade. See `orchardcore-ai-realtime-voice`.

## Code: Persist an Extra Interaction Setting

The chat page saves settings over SignalR and never posts the editor form, so a field rendered by a `DisplayDriver<ChatInteraction>` is only stored when a settings handler reads it from the payload:

```csharp
using System.Text.Json;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;

public sealed class TicketQueueMetadata
{
    public string QueueName { get; set; }
}

public sealed class TicketQueueSettingsHandler : IChatInteractionSettingsHandler
{
    private const string QueueNameKey = "ticketQueueName";

    public Task UpdatingAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
    {
        // A payload without the key leaves the stored value untouched.
        if (settings.ValueKind != JsonValueKind.Object ||
            !settings.TryGetProperty(QueueNameKey, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return Task.CompletedTask;
        }

        interaction.Alter<TicketQueueMetadata>(metadata => metadata.QueueName = property.GetString());

        return Task.CompletedTask;
    }

    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IChatInteractionSettingsHandler, TicketQueueSettingsHandler>();
    }
}
```

Read the same key the settings panel sends for your field. The built-in handlers read keys such as `toolInstanceNames`, `copilotModel` and `isAllowAll`.

## Storing API Keys Securely

```bash
dotnet user-secrets set "OrchardCore:CrestApps:AI:Connections:0:ApiKey" "sk-your-api-key"
```

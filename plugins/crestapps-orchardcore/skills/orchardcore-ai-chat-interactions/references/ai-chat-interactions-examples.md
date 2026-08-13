# Orchard Core AI Chat Interactions Practical Examples

## Recipe: Full Chat Interactions Setup with Document RAG (Azure AI Search)

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.AzureAI",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml",
        "OrchardCore.AzureAI",
        "CrestApps.OrchardCore.OpenAI"
      ]
    },
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "default",
          "DisplayText": "OpenAI Default",
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
      "deployments": [
        {
          "ItemId": "openai-chat",
          "Name": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Purpose": "Chat",
          "IsDefault": true
        }
      ]
    }
  ]
}
```

After running this recipe:
1. Navigate to **Search → Indexing** and create a new index (e.g., "ChatDocuments") using Azure AI Search as the provider.
2. Navigate to **Settings → Artificial Intelligence → Chat Interactions** and select the new index as the default document index.
3. Navigate to **Artificial Intelligence → Chat Interactions** and start a new chat session, selecting your chat and utility deployments or relying on the configured defaults.

## Recipe: Chat Interactions with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.Elasticsearch",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "OrchardCore.Elasticsearch",
        "CrestApps.OrchardCore.OpenAI"
      ]
    }
  ]
}
```

## Configuration: Full Deployment Setup for Chat Interactions

Configure the provider connection and define deployments for chat, embeddings, utility work, and images:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
        {
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Name": "gpt-4o",
          "Purpose": "Chat"
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Name": "text-embedding-3-small",
          "Purpose": "Embedding"
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Name": "gpt-4o-mini",
          "Purpose": "Utility"
        },
        {
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Name": "dall-e-3",
          "Purpose": "Image"
        }
          ]
        }
      }
    }
}
```

## Recipe: Enable Chat Interactions with Image Generation

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.OpenAI"
      ]
    },
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "default",
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
      "deployments": [
        {
          "ItemId": "openai-chat",
          "Name": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Purpose": "Chat"
        },
        {
          "ItemId": "openai-image",
          "Name": "dall-e-3",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Purpose": "Image"
        }
      ]
    }
  ]
}
```

The registered `generate_image` system tool uses the Image-purpose deployment; the registered `generate_chart` system tool produces Chart.js configuration from a data description.

## Storing API Keys Securely

```bash
dotnet user-secrets set "OrchardCore:CrestApps:AI:Connections:0:ApiKey" "sk-your-api-key"
```

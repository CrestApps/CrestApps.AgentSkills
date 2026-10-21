# Orchard Core AI Chat Practical Examples

## Recipe: Full AI Chat Setup with OpenAI

Enable AI Chat, add an OpenAI connection, declare two deployments by capability, and create a chat profile:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat",
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
          "Name": "gpt-4o-mini",
          "ClientName": "OpenAI",
          "ConnectionName": "openai",
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "toolCalling", "streaming" ]
            }
          }
        }
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "general-assistant",
          "DisplayText": "General Assistant",
          "WelcomeMessage": "Hi! I'm your AI assistant. How can I help?",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ChatDeploymentName": "gpt-4o",
          "UtilityDeploymentName": "gpt-4o-mini",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful assistant. Provide clear and concise answers.",
              "Temperature": 0.5,
              "MaxTokens": 2048,
              "PastMessagesCount": 10
            }
          },
          "Settings": {
            "AIChatProfileSettings": {
              "IsOnAdminMenu": true
            }
          }
        }
      ]
    }
  ]
}
```

Pick the deployments as **Default Chat Deployment** and **Default Utility Deployment** under **Settings → Artificial Intelligence → Default Deployments** so profiles that leave them empty still resolve.

## Recipe: AI Chat with Azure OpenAI

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.OpenAI.Azure"
      ]
    },
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Source": "Azure",
          "Name": "azure-openai",
          "DisplayText": "Azure OpenAI",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://your-resource.openai.azure.com/",
              "AuthenticationType": "ApiKey",
              "ApiKey": "{{YourAzureApiKey}}"
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
          "Properties": {
            "AIDeploymentMetadata": {
              "Features": [ "textGeneration", "toolCalling", "streaming" ]
            }
          }
        }
      ]
    }
  ]
}
```

## Recipe: A Public Website Assistant

Create a chat profile, let anonymous visitors query it, and add the frontend widget to a layer. `OrchardCore.Widgets` and `OrchardCore.Layers` must be enabled.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Widgets",
        "OrchardCore.Layers",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.OpenAI"
      ]
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "website-assistant",
          "DisplayText": "Website Assistant",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ChatDeploymentName": "gpt-4o",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You answer questions about this website. Keep answers short."
            }
          }
        }
      ]
    },
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "Anonymous",
          "PermissionBehavior": "Add",
          "Permissions": [ "QueryAIProfile_website-assistant" ]
        }
      ]
    }
  ]
}
```

Then add an **Artificial Intelligence Chat** widget to a layer zone and select the profile in its `AIProfilePart`. The profile's own `ItemId` is what `AIProfilePart.ProfileId` stores, so pick it in the admin rather than hard-coding it in a recipe.

## Recipe: Chat Embedded in a Frame on Another Site

```json
{
  "steps": [
    {
      "name": "Settings",
      "AIVisitorIdentityOptions": {
        "AllowCrossSiteEmbedding": true,
        "UsePartitionedCookie": true,
        "RemoteAddressMode": "Hashed"
      }
    }
  ]
}
```

Without cross-site embedding the `SameSite=Lax` visitor cookie is refused inside a third-party frame, so every request looks like a new visitor and the conversation does not survive a page load.

## Code: Turn a Profile into a Voice Conversation

```csharp
public sealed class VoiceAssistantMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public VoiceAssistantMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.FindByNameAsync("website-assistant");

        if (profile is null)
        {
            return 1;
        }

        profile.AlterSettings<ChatModeProfileSettings>(settings =>
        {
            settings.ChatMode = ChatMode.Conversation;

            // Leave empty to use the site's default realtime deployment.
            settings.ConversationDeploymentName = null;
        });

        await _profileManager.UpdateAsync(profile);

        return 1;
    }
}
```

## Configuration: AI Settings in appsettings.json

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "DefaultParameters": {
          "Temperature": 0.5,
          "MaxOutputTokens": 2048,
          "PastMessagesCount": 10
        },
        "Connections": [
          {
            "Name": "openai",
            "ClientName": "OpenAI",
            "ApiKey": "Use a secret provider"
          }
        ],
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
          }
        ]
      }
    }
  }
}
```

A tenant's own `App_Data/Sites/{tenant}/appsettings.json` uses the same keys without the `OrchardCore` wrapper.

## Storing API Keys Securely

```bash
dotnet user-secrets set "OrchardCore:CrestApps:AI:Connections:0:ApiKey" "sk-your-api-key"
```

In production use the environment variable form, for example `OrchardCore__CrestApps__AI__Connections__0__ApiKey`, or a key vault.

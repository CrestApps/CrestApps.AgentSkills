---
name: orchardcore-ai
description: Skill for configuring AI integrations in Orchard Core. Covers AI service registration, MCP enablement, prompt configuration, and agent framework integration.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI - Prompt Templates

## Configure AI Integration

You are an Orchard Core expert. Generate code and configuration for AI integrations in Orchard Core.

### Guidelines

- Orchard Core supports AI integrations through the CrestApps AI module ecosystem.
- Supported AI providers: OpenAI, Azure, AzureAIInference, and Ollama.
- Configure AI services through `appsettings.json` or the admin UI.
- Use dependency injection to access AI services in modules.
- Always secure API keys using user secrets or environment variables, never hardcode them.
- AI profiles define how the AI system interacts with users, including system messages and response behavior.
- Profile types include `Chat`, `Utility`, `TemplatePrompt`, and `Agent`.
- Agent profiles are reusable agents exposed as AI tools — each agent requires a `Description` field.
- Agent availability: `OnDemand` (default, included via selection) or `AlwaysAvailable` (auto-included in every request).
- `ISpeechToTextClient` is available via `IAIClientFactory.CreateSpeechToTextClientAsync()` for providers that support it (OpenAI, Azure OpenAI).

### Enabling AI Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI"
      ],
      "disable": []
    }
  ]
}
```

### AI Configuration in appsettings.json

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "DefaultParameters": {
        "Temperature": 0,
        "MaxOutputTokens": 800,
        "TopP": 1,
        "FrequencyPenalty": 0,
        "PresencePenalty": 0,
        "PastMessagesCount": 10,
        "MaximumIterationsPerRequest": 1,
        "EnableOpenTelemetry": false,
        "EnableDistributedCaching": true
      },
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "default",
          "DefaultDeploymentName": "gpt-4o",
          "DefaultIntentDeploymentName": "gpt-4o-mini",
          "DefaultEmbeddingDeploymentName": "",
          "DefaultImagesDeploymentName": "",
          "DefaultSpeechToTextDeploymentName": "",
          "Connections": {
            "default": {
              "DefaultDeploymentName": "gpt-4o",
              "DefaultIntentDeploymentName": "gpt-4o-mini"
            }
          }
        }
      }
    }
  }
}
```

### Deployment Name Settings

| Setting | Description | Required |
|---------|-------------|----------|
| `DefaultDeploymentName` | The default model for chat completions | Yes |
| `DefaultEmbeddingDeploymentName` | The model for generating embeddings (for RAG/vector search) | No |
| `DefaultIntentDeploymentName` | A lightweight model for intent classification (e.g., `gpt-4o-mini`) | No |
| `DefaultImagesDeploymentName` | The model for image generation (e.g., `dall-e-3`) | No |
| `DefaultSpeechToTextDeploymentName` | The model for speech-to-text (e.g., `whisper-1`) | No |

### Adding AI Provider Connection via Recipe

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "default",
          "IsDefault": true,
          "DefaultDeploymentName": "gpt-4o",
          "DisplayText": "OpenAI",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "{{YourApiKey}}"
            }
          }
        }
      ]
    }
  ]
}
```

### AI Profile Types

| Type | Description | Key Properties |
|------|-------------|----------------|
| `Chat` | Interactive conversational profile | WelcomeMessage, SystemMessage, tools, agents |
| `Utility` | Background processing profile | SystemMessage, tools |
| `TemplatePrompt` | Template-driven prompt profile | PromptTemplate, PromptSubject |
| `Agent` | Reusable agent exposed as an AI tool | Description (required), SystemMessage, tools, agents |

### Creating AI Profiles via Recipe

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Source": "OpenAI",
          "Name": "{{ProfileName}}",
          "DisplayText": "{{DisplayName}}",
          "WelcomeMessage": "{{WelcomeMessage}}",
          "Description": "",
          "FunctionNames": [],
          "AgentNames": [],
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "PromptTemplate": null,
          "ConnectionName": "",
          "DeploymentId": "",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "{{SystemMessage}}",
              "Temperature": null,
              "TopP": null,
              "FrequencyPenalty": null,
              "PresencePenalty": null,
              "MaxTokens": null,
              "PastMessagesCount": null
            }
          }
        }
      ]
    }
  ]
}
```

### Creating an Agent Profile via Recipe

Agent profiles are exposed as AI tools that other profiles/interactions can invoke. The `Description` field is required — it's used by the LLM to decide when to invoke the agent.

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Source": "OpenAI",
          "Name": "research-agent",
          "DisplayText": "Research Agent",
          "Description": "An agent that can research topics on the internet and provide comprehensive summaries with citations.",
          "Type": "Agent",
          "TitleType": "InitialPrompt",
          "ConnectionName": "",
          "DeploymentId": "",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a research assistant. Gather information, verify facts, and provide comprehensive answers with sources.",
              "Temperature": 0.3,
              "MaxTokens": 4096
            },
            "AgentMetadata": {
              "Availability": "OnDemand"
            }
          }
        }
      ]
    }
  ]
}
```

### Agent Availability

| Value | Description |
|-------|-------------|
| `OnDemand` | Default. Agent is only included when explicitly selected by the user in the Capabilities tab. |
| `AlwaysAvailable` | Agent is automatically included in every AI request. Warning: increases token usage. Not shown in the agent selection UI. |

### Defining Chat Profiles Using Code

```csharp
public sealed class SystemDefinedAIProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public SystemDefinedAIProfileMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync("OpenAI");

        profile.Name = "UniqueTechnicalName";
        profile.DisplayText = "A Display name for the profile";
        profile.Type = AIProfileType.Chat;

        profile.WithSettings(new AIProfileSettings
        {
            LockSystemMessage = true,
            IsRemovable = false,
            IsListable = false,
        });

        profile.WithSettings(new AIChatProfileSettings
        {
            IsOnAdminMenu = true,
        });

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "some system message",
            Temperature = 0.3f,
            MaxTokens = 4096,
        });

        await _profileManager.SaveAsync(profile);

        return 1;
    }
}
```

### Creating an Agent Profile in Code

```csharp
public async Task<int> CreateAsync()
{
    var profile = await _profileManager.NewAsync("OpenAI");

    profile.Name = "research-agent";
    profile.DisplayText = "Research Agent";
    profile.Description = "Researches topics and provides comprehensive summaries with citations.";
    profile.Type = AIProfileType.Agent;

    profile.Put(new AIProfileMetadata
    {
        SystemMessage = "You are a research assistant...",
        Temperature = 0.3f,
        MaxTokens = 4096,
    });

    profile.Put(new AgentMetadata
    {
        Availability = AgentAvailability.OnDemand,
    });

    await _profileManager.SaveAsync(profile);
    return 1;
}
```

### Using ISpeechToTextClient

```csharp
public sealed class MySpeechService
{
    private readonly IAIClientFactory _clientFactory;

    public MySpeechService(IAIClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<string> TranscribeAsync(Stream audioStream, string providerName, string connectionName)
    {
        var client = await _clientFactory.CreateSpeechToTextClientAsync(providerName, connectionName);

        var response = await client.GetTextAsync(audioStream);

        return response.Text;
    }
}
```

> **Note:** `ISpeechToTextClient` is supported by OpenAI and Azure OpenAI providers. Ollama and Azure AI Inference throw `NotSupportedException`.

### Extending AI Chat with Custom Functions

```csharp
public sealed class GetWeatherFunction : AIFunction
{
    public const string TheName = "get_weather";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
     {
       "type": "object",
       "properties": {
         "Location": {
           "type": "string",
           "description": "The geographic location for which the weather information is requested."
         }
       },
       "additionalProperties": false,
       "required": ["Location"]
     }
    """);

    public override string Name => TheName;

    public override string Description => "Retrieves weather information for a specified location.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetValue("Location", out var prompt) || prompt is null)
        {
            return ValueTask.FromResult<object>("Location is required.");
        }

        var location = prompt is JsonElement jsonElement
            ? jsonElement.GetString()
            : prompt?.ToString();

        var weather = Random.Shared.NextDouble() > 0.5
            ? $"It's sunny in {location}."
            : $"It's raining in {location}.";

        return ValueTask.FromResult<object>(weather);
    }
}
```

### Registering Custom AI Tools

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.TheName);
```

Or with configuration options:

```csharp
services.AddAITool<GetWeatherFunction>(GetWeatherFunction.TheName, options =>
{
    options.Title = "Weather Getter";
    options.Description = "Retrieves weather information for a specified location.";
    options.Category = "Service";
});
```

### Security Best Practices

- Store API keys in user secrets during development: `dotnet user-secrets set "OrchardCore:CrestApps_AI:Providers:OpenAI:Connections:default:ApiKey" "your-key"`
- Use environment variables in production.
- Apply appropriate permissions to restrict AI feature access.
- Monitor token usage and set rate limits for production deployments.

---
name: orchardcore-ai-providers
description: Skill for configuring AI provider modules in Orchard Core using CrestApps packages. Covers OpenAI, Azure OpenAI, Azure AI Inference (GitHub Models), Ollama, Claude orchestrator, and Copilot orchestrator. Use this skill when requests mention AI Providers, Provider Connections, Provider Deployments, OpenAI Connection, Azure OpenAI Connection, Ollama Setup, Claude Settings, Copilot Settings, or closely related Orchard Core provider setup work. Strong matches include CrestApps.OrchardCore.OpenAI, CrestApps.OrchardCore.OpenAI.Azure, CrestApps.OrchardCore.AzureAIInference, CrestApps.OrchardCore.Ollama, CrestApps.OrchardCore.AI.Chat.Claude, CrestApps.OrchardCore.AI.Chat.Copilot, OpenAIConnectionMetadata, AzureConnectionMetadata, AzureAIInferenceConnectionMetadata, ClaudeSettings, CopilotSettings. It also helps with connection recipes, deployment recipes, orchestrator configuration, plus the admin flows and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Providers - Prompt Templates

## Configure AI Providers

You are an Orchard Core expert. Generate code, configuration, and recipes for setting up AI provider modules in an Orchard Core application using CrestApps packages.

### Guidelines

- Every AI feature in Orchard Core requires a provider module. Enable at least one provider alongside `CrestApps.OrchardCore.AI`.
- Provider modules fall into two categories: **connection-based providers** (OpenAI, Azure OpenAI, Azure AI Inference, Ollama) that create connections and deployments, and **orchestrators** (Claude, Copilot) that provide alternative chat session orchestration via site settings.
- Connection-based providers follow a three-layer model: a **connection** holds credentials, a **deployment** maps a model name to a connection, and an **AI profile** references deployments by name.
- Always secure API keys using `dotnet user-secrets` during development and environment variables or Azure Key Vault in production. Never hardcode keys.
- Install all CrestApps provider NuGet packages in the web/startup project.
- When multiple shared connections exist for the same provider, assign the intended `ConnectionName` on each deployment explicitly.

### Provider Feature Overview

| Provider | Feature ID | NuGet Package | Description |
|----------|-----------|---------------|-------------|
| OpenAI | `CrestApps.OrchardCore.OpenAI` | `CrestApps.OrchardCore.OpenAI` | Connect to OpenAI or any OpenAI-compatible endpoint (DeepSeek, Gemini, Together AI, LM Studio, etc.) |
| Azure OpenAI | `CrestApps.OrchardCore.OpenAI.Azure` | `CrestApps.OrchardCore.OpenAI.Azure` | Azure-hosted OpenAI models with support for API key, Managed Identity, and Azure AI Services speech |
| Azure AI Inference | `CrestApps.OrchardCore.AzureAIInference` | `CrestApps.OrchardCore.AzureAIInference` | Azure AI Inference service and GitHub Models |
| Ollama | `CrestApps.OrchardCore.Ollama` | `CrestApps.OrchardCore.Ollama` | Local Ollama models for self-hosted AI |
| Claude Orchestrator | `CrestApps.OrchardCore.AI.Chat.Claude` | `CrestApps.OrchardCore.AI.Chat.Claude` | Claude-based orchestrator for AI chat sessions using the Anthropic API |
| Copilot Orchestrator | `CrestApps.OrchardCore.AI.Chat.Copilot` | `CrestApps.OrchardCore.AI.Chat.Copilot` | GitHub Copilot SDK-based orchestrator for AI chat sessions |

### Enabling Provider Features

Enable one or more providers alongside the core AI feature:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.OpenAI",
        "CrestApps.OrchardCore.OpenAI.Azure",
        "CrestApps.OrchardCore.AzureAIInference",
        "CrestApps.OrchardCore.Ollama"
      ],
      "disable": []
    }
  ]
}
```

Only include the providers you need. Each provider registers its own connection source, deployment provider, and AI profile source.

## OpenAI Provider

The OpenAI provider (`CrestApps.OrchardCore.OpenAI`) connects to the OpenAI API or any OpenAI-compatible endpoint. Its `Source` / `ClientName` is `"OpenAI"`.

### OpenAI Connection via Recipe

```json
{
  "steps": [
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
              "ApiKey": "{{YourOpenAIApiKey}}"
            }
          }
        }
      ]
    }
  ]
}
```

### OpenAI Connection via appsettings.json

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "ApiKey": "<!-- Your API Key -->",
              "Deployments": [
                { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true },
                { "Name": "gpt-4o-mini", "Type": "Utility", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

### OpenAI Deployments via Recipe

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "ItemId": "openai-chat",
          "Name": "gpt-4o",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Chat",
          "IsDefault": true
        },
        {
          "ItemId": "openai-utility",
          "Name": "gpt-4o-mini",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Utility",
          "IsDefault": true
        }
      ]
    }
  ]
}
```

### Using OpenAI-Compatible Endpoints

To connect to DeepSeek, Together AI, LM Studio, or other OpenAI-compatible services, create an OpenAI connection with a custom endpoint:

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "OpenAI",
          "Name": "deepseek",
          "DisplayText": "DeepSeek",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.deepseek.com/v1",
              "ApiKey": "{{YourDeepSeekApiKey}}"
            }
          }
        }
      ]
    }
  ]
}
```

## Azure OpenAI Provider

The Azure OpenAI provider (`CrestApps.OrchardCore.OpenAI.Azure`) connects to Azure-hosted OpenAI models. Its `Source` / `ClientName` is `"Azure"`. It also registers an `"AzureSpeech"` deployment provider for Azure AI Services speech.

### Azure OpenAI Connection via Recipe

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "Azure",
          "Name": "default",
          "DisplayText": "Azure OpenAI",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://{{YourResourceName}}.openai.azure.com",
              "AuthenticationType": "ApiKey",
              "ApiKey": "{{YourAzureApiKey}}"
            }
          }
        }
      ]
    }
  ]
}
```

### Azure OpenAI Authentication Types

| AuthenticationType | Description |
|--------------------|-------------|
| `Default` | Uses Azure Default Credential (environment, managed identity, Visual Studio, etc.) |
| `ApiKey` | API key authentication |
| `ManagedIdentity` | Azure Managed Identity with optional `IdentityId` for user-assigned identities |

### Azure OpenAI Connection with Managed Identity

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "Azure",
          "Name": "azure-managed",
          "DisplayText": "Azure OpenAI (Managed Identity)",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://{{YourResourceName}}.openai.azure.com",
              "AuthenticationType": "ManagedIdentity",
              "IdentityId": "{{OptionalUserAssignedIdentityId}}"
            }
          }
        }
      ]
    }
  ]
}
```

### Azure OpenAI Deployments via Recipe

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "ItemId": "azure-chat",
          "Name": "gpt-4o",
          "ClientName": "Azure",
          "ConnectionName": "default",
          "Type": "Chat",
          "IsDefault": true
        },
        {
          "ItemId": "azure-embedding",
          "Name": "text-embedding-3-large",
          "ClientName": "Azure",
          "ConnectionName": "default",
          "Type": "Embedding",
          "IsDefault": true
        }
      ]
    }
  ]
}
```

### Azure Client Options via appsettings.json

Configure Azure-specific client options at the shell level:

```json
{
  "OrchardCore": {
    "CrestApps:AI:AzureClient": {
      "Retry": {
        "MaxRetries": 3
      }
    }
  }
}
```

## Azure AI Inference Provider

The Azure AI Inference provider (`CrestApps.OrchardCore.AzureAIInference`) connects to Azure AI Inference and GitHub Models. Its `Source` / `ClientName` is `"AzureAIInference"`.

### Azure AI Inference Connection via Recipe

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "AzureAIInference",
          "Name": "default",
          "DisplayText": "GitHub Models",
          "Properties": {
            "AzureAIInferenceConnectionMetadata": {
              "Endpoint": "https://models.inference.ai.azure.com",
              "AuthenticationType": "ApiKey",
              "ApiKey": "{{YourGitHubToken}}"
            }
          }
        }
      ]
    }
  ]
}
```

The `AzureAIInferenceConnectionMetadata` supports the same authentication types as Azure OpenAI (`Default`, `ApiKey`, `ManagedIdentity`).

### Azure AI Inference Deployments via Recipe

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "ItemId": "github-chat",
          "Name": "gpt-4o",
          "ClientName": "AzureAIInference",
          "ConnectionName": "default",
          "Type": "Chat",
          "IsDefault": true
        }
      ]
    }
  ]
}
```

## Ollama Provider

The Ollama provider (`CrestApps.OrchardCore.Ollama`) connects to a local Ollama instance. Its `Source` / `ClientName` is `"Ollama"`. It uses the OpenAI-compatible connection pattern, so no API key is required for local setups.

### Ollama Connection via Recipe

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "connections": [
        {
          "Source": "Ollama",
          "Name": "default",
          "DisplayText": "Ollama",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "http://localhost:11434"
            }
          }
        }
      ]
    }
  ]
}
```

### Ollama Deployments via Recipe

```json
{
  "steps": [
    {
      "name": "AIDeployment",
      "deployments": [
        {
          "ItemId": "ollama-chat",
          "Name": "llama3.1",
          "ClientName": "Ollama",
          "ConnectionName": "default",
          "Type": "Chat",
          "IsDefault": true
        }
      ]
    }
  ]
}
```

## Claude Orchestrator

The Claude orchestrator (`CrestApps.OrchardCore.AI.Chat.Claude`) provides an alternative chat orchestration layer using the Anthropic Claude API. Unlike connection-based providers, Claude is configured through site settings or shell configuration, not through the `AIProviderConnections` recipe step.

### Enabling the Claude Orchestrator

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Chat.Claude"
      ],
      "disable": []
    }
  ]
}
```

### Claude Configuration via appsettings.json

```json
{
  "OrchardCore": {
    "CrestApps:Claude": {
      "ApiKey": "{{YourClaudeApiKey}}",
      "BaseUrl": "https://api.anthropic.com",
      "DefaultModel": "claude-sonnet-4-20250514"
    }
  }
}
```

### Claude Site Settings

Navigate to **Settings → AI → Claude** in the admin UI to configure:

| Setting | Description |
|---------|-------------|
| Authentication Type | `ApiKey` or `NotConfigured` |
| API Key | Anthropic API key (stored encrypted) |
| Base URL | API endpoint (defaults to `https://api.anthropic.com`) |
| Default Model | The Claude model to use |

Site settings override values from `appsettings.json`. The orchestrator appears as an option when creating or editing AI chat profiles.

## Copilot Orchestrator

The Copilot orchestrator (`CrestApps.OrchardCore.AI.Chat.Copilot`) provides chat orchestration using the GitHub Copilot SDK. Like Claude, it is configured via site settings or shell configuration.

### Enabling the Copilot Orchestrator

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.AI.Chat.Copilot"
      ],
      "disable": []
    }
  ]
}
```

### Copilot Configuration via appsettings.json (GitHub OAuth)

```json
{
  "OrchardCore": {
    "CrestApps:Copilot": {
      "AuthenticationType": "GitHubOAuth",
      "ClientId": "{{YourGitHubAppClientId}}",
      "ClientSecret": "{{YourGitHubAppClientSecret}}",
      "Scopes": ["user:email", "read:org"]
    }
  }
}
```

### Copilot Configuration via appsettings.json (API Key)

```json
{
  "OrchardCore": {
    "CrestApps:Copilot": {
      "AuthenticationType": "ApiKey",
      "BaseUrl": "{{YourEndpointUrl}}",
      "DefaultModel": "{{YourModelName}}",
      "ApiKey": "{{YourApiKey}}"
    }
  }
}
```

### Copilot Authentication Types

| AuthenticationType | Description |
|--------------------|-------------|
| `GitHubOAuth` | GitHub OAuth flow requiring `ClientId` and `ClientSecret` from a GitHub App. Per-user credentials are stored and refreshed automatically. |
| `ApiKey` | Direct API key authentication. Requires `BaseUrl`, `DefaultModel`, and `ApiKey`. Optionally set `AzureApiVersion` for Azure-backed endpoints. |

### Copilot Site Settings

Navigate to **Settings → AI → Copilot** in the admin UI. Site settings overlay configuration values and support both authentication types. Secrets are stored encrypted using data protection.

## Connections, Deployments, and Profiles

The CrestApps AI module uses a three-layer architecture for connection-based providers:

1. **Connection** — holds provider credentials (API key, endpoint, authentication type). Created via the `AIProviderConnections` recipe step or the admin UI at **Artificial Intelligence → Connections**.
2. **Deployment** — maps a model name to a connection and specifies the deployment type (`Chat`, `Utility`, `Embedding`, `Image`, `SpeechToText`). Created via the `AIDeployment` recipe step or the admin UI at **Artificial Intelligence → Deployments**.
3. **Profile** — references deployments by name (`ChatDeploymentName`, `UtilityDeploymentName`) and is source-agnostic. Created via the `AIProfile` recipe step.

Orchestrators (Claude, Copilot) bypass the connection/deployment layer entirely. They appear as orchestration options in the chat profile editor once their feature is enabled and configured.

### Full Setup Example (OpenAI)

This recipe enables the OpenAI provider, creates a connection, adds deployments, and creates a chat profile:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
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
          "Type": "Chat",
          "IsDefault": true
        },
        {
          "ItemId": "openai-utility",
          "Name": "gpt-4o-mini",
          "ClientName": "OpenAI",
          "ConnectionName": "default",
          "Type": "Utility",
          "IsDefault": true
        }
      ]
    },
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "assistant",
          "DisplayText": "AI Assistant",
          "WelcomeMessage": "Hello! How can I help you?",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ChatDeploymentName": "gpt-4o",
          "UtilityDeploymentName": "gpt-4o-mini",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful assistant.",
              "Temperature": 0.3,
              "MaxTokens": 2048,
              "PastMessagesCount": 10
            }
          }
        }
      ]
    }
  ]
}
```

### Security Best Practices

- Store API keys using `dotnet user-secrets` during development.
- Use environment variables or Azure Key Vault in production.
- For Azure providers, prefer Managed Identity authentication over API keys when possible.
- API keys in connections are encrypted at rest using data protection.
- Orchestrator secrets (Claude API key, Copilot client secret) are also encrypted in site settings.
- Restrict provider configuration access with Orchard Core permissions.

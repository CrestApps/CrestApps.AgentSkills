---
name: orchardcore-ai-connections
description: Skill for managing AI provider connections in Orchard Core using CrestApps AI modules. Covers connection creation, editing, deletion, provider configuration, deployment, and recipe-based provisioning for OpenAI, Azure OpenAI, Ollama, and Azure AI Inference. Use this skill when requests mention AI Connection Management, Provider Connections, AI Provider Setup, Connection Recipe Steps, AI Connection Deployments, or related Orchard Core setup and troubleshooting. Strong matches include work with CrestApps.OrchardCore.AI.ConnectionManagement, AIProviderConnection, AIProviderConnectionsStep, AIProviderConnectionDeploymentStep, CrestApps.OrchardCore.OpenAI, CrestApps.OrchardCore.OpenAI.Azure, CrestApps.OrchardCore.Ollama, CrestApps.OrchardCore.AzureAIInference. It also helps with provider-specific connection metadata, API key encryption, managed identity authentication, admin UI for connections, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Connection Management - Prompt Templates

## Configure AI Provider Connections

You are an Orchard Core expert specializing in CrestApps AI provider connection management. Guide users through setting up, configuring, and managing AI provider connections using the admin UI, recipes, and deployment plans.

### Guidelines

- The AI Connection Management feature provides a centralized admin UI for managing connections to AI providers.
- Enable the `CrestApps.OrchardCore.AI.ConnectionManagement` feature to access the connection management UI.
- Each provider (OpenAI, Azure OpenAI, Ollama, Azure AI Inference) requires its own NuGet package installed in the web project.
- API keys are encrypted at rest using the ASP.NET Core Data Protection system.
- Configuration-backed connections (defined in `appsettings.json`) are read-only in the admin UI.
- Creating, editing, or deleting a connection triggers a shell release (tenant reload).
- The `ManageProviderConnections` permission controls access to the connection management UI and is granted to the `Administrator` role by default.
- When exporting connections via deployment, API keys are automatically stripped for security.

### Feature IDs

| Feature | Feature ID |
|---------|-----------|
| AI Connection Management | `CrestApps.OrchardCore.AI.ConnectionManagement` |
| OpenAI Provider | `CrestApps.OrchardCore.OpenAI` |
| Azure OpenAI Provider | `CrestApps.OrchardCore.OpenAI.Azure` |
| Ollama Provider | `CrestApps.OrchardCore.Ollama` |
| Azure AI Inference Provider | `CrestApps.OrchardCore.AzureAIInference` |

### NuGet Packages

Install the connection management and at least one provider package in your web/startup project:

| Package | Description |
|---------|-------------|
| `CrestApps.OrchardCore.AI` | Core AI services and connection management UI |
| `CrestApps.OrchardCore.OpenAI` | OpenAI provider (GPT models via OpenAI API) |
| `CrestApps.OrchardCore.OpenAI.Azure` | Azure OpenAI provider (GPT models via Azure) |
| `CrestApps.OrchardCore.Ollama` | Ollama provider (local/self-hosted models) |
| `CrestApps.OrchardCore.AzureAIInference` | Azure AI Inference provider (GitHub Models and Azure AI) |

### Supported Providers

| Provider | Client Name | Authentication |
|----------|-------------|---------------|
| OpenAI | `OpenAI` | API Key |
| Azure OpenAI | `Azure` | API Key, Managed Identity, or Default Azure Credential |
| Ollama | `Ollama` | None (local endpoint) |
| Azure AI Inference | `AzureAIInference` | API Key, Managed Identity, or Default Azure Credential |

### Enabling AI Connection Management Features

Enable the connection management feature along with at least one provider. Navigate to the admin dashboard under **Configuration → Features** and enable:

1. `CrestApps.OrchardCore.AI.ConnectionManagement` — enables the connection management admin UI
2. One or more provider features (e.g., `CrestApps.OrchardCore.OpenAI`)

You can also enable features via recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.ConnectionManagement",
        "CrestApps.OrchardCore.OpenAI"
      ]
    }
  ]
}
```

### Configuring Connections via Admin UI

Once features are enabled, navigate to **Artificial Intelligence → Provider Connections** in the admin menu.

From this page you can:

- **Create** a connection by selecting a provider and filling in endpoint and credentials.
- **Edit** an existing connection to update its display name, endpoint, or credentials.
- **Delete** connections that are no longer needed.
- **Bulk remove** multiple connections at once.

> **Note:** Connections defined via `appsettings.json` appear as read-only entries and cannot be modified through the admin UI.

### Connection Properties

Each provider connection stores the following common properties:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Unique technical name for the connection |
| `DisplayText` | `string` | Human-readable display name |
| `Source` | `string` | The provider client name (e.g., `OpenAI`, `Azure`) |
| `Properties` | `object` | Provider-specific metadata (endpoint, credentials) |

Provider-specific metadata stored in `Properties`:

**OpenAI**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Endpoint` | `Uri` | Yes | The OpenAI API endpoint |
| `ApiKey` | `string` | Yes | API key (encrypted at rest) |

**Azure OpenAI**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Endpoint` | `Uri` | Yes | The Azure OpenAI resource endpoint |
| `AuthenticationType` | `string` | Yes | `Default`, `ApiKey`, or `ManagedIdentity` |
| `ApiKey` | `string` | When using `ApiKey` auth | API key (encrypted at rest) |
| `IdentityId` | `string` | No | Optional managed identity client ID |

**Azure AI Inference (GitHub Models)**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Endpoint` | `Uri` | Yes | The Azure AI Inference endpoint |
| `AuthenticationType` | `string` | Yes | `Default`, `ApiKey`, or `ManagedIdentity` |
| `ApiKey` | `string` | When using `ApiKey` auth | API key (encrypted at rest) |
| `IdentityId` | `string` | No | Optional managed identity client ID |

**Ollama**

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Endpoint` | `Uri` | Yes | The Ollama server endpoint (e.g., `http://localhost:11434`) |

### Adding a Provider Connection via Recipe

Use the `AIProviderConnections` recipe step to provision connections. The step matches existing connections by `Name` and `Source`, creating new ones when no match is found.

**OpenAI Connection**

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Name": "default-openai",
          "DisplayText": "Default OpenAI Connection",
          "Source": "OpenAI",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "sk-your-api-key-here"
            }
          }
        }
      ]
    }
  ]
}
```

**Azure OpenAI Connection (API Key)**

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Name": "default-azure-openai",
          "DisplayText": "Default Azure OpenAI Connection",
          "Source": "Azure",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://your-resource.openai.azure.com",
              "AuthenticationType": "ApiKey",
              "ApiKey": "your-azure-api-key-here"
            }
          }
        }
      ]
    }
  ]
}
```

**Azure OpenAI Connection (Managed Identity)**

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Name": "azure-openai-mi",
          "DisplayText": "Azure OpenAI with Managed Identity",
          "Source": "Azure",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://your-resource.openai.azure.com",
              "AuthenticationType": "ManagedIdentity",
              "IdentityId": "your-managed-identity-client-id"
            }
          }
        }
      ]
    }
  ]
}
```

**Azure AI Inference Connection (GitHub Models)**

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Name": "default-github-models",
          "DisplayText": "GitHub Models Connection",
          "Source": "AzureAIInference",
          "Properties": {
            "AzureAIInferenceConnectionMetadata": {
              "Endpoint": "https://models.inference.ai.azure.com",
              "AuthenticationType": "ApiKey",
              "ApiKey": "your-github-token-here"
            }
          }
        }
      ]
    }
  ]
}
```

**Ollama Connection**

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Name": "default-ollama",
          "DisplayText": "Local Ollama Connection",
          "Source": "Ollama",
          "Properties": {
            "OllamaConnectionMetadata": {
              "Endpoint": "http://localhost:11434"
            }
          }
        }
      ]
    }
  ]
}
```

### Managing AI Connection Deployments

Use the `AIProviderConnectionDeploymentStep` deployment step to include provider connections in a deployment plan.

- Set `IncludeAll` to `true` to export all connections.
- Specify individual connections using the `ConnectionIds` array.

> **Important:** Exported connections have API keys stripped for security. After importing a deployment plan, you must re-enter API keys through the admin UI or provide them via a follow-up recipe.

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Name": "default-openai",
          "DisplayText": "Default OpenAI Connection",
          "Source": "OpenAI",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": ""
            }
          }
        }
      ]
    }
  ]
}
```

### Security Best Practices

- **Never commit API keys** to source control. Use environment variables or Azure Key Vault to inject secrets at deployment time.
- **Use Managed Identity** when deploying to Azure to avoid storing API keys entirely.
- **Limit permissions** — grant the `ManageProviderConnections` permission only to trusted administrator roles.
- **Review deployment plans** before importing to ensure no sensitive data is accidentally included.
- API keys are encrypted at rest using the `AIProviderConnection` data protector via `IDataProtectionProvider`.

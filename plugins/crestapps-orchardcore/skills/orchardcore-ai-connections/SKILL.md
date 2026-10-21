---
name: orchardcore-ai-connections
description: Skill for managing AI provider connections in Orchard Core with the CrestApps.OrchardCore.AI.ConnectionManagement feature in CrestApps.OrchardCore 3.0. Covers the Provider Connections admin list and editor, the ManageProviderConnections permission, provider-specific connection metadata (OpenAIConnectionMetadata, AzureConnectionMetadata, AzureAIInferenceConnectionMetadata, Ollama Endpoint), API key encryption with data protection, managed identity authentication, configuration-backed read-only connections in appsettings.json, the AIProviderConnections recipe step, the AI Provider Connections deployment step and stripped API keys, shell release on save, and troubleshooting. Use this skill when requests mention AI connection management, provider connections, AIProviderConnection, AIProviderConnections recipe step, AIProviderConnectionDeploymentStep, connection credentials, or connecting Orchard Core to OpenAI, Azure OpenAI, Azure AI Inference (GitHub Models) or Ollama.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Connection Management

A connection stores the endpoint and credentials for one provider account. Deployments name a connection
through `ConnectionName`; profiles name deployments. This skill covers the connection layer only; for
deployments and capabilities see `orchardcore-ai` and `orchardcore-ai-model-capabilities`.

## Guidelines

- Enable `CrestApps.OrchardCore.AI.ConnectionManagement` to get the **Artificial Intelligence > Provider
  Connections** screen, the provider connection editors and the `AIProviderConnections` recipe step. The
  step is registered only while this feature is enabled.
- Install each provider package (`CrestApps.OrchardCore.OpenAI`, `CrestApps.OrchardCore.OpenAI.Azure`,
  `CrestApps.OrchardCore.AzureAIInference`, `CrestApps.OrchardCore.Ollama`) in the web/startup project and
  enable its feature.
- API keys are encrypted at rest with ASP.NET Core data protection (protector name `AIProviderConnection`).
  Data protection keys must be shared across nodes and preserved across deployments, or stored keys cannot
  be decrypted.
- Creating, editing or deleting a connection releases the tenant shell so clients pick up the change.
- `ManageProviderConnections` (granted to Administrator) controls the screen.
- Connections defined in `appsettings.json` are listed separately and are read-only.
- Never commit API keys. Prefer managed identity on Azure.

## Features and providers

| Provider | Feature ID | `Source` / `ClientName` | Metadata object in `Properties` | Authentication |
| --- | --- | --- | --- | --- |
| OpenAI and OpenAI-compatible | `CrestApps.OrchardCore.OpenAI` | `OpenAI` | `OpenAIConnectionMetadata` (`Endpoint`, `ApiKey`) | API key |
| Azure OpenAI | `CrestApps.OrchardCore.OpenAI.Azure` | `Azure` | `AzureConnectionMetadata` (`Endpoint`, `AuthenticationType`, `ApiKey`, `IdentityId`) | `Default`, `ManagedIdentity`, `ApiKey` |
| Azure AI Inference (GitHub Models) | `CrestApps.OrchardCore.AzureAIInference` | `AzureAIInference` | `AzureAIInferenceConnectionMetadata` (same keys as Azure) | `Default`, `ManagedIdentity`, `ApiKey` |
| Ollama | `CrestApps.OrchardCore.Ollama` | `Ollama` | none; a plain `Endpoint` key | none |

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.ConnectionManagement",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

## Admin UI

**Artificial Intelligence > Provider Connections** (`/Admin/ai/provider/connections`) lists stored
connections in Orchard Core's `AdminList` (list name `AIProviderConnections`) with search, bulk **Delete**
and the List or Grid layout. Configuration-backed connections appear below in a read-only list
(`AIProviderConnectionsReadOnly`) without select, edit or delete. **Add Connection** asks for the provider
first, then shows that provider's editor. To add a column or row action, use `IAdminListColumnsProvider` or
a `SummaryAdminGrid` placement (see `orchardcore-admin-lists`).

## Connections by recipe

The step matches an existing connection by `ItemId`, or by `Name` plus `Source`, and creates it otherwise.

```json
{
  "steps": [
    {
      "name": "AIProviderConnections",
      "Connections": [
        {
          "Source": "OpenAI",
          "Name": "openai-cloud",
          "DisplayText": "OpenAI",
          "Properties": {
            "OpenAIConnectionMetadata": {
              "Endpoint": "https://api.openai.com/v1",
              "ApiKey": "{{OpenAIApiKey}}"
            }
          }
        },
        {
          "Source": "Azure",
          "Name": "azure-openai",
          "DisplayText": "Azure OpenAI (managed identity)",
          "Properties": {
            "AzureConnectionMetadata": {
              "Endpoint": "https://my-resource.openai.azure.com/",
              "AuthenticationType": "ManagedIdentity",
              "IdentityId": "optional-user-assigned-client-id"
            }
          }
        },
        {
          "Source": "AzureAIInference",
          "Name": "github-models",
          "DisplayText": "GitHub Models",
          "Properties": {
            "AzureAIInferenceConnectionMetadata": {
              "Endpoint": "https://models.inference.ai.azure.com",
              "AuthenticationType": "ApiKey",
              "ApiKey": "{{GitHubToken}}"
            }
          }
        },
        {
          "Source": "Ollama",
          "Name": "local-ollama",
          "DisplayText": "Local Ollama",
          "Properties": {
            "Endpoint": "http://localhost:11434"
          }
        }
      ]
    }
  ]
}
```

- `AuthenticationType: "ApiKey"` requires `ApiKey`; `ManagedIdentity` without `IdentityId` uses the
  system-assigned identity; `Default` uses the Azure default credential chain.
- For an OpenAI-compatible service (DeepSeek, Gemini, Together AI, vLLM, LM Studio) use `Source: "OpenAI"`
  with that service's `Endpoint`.
- An unknown `Source` stops the step with "Unable to find a connection-source that can handle the source".
- Follow the connection with an `AIDeployment` step that names it in `ConnectionName`.

## Connections in appsettings.json

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Connections": [
          {
            "Name": "azure-openai",
            "ClientName": "Azure",
            "Endpoint": "https://my-resource.openai.azure.com/",
            "AuthenticationType": "ApiKey",
            "ApiKey": "<from a secret store>"
          },
          {
            "Name": "local-ollama",
            "ClientName": "Ollama",
            "Endpoint": "http://localhost:11434"
          }
        ]
      }
    }
  }
}
```

Configuration connections use flat keys and `ClientName`. Entries are addressed by index, so
`OrchardCore__CrestApps__AI__Connections__0__ApiKey` replaces the key of the first entry; use a high index
(for example `__90__`) to add a connection beside existing ones. The older `CrestApps_AI:Providers` shape is
still read. Both stored and configured connections appear in deployment editors.

## Export connections

The **AI Provider Connections** deployment step (`AIProviderConnectionDeploymentStep`) exports all
connections (`IncludeAll`) or selected ones (`ConnectionIds`) as an `AIProviderConnections` recipe step.
API keys are written as empty strings, so re-enter them after import through the admin, a follow-up
recipe, or configuration.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Recipe step `AIProviderConnections` is unknown | Enable `CrestApps.OrchardCore.AI.ConnectionManagement` before the step runs |
| Provider missing from **Add Connection** | Enable the provider feature |
| Stored key fails after redeploy or on another node | Data protection keys were not shared or persisted |
| A configuration connection cannot be edited | It is read-only by design; change configuration instead |
| A deployment cannot find its connection | `ConnectionName` must match the connection `Name` exactly |

## Related skills

- `orchardcore-ai-providers` for per-provider setup, deployments and the Azure Speech deployment.
- `orchardcore-ai` for deployments, profiles and AI settings.
- `orchardcore-ai-model-capabilities` for what deployments declare.
- `orchardcore-deployments` and `orchardcore-recipes` for deployment plans and recipe mechanics.

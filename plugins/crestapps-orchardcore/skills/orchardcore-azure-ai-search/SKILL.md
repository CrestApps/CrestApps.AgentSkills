---
name: orchardcore-azure-ai-search
description: Skill for configuring Azure AI Search in Orchard Core. Covers Azure AI Search setup, index creation and management, recipe steps for index operations, search module integration, custom data indexing, frontend search configuration, and Azure-specific search settings. Use this skill when requests mention Orchard Core Azure AI Search, Configure Azure AI Search, Enabling Azure AI Search Features, Azure AI Search Connection Configuration, Creating an Azure AI Search Index Profile (Recommended), Legacy Index Creation (Obsolete), or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Search.AzureAI, OrchardCore.OrchardCore_AzureAISearch, OrchardCore.Search, OrchardCore.Indexing. It also helps with Creating an Azure AI Search Index Profile (Recommended), Legacy Index Creation (Obsolete), Reset Azure AI Search Index, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Azure AI Search - Prompt Templates

## Configure Azure AI Search

You are an Orchard Core expert. Generate code and configuration for Azure AI Search integration in Orchard Core.

### Guidelines

- Enable `OrchardCore.Search.AzureAI` to manage Azure AI Search indices.
- Configure the Azure AI Search connection in `appsettings.json` under `OrchardCore.OrchardCore_AzureAISearch` or through the admin UI at Settings > Search > Azure AI Search.
- Use the `CreateOrUpdateIndexProfile` recipe step to create indexes (preferred over the obsolete `azureai-index-create` step).
- Use the `ResetIndex` recipe step to restart indexing without deleting entries (preferred over the obsolete `azureai-index-reset` step).
- Use the `RebuildIndex` recipe step to delete and recreate indexes (preferred over the obsolete `azureai-index-rebuild` step).
- Supported authentication types: `ApiKey`, `Default`, `ManagedIdentity`.
- Use `IndexesPrefix` to prevent naming conflicts when sharing an Azure AI Search instance across environments.
- Enable `OrchardCore.Search` alongside Azure AI Search for frontend search functionality.
- Azure AI Search uses a different field naming convention with double underscores (e.g., `Content__ContentItem__FullText`).
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Azure AI Search Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Search",
        "OrchardCore.Search.AzureAI",
        "OrchardCore.Indexing"
      ],
      "disable": []
    }
  ]
}
```

### Azure AI Search Connection Configuration

Configure in `appsettings.json`:

```json
{
  "OrchardCore": {
    "OrchardCore_AzureAISearch": {
      "Endpoint": "https://[search-service-name].search.windows.net",
      "IndexesPrefix": "",
      "AuthenticationType": "ApiKey",
      "IdentityClientId": null,
      "DisableUIConfiguration": false,
      "Credential": {
        "Key": "your-api-key-here"
      }
    }
  }
}
```

| Property | Description |
|----------|-------------|
| `Endpoint` | The Azure AI Search service endpoint URL. |
| `IndexesPrefix` | Prefix for all index names. Use environment names (e.g., `"staging-"`) to prevent conflicts when sharing a single Azure AI Search instance. |
| `AuthenticationType` | `"ApiKey"` for key-based, `"Default"` for default Azure credentials, or `"ManagedIdentity"` for managed identity. |
| `IdentityClientId` | Optional client ID for user-assigned managed identity. Omit for system-assigned identity. |
| `DisableUIConfiguration` | When `true`, disables per-tenant UI configuration and forces all tenants to use appsettings values. |
| `Credential.Key` | The API key for the Azure AI Search service. Required for `ApiKey` authentication. |

### Creating an Azure AI Search Index Profile (Recommended)

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "BlogPostsAI",
          "IndexName": "blogposts",
          "ProviderName": "AzureAISearch",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": ["BlogPost"],
              "Culture": "any"
            },
            "AzureAISearchIndexMetadata": {
              "AnalyzerName": "standard"
            },
            "AzureAISearchDefaultQueryMetadata": {
              "QueryAnalyzerName": "standard.lucene",
              "DefaultSearchFields": [
                "Content__ContentItem__FullText"
              ]
            }
          }
        }
      ]
    }
  ]
}
```

| Property | Description |
|----------|-------------|
| `Name` | Unique display name for the index profile. |
| `IndexName` | Physical index name in Azure AI Search. |
| `ProviderName` | Must be `"AzureAISearch"`. |
| `Type` | Source category, typically `"Content"` for content items. |
| `IndexLatest` | When `true`, indexes both published and draft items. |
| `IndexedContentTypes` | Array of content type names to include. |
| `Culture` | `"any"` for all cultures or a specific culture code. |
| `AnalyzerName` | Azure AI Search analyzer (e.g., `"standard"`, `"standard.lucene"`). |
| `QueryAnalyzerName` | Analyzer used at query time. |
| `DefaultSearchFields` | Fields to search across. Uses double underscores as separators (e.g., `Content__ContentItem__FullText`). |

### Legacy Index Creation (Obsolete)

```json
{
  "steps": [
    {
      "name": "azureai-index-create",
      "Indices": [
        {
          "Source": "Contents",
          "IndexName": "articles",
          "IndexLatest": false,
          "IndexedContentTypes": ["Article"],
          "AnalyzerName": "standard.lucene",
          "Culture": "any"
        }
      ]
    }
  ]
}
```

### Reset Azure AI Search Index

Restarts indexing from the beginning without deleting existing entries:

```json
{
  "steps": [
    {
      "name": "ResetIndex",
      "indexNames": [
        "BlogPostsAI"
      ]
    }
  ]
}
```

To reset all indices:

```json
{
  "steps": [
    {
      "name": "ResetIndex",
      "IncludeAll": true
    }
  ]
}
```

### Rebuild Azure AI Search Index

Deletes and recreates the full index content:

```json
{
  "steps": [
    {
      "name": "RebuildIndex",
      "indexNames": [
        "BlogPostsAI"
      ]
    }
  ]
}
```

To rebuild all indices:

```json
{
  "steps": [
    {
      "name": "RebuildIndex",
      "IncludeAll": true
    }
  ]
}
```

### Indexing Custom Data

Register a custom indexing source in `Startup.cs`:

```csharp
using OrchardCore.Modules;

namespace MyModule;

[Feature("MyModule.CustomAzureAIIndex")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAzureAISearchIndexingSource("CustomSource", o =>
        {
            o.DisplayName = S["Custom Source"];
            o.Description = S["Create an Azure AI Search index based on a custom data source."];
        });
    }
}
```

For custom index field mapping, implement `IIndexProfileHandler` and populate `AzureAISearchIndexMetadata` with `IndexMappings` in the `CreatingAsync` and `UpdatingAsync` methods. Use `IndexProfileHandlerBase` to simplify the implementation.

To capture custom UI data related to your source, implement `DisplayDriver<IndexEntity>`.

### Frontend Search Integration

When `OrchardCore.Search` is enabled alongside Azure AI Search:

1. Navigate to **Settings** > **Search** > **Site Search** in the admin dashboard.
2. Select the Azure AI Search index as the default search provider.
3. The `/search` route becomes available for frontend search.
4. Configure search permissions per role to control which indexes are queryable by anonymous or authenticated users.

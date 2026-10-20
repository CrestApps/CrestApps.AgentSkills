---
name: orchardcore-azure-ai-search
description: Skill for configuring Azure AI Search in Orchard Core. Covers Azure AI Search setup, the OrchardCore Search AzureAISearch configuration section (formerly OrchardCore_AzureAISearch), AzureAISearchDefaultOptions read through IOptionsMonitor, index creation and management, recipe steps for index operations, search module integration, custom data indexing, vector fields and vector or hybrid queries with AzureAIClientFactory, permissions including ManageAzureAISearchSettings, and frontend search configuration. Use this skill when requests mention Orchard Core Azure AI Search, Configure Azure AI Search, Enabling Azure AI Search Features, Azure AI Search Connection Configuration, Creating an Azure AI Search Index Profile, Legacy Index Creation (Obsolete), or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.AzureAI, AzureAISearchIndexMetadata, AzureAISearchIndexMap, VectorSearchMappings, OrchardCore.Search, OrchardCore.Indexing.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Azure AI Search - Prompt Templates

## Configure Azure AI Search

You are an Orchard Core expert. Generate code and configuration for Azure AI Search integration in Orchard Core.

### Guidelines

- Enable `OrchardCore.AzureAI` to manage Azure AI Search indices (`OrchardCore.Search.AzureAI` is an obsolete feature id kept for compatibility).
- Configure the Azure AI Search connection in `appsettings.json` under `OrchardCore:Search:AzureAISearch` (Orchard Core 4.0; `OrchardCore_AzureAISearch` is deprecated but still read) or through the admin UI at **Settings > Search > Azure AI Search**.
- Read the effective connection settings with `IOptionsMonitor<AzureAISearchDefaultOptions>.CurrentValue`. Admin settings changes refresh the monitor without a shell release, so a cached `IOptions<AzureAISearchDefaultOptions>.Value` goes stale.
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
        "OrchardCore.AzureAI",
        "OrchardCore.Indexing"
      ],
      "disable": []
    }
  ]
}
```

### Azure AI Search Connection Configuration

Configure in `appsettings.json` (for all tenants, or per tenant through any configuration source):

```json
{
  "OrchardCore": {
    "Search": {
      "AzureAISearch": {
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
}
```

Environment variable example: `OrchardCore__Search__AzureAISearch__Credential__Key`.

| Property | Description |
|----------|-------------|
| `Endpoint` | The Azure AI Search service endpoint URL. |
| `IndexesPrefix` | Prefix for all index names. Use environment names (e.g., `"staging-"`) to prevent conflicts when sharing a single Azure AI Search instance. |
| `AuthenticationType` | `"ApiKey"` for key-based, `"Default"` for default Azure credentials, or `"ManagedIdentity"` for managed identity. |
| `IdentityClientId` | Optional client ID for user-assigned managed identity. Omit for system-assigned identity. |
| `DisableUIConfiguration` | When `true`, disables per-tenant UI configuration and forces all tenants to use appsettings values. |
| `Credential.Key` | The API key for the Azure AI Search service. Required for `ApiKey` authentication. |

### Reading the Connection Options in Code

```csharp
using Microsoft.Extensions.Options;
using OrchardCore.AzureAI.Models;

public sealed class AzureSearchStatusService
{
    private readonly IOptionsMonitor<AzureAISearchDefaultOptions> _options;

    public AzureSearchStatusService(IOptionsMonitor<AzureAISearchDefaultOptions> options)
    {
        _options = options;
    }

    // Always read CurrentValue: admin settings changes update it after the session commits.
    public bool IsConfigured()
        => _options.CurrentValue.ConfigurationExists();

    public string GetIndexesPrefix()
        => _options.CurrentValue.IndexesPrefix;
}
```

`AzureAIClientFactory` (namespace `OrchardCore.AzureAI.Services`) also reads `IOptionsMonitor<AzureAISearchDefaultOptions>` and recreates its clients when the endpoint or credentials change. Use `CreateSearchClient(indexFullName)` and `CreateSearchIndexClient()` instead of building SDK clients yourself.

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
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace MyModule;

[Feature("MyModule.CustomAzureAIIndex")]
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> localizer)
    {
        S = localizer;
    }

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

For custom index field mapping, implement `IIndexProfileHandler` and populate `AzureAISearchIndexMetadata` with `IndexMappings` in the `CreatingAsync` and `UpdatingAsync` methods. Use `IndexProfileHandlerBase` to simplify the implementation, and register it with `services.AddIndexProfileHandler<THandler>()`.

To capture custom UI data related to your source, implement `DisplayDriver<IndexProfile>`.

### Indexing Vector Fields

If a custom source emits embeddings, add a vector field to `AzureAISearchIndexMetadata.IndexMappings` and define the vector search profile in `AzureAISearchIndexMetadata.VectorSearchMappings`:

```csharp
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using static OrchardCore.Indexing.DocumentIndex;

public sealed class ProductIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
    {
        Configure(context.Model);
        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
    {
        Configure(context.Model);
        return Task.CompletedTask;
    }

    private static void Configure(IndexProfile index)
    {
        var metadata = index.GetOrCreate<AzureAISearchIndexMetadata>();

        metadata.IndexMappings.Clear();

        metadata.IndexMappings.Add(new AzureAISearchIndexMap("documentId", Types.Text)
        {
            IndexingKey = "DocumentId",
            IsKey = true,
            IsFilterable = true,
            IsSortable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap("title", Types.Text)
        {
            IndexingKey = "Title",
            IsSearchable = true,
        });

        metadata.IndexMappings.Add(new AzureAISearchIndexMap("embedding", Types.Vector)
        {
            AzureFieldKey = "Embedding",
            IsSearchable = true,
            VectorInfo = new AzureAISearchIndexMapVectorInfo
            {
                Dimensions = 1536,
                VectorSearchConfiguration = "products-vector",
            },
        });

        metadata.VectorSearchMappings = new VectorSearchMappings
        {
            Profiles =
            [
                new VectorSearchProfileMap { Name = "default", AlgorithmConfigurationName = "products-vector" },
            ],
            Algorithms =
            [
                new VectorSearchAlgorithmMap { Name = "products-vector", Kind = VectorSearchAlgorithmMap.HnswKind },
            ],
        };

        index.Put(metadata);
    }
}
```

Write each embedding with `documentIndex.Set("embedding", embedding, embedding.Length, DocumentIndexOptions.Store)`. Pass `new Dictionary<string, object> { ["VectorSearchConfiguration"] = "products-vector" }` as a fifth argument to choose a profile per field; without it, the first configured profile (or the built-in default) is used.

### Querying Vector Fields

`ISearchService` and the Site Search UI run full-text search only. For vector or hybrid queries, use the SDK through `AzureAIClientFactory`:

```csharp
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Services;
using OrchardCore.Indexing;

public sealed class ProductVectorSearchService
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly IIndexProfileStore _indexProfileStore;

    public ProductVectorSearchService(AzureAIClientFactory clientFactory, IIndexProfileStore indexProfileStore)
    {
        _clientFactory = clientFactory;
        _indexProfileStore = indexProfileStore;
    }

    public async Task<IReadOnlyList<SearchDocument>> SearchAsync(float[] embedding, CancellationToken cancellationToken)
    {
        var index = await _indexProfileStore.FindByIndexNameAndProviderAsync("products", AzureAISearchConstants.ProviderName)
            ?? throw new InvalidOperationException("The 'products' Azure AI Search index profile doesn't exist.");

        var client = _clientFactory.CreateSearchClient(index.IndexFullName);

        var options = new SearchOptions
        {
            Size = 5,
            Select = { "documentId", "title" },
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(embedding) { Fields = { "embedding" }, KNearestNeighborsCount = 5 },
                },
            },
        };

        // Replace "*" with a text query for hybrid search.
        var response = await client.SearchAsync<SearchDocument>("*", options, cancellationToken);
        var documents = new List<SearchDocument>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            documents.Add(result.Document);
        }

        return documents;
    }
}
```

### Frontend Search Integration

When `OrchardCore.Search` is enabled alongside Azure AI Search:

1. Navigate to **Settings** > **Search** > **Site Search** in the admin dashboard.
2. Select the Azure AI Search index as the default search provider.
3. The `/search` route becomes available for frontend search.
4. Configure search permissions per role to control which indexes are queryable by anonymous or authenticated users.

### Permissions

| Permission | Member | Notes |
|------------|--------|-------|
| `ManageAzureAISearchSettings` | `OrchardCore.AzureAI.AzureAISearchPermissions.ManageAzureAISearchSettings` | Edit the **Azure AI Search** settings and the per-field index settings. |
| `ManageIndexes` | `OrchardCore.Indexing.Core.IndexingPermissions.ManageIndexes` | Manage index profiles under **Search > Indexes**. |
| `QuerySearchIndex` / `QueryIndex_{ProfileName}` | `IndexingPermissions.QuerySearchIndex` / `IndexingPermissions.CreateDynamicPermission(indexProfile)` | Front-end search on any index or on one index. |

### Upgrading from 3.x

- Rename the `OrchardCore_AzureAISearch` section to `OrchardCore:Search:AzureAISearch` (the old name is still read; the new one wins when both exist).
- Replace `IOptions<AzureAISearchDefaultOptions>` with `IOptionsMonitor<AzureAISearchDefaultOptions>` and read `CurrentValue`; settings changes no longer release the shell.
- The 3.x permission `AzureAISearchPermissions.ManageAzureAISearchISettings` (stored name `ManageAzureAISearchISettings`) is now `ManageAzureAISearchSettings`, for both the member and the stored name. Re-grant **Manage Azure AI Search Settings** to non-administrator roles that had it.
- `OrchardCore.Search.Permissions.QuerySearchIndex` was removed; use `IndexingPermissions.QuerySearchIndex`.

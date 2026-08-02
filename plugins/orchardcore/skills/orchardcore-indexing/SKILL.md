---
name: orchardcore-indexing
description: Skill for understanding the Orchard Core indexing infrastructure. Covers the indexing abstraction layer, content item indexing pipeline, custom indexing sources, recipe steps for index management, index reset and rebuild operations, and indexing UI configuration. Use this skill when requests mention Orchard Core Indexing Infrastructure, Configure Indexing Infrastructure, Enabling the Indexing Module, Index Profile Architecture, Creating an Index Profile via Recipe, Content Index Metadata Properties, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Indexing, OrchardCore.Modules, CreateOrUpdateIndexProfile, ResetIndex, RebuildIndex, ArticlesIndex, BlogPostsIndex, IIndexManager. It also helps with Creating an Index Profile via Recipe, Content Index Metadata Properties, Reset Index Step, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Indexing Infrastructure - Prompt Templates

## Configure Indexing Infrastructure

You are an Orchard Core expert. Generate code and configuration for the Orchard Core indexing infrastructure, including custom indexing sources and index lifecycle management.

### Guidelines

- Enable `OrchardCore.Indexing` as the core indexing module that provides the abstraction layer for all index providers.
- The indexing module is provider-agnostic and supports Lucene, Elasticsearch, and Azure AI Search.
- The module maintains an append-only log of indexing tasks (Update or Delete) using a cursor-based interface.
- Content item indexing is a consumer of the core indexing infrastructure, not a hard requirement.
- Custom data sources (REST APIs, databases, in-memory structures) can be indexed alongside content items.
- Use the `CreateOrUpdateIndexProfile` recipe step to create or update index profiles.
- Use the `ResetIndex` recipe step to restart indexing without deleting existing entries.
- Use the `RebuildIndex` recipe step to delete and recreate the full index.
- Reset and rebuild operations run asynchronously in the background.
- The admin UI is available under Search > Indexes for index lifecycle management.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling the Indexing Module

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Indexing"
      ],
      "disable": []
    }
  ]
}
```

### Index Profile Architecture

The indexing module uses an append-only task log where each entry represents either an **Update** or a **Deletion**. Consumers track changes independently using their own cursor positions. This enables:

- Processing changes at individual pace.
- Implementing custom search pipelines.
- Integrating with analytics, auditing, or event-driven workflows.
- Loose coupling between the indexing infrastructure and data consumers.

### Creating an Index Profile via Recipe

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Id": "unique-profile-id",
          "Name": "ArticlesIndex",
          "IndexName": "articles",
          "ProviderName": "Lucene",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": ["Article", "BlogPost"],
              "Culture": "any"
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
| `Id` | Optional unique identifier for the index profile. |
| `Name` | Unique display name for the profile. |
| `IndexName` | Physical index name used by the provider. |
| `ProviderName` | The search provider: `"Lucene"`, `"Elasticsearch"`, or `"AzureAISearch"`. |
| `Type` | Source category. Use `"Content"` for content items or a custom category name. |
| `Properties` | Provider-specific and source-specific metadata. |

### Content Index Metadata Properties

| Property | Description |
|----------|-------------|
| `IndexLatest` | When `true`, indexes both published and draft content items. Default `false`. |
| `IndexedContentTypes` | Array of content type technical names to include in the index. |
| `Culture` | `"any"` to index all cultures, or a specific culture code (e.g., `"en-US"`). |

### Reset Index Step

Restarts the indexing process from the beginning. Existing entries are preserved; new or updated items are added. The operation runs asynchronously in the background.

```json
{
  "steps": [
    {
      "name": "ResetIndex",
      "indexNames": [
        "ArticlesIndex",
        "BlogPostsIndex"
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

### Rebuild Index Step

Deletes the existing index and rebuilds from scratch. The operation runs asynchronously in the background.

```json
{
  "steps": [
    {
      "name": "RebuildIndex",
      "indexNames": [
        "ArticlesIndex",
        "BlogPostsIndex"
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

### Implementing Custom Indexing Sources

To index data beyond content items, implement three interfaces and register the source.

**1. Implement `IIndexManager`** — Creates, rebuilds, and deletes provider indexes:

```csharp
using OrchardCore.Indexing;

namespace MyModule;

public sealed class ProductIndexManager : IIndexManager
{
    public Task<bool> CreateAsync(IndexProfile indexProfile)
        => throw new NotImplementedException();

    public Task<bool> RebuildAsync(IndexProfile indexProfile)
        => throw new NotImplementedException();

    public Task<bool> DeleteAsync(IndexProfile indexProfile)
        => throw new NotImplementedException();

    public Task<bool> ExistsAsync(string indexFullName)
        => throw new NotImplementedException();
}
```

**2. Implement `IDocumentIndexManager`** — Writes documents and tracks the task cursor:

```csharp
using OrchardCore.Indexing;

namespace MyModule;

public sealed class ProductDocumentIndexManager : IDocumentIndexManager
{
    public Task<bool> AddOrUpdateDocumentsAsync(IndexProfile indexProfile, IEnumerable<DocumentIndex> documents)
        => throw new NotImplementedException();

    public Task<bool> DeleteDocumentsAsync(IndexProfile indexProfile, IEnumerable<string> documentIds)
        => throw new NotImplementedException();

    public Task<bool> DeleteAllDocumentsAsync(IndexProfile indexProfile)
        => throw new NotImplementedException();

    public Task<long> GetLastTaskIdAsync(IndexProfile indexProfile)
        => throw new NotImplementedException();

    public Task SetLastTaskIdAsync(IndexProfile indexProfile, long lastTaskId)
        => throw new NotImplementedException();

    public IContentIndexSettings GetContentIndexSettings()
        => throw new NotImplementedException();
}
```

**3. Implement `IIndexNameProvider`** — Provides physical names for profiles:

```csharp
using OrchardCore.Indexing;

namespace MyModule;

public sealed class ProductIndexNameProvider : IIndexNameProvider
{
    public string GetFullIndexName(string name) => name;
}
```

**4. Register the custom source in `Startup.cs`:**

```csharp
using OrchardCore.Modules;
using Microsoft.Extensions.Localization;

namespace MyModule;

[Feature("MyModule.ProductIndex")]
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexingSource<ProductIndexManager, ProductDocumentIndexManager, ProductIndexNameProvider>(
            "Lucene",
            "Products",
            o =>
            {
                o.DisplayName = S["Product Index"];
                o.Description = S["Creates a search index for product data."];
            });
    }
}
```

### Custom UI Integration

To provide a configuration screen for a custom indexing source:

- **Display driver**: Inherit from `DisplayDriver<IndexProfile>` to render configuration UI.
- **Profile handler**: Implement `IIndexProfileHandler` (or inherit `IndexProfileHandlerBase`) to react to lifecycle events like creation, update, or deletion.

### Indexing UI

The admin dashboard under **Search** > **Indexes** provides:

- Create and configure index profiles.
- Reset or rebuild existing indexes.
- View provider-specific options.
- Configure which data types to index (e.g., content types).

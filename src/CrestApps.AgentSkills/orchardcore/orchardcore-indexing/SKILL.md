---
name: orchardcore-indexing
description: Skill for the Orchard Core indexing infrastructure. Covers how content items reach an index (immediate deferred indexing and the task log processed by the Indexing Worker), content part, field and document index handler contracts, DocumentIndex value types and DocumentIndexOptions, IIndexProfileManager for managing index profiles from code, custom indexing sources and provider keyed services, recipe and deployment steps for index management, obsolete provider-specific steps, reset and rebuild, IndexingPermissions and dynamic per-index permissions, and the indexing UI. Use this skill when requests mention Orchard Core Indexing, index profiles, Content Index Metadata, index handlers, or related troubleshooting. Strong matches include OrchardCore.Indexing, OrchardCore.Indexing.Worker, CreateOrUpdateIndexProfile, ResetIndex, RebuildIndex, IIndexManager, IDocumentIndexManager, IIndexNameProvider, IIndexProfileManager, ContentPartIndexHandler, ContentFieldIndexHandler, IndexingPermissions.
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
- Enable `OrchardCore.Indexing.Worker` (depends on `OrchardCore.Contents`) for the background task that keeps indexes in sync with content item changes.
- Reference permissions from `OrchardCore.Indexing.Core.IndexingPermissions`: `ManageIndexes`, `QuerySearchIndex` and the per-profile `CreateDynamicPermission(indexProfile)` (`QueryIndex_{ProfileName}`).
- Indexing sources are not the data sources of data pipelines (`IDataSource`, see `orchardcore-data-sources`).
- Index profiles (full-text documents in Lucene, Elasticsearch or Azure AI Search) are not YesSql `MapIndex` tables (relational lookups created in migrations, see `orchardcore-yessql-indexes`) and not saved queries (see `orchardcore-content-queries`).
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

### How Content Gets Indexed

With `OrchardCore.Contents` enabled, content changes reach index profiles of type `Content` by two paths.

1. Immediate. `IndexingContentHandler` collects items created, updated, published, unpublished, saved as draft or removed during the request (preview requests are ignored) and, in a `ShellScope` deferred task after the request, rebuilds their documents in every `Content` profile whose `IndexedContentTypes`, `Culture` and `IndexLatest` match.
2. Task log. `CreateIndexingTaskContentHandler` also appends an `Update` or `Delete` task with the `Content` category (`IndexingConstants.ContentsIndexSource`). With `OrchardCore.Indexing.Worker` enabled, `ContentIndexingBackgroundTask` ("Indexes Content Updater", every minute) calls `ContentIndexingService.ProcessRecordsForAllIndexesAsync()`, which reads tasks after each profile's last task id in batches of 100 under a distributed lock and catches up anything missed (other nodes, failures, resets).

Both paths build a `ContentItemDocumentIndex`, run every `IDocumentIndexHandler` (the content coordinator in turn calls each `IContentPartIndexHandler` and `IContentFieldIndexHandler`), and write through the provider's `IDocumentIndexManager` (`AddOrUpdateDocumentsAsync` is an upsert, `DeleteDocumentsAsync` removes). The worker then advances the cursor with `SetLastTaskIdAsync`. A profile whose physical index does not exist is skipped with a warning; rebuild it.

### Index Handler Contracts

Part and field handlers live in `OrchardCore.Indexing.Abstractions` (namespace `OrchardCore.Indexing`) and are registered as scoped services. `orchardcore-search-indexing` has full part and document handler examples.

```csharp
using OrchardCore.ContentFields.Fields;
using OrchardCore.Indexing;

public sealed class {{FieldName}}IndexHandler : ContentFieldIndexHandler<{{FieldName}}>
{
    public override Task BuildIndexAsync({{FieldName}} field, BuildFieldIndexContext context)
    {
        // Honors the per-field index settings (stored, keyword, sanitized).
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, field.Text, options);
        }

        return Task.CompletedTask;
    }
}

// services.AddScoped<IContentFieldIndexHandler, {{FieldName}}IndexHandler>();
// services.AddScoped<IContentPartIndexHandler, {{PartName}}IndexHandler>();
// services.AddScoped<IDocumentIndexHandler, {{Name}}DocumentIndexHandler>();
```

- `context.Keys` holds every index field name to write; always loop it. For a part the keys are `{PartName}` and `{ContentType}.{PartName}`; field keys append `.{FieldName}`.
- `ContentPartIndexHandler<TPart>` only runs for parts of type `TPart`; `ContentFieldIndexHandler<TField>` only for fields whose definition is `TField`.
- `BuildPartIndexContext` adds `ContentTypePartDefinition`; `BuildFieldIndexContext` adds `ContentPart` and `ContentPartFieldDefinition`. The source record is `context.Record` and `context.Settings` is the provider's `IContentIndexSettings`.
- `IDocumentIndexHandler.BuildIndexAsync(BuildDocumentIndexContext)` runs once per document; check `context.Record` type first. Its default-implemented `DocumentsAddedOrUpdatedAsync` and `DocumentsDeletedAsync` hooks run after the provider writes.

`DocumentIndex.Set(name, value, options)` overloads map to `DocumentIndex.Types`:

| Value | Type |
|-------|------|
| `string`, `IHtmlContent` | `Text` |
| `DateTimeOffset?` | `DateTime` |
| `int?` | `Integer` |
| `bool?` | `Boolean` |
| `double?`, `decimal?` | `Number` |
| `DocumentIndex.GeoPoint` | `GeoPoint` |
| `float[]` with dimensions | `Vector` |
| `object` (optional metadata) | `Complex` |

`DocumentIndexOptions` is a flags enum: `None` (analyzed text), `Store` (retrievable from the index), `Sanitize` (strip HTML before indexing), `Keyword` (not analyzed, exact match).

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

An indexing source is a provider name plus a source category (the profile `Type`). The provider's `IIndexManager`, `IDocumentIndexManager` and `IIndexNameProvider` are registered as keyed services under the provider name with `TryAdd`, so the first registration for a provider name wins.

- To index custom records into an existing provider, keep its managers and register only a new category with the provider wrapper, for example `services.AddLuceneIndexingSource("Products", ...)`, then feed the category's task log (see `orchardcore-indexing-sources`).
- Implement the three interfaces below only for a new provider name (a new search engine).

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
            "Contoso",
            "Products",
            o =>
            {
                o.DisplayName = S["Products in Contoso"];
                o.Description = S["Creates a search index for product data."];
            });
    }
}
```

### Managing Index Profiles from Code

Use `IIndexProfileManager` (namespace `OrchardCore.Indexing`) instead of writing profiles directly; it runs the `IIndexProfileHandler` pipeline and validation. Creating the profile does not create the physical index; do what the `CreateOrUpdateIndexProfile` step does and call the provider's keyed `IIndexManager`.

```csharp
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;

public sealed class ArticleIndexSetup
{
    private readonly IIndexProfileManager _indexProfileManager;
    private readonly IServiceProvider _serviceProvider;

    public ArticleIndexSetup(IIndexProfileManager indexProfileManager, IServiceProvider serviceProvider)
    {
        _indexProfileManager = indexProfileManager;
        _serviceProvider = serviceProvider;
    }

    public async Task EnsureAsync()
    {
        if (await _indexProfileManager.FindByNameAsync("ArticlesIndex") is not null)
        {
            return;
        }

        // The data shape matches one entry of the CreateOrUpdateIndexProfile step.
        var profile = await _indexProfileManager.NewAsync("Lucene", "Content", new JsonObject
        {
            ["Name"] = "ArticlesIndex",
            ["IndexName"] = "articles",
            ["Properties"] = new JsonObject
            {
                ["ContentIndexMetadata"] = new JsonObject
                {
                    ["IndexedContentTypes"] = new JsonArray("Article"),
                    ["Culture"] = "any",
                },
            },
        });

        var validation = await _indexProfileManager.ValidateAsync(profile);

        if (!validation.Succeeded)
        {
            return;
        }

        await _indexProfileManager.CreateAsync(profile);

        var indexManager = _serviceProvider.GetRequiredKeyedService<IIndexManager>(profile.ProviderName);

        if (!await indexManager.CreateAsync(profile))
        {
            await _indexProfileManager.DeleteAsync(profile);

            return;
        }

        // Indexes the existing records after the request ends.
        await _indexProfileManager.SynchronizeAsync(profile);
    }
}
```

| Member | Purpose |
|--------|---------|
| `NewAsync(providerName, type, data)` | Build a profile for a registered provider and source category. |
| `ValidateAsync` / `CreateAsync` / `UpdateAsync(profile, data)` / `DeleteAsync` | Validate and persist the profile. |
| `FindByIdAsync`, `FindByNameAsync`, `FindByNameAndProviderAsync`, `GetAllAsync`, `GetByProviderAsync`, `GetAsync(provider, type)`, `PageAsync` | Look up profiles. |
| `SynchronizeAsync` | Run the profile handlers' synchronization (indexing the source records) in a background job after the request. |
| `ResetAsync` | Run the profile handlers' reset so the profile is re-indexed from the start. |

### Custom UI Integration

To provide a configuration screen for a custom indexing source:

- **Display driver**: Inherit from `DisplayDriver<IndexProfile>` to render configuration UI.
- **Profile handler**: Implement `IIndexProfileHandler` (or inherit `IndexProfileHandlerBase`) to react to lifecycle events like creation, update, or deletion.

### Deployment Steps

The **Indexing** deployment category provides **Index Profiles** (exports `CreateOrUpdateIndexProfile`), **Reset Indexes** (`ResetIndex`) and **Rebuild Indexes** (`RebuildIndex`). Each takes `IncludeAll` or a list of `IndexNames`.

### Permissions

| Permission | Member | Notes |
|------------|--------|-------|
| `ManageIndexes` | `IndexingPermissions.ManageIndexes` | Manage index profiles. Implies provider manage permissions such as `ManageElasticIndexes`. |
| `QuerySearchIndex` | `IndexingPermissions.QuerySearchIndex` | Query any index. |
| `QueryIndex_{ProfileName}` | `IndexingPermissions.CreateDynamicPermission(indexProfile)` | Query one index; implied by `ManageIndexes` and `QuerySearchIndex`. |

Authorize a query against a specific profile with `authorizationService.AuthorizeAsync(user, IndexingPermissions.QuerySearchIndex, indexProfile)`; the indexing authorization handler resolves the dynamic permission.

### Upgrading from 3.x

- The provider-specific steps `lucene-index`, `lucene-index-reset`, `lucene-index-rebuild`, `ElasticIndexSettings` and `azureai-index-create` still run but are obsolete; use `CreateOrUpdateIndexProfile`, `ResetIndex` and `RebuildIndex`.
- `OrchardCore.Search.Permissions.QuerySearchIndex` was removed; use `OrchardCore.Indexing.Core.IndexingPermissions.QuerySearchIndex`.
- `ElasticsearchIndexPermissionHelper.GetElasticIndexPermission(string)` was removed; use `IndexingPermissions.CreateDynamicPermission(IndexProfile)`.
- `DeploymentStep.Category` and `Title` are `LocalizationSource` values; custom indexing deployment steps assign them in the parameterless constructor, for example `Category = LocalizationSource.Create<MyIndexDeploymentStep>("Indexing")`.

### Indexing UI

The admin dashboard under **Search** > **Indexes** provides:

- Create and configure index profiles.
- Reset or rebuild existing indexes.
- View provider-specific options.
- Configure which data types to index (e.g., content types).

---
name: orchardcore-search-indexing
description: Skill for configuring search and indexing in Orchard Core. Covers unified IndexProfile definitions, Lucene, Elasticsearch, and Azure AI Search providers, provider configuration sections under OrchardCore Search, document index handlers, index lifecycle recipes, IndexingPermissions and SearchPermissions, keyed ISearchService resolution, the GET-only search page, and search queries. Use this skill when requests mention Orchard Core Search and Indexing, Configure Search and Indexing, Enabling Search Features, Index Profile Recipe, Elasticsearch Configuration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Lucene, OrchardCore.Elasticsearch, OrchardCore.AzureAI, OrchardCore.Search, OrchardCore.Indexing, IIndexProfileManager, ISearchService, IDocumentIndexHandler, BuildDocumentIndexContext, ContentIndexingConstants, IndexingPermissions, and custom content-part indexing.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Search & Indexing - Prompt Templates

## Configure Search and Indexing

You are an Orchard Core expert. Generate search and indexing configurations for Orchard Core.

### Guidelines

- Orchard Core uses a unified `IndexProfile` for Lucene, Elasticsearch, and Azure AI Search indexes.
- Enable the required provider feature such as `OrchardCore.Lucene`, `OrchardCore.Elasticsearch`, or `OrchardCore.AzureAI`.
- Lucene indexes are stored on the local file system.
- Elasticsearch requires an external Elasticsearch cluster.
- Create indexes with the provider-agnostic `CreateOrUpdateIndexProfile` step.
- Use queries to search indexed content programmatically or via Liquid.
- Content indexing is triggered automatically when content is published or updated.
- Rebuild indexes after changing index definitions.
- Use `IDocumentIndexHandler` for document-wide indexing and read the source
  record from `BuildDocumentIndexContext.Record`.
- `DocumentIndex` supports text, numeric, date, boolean, geo-point, `Complex`,
  and `Vector` entries. Use the matching `Set` overload for the value type.
- Use the centralized `IndexingPermissions.QuerySearchIndex` permission
  (`OrchardCore.Indexing.Core`). A profile-specific `QueryIndex_{name}`
  permission is created by `IndexingPermissions.CreateDynamicPermission`.
- Provider connection settings live under `OrchardCore:Search:Elasticsearch` and
  `OrchardCore:Search:AzureAISearch` since Orchard Core 4.0 (the
  `OrchardCore_*` names are deprecated).
- `ISearchService` implementations are keyed by provider name; resolve one with
  `GetKeyedService<ISearchService>(indexProfile.ProviderName)`.

### Enabling Search Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Search",
        "OrchardCore.Lucene",
        "OrchardCore.Indexing"
      ],
      "disable": []
    }
  ]
}
```

### Lucene Index Profile via Recipe

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "Search",
          "IndexName": "search",
          "ProviderName": "Lucene",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": [
                "Article",
                "BlogPost"
              ],
              "Culture": "any"
            },
            "LuceneIndexMetadata": {
              "AnalyzerName": "standardanalyzer",
              "StoreSourceData": false
            }
          }
        }
      ]
    }
  ]
}
```

### Elasticsearch Configuration

Configure in `appsettings.json`:

```json
{
  "OrchardCore": {
    "Search": {
      "Elasticsearch": {
        "Url": "https://localhost",
        "Ports": [9200],
        "ConnectionType": "SingleNodeConnectionPool"
      }
    }
  }
}
```

See `orchardcore-elasticsearch` and `orchardcore-azure-ai-search` for the full provider settings.

### Elasticsearch Index Profile via Recipe

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "Search",
          "IndexName": "search",
          "ProviderName": "Elasticsearch",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": [
                "Article",
                "BlogPost"
              ],
              "Culture": "any"
            },
            "ElasticsearchIndexMetadata": {
              "AnalyzerName": "standard",
              "StoreSourceData": false
            }
          }
        }
      ]
    }
  ]
}
```

### Lucene Queries via Recipe

```json
{
  "steps": [
    {
      "name": "Queries",
      "Queries": [
        {
          "Source": "Lucene",
          "Name": "RecentBlogPosts",
          "Index": "Search",
          "Template": "{\"query\":{\"bool\":{\"filter\":[{\"term\":{\"Content.ContentItem.ContentType\":\"BlogPost\"}}]}},\"sort\":{\"Content.ContentItem.CreatedUtc\":{\"order\":\"desc\"}},\"size\":10}",
          "ReturnContentItems": true,
          "Schema": "[]"
        }
      ]
    }
  ]
}
```

### Using Search in Liquid

```liquid
{% assign results = Queries.RecentBlogPosts | query %}
{% for item in results %}
    <article>
        <h2>{{ item.DisplayText }}</h2>
        <p>{{ item.Content.BlogPost.Subtitle.Text }}</p>
    </article>
{% endfor %}
```

### Programmatic Search Queries

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Search.Abstractions;

public sealed class SiteSearchService
{
    private readonly IIndexProfileManager _indexProfileManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthorizationService _authorizationService;

    public SiteSearchService(
        IIndexProfileManager indexProfileManager,
        IServiceProvider serviceProvider,
        IAuthorizationService authorizationService)
    {
        _indexProfileManager = indexProfileManager;
        _serviceProvider = serviceProvider;
        _authorizationService = authorizationService;
    }

    public async Task<IList<string>> SearchAsync(ClaimsPrincipal user, string query)
    {
        var indexProfile = await _indexProfileManager.FindByNameAsync("Search")
            ?? throw new InvalidOperationException("The Search index profile does not exist.");

        // Resolves the profile-specific QueryIndex_{name} permission.
        if (!await _authorizationService.AuthorizeAsync(user, IndexingPermissions.QuerySearchIndex, indexProfile))
        {
            return [];
        }

        // Search services are registered with AddSearchService<T>(providerName) as keyed services.
        var searchService = _serviceProvider.GetKeyedService<ISearchService>(indexProfile.ProviderName)
            ?? throw new InvalidOperationException($"No search service for '{indexProfile.ProviderName}'.");

        var result = await searchService.SearchAsync(indexProfile, query, 0, 10);

        return result.ContentItemIds;
    }
}
```

The front-end search page is `GET /search/{index?}?terms=...` (GET-only since Orchard Core 4.0); `index` is the index profile name, otherwise the default index of the search settings is used.

### Custom Document Indexing

Implement `IDocumentIndexHandler` when indexing requires the complete source
record. `BuildDocumentIndexContext.Record` is an `object`, so check its type
before adding entries. The `Complex` and `Vector` overloads preserve structured
values and vector dimensions for providers that support them.

A handler only shapes records that already reach the indexing pipeline. To
index records that are not content items (task log entries, a
`NamedIndexingService` subclass, a background task, and a profile handler for
sync and reset), follow `orchardcore-indexing-sources`.

```csharp
using System.Collections.Generic;
using OrchardCore.Indexing;

public sealed class ProductDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ProductRecord record)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(
            "Product.Name",
            record.Name,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            "Product.Attributes",
            record.Attributes,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            "Product.Embedding",
            record.Embedding,
            record.Embedding.Length,
            DocumentIndexOptions.None);

        return Task.CompletedTask;
    }
}

public sealed class ProductRecord
{
    public string Name { get; init; } = string.Empty;
    public Dictionary<string, object> Attributes { get; init; } = [];
    public float[] Embedding { get; init; } = [];
}
```

Register the handler as a scoped service:

```csharp
services.AddScoped<IDocumentIndexHandler, ProductDocumentIndexHandler>();
```

Use `ContentIndexingConstants` from `OrchardCore.Contents.Indexing` for
built-in content index keys such as `ContentTypeKey`, `ContentItemIdKey`, and
`FullTextKey`.

### Indexing Permissions

`IndexingPermissions.QuerySearchIndex` is the centralized permission for
querying an index. The indexing authorization handler resolves the
profile-specific dynamic permission created by
`IndexingPermissions.CreateDynamicPermission(indexProfile)`, whose name is
`QueryIndex_{indexProfile.Name}` and which is implied by
`IndexingPermissions.ManageIndexes` and `IndexingPermissions.QuerySearchIndex`.

| Permission | Member |
|------------|--------|
| Query any index | `OrchardCore.Indexing.Core.IndexingPermissions.QuerySearchIndex` |
| Manage indexes | `OrchardCore.Indexing.Core.IndexingPermissions.ManageIndexes` |
| Query one index | `IndexingPermissions.CreateDynamicPermission(indexProfile)` |
| Manage search settings | `OrchardCore.Search.SearchPermissions.ManageSearchSettings` |

### Upgrading from 3.x

- `OrchardCore.Search.Permissions.QuerySearchIndex` was removed; use `OrchardCore.Indexing.Core.IndexingPermissions.QuerySearchIndex`.
- `OrchardCore.Search.Permissions.ManageSearchSettings` was removed; use `OrchardCore.Search.SearchPermissions.ManageSearchSettings`.
- `ElasticsearchIndexPermissionHelper.GetElasticIndexPermission(string)` was removed; use `IndexingPermissions.CreateDynamicPermission(IndexProfile)`.
- Rename `OrchardCore_Elasticsearch` to `OrchardCore:Search:Elasticsearch` and `OrchardCore_AzureAISearch` to `OrchardCore:Search:AzureAISearch`.
- The search action only answers `GET`; forms posting to `/search` must use `method="get"` (the built-in `Search Form` already does).

### Custom Content Part Indexing

```csharp
using OrchardCore.Indexing;

public sealed class MyPartIndexHandler : ContentPartIndexHandler<MyPart>
{
    public override Task BuildIndexAsync(
        MyPart part,
        BuildPartIndexContext context)
    {
        var options = DocumentIndexOptions.Store;

        context.DocumentIndex.Set(
            $"{nameof(MyPart)}.{nameof(MyPart.MyField)}",
            part.MyField,
            options);

        return Task.CompletedTask;
    }
}
```

### Registering Index Handler

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentPartIndexHandler, MyPartIndexHandler>();
    }
}
```

### Search Settings via Recipe

```json
{
  "steps": [
    {
      "name": "Settings",
      "SearchSettings": {
        "DefaultIndexProfileName": "Search",
        "Placeholder": "Search...",
        "PageTitle": "Search"
      }
    }
  ]
}
```

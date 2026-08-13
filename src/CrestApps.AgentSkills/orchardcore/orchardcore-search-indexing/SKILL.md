---
name: orchardcore-search-indexing
description: Skill for configuring search and indexing in Orchard Core. Covers unified IndexProfile definitions, Lucene, Elasticsearch, and Azure AI Search providers, document index handlers, index lifecycle recipes, permissions, and search queries. Use this skill when requests mention Orchard Core Search and Indexing, Configure Search and Indexing, Enabling Search Features, Index Profile Recipe, Elasticsearch Configuration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Lucene, OrchardCore.Elasticsearch, OrchardCore.AzureAI, OrchardCore.Search, OrchardCore.Indexing, IIndexProfileManager, ISearchService, IDocumentIndexHandler, BuildDocumentIndexContext, ContentIndexingConstants, IndexingPermissions, and custom content-part indexing. It also helps with Index Profile recipes, provider selection, Lucene Queries via Recipe, and the code patterns captured in this skill.
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
- Use the centralized `IndexingPermissions.QuerySearchIndex` permission. A
  profile-specific `QueryIndex_{name}` permission is created by
  `IndexingPermissions.CreateDynamicPermission`.

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
  "OrchardCore_Elasticsearch": {
    "Url": "https://localhost:9200",
    "Ports": [9200],
    "ConnectionType": "SingleNodeConnectionPool"
  }
}
```

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
using System.Collections.Generic;
using OrchardCore.Indexing;
using OrchardCore.Search.Abstractions;

public sealed class SearchService
{
    private readonly IIndexProfileManager _indexProfileManager;
    private readonly IEnumerable<ISearchService> _searchServices;

    public SearchService(
        IIndexProfileManager indexProfileManager,
        IEnumerable<ISearchService> searchServices)
    {
        _indexProfileManager = indexProfileManager;
        _searchServices = searchServices;
    }

    public async Task<IList<string>> SearchAsync(string query)
    {
        var indexProfile = await _indexProfileManager.FindByNameAsync("Search")
            ?? throw new InvalidOperationException("The Search index profile does not exist.");

        var searchService = _searchServices.Single(service =>
            string.Equals(service.Name, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase));

        var result = await searchService.SearchAsync(indexProfile, query, 0, 10);

        return result.ContentItemIds;
    }
}
```

### Custom Document Indexing

Implement `IDocumentIndexHandler` when indexing requires the complete source
record. `BuildDocumentIndexContext.Record` is an `object`, so check its type
before adding entries. The `Complex` and `Vector` overloads preserve structured
values and vector dimensions for providers that support them.

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

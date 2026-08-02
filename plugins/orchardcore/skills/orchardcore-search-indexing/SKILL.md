---
name: orchardcore-search-indexing
description: Skill for configuring search and indexing in Orchard Core. Covers unified IndexProfile definitions, Lucene, Elasticsearch, and Azure AI Search providers, search settings, index lifecycle recipes, and search queries. Use this skill when requests mention Orchard Core Search and Indexing, Configure Search and Indexing, Enabling Search Features, Index Profile Recipe, Elasticsearch Configuration, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Lucene, OrchardCore.Elasticsearch, OrchardCore.AzureAI, OrchardCore.Search, OrchardCore.Indexing, IIndexProfileManager, ISearchService, ContentPartIndexHandler, and IContentPartIndexHandler. It also helps with Index Profile recipes, provider selection, Lucene Queries via Recipe, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
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

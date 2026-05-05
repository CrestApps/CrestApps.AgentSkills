---
name: orchardcore-lucene
description: Skill for configuring Lucene search in Orchard Core. Covers Lucene index creation and management, recipe steps for index operations, Lucene Query API, query filters, Lucene Worker background tasks, custom data indexing, automatic field mapping, and Elasticsearch Query DSL support in Lucene. Use this skill when requests mention Orchard Core Lucene Search, Configure Lucene Search, Enabling Lucene Features, Creating a Lucene Index Profile (Recommended), Legacy Index Creation (Obsolete), Reset Lucene Index, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Lucene, OrchardCore.Lucene.Worker, OrchardCore.Search, OrchardCore.Indexing, OrchardCore.Modules, CreateOrUpdateIndexProfile, LuceneIndexSettings. It also helps with Legacy Index Creation (Obsolete), Reset Lucene Index, Rebuild Lucene Index, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
---

# Orchard Core Lucene Search - Prompt Templates

## Configure Lucene Search

You are an Orchard Core expert. Generate code and configuration for Lucene-based full-text search in Orchard Core.

### Guidelines

- Enable `OrchardCore.Lucene` to manage Lucene indexes.
- Lucene indexes are stored on the local file system under `/App_Data/Sites/{TenantName}/Lucene/{IndexName}`.
- Use the `CreateOrUpdateIndexProfile` recipe step to create indexes (preferred over the obsolete `lucene-index` and `LuceneIndexSettings` steps).
- Use the `ResetIndex` recipe step to restart indexing from the beginning without deleting existing entries (preferred over the obsolete `lucene-index-reset` step).
- Use the `RebuildIndex` recipe step to delete and recreate the full index (preferred over the obsolete `lucene-index-rebuild` step).
- Lucene queries use Elasticsearch Query DSL syntax.
- Supported query types: `bool`, `match`, `match_all`, `match_phrase`, `term`, `terms`, `wildcard`, `prefix`, `fuzzy`, `range`, `regexp`, `query_string`, `simple_query_string`, `geo_distance`, `geo_bounding_box`.
- Text fields with a `.keyword` suffix are automatically stored in the index (max 256 chars) for term-level queries.
- Enable `OrchardCore.Lucene.Worker` only when running the same tenant on multiple instances (farm) with a file system index.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Lucene Features

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

### Creating a Lucene Index Profile (Recommended)

Use `CreateOrUpdateIndexProfile` to create a Lucene index for content items:

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "BlogPostsLucene",
          "IndexName": "blogposts",
          "ProviderName": "Lucene",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": ["BlogPost"],
              "Culture": "any"
            },
            "LuceneIndexMetadata": {
              "AnalyzerName": "standard",
              "StoreSourceData": true
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
| `IndexName` | Physical index name on disk. |
| `ProviderName` | Must be `"Lucene"`. |
| `Type` | Source category, typically `"Content"` for content items. |
| `IndexLatest` | When `true`, indexes both published and draft items. Default `false` indexes published only. |
| `IndexedContentTypes` | Array of content type names to include. |
| `Culture` | `"any"` for all cultures or a specific culture code. |
| `AnalyzerName` | Lucene analyzer name (e.g., `"standard"`). |
| `StoreSourceData` | When `true`, stores source data for retrieval. |

### Legacy Index Creation (Obsolete)

The `lucene-index` step is obsolete but still supported:

```json
{
  "steps": [
    {
      "name": "lucene-index",
      "Indices": [
        {
          "Search": {
            "AnalyzerName": "standard",
            "IndexLatest": false,
            "IndexedContentTypes": [
              "Article",
              "BlogPost"
            ]
          }
        }
      ]
    }
  ]
}
```

### Reset Lucene Index

Restarts indexing from the beginning without deleting existing entries:

```json
{
  "steps": [
    {
      "name": "ResetIndex",
      "indexNames": [
        "BlogPostsLucene"
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

### Rebuild Lucene Index

Deletes and recreates the full index content:

```json
{
  "steps": [
    {
      "name": "RebuildIndex",
      "indexNames": [
        "BlogPostsLucene"
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

### Creating a Lucene Query via Recipe

```json
{
  "steps": [
    {
      "Source": "Lucene",
      "Name": "RecentBlogPosts",
      "Index": "Search",
      "Template": "{ \"query\": { \"match_all\": {} }, \"size\": 10 }",
      "ReturnContentItems": true
    }
  ]
}
```

### Lucene Query Filters

Query filters retrieve records without considering boost values, similar to SQL filtering.

**Bool filter with term and wildcard:**

```json
{
  "query": {
    "bool": {
      "filter": [
        {
          "term": {
            "Content.ContentItem.Published": "true"
          }
        },
        {
          "wildcard": {
            "Content.ContentItem.DisplayText": "Main*"
          }
        }
      ]
    }
  }
}
```

**Bool with must and filter (filtering by content type):**

```json
{
  "query": {
    "bool": {
      "must": {
        "term": {
          "Content.ContentItem.ContentType.keyword": "Menu"
        }
      },
      "filter": [
        {
          "term": {
            "Content.ContentItem.Published": "true"
          }
        },
        {
          "wildcard": {
            "Content.ContentItem.DisplayText": "Main*"
          }
        }
      ]
    }
  }
}
```

**Full-text search with `query_string`:**

```json
{
  "query": {
    "query_string": {
      "query": "Content.ContentItem.FullText:\"exploration\""
    }
  }
}
```

**Full-text search with `simple_query_string` and default field:**

```json
{
  "query": {
    "simple_query_string": {
      "query": "\"exploration\"",
      "fields": [
        "Content.ContentItem.FullText"
      ]
    }
  }
}
```

### Web APIs

**`api/lucene/content`** — Executes a query and returns content items.

Verbs: `POST` and `GET`

| Parameter | Example | Description |
|-----------|---------|-------------|
| `indexName` | `"search"` | The name of the index to query. |
| `query` | `{ "query": { "match_all": {} }, "size": 10 }` | JSON query object. |
| `parameters` | `{ "size": 3 }` | JSON parameters object. |

**`api/lucene/documents`** — Executes a query and returns Lucene documents (stored fields only).

Same parameters as `api/lucene/content`.

### Lucene Worker

Enable `OrchardCore.Lucene.Worker` to synchronize the local file system index across multiple instances. This creates a background task that keeps indexes in sync in a farm scenario.

Do **not** enable the Worker if:
- You are running a single instance.
- You are running on Azure App Services.
- You are using Elasticsearch instead of Lucene.

### Indexing Custom Data

Register a custom indexing source in `Startup.cs`:

```csharp
using OrchardCore.Modules;

namespace MyModule;

[Feature("MyModule.CustomLuceneIndex")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLuceneIndexingSource("CustomSource", o =>
        {
            o.DisplayName = S["Custom Source"];
            o.Description = S["Create a Lucene index based on a custom data source."];
        });
    }
}
```

### Automatic Field Mapping

Lucene automatically maps text fields with a `.keyword` suffix as `stored` values in the index, unless the document is already explicitly stored. Values longer than 256 characters are ignored. This enables any `TextField` to be searched using term queries by appending `.keyword` to the field name.

For example, to query by the exact `DisplayText` value, use `Content.ContentItem.DisplayText.keyword` in a `term` query.

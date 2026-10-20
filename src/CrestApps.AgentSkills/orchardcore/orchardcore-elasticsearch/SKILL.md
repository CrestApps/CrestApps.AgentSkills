---
name: orchardcore-elasticsearch
description: Skill for configuring Elasticsearch in Orchard Core. Covers Elasticsearch setup and Docker deployment, connection configuration in the OrchardCore Search Elasticsearch section (formerly OrchardCore_Elasticsearch), analyzers and token filters, index creation recipe steps, queries, comparison with Lucene, field mapping, custom data indexing, ElasticsearchPermissions, admin action authorization, and upgrading from GetElasticIndexPermission to IndexingPermissions.CreateDynamicPermission. Use this skill when requests mention Orchard Core Elasticsearch, Configure Elasticsearch, Enabling Elasticsearch Features, Elasticsearch Connection Configuration, Docker Deployment, Creating an Elasticsearch Index Profile, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with OrchardCore.Elasticsearch, OrchardCore.Search.Elasticsearch.Worker, OrchardCore.Search, OrchardCore.Indexing, ManageElasticIndexes, QueryElasticApi.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Elasticsearch - Prompt Templates

## Configure Elasticsearch

You are an Orchard Core expert. Generate code and configuration for Elasticsearch-based search in Orchard Core.

### Guidelines

- Enable `OrchardCore.Elasticsearch` to manage Elasticsearch indices.
- Configure the Elasticsearch connection in `appsettings.json` under the `OrchardCore:Search:Elasticsearch` section (Orchard Core 4.0; the former `OrchardCore_Elasticsearch` section is deprecated but still read). The connection can be configured globally or per tenant.
- Use the `CreateOrUpdateIndexProfile` recipe step to create indexes (preferred over the obsolete `ElasticIndexSettings` step).
- Use the `ResetIndex` recipe step to restart indexing without deleting entries (preferred over the obsolete `elastic-index-reset` step).
- Use the `RebuildIndex` recipe step to delete and recreate indexes (preferred over the obsolete `elastic-index-rebuild` step).
- Elasticsearch uses the Elasticsearch Query DSL for queries.
- Elasticsearch automatically maps string fields to both `text` and `keyword` types.
- Supported connection types: `SingleNodeConnectionPool`, `CloudConnectionPool`, `StaticConnectionPool`, `SniffingConnectionPool`, `StickyConnectionPool`.
- Supported authentication types: `Basic`, `ApiKey`, `Base64ApiKey`, `KeyIdAndKey`.
- Custom analyzers and token filters are defined in `appsettings.json`.
- Both Lucene and Elasticsearch modules can be enabled simultaneously.
- `OrchardCore.Search.Elasticsearch` is an obsolete feature id kept for compatibility; it enables `OrchardCore.Elasticsearch`.
- Reference permissions from `OrchardCore.Elasticsearch.ElasticsearchPermissions` and `OrchardCore.Indexing.Core.IndexingPermissions`.
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling Elasticsearch Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Search",
        "OrchardCore.Elasticsearch",
        "OrchardCore.Indexing"
      ],
      "disable": []
    }
  ]
}
```

### Elasticsearch Connection Configuration

Configure the connection in `appsettings.json`:

```json
{
  "OrchardCore": {
    "Search": {
      "Elasticsearch": {
        "ConnectionType": "SingleNodeConnectionPool",
        "Url": "http://localhost",
        "Ports": [9200],
        "AuthenticationType": "Basic",
        "Username": "admin",
        "Password": "admin",
        "CertificateFingerprint": "",
        "EnableDebugMode": false,
        "EnableHttpCompression": true,
        "IndexPrefix": "",
        "Analyzers": {
          "standard": {
            "type": "standard"
          }
        }
      }
    }
  }
}
```

Environment variables follow the same shape, for example `OrchardCore__Search__Elasticsearch__Url`.

| Property | Description |
|----------|-------------|
| `ConnectionType` | Connection pool type. Use `CloudConnectionPool` for Elastic Cloud. |
| `Url` | Elasticsearch server URL. |
| `Ports` | Array of ports for the Elasticsearch nodes. |
| `AuthenticationType` | `Basic`, `ApiKey`, `Base64ApiKey`, or `KeyIdAndKey`. |
| `Username` / `Password` | Required for `Basic` authentication. |
| `ApiKey` | Required for `ApiKey` authentication. |
| `Base64ApiKey` | Required for `Base64ApiKey` authentication. |
| `CloudId` | Required for `CloudConnectionPool` connection type. |
| `KeyId` / `Key` | Required for `KeyIdAndKey` authentication. |
| `CertificateFingerprint` | TLS certificate fingerprint (not needed for `CloudConnectionPool`). |
| `EnableDebugMode` | Enables debug output for troubleshooting. |
| `EnableHttpCompression` | Enables gzip compression for requests. |
| `IndexPrefix` | Prefix applied to all index names (useful for environment isolation). |

### Docker Deployment

For local development, deploy Elasticsearch with Docker Compose.

**WSL2 prerequisite** — set `vm.max_map_count` in `%userprofile%\.wslconfig`:

```
[wsl2]
kernelCommandLine = "sysctl.vm.max_map_count=262144"
```

Then restart WSL: `wsl --shutdown`

### Creating an Elasticsearch Index Profile (Recommended)

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "BlogPostsES",
          "IndexName": "blogposts",
          "ProviderName": "Elasticsearch",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": ["BlogPost"],
              "Culture": "any"
            },
            "ElasticsearchIndexMetadata": {
              "AnalyzerName": "standard",
              "StoreSourceData": true
            },
            "ElasticsearchDefaultQueryMetadata": {
              "QueryAnalyzerName": "standard",
              "SearchType": "",
              "DefaultQuery": "",
              "DefaultSearchFields": [
                "Content.ContentItem.FullText"
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
| `AnalyzerName` | Index-time analyzer (e.g., `"standard"`). |
| `StoreSourceData` | Stores the `_source` data in Elasticsearch. |
| `QueryAnalyzerName` | Analyzer used when querying this index. |
| `SearchType` | `""` (default multi-match), `"query_string"`, or `"custom"`. |
| `DefaultQuery` | Custom query template when `SearchType` is `"custom"`. Use `{{ term }}` for the search term. |
| `DefaultSearchFields` | Array of fields to search across. |

### Legacy Index Creation (Obsolete)

```json
{
  "steps": [
    {
      "name": "ElasticIndexSettings",
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

### Reset Elasticsearch Index

```json
{
  "steps": [
    {
      "name": "ResetIndex",
      "indexNames": [
        "BlogPostsES"
      ]
    }
  ]
}
```

### Rebuild Elasticsearch Index

```json
{
  "steps": [
    {
      "name": "RebuildIndex",
      "indexNames": [
        "BlogPostsES"
      ]
    }
  ]
}
```

### Creating an Elasticsearch Query via Recipe

```json
{
  "steps": [
    {
      "name": "Queries",
      "Queries": [
        {
          "Source": "Elasticsearch",
          "Name": "RecentBlogPosts",
          "Index": "BlogPostsES",
          "Template": "{ \"query\": { \"match_all\": {} }, \"size\": 10 }",
          "ReturnContentItems": true
        }
      ]
    }
  ]
}
```

`Index` is the index profile `Name`. Since Orchard Core 4.0, the GraphQL field of a named query requires `ExecuteApi_{QueryName}` (see `orchardcore-content-queries`).

### Configuring Analyzers

Define built-in and custom analyzers in `appsettings.json`:

```json
{
  "OrchardCore": {
    "Search": {
      "Elasticsearch": {
        "Analyzers": {
          "standard": {
            "type": "standard"
          },
          "stop": {
            "type": "stop",
            "stopwords": ["a", "the", "and", "or"]
          },
          "english_analyzer": {
            "type": "custom",
            "tokenizer": "standard",
            "filter": ["lowercase", "stop"],
            "char_filter": ["html_strip"]
          }
        }
      }
    }
  }
}
```

### Configuring Token Filters

Define custom token filters in `appsettings.json` and reference them from analyzers:

```json
{
  "OrchardCore": {
    "Search": {
      "Elasticsearch": {
        "TokenFilters": {
          "english_stop": {
            "type": "stop",
            "stopwords": "_english_"
          }
        },
        "Analyzers": {
          "my_new_analyzer": {
            "type": "custom",
            "tokenizer": "standard",
            "filter": ["english_stop"]
          }
        }
      }
    }
  }
}
```

### Custom Query with Search Highlights

When using `"custom"` search type, define a query with highlighting using the Liquid `{{ term }}` placeholder:

```json
{
  "query": {
    "multi_match": {
      "fields": ["Content.ContentItem.FullText"],
      "query": "{{ term }}",
      "fuzziness": "AUTO"
    }
  },
  "highlight": {
    "pre_tags": ["<span style='background-color: #FFF3CD;'>"],
    "post_tags": ["</span>"],
    "fields": {
      "Content.ContentItem.FullText": {
        "fragment_size": 150,
        "number_of_fragments": 3
      }
    }
  }
}
```

Highlight requests require `StoreSourceData` to be enabled on the index.

### Web APIs

**`api/elasticsearch/content`** — Executes a query and returns content items.

Verbs: `POST` and `GET`

| Parameter | Example | Description |
|-----------|---------|-------------|
| `indexName` | `"search"` | The name of the index to query. |
| `query` | `{ "query": { "match_all": {} }, "size": 10 }` | JSON query object. |
| `parameters` | `{ "size": 3 }` | JSON parameters object. |

**`api/elasticsearch/documents`** — Executes a query and returns Elasticsearch documents (stored fields only).

Same parameters as `api/elasticsearch/content`.

Both endpoints use the `Api` authentication scheme and require `ElasticsearchPermissions.QueryElasticApi`. Endpoint names: `ApiGetElasticsearchContent`, `ApiPostElasticsearchContent`, `ApiGetElasticsearchDocuments`, `ApiPostElasticsearchDocuments`.

### Permissions

| Permission | Member | Notes |
|------------|--------|-------|
| `ManageElasticIndexes` | `OrchardCore.Elasticsearch.ElasticsearchPermissions.ManageElasticIndexes` | Manage indexes, view index info, run admin queries. Implied by `IndexingPermissions.ManageIndexes`. |
| `QueryElasticsearchApi` | `ElasticsearchPermissions.QueryElasticApi` | Call `api/elasticsearch/*`. Implied by `ManageElasticIndexes`. |
| `QueryIndex_{ProfileName}` | `IndexingPermissions.CreateDynamicPermission(indexProfile)` | Query one index from the front-end search. |

In Orchard Core 4.0, the admin **Index info** (`Elasticsearch/indexinfo/{id}`) and **Query** (`Elasticsearch/query/{indexName}`) actions check `ManageElasticIndexes` like the other admin actions; previously they were reachable by any admin user.

### Returning Specific Fields

Elasticsearch can return specific fields instead of full source:

```json
{
  "query": {
    "match_all": {}
  },
  "fields": [
    "ContentItemId.keyword",
    "ContentItemVersionId.keyword"
  ],
  "_source": false
}
```

### Indexing Custom Data

Register a custom indexing source in `Startup.cs`:

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace MyModule;

[Feature("MyModule.CustomElasticsearchIndex")]
public sealed class Startup : StartupBase
{
    private readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> localizer)
    {
        S = localizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddElasticsearchIndexingSource("CustomSource", o =>
        {
            o.DisplayName = S["Custom Source"];
            o.Description = S["Create an Elasticsearch index based on a custom data source."];
        });
    }
}
```

### Elasticsearch vs Lucene Field Mapping

Elasticsearch automatically maps string fields to both `text` and `keyword` types. Lucene uses explicit `StringField` (keyword) and `TextField` (text) types.

| Lucene | Elasticsearch | Description | Search Query Type |
|--------|--------------|-------------|-------------------|
| `StringField` | `keyword` | Indexed as a single token, not tokenized. | Term-level queries. |
| `TextField` | `text` | Indexed and tokenized for full-text search. | Full-text queries. |
| `StoredField` | `_source` | Original value stored, not analyzed. | Term-level queries. |

Adapt queries when switching between Lucene and Elasticsearch — field types may differ for the same field name.

### Upgrading from 3.x

- Rename the `OrchardCore_Elasticsearch` configuration section to `OrchardCore:Search:Elasticsearch` (env var `OrchardCore__OrchardCore_Elasticsearch__Url` becomes `OrchardCore__Search__Elasticsearch__Url`). The old name still works; when both exist, the new one wins.
- `OrchardCore.Elasticsearch.ElasticsearchIndexPermissionHelper.GetElasticIndexPermission(string)` was removed; use `OrchardCore.Indexing.Core.IndexingPermissions.CreateDynamicPermission(IndexProfile)`, or authorize `IndexingPermissions.QuerySearchIndex` against the `IndexProfile` resource.
- `OrchardCore.Search.Permissions.QuerySearchIndex` was removed; use `IndexingPermissions.QuerySearchIndex`.
- Roles keep their stored permissions; only code referencing the removed members must change.

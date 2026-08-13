---
name: crestapps-core-elasticsearch
description: Skill for integrating Elasticsearch indexing data sources documents vector retrieval and memory in CrestApps.Core.
---

# CrestApps.Core Elasticsearch - Prompt Templates

## Add Elasticsearch

You are a CrestApps.Core expert. Configure `CrestApps.Core.Elasticsearch` with `CrestApps.Core.AI.Elasticsearch` through the indexing builder.

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddIndexingServices(indexing => indexing
        .AddElasticsearch(
            builder.Configuration.GetSection("CrestApps:Search:Elasticsearch"),
            search => search
                .AddAIDocuments()
                .AddAIDataSources()
                .AddAIMemory())
    )
);
```

`AddCoreElasticsearchServices(...)` only registers the connection options, `IElasticsearchClientFactory`, the default `ElasticsearchClient`, and the provider-keyed indexing primitives. It does not add AI document, data-source, or memory support. For lower-level composition, call the matching `AddCoreElasticsearchAIDocumentSource()`, `AddCoreElasticsearchAIDataSource()`, and/or `AddCoreElasticsearchAIMemorySource()` methods after registering the primitive services.

## Configuration

```json
{
  "CrestApps": {
    "Search": {
      "Elasticsearch": {
        "Url": "https://localhost:9200",
        "AuthenticationType": "Basic",
        "Username": "elastic",
        "Password": "use-a-secret",
        "IndexPrefix": "app_"
      }
    }
  }
}
```

`ElasticsearchConnectionOptions` supports `Url`, `CloudId`, `AuthenticationType`, `Username`, `Password`, `ApiKey`, `Base64ApiKey`, `ApiKeyId`, `CertificateFingerprint`, and `IndexPrefix`. `IndexPrefix` is prepended to framework-managed index names. Configure either endpoint or cloud deployment credentials as required and keep secrets out of checked-in settings.

## What Each AI Option Adds

- `AddAIDocuments()` adds an `AIDocuments` index-profile source, its profile handler, and keyed `IVectorSearchService`.
- `AddAIDataSources()` adds a `DataSource` index-profile source, the shared data-source RAG stack, and a keyed `ElasticsearchAIDataSourceSourceHandler`.
- `AddAIMemory()` adds an `AIMemory` index-profile source, its profile handler, and keyed `IMemoryVectorSearchService`.

The primitive package registers keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, and `ISearchDocumentManager`. These are provider-keyed services, not default implementations.

## External Sources and Indexing

`AIDataSource` records with source type `Elasticsearch` can read an externally managed index. Their source credentials and index name are independent from the shared backend that stores CrestApps RAG chunks. After external index changes, call `IAIDataSourceChangeNotifier`.

`AddAIDataSources()` composes the shared asynchronous synchronization stack: `IAIDataSourceIndexingQueue`, `IAIDataSourceIndexingService`, catalog and document handlers, `AIDataSourceIndexingBackgroundService`, `AIDataSourceAlignmentBackgroundService`, and `DataSourceSearchIndexProfileHandler`.

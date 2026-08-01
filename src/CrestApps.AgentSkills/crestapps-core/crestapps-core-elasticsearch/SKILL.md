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

`AddCoreElasticsearchServices(...)` is the lower-level equivalent. It configures `ElasticsearchConnectionOptions`, `IElasticsearchClientFactory`, `ElasticsearchClient`, and keyed services using `ElasticsearchConstants.ProviderName`.

## Configuration

```json
{
  "CrestApps": {
    "Search": {
      "Elasticsearch": {
        "Url": "https://localhost:9200",
        "AuthenticationType": "Basic",
        "Username": "elastic",
        "Password": "use-a-secret"
      }
    }
  }
}
```

`ElasticsearchConnectionOptions` supports `Url`, `CloudId`, `AuthenticationType`, `Username`, `Password`, `ApiKey`, `Base64ApiKey`, `ApiKeyId`, and `CertificateFingerprint`. Configure either endpoint or cloud deployment credentials as required and keep secrets out of checked-in settings.

## What Each AI Option Adds

- `AddAIDocuments()` adds the `AIDocuments` profile source, its handler, and keyed `IVectorSearchService`.
- `AddAIDataSources()` adds the `DataSource` profile source, the shared data-source RAG stack, the `Elasticsearch` `IAIDataSourceSourceHandler`, and its handler.
- `AddAIMemory()` adds the `AIMemory` profile source, its handler, and keyed `IMemoryVectorSearchService`.

The primitive package registers keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, and `ISearchDocumentManager`. These are provider-keyed services, not default implementations.

## External Sources and Indexing

`AIDataSource` records with source type `Elasticsearch` can read an externally managed index. Their source credentials and index name are independent from the shared backend that stores CrestApps RAG chunks. After external index changes, call `IAIDataSourceChangeNotifier`.

`AddAIDataSources()` composes the shared asynchronous synchronization stack: `IAIDataSourceIndexingQueue`, `IAIDataSourceIndexingService`, catalog and document handlers, `AIDataSourceIndexingBackgroundService`, `AIDataSourceAlignmentBackgroundService`, and `DataSourceSearchIndexProfileHandler`.

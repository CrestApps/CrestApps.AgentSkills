---
name: crestapps-core-postgresql
description: Skill for integrating PostgreSQL pgvector indexing data sources documents vector retrieval and memory in CrestApps.Core.
---

# CrestApps.Core PostgreSQL - Prompt Templates

## Add PostgreSQL with pgvector

You are a CrestApps.Core expert. Configure `CrestApps.Core.PostgreSQL` with `CrestApps.Core.AI.PostgreSQL` through the indexing builder.

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddIndexingServices(indexing => indexing
        .AddPostgreSQL(
            builder.Configuration.GetSection("CrestApps:PostgreSQL"),
            postgreSQL => postgreSQL
                .AddAIDocuments()
                .AddAIDataSources()
                .AddAIMemory())
    )
);
```

`AddCorePostgreSQLServices(...)` is the lower-level equivalent. It configures `PostgreSQLConnectionOptions`, `IPostgreSQLClientFactory`, and provider-keyed services using `PostgreSQLConstants.ProviderName`.

## Configuration

```json
{
  "CrestApps": {
    "PostgreSQL": {
      "ConnectionString": "Host=localhost;Port=5432;Database=vectordb;Username=postgres;Password=use-a-secret",
      "IndexPrefix": ""
    }
  }
}
```

Use PostgreSQL with the `vector` extension available. `PostgreSQLConnectionOptions` contains `ConnectionString` and `IndexPrefix`; protect the connection string through secrets or managed configuration.

## What Each AI Option Adds

- `AddAIDocuments()` registers the `AIDocuments` source, document index-profile handler, and keyed `IVectorSearchService`.
- `AddAIDataSources()` registers the `DataSource` source, shared RAG synchronization, the `PostgreSQL` `IAIDataSourceSourceHandler`, and data-source index-profile handler.
- `AddAIMemory()` registers the `AIMemory` source, memory index-profile handler, and keyed `IMemoryVectorSearchService`.

Provider primitives register keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, and `ISearchDocumentManager`. Retrieve them with the PostgreSQL provider key.

## External Sources and Indexing

An `AIDataSource` using source type `PostgreSQL` reads an external table using source-specific connection settings and table name. This is distinct from the configured pgvector backend that contains CrestApps search documents. Call `IAIDataSourceChangeNotifier` when the external table changes.

`AddAIDataSources()` includes `IAIDataSourceIndexingQueue`, `IAIDataSourceIndexingService`, `AIDataSourceCatalogIndexingHandler`, `AIDataSourceSearchDocumentHandler`, `AIDataSourceIndexingBackgroundService`, `AIDataSourceAlignmentBackgroundService`, and `DataSourceSearchIndexProfileHandler`.

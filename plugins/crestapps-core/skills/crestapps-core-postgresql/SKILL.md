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

`AddCorePostgreSQLServices(...)` only registers `PostgreSQLConnectionOptions`, `IPostgreSQLClientFactory`, and provider-keyed indexing primitives. It does not add AI document, data-source, or memory support. For lower-level composition, call the matching `AddCorePostgreSQLAIDocumentSource()`, `AddCorePostgreSQLAIDataSource()`, and/or `AddCorePostgreSQLAIMemorySource()` methods after registering the primitive services.

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

Use PostgreSQL with the `vector` extension available. `PostgreSQLConnectionOptions` contains `ConnectionString` and `IndexPrefix`. The prefix is applied to framework-managed index table names. Protect the connection string through secrets or managed configuration.

## What Each AI Option Adds

- `AddAIDocuments()` registers an `AIDocuments` index-profile source, its profile handler, and keyed `IVectorSearchService`.
- `AddAIDataSources()` registers a `DataSource` index-profile source, shared RAG synchronization, and a keyed `PostgreSQLAIDataSourceSourceHandler`.
- `AddAIMemory()` registers an `AIMemory` index-profile source, its profile handler, and keyed `IMemoryVectorSearchService`.

Provider primitives register keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, and `ISearchDocumentManager`. Retrieve them with the PostgreSQL provider key.

## External Sources and Indexing

An `AIDataSource` using source type `PostgreSQL` reads an external table using source-specific connection settings and table name. This is distinct from the configured pgvector backend that contains CrestApps search documents. Call `IAIDataSourceChangeNotifier` when the external table changes.

`AddAIDataSources()` includes `IAIDataSourceIndexingQueue`, `IAIDataSourceIndexingService`, `AIDataSourceCatalogIndexingHandler`, `AIDataSourceSearchDocumentHandler`, `AIDataSourceIndexingBackgroundService`, `AIDataSourceAlignmentBackgroundService`, and `DataSourceSearchIndexProfileHandler`.

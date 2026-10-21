---
name: crestapps-core-postgresql
description: Skill for using PostgreSQL with pgvector as the CrestApps.Core indexing and vector backend. Covers CrestApps.Core.PostgreSQL and CrestApps.Core.AI.PostgreSQL, AddPostgreSQL on the indexing builder with AddAIDocuments, AddAIDataSources and AddAIMemory, AddCorePostgreSQLServices and the lower-level source registrations, PostgreSQLConnectionOptions and IndexPrefix, the vector extension, typed knowledge columns and in-place schema upgrades through TryAddFieldsAsync, external PostgreSQL table sources with IAIDataSourceChangeNotifier, the Aspire AppHost and Docker setup, and troubleshooting.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core PostgreSQL

Use PostgreSQL with pgvector as a lightweight, self-hosted knowledge-base backend for AI documents, AI data sources and AI memory. For the data source model see `crestapps-core-data-sources`.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai.AddOpenAI())
    .AddIndexingServices(indexing => indexing
        .AddYesSqlStores()
        .AddPostgreSQL(
            builder.Configuration.GetSection("CrestApps:PostgreSQL"),
            postgreSQL => postgreSQL
                .AddAIDocuments()
                .AddAIDataSources()
                .AddAIMemory())));
```

| Call | Package | Adds |
|---|---|---|
| `AddCorePostgreSQLServices(section)` | `CrestApps.Core.PostgreSQL` | `PostgreSQLConnectionOptions`, `IPostgreSQLClientFactory` (manages `NpgsqlDataSource` instances), keyed `IDataSourceContentManager`, `IDataSourceDocumentReader`, `IODataFilterTranslator`, `ISearchIndexManager`, `ISearchDocumentManager` |
| `AddAIDocuments()` / `AddCorePostgreSQLAIDocumentSource()` | `CrestApps.Core.AI.PostgreSQL` | `AIDocuments` index-profile source, profile handler, keyed `IVectorSearchService` |
| `AddAIDataSources()` / `AddCorePostgreSQLAIDataSource()` | `CrestApps.Core.AI.PostgreSQL` | `DataSource` index-profile source, keyed `PostgreSQLAIDataSourceSourceHandler`, and `AddCoreAIDataSourceRag()` |
| `AddAIMemory()` / `AddCorePostgreSQLAIMemorySource()` | `CrestApps.Core.AI.PostgreSQL` | `AIMemory` index-profile source, profile handler, keyed `IMemoryVectorSearchService` |

Every keyed service uses `PostgreSQLConstants.ProviderName` (`"PostgreSQL"`). Unlike Elasticsearch and Azure AI Search there is no translator-only registration; the translator comes with `AddCorePostgreSQLServices()`.

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

`PostgreSQLConnectionOptions` has `ConnectionString` (any Npgsql parameters) and `IndexPrefix` (prepended to framework-managed table names, useful when tenants share a database). Each index profile is its own table. The index manager runs `CREATE EXTENSION IF NOT EXISTS vector`, so the database user needs permission to create it, or a DBA must create it first. Requires PostgreSQL 12+ and pgvector 0.5.0+.

For local development, `dotnet run --project src/Startup/CrestApps.Core.Aspire.AppHost` starts a `pgvector/pgvector:pg16` container and injects `CrestApps__PostgreSQL__ConnectionString` into both sample hosts. Without Aspire, run the same image with Docker Compose.

## Typed Columns

`contentType`, `rootId`, `parentId` and `page` are real columns. `PostgreSQLSearchIndexManager.TryAddFieldsAsync` adds them to an existing table on the next data source sync, and the OData translator filters on them directly. Other filter fields target the row's filter bag.

## External PostgreSQL Sources

An `AIDataSource` with `Source = AIDataSourceSourceTypes.PostgreSQL` reads an external table configured on the data source (`ConnectionString` protected with `AIDataSourceProtectionConstants.SourceSecretPurpose`, and `TableName`). It is separate from the pgvector backend that stores CrestApps rows. Call `IAIDataSourceChangeNotifier` when the external table changes.

## Troubleshooting

| Symptom | Check |
|---|---|
| Index profile creation reports the provider is not configured | `ConnectionString` missing from the bound section |
| `type "vector" does not exist` | pgvector not installed, or the user cannot create extensions |
| Old rows not filtered by type | expected; rows written before the typed columns have `contentType` null and read as `text` |

## Upgrading from 1.x

- Existing tables keep working; the typed columns are added in place on the next sync.
- Embeddings resolve through the `embedding` deployment slot or the index profile's `EmbeddingDeploymentName`; deployment `Purpose` is gone.

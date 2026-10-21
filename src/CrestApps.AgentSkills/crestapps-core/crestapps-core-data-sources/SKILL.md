---
name: crestapps-core-data-sources
description: Skill for CrestApps.Core 2.0 AI data sources and search-backed RAG in any ASP.NET Core host. Covers the AIDataSource source and knowledge-base model, the SearchIndexProfile, Elasticsearch, AzureAISearch, PostgreSQL, Web and File source types, AddAIDataSources and AddCoreAIDataSourceRag, the asynchronous synchronization queue and IAIDataSourceChangeNotifier, custom IAIDataSourceSourceHandler implementations, typed knowledge columns contentType, rootId, parentId and page, OData filters and IODataFilterTranslator, AIDataSourceRagMetadata including ObjectTypes, the data source search tool instance source with Chunk and Hierarchical retrieval and ContentTypes, figure citations, and upgrading from the Ingested source type.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Data Sources

An `AIDataSource` connects a **source** (where raw documents come from) to a **knowledge base** (a vector index profile holding chunked, embedded rows). AI profiles and chat interactions attach a data source for preemptive RAG and the `search_data_sources` tool, and tool instances can expose several data sources side by side.

Pick a backend skill for the vector store: `crestapps-core-elasticsearch`, `crestapps-core-azure-ai-search` or `crestapps-core-postgresql`. For uploads see `crestapps-core-document-processing`. For the record types that fill `Web` and `File` data sources see `crestapps-core-web-crawlers` and `crestapps-core-file-sources`.

## Source Types

`AIDataSourceSourceTypes` (namespace `CrestApps.Core.AI.Models`):

| Constant | Reads | Kept in step by |
|---|---|---|
| `SearchIndexProfile` | a CrestApps-managed source index profile | automatic, when writes go through `ISearchDocumentManager` |
| `Elasticsearch` | an external Elasticsearch index with per-data-source credentials | `IAIDataSourceChangeNotifier` |
| `AzureAISearch` | an external Azure AI Search index | `IAIDataSourceChangeNotifier` |
| `PostgreSQL` | an external PostgreSQL table | `IAIDataSourceChangeNotifier` |
| `Web` | pages from web crawler records (`AddCoreWebCrawlers()`) | scheduled re-crawl |
| `File` | typed knowledge objects from file source records (`AddCoreFileSources()`) | scheduled runs |

`AIDataSource` stores `Source`, `AIKnowledgeBaseIndexProfileName`, `SourceIndexProfileName` and the field mapping `KeyFieldName`, `TitleFieldName`, `ContentFieldName`. `Web` and `File` data sources need no field mapping because every row already carries its key, title and content. Source credentials go on `AIDataSource.Properties`, protected with `AIDataSourceProtectionConstants.SourceSecretPurpose`.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddChatInteractions(chatInteractions => chatInteractions
            .AddYesSqlStores())
        .AddToolInstances(toolInstances => toolInstances
            .AddDataSourceSearchSource()
            .AddYesSqlStores()))
    .AddIndexingServices(indexing => indexing
        .AddYesSqlStores()
        .AddPostgreSQL(
            builder.Configuration.GetSection("CrestApps:PostgreSQL"),
            postgreSQL => postgreSQL.AddAIDataSources())));

builder.Services.AddCoreAIDataSourceStoresYesSql(); // or AddCoreAIDataSourceStoresEntityCore()
```

- `AddAIDataSources()` on a backend builder registers the `DataSource` index-profile source, the keyed backend `IAIDataSourceSourceHandler`, and calls `AddCoreAIDataSourceRag()`.
- `AddCoreAIDataSourceRag()` registers `IAIDataSourceIndexingQueue` (in-memory channel), `IAIDataSourceChangeNotifier`, `IAIDataSourceIndexingService`, the `SearchIndexProfile` source handler, `AIDataSourceCatalogHandler`, `AIDataSourceSearchDocumentHandler`, `AIDataSourceIndexingBackgroundService`, `AIDataSourceAlignmentBackgroundService` (nightly 2:00 AM UTC reconciliation), `DataSourceOrchestrationHandler`, `DataSourcePreemptiveRagHandler` and the `search_data_sources` system tool.
- The document-processing builder's `AddYesSqlStores()` / `AddEntityCoreStores()` also registers the data source store.
- A metadata store alone is not a backend. Register a real backend and call its `AddAIDataSources()`.

## Synchronization

1. Creating, editing or deleting an `AIDataSource` queues a full rebuild or cleanup through `AIDataSourceCatalogHandler`.
2. `SearchIndexProfile` sources sync automatically from `ISearchDocumentHandler` notifications.
3. External and custom sources must notify the framework.

```csharp
await changeNotifier.QueueDocumentsAddedOrUpdatedAsync(dataSourceId, [recordId], cancellationToken);
await changeNotifier.QueueDocumentsDeletedAsync(dataSourceId, [recordId], cancellationToken);
await changeNotifier.QueueFullSyncAsync(dataSourceId, cancellationToken);
```

Replace `IAIDataSourceIndexingQueue` for a durable or multi-node queue. Set `Trace` logging on `CrestApps.Core.AI` categories to follow queue and alignment activity.

## Custom Sources

Register a keyed `IAIDataSourceSourceHandler` and a descriptor in `AIDataSourceSourceOptions`. See [references/custom-source-examples.md](references/custom-source-examples.md) for a complete, compiling handler and sync bridge.

```csharp
builder.Services
    .AddCoreAIDataSourceRag()
    .Configure<AIDataSourceSourceOptions>(options => options.AddOrUpdate(
        "CustomCatalog",
        new LocalizedString("CustomCatalog", "Custom Catalog"),
        new LocalizedString("CustomCatalog Description", "Reads documents from the catalog service.")))
    .AddKeyedScoped<IAIDataSourceSourceHandler, CustomCatalogAIDataSourceSourceHandler>("CustomCatalog");
```

`IAIDataSourceSourceHandler.ProducesTypedKnowledge` defaults to `false`; only the `File` handler returns typed knowledge objects.

## Typed Knowledge Columns

Every knowledge-base row now carries `contentType` (`document`, `article`, `text`, `figure`, `chart`, `table`), `rootId`, `parentId` and `page`. Rows from non-`File` sources carry `contentType = "text"`, and every reader treats a missing value as `text`, so older indexes keep working.

- `ISearchIndexManager.TryAddFieldsAsync` (default-implemented) adds the columns to an existing index on the next sync. PostgreSQL, Elasticsearch and Azure AI Search support it.
- `IDataSourceContentManager.DeleteByReferenceIdsAsync` (default-implemented) deletes a reference's rows by predicate.
- `SourceDocument.IsPreChunked` stores a row as one chunk with no title prefix.
- OData translators map `contentType`, `rootId`, `parentId` and `page` to the real columns, and `contentType eq null` is a null test. Other fields still target the per-row filter bag.

## Retrieval Settings

`AIDataSourceRagMetadata` on an AI profile or chat interaction: `Strictness`, `TopNDocuments`, `IsInScope`, `Filter` (OData) and `ObjectTypes` (for example `["figure", "chart"]`). Preemptive RAG and `search_data_sources` read it through one shared resolver, so both apply the same filter, strictness and top N. `ObjectTypes` becomes part of the filter. Asking for `text` also admits rows with no `contentType`.

When a filter is set and no keyed `IODataFilterTranslator` exists for the knowledge base provider, both paths log a warning and search without it. The chat interaction hub validates filters with `IODataValidator`.

## Data Source Search Tool Instances

`AddDataSourceSearchSource()` (source name `data-source-search`, category `Knowledgebase`) turns one data source into its own model-callable function, so several knowledge bases can be offered at once.

```csharp
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling.Instances.DataSources;

instance.Put(new DataSourceSearchToolSettings
{
    DataSourceId = dataSource.ItemId,
    RetrievalMode = DataSourceRetrievalMode.Hierarchical, // default Chunk
    TopNDocuments = 5,
    Strictness = 3,
    Filter = "category eq 'support'",
    ContentTypes = ["text", "table"],
});
```

- Queries are embedded with the knowledge base's own embedding deployment (`SearchIndexProfile.EmbeddingDeploymentName`, or `DataSourceIndexProfileMetadata.EmbeddingDeploymentName`).
- One call may carry up to three phrases, embedded in one batch, searched in parallel and merged with Reciprocal Rank Fusion.
- `Hierarchical` returns each matching document's full text through its source handler and falls back to chunks when it cannot.
- A `contentTypes` argument lets the model narrow, never widen, what the instance searches.
- `AddKnowledgeObjectSource()` and `AddKnowledgeObjectListSource()` (in `CrestApps.Core.AI.Documents.Tooling`) read or list one article, figure, chart or table by the identifier a search reported.

## Figures in Answers

Retrieval over a `File` data source renders figures and tables in their own blocks with short labels such as `[fig:1]` and `[tbl:1]`. The model writes the label, and the chat client replaces it with the picture registered under that marker (`AICompletionReference.IsImage`). The address comes from `IAIReferenceLinkResolver` (`RetrievedFigure.Link`), and MCP clients read `RetrievedFigure.Uri` (`crestapps://datasource/{dataSourceId}/figure/{canonicalId}`). Register `AddCoreAIDocumentReferenceDownloads()` and map `AddDownloadKnowledgeFigureEndpoint()` so figures have a servable address. Citations that print the same line and link are merged into one.

## Upgrading from 1.x

- `AIDataSourceSourceTypes.Ingested` is renamed `File`, with `FileAIDataSourceSourceHandler` and `FileReferenceLinkResolver`. A data source saved under a preview build with source `Ingested` must be rewritten to `File`.
- The **Files** entry in the data source picker is registered by `AddCoreFileSources()`, and **Web** by `AddCoreWebCrawlers()`. The `File` read-side handler is registered by `AddCoreAIDocumentIngestion()`.
- The manual **Upload files** action on a data source is gone. Use a file source, or call `IKnowledgeIngestionService` from code.
- Custom `ISearchIndexManager` and `IDataSourceContentManager` implementations keep compiling. Override `TryAddFieldsAsync` and `DeleteByReferenceIdsAsync` to support typed columns and efficient deletes.
- Embedding deployments are resolved through the `embedding` slot; deployment `Purpose` no longer exists.

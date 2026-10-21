---
name: orchardcore-ai-data-sources
description: Skill for CrestApps AI Data Sources in Orchard Core 4.0 (CrestApps.OrchardCore 3.0). Covers knowledge base indexes, RAG retrieval, the source types SearchIndexProfile, Elasticsearch, AzureAISearch, PostgreSQL, Web and File, embedding deployments picked by the embedding slot, OData filters and translators, preemptive RAG and IsInScope, citations and IAIReferenceLinkResolver, sync and alignment, the AIDataSource recipe and deployment steps, global Elasticsearch and PostgreSQL connections, AIDataSourceOptions through IOptionsMonitor, and the AIDataSources admin list. Use it for CrestApps.OrchardCore.AI.DataSources, CrestApps.OrchardCore.AI.DataSources.Elasticsearch, CrestApps.OrchardCore.AI.DataSources.AzureAI, CrestApps.OrchardCore.AI.DataSources.PostgreSQL, AIDataSource, AIDataSourceRagMetadata, DataSourceMetadata, PostgreSQLSourceMetadata, IAIDataSourceIndexingQueue, IAIDataSourceSourceHandler, IODataFilterTranslator, and ManageAIDataSources.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Data Sources

## Configure AI data sources and knowledge base retrieval

You are an Orchard Core expert. Generate configuration, recipes, and code for CrestApps AI data sources: a data source reads documents from a source, chunks and embeds them into a shared **AI knowledge base index**, and AI profiles, chat interactions, workflow tasks, and tool instances retrieve from it (RAG).

### Guidelines

- `CrestApps.OrchardCore.AI.DataSources` is `EnabledByDependencyOnly`. Enable a provider feature (Elasticsearch, Azure AI Search, PostgreSQL), `CrestApps.OrchardCore.AI.WebCrawlers`, or `CrestApps.OrchardCore.AI.FileSources` and it comes along. It depends on `CrestApps.OrchardCore.AI` and `OrchardCore.Indexing`.
- The knowledge base index is an Orchard index profile of type `DataSourceIndex`, created under **Search > Indexing** as **AI Knowledge Base Index (Elasticsearch)** or **AI Knowledge Base Index (Azure AI Search)**. Lucene cannot hold a knowledge base.
- Each knowledge base index names one **embedding deployment**. The picker lists deployments that fill the `embedding` slot, so the deployment must declare the `textEmbedding` capability. The deployment is locked once the index exists, because it fixes the vector dimensions.
- A `SearchIndexProfile` source must be an Elasticsearch or Azure AI Search index profile. Only those providers register an `IDataSourceDocumentReader`; a Lucene index or an AI-managed index (AI Documents, AI Memory, another knowledge base) is rejected.
- The source type is chosen in the **Add Data Source** dialog and stays fixed. The destination index and field mapping lock once the data source is configured.
- Field mapping (key, title, content) is shown and required only for `SearchIndexProfile`, `Elasticsearch`, `AzureAISearch`, and `PostgreSQL`. `Web` and `File` sources shape their own documents.
- Read data source defaults through `IOptionsMonitor<AIDataSourceOptions>.CurrentValue`. Saving the **Data Sources** settings no longer releases the tenant.
- Install provider packages in the web/startup project and keep connection strings and keys in user secrets, environment variables, or a key vault.

### Features

| Feature | Feature ID | Depends on |
|---|---|---|
| AI Data Sources | `CrestApps.OrchardCore.AI.DataSources` | `CrestApps.OrchardCore.AI`, `OrchardCore.Indexing` (dependency only) |
| AI Data Sources - Elasticsearch | `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | AI Data Sources, `OrchardCore.Elasticsearch` |
| AI Data Sources - Azure AI Search | `CrestApps.OrchardCore.AI.DataSources.AzureAI` | AI Data Sources, `OrchardCore.Indexing`, `OrchardCore.AzureAI` |
| AI Data Sources - PostgreSQL | `CrestApps.OrchardCore.AI.DataSources.PostgreSQL` | AI Data Sources |
| AI Web Crawlers | `CrestApps.OrchardCore.AI.WebCrawlers` | AI Data Sources (see `orchardcore-ai-web-crawlers`) |
| AI File Sources | `CrestApps.OrchardCore.AI.FileSources` | AI Data Sources, AI Documents (see `orchardcore-ai-file-sources`) |

PostgreSQL only reads source rows. Pair it with the Elasticsearch or Azure AI Search feature, which provides the knowledge base index.

### Source types

| `Source` value | Added by | Reads from |
|---|---|---|
| `SearchIndexProfile` | AI Data Sources | An Orchard Elasticsearch or Azure AI Search index profile, for example the content index |
| `Elasticsearch` | Elasticsearch feature | An external Elasticsearch index (self-managed URL or Elastic Cloud ID) |
| `AzureAISearch` | Azure AI Search feature | An external Azure AI Search index |
| `PostgreSQL` | PostgreSQL feature | A PostgreSQL table, read in batches ordered by the key column |
| `Web` | AI Web Crawlers | Pages scraped by web crawlers that target the data source |
| `File` | AI File Sources | Files ingested by file sources that target the data source |

### Enable Elasticsearch-backed data sources

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Elasticsearch",
        "CrestApps.OrchardCore.AI.DataSources.Elasticsearch"
      ],
      "disable": []
    }
  ]
}
```

For Azure AI Search, enable `OrchardCore.AzureAI` and `CrestApps.OrchardCore.AI.DataSources.AzureAI`. To read a PostgreSQL table, add `CrestApps.OrchardCore.AI.DataSources.PostgreSQL` next to one of them.

### End-to-end setup

1. Create an embedding deployment that declares `textEmbedding` (see `orchardcore-ai-model-capabilities`).
2. Go to **Search > Indexing**, add **AI Knowledge Base Index (Elasticsearch)** or **(Azure AI Search)**, and pick the embedding deployment.
3. Go to **Artificial Intelligence > Data Sources**, choose **Add Data Source**, pick a source type, then set the source settings, the destination index, and the field mapping.
4. Saving queues a full sync. Use **Sync index** in the row's **Actions** menu to re-run one later.
5. Attach the data source on the **Knowledge** tab of an AI profile, profile template, or chat interaction, or bind it to a **Data source search** tool instance (see `orchardcore-ai-tool-instances`).

Field defaults for the content index: Elasticsearch uses key `ContentItemId`, title `Content.ContentItem.DisplayText.keyword`, content `Content.ContentItem.FullText`; Azure AI Search uses `ContentItemId`, `Content__ContentItem__DisplayText__keyword`, `Content__ContentItem__FullText`.

### Create a data source with a recipe

```json
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "ItemId": "articlesdatasource01",
          "DisplayText": "Articles",
          "Source": "SearchIndexProfile",
          "SourceIndexProfileName": "articles",
          "AIKnowledgeBaseIndexProfileName": "AIRagKnowledgeBase",
          "KeyFieldName": "ContentItemId",
          "TitleFieldName": "Content.ContentItem.DisplayText.keyword",
          "ContentFieldName": "Content.ContentItem.FullText"
        }
      ]
    }
  ]
}
```

- `Source` defaults to `SearchIndexProfile` and must be a registered source type, otherwise the step reports *Invalid source used for AI data source*.
- An existing `ItemId` updates that data source; a new valid `ItemId` is kept as the id.
- `DisplayText` and `AIKnowledgeBaseIndexProfileName` are always required, and `ContentFieldName` is required for every source except `Web`.
- Source-specific settings go in `Properties`, for example `PostgreSQLSourceMetadata` or `ElasticsearchSourceMetadata`. See [data source examples](references/data-sources-examples.md) for each source type, the knowledge base index recipe, and the profile recipe.

The `AIDataSource` deployment step (**Artificial Intelligence** category) exports all or selected data sources (`IncludeAll`, `SourceIds`).

### Attach a data source to an AI profile

The profile stores the selection in `DataSourceMetadata` and the retrieval settings in `AIDataSourceRagMetadata`:

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "knowledge-assistant",
          "DisplayText": "Knowledge Assistant",
          "Type": "Chat",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "Answer using the provided knowledge base context."
            },
            "DataSourceMetadata": {
              "DataSourceId": "articlesdatasource01"
            },
            "AIDataSourceRagMetadata": {
              "Strictness": 3,
              "TopNDocuments": 5,
              "IsInScope": true,
              "Filter": "category eq 'docs'"
            }
          }
        }
      ]
    }
  ]
}
```

| Editor field (Knowledge tab) | Property | Notes |
|---|---|---|
| **Data source** | `DataSourceMetadata.DataSourceId` | |
| **Strictness** | `AIDataSourceRagMetadata.Strictness` | 1 to 5, blank uses the site default |
| **Retrieved documents** | `AIDataSourceRagMetadata.TopNDocuments` | 3 to 20, blank uses the site default |
| **Restrict answers to retrieved data only** | `AIDataSourceRagMetadata.IsInScope` | |
| **Filter** | `AIDataSourceRagMetadata.Filter` | OData, validated with `IODataValidator` on save |

Site defaults live in the **Data Sources** section of **Settings > Artificial Intelligence** (`AIDataSourceSettings.DefaultStrictness`, default 3, and `DefaultTopNDocuments`, default 5). Preemptive retrieval is the **Enable preemptive retrieval-augmented generation (RAG)** switch of the **Default Orchestrator** section; when a profile has tools disabled, preemptive RAG always runs.

### Retrieval behavior

| Preemptive RAG | IsInScope | Behavior |
|---|---|---|
| On | On | Context is injected; the model must answer only from it |
| On | Off | Context is injected; the model may add general knowledge |
| Off | On | No injected context; the model must call `search_data_sources` first and use nothing else |
| Off | Off | No injected context; the model calls `search_data_sources` first, then may use general knowledge |

The preemptive retrieval and the `search_data_sources` tool read the same filter, strictness, and retrieved document count from the profile or chat interaction. Injected sections use bracket headers such as `[Data Source Context]`, `[Knowledge Source Instructions]`, and `[Scope Constraint]`.

### OData filters

A knowledge base chunk stores every source field in a `filters` object. Write a field by its source name (`category eq 'docs'`); the translator adds the `filters.` prefix. The typed columns `contentType`, `rootId`, `parentId`, and `page` are not prefixed.

| Knowledge base index | Filter support |
|---|---|
| Elasticsearch | Translated by the CrestApps.Core Elasticsearch `IODataFilterTranslator` (`AddElasticsearchODataFilterTranslator()`) into a bool query. Strings are dynamically mapped as `text` plus `keyword`, so use `category.keyword eq 'Support Docs'` for an exact match. |
| Azure AI Search | Not supported. No translator is registered, so the filter is ignored and *No OData filter translator available for provider* is logged. |

### Citations and links

Answers carry `[doc:N]` markers. Each reference has a `ReferenceId` (source key), a `ReferenceType` (the source index profile type), and a title. Indices are shared across data sources, documents, and tool calls through `AIInvocationScope.Current`. A reference gets a link only when an `IAIReferenceLinkResolver` is registered for its type:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped<IAIReferenceLinkResolver, ProductLinkResolver>("ProductIndex");
    }
}
```

The Web source registers its own resolver, so crawled pages link back to their URL.

### Keeping the knowledge base in sync

- `DataSourceContentHandler` (requires `OrchardCore.Contents`) queues chunk updates and removals when content changes.
- Orchard Core raises `IDocumentIndexHandler` notifications after provider upserts and deletes; the module bridges them into `IAIDataSourceIndexingQueue`.
- `OrchardAIDataSourceIndexingQueue` batches the request's work and runs it after the response in a background job, through `IAIDataSourceIndexingService`.
- Synchronizing a source index profile queues a full re-sync of every data source that reads it; synchronizing a knowledge base index re-syncs the data sources that write to it.
- `DataSourceAlignmentBackgroundTask` (**AI Data Source Alignment**, `0 2 * * *`) repairs drift every night.
- Creating or updating a data source queues a full sync; deleting one removes its chunks with `IDataSourceContentManager.DeleteByDataSourceIdAsync`.
- External `Elasticsearch`, `AzureAISearch`, and `PostgreSQL` sources are not touched by Orchard content events. Re-sync them with **Sync index** or the nightly task.

### Extend with a custom source type

A source type is a keyed `IAIDataSourceSourceHandler`, an entry in `AIDataSourceSourceOptions`, and a display driver for its editor section. Opt into the shared field mapping with `AIDataSourceFieldMappingOptions.Require`:

```csharp
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped<IAIDataSourceSourceHandler, CrmAIDataSourceSourceHandler>("Crm");
        services.AddDisplayDriver<AIDataSource, CrmAIDataSourceDisplayDriver>();
        services.Configure<AIDataSourceFieldMappingOptions>(options => options.Require("Crm"));
        services.Configure<AIDataSourceSourceOptions>(options => options.AddOrUpdate(
            "Crm",
            S["CRM"],
            S["Read source documents from the CRM."]));
    }
}
```

The handler returns its key from `SourceType`, validates its settings (`ValidateAsync`), reports the reference type (`GetReferenceTypeAsync`), and streams documents (`ReadAsync`, `ReadByIdsAsync`). Follow the PostgreSQL handler: store settings as a metadata object on the data source, protect secrets with `IDataProtectionProvider` and `AIDataSourceProtectionConstants.SourceSecretPurpose`, and show the driver only when `dataSource.Source` matches. The shared contracts are covered in `crestapps-core-data-sources`.

### Admin list

**Artificial Intelligence > Data Sources** renders through the Orchard Core `AdminList` shape under the list name `AIDataSources`, with the grid columns `Name`, `Indexes`, `Modified`, and `Actions`. Each row is the `AIDataSource` shape in the `SummaryAdmin` (List) and `SummaryAdminGrid` (Grid) display types. A driver that adds to a row must place its shape for both display types; add a column with an `IAdminListColumnsProvider` for `AIDataSources`. Mechanics are in `orchardcore-admin-lists`.

### Permissions

`ManageAIDataSources` (Administrator by default) protects the data source screens. The **Data Sources** settings section is part of the AI settings group, guarded by `ManageAIProfiles`.

### Upgrading from 2.x

- Pick embedding deployments by capability. A deployment stored or configured with the legacy `Purpose: "Embedding"` is projected onto `textEmbedding` automatically; declare `Properties.AIDeploymentMetadata.Features: ["textEmbedding"]` for new ones.
- Replace `IOptions<AIDataSourceOptions>` with `IOptionsMonitor<AIDataSourceOptions>`; a singleton `IOptions` keeps the value captured at first use.
- Filters on an Elasticsearch knowledge base are applied again. Review filters written while they were silently ignored, and use `.keyword` for exact string matches.
- A filter on an Azure AI Search knowledge base is still ignored, but now logs a warning.
- In a chat interaction, `search_data_sources` now applies the interaction's filter, strictness, and retrieved document count, and an invalid filter rejects the save.
- The data source list is an `AdminList`. Row overrides only apply to the List layout; give added shapes a `SummaryAdminGrid` location.
- Recipes that omit `Source` still import as `SearchIndexProfile`, and the legacy `ProfileSource` key still maps to `SourceIndexProfileName`.

### Troubleshooting

| Symptom | Check |
|---|---|
| No knowledge base index type in **Search > Indexing** | Enable the Elasticsearch or Azure AI Search data source feature and its Orchard Core search feature. |
| No embedding deployment to pick | Declare `textEmbedding` on an embedding deployment. Chat deployments are not offered. |
| Index stays empty | The knowledge base index has no embedding deployment, or the data source was never synced. |
| *The selected source index provider cannot read source documents* | The source index is Lucene. Use an Elasticsearch or Azure AI Search index. |
| Filter has no effect | The knowledge base is Azure AI Search, or the string needs `.keyword` on Elasticsearch. |
| External source misses changes | External sources sync only on **Sync index**, on save, or nightly. |

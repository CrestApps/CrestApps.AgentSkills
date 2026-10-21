---
name: crestapps-core-yessql-stores
description: Skill for the CrestApps.Core.Data.YesSql persistence backend in CrestApps.Core 2.0. Covers AddYesSqlDataStore and AddCoreYesSqlDataStore (IStore, collections, index provider registration, scoped ISession, YesSqlStoreCommitter), YesSqlStoreOptions collection names, per-feature AddYesSqlStores on every feature builder including personas and tool instances, the service-collection store methods for web crawlers, file sources, knowledge objects and ingestion item state, the full index and IndexProvider inventory, creating index tables with the Create*IndexSchemaAsync SchemaBuilder extensions, custom models with DocumentCatalog and CatalogItemIndex, and QueryContext paging. Use it when a host persists CrestApps.Core data with YesSql or troubleshoots missing tables, collections, or uncommitted writes.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core YesSql Stores

You are a CrestApps.Core expert. Generate accurate YesSql persistence code with `CrestApps.Core.Data.YesSql`.

## Rules

- Register `.AddYesSqlDataStore(configuration => configuration.UseSqLite(...))` on the root builder (or `services.AddCoreYesSqlDataStore(...)`). On first resolution it creates the singleton `IStore`, initializes the `AI`, `AIDocs`, `AIMemory` and default collections plus any collection an index provider names, and registers every `IIndexProvider` in DI. It also registers scoped `ISession`, scoped `YesSqlStoreCommitter` as `IStoreCommitter`, and `AddCatalogManagers()`.
- `AddYesSqlStores()` on a feature builder only selects stores and index providers. Without `AddYesSqlDataStore(...)` nothing can resolve `ISession`.
- Register custom `IIndexProvider` implementations before the `IStore` is first resolved, or they are never registered with the store.
- The package does not create index tables. Run the `Create*IndexSchemaAsync` extensions once per database before serving requests (the samples' `InitializeYesSqlSchemaAsync()` is sample code).
- Always pass the resolved `YesSqlStoreOptions` to the schema extensions. They read collection names from it; passing `null` creates the tables in the root collection while the index providers query the `AI` collection.
- `YesSqlStoreCommitter` flushes the scoped `ISession`. Add the MVC, hub or endpoint commit filter, or commit explicitly in background scopes.

## Registration

```csharp
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.Data.YesSql;
using YesSql.Provider.Sqlite;

builder.Services.AddControllersWithViews()
    .AddCrestAppsStoreCommitterFilter();

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddYesSqlStores()
        .AddChatInteractions(chat => chat.AddYesSqlStores())
        .AddDocumentProcessing(documents => documents.AddYesSqlStores())
        .AddAIMemory(memory => memory.AddYesSqlStores())
        .AddPersonas(personas => personas.AddYesSqlStores())
        .AddToolInstances(tools => tools.AddYesSqlStores()))
    .AddIndexingServices(indexing => indexing.AddYesSqlStores())
    .AddYesSqlDataStore(configuration => configuration
        .UseSqLite("Data Source=App_Data/crestapps.db;Cache=Shared")));

builder.Services.AddCoreWebCrawlerStoresYesSql();
builder.Services.AddCoreFileSourceStoresYesSql();
```

`AddYesSqlStores()` also exists on `CrestAppsA2AClientBuilder`, `CrestAppsMcpClientBuilder` and `CrestAppsMcpServerBuilder`.

## Collections

| `YesSqlStoreOptions` property | Default | Indexes stored there |
|---|---|---|
| `AICollectionName` | `"AI"` | profiles, templates, personas, connections, deployments, chat sessions, prompts, metrics, completion usage, extracted data, chat interactions, data sources, tool instances, A2A and MCP records, file sources, ingestion item state, web crawlers, crawl state, knowledge objects |
| `AIDocsCollectionName` | `"AIDocs"` | `AIDocument`, `AIDocumentChunk` |
| `AIMemoryCollectionName` | `"AIMemory"` | `AIMemoryEntry` |
| `DefaultCollectionName` | `null` (root) | `SearchIndexProfile`, and custom catalogs registered without a collection |

Change them with `builder.Services.Configure<YesSqlStoreOptions>(...)` before the store is first resolved, and use the same options when creating tables.

## Create the Index Tables

```csharp
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.A2A;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.Core.Data.YesSql.Indexes.AIMemory;
using CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;
using CrestApps.Core.Data.YesSql.Indexes.DataSources;
using CrestApps.Core.Data.YesSql.Indexes.FileSources;
using CrestApps.Core.Data.YesSql.Indexes.Indexing;
using CrestApps.Core.Data.YesSql.Indexes.Knowledge;
using CrestApps.Core.Data.YesSql.Indexes.Mcp;
using CrestApps.Core.Data.YesSql.Indexes.Tooling;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Sql;

namespace MyApp.Data;

public sealed class CrestAppsYesSqlSchema
{
    private readonly IStore _store;
    private readonly YesSqlStoreOptions _storeOptions;
    private readonly AIChatSessionMetricsIndexSchemaOptions _metricsOptions;
    private readonly ILogger<CrestAppsYesSqlSchema> _logger;

    public CrestAppsYesSqlSchema(
        IStore store,
        IOptions<YesSqlStoreOptions> storeOptions,
        IOptions<AIChatSessionMetricsIndexSchemaOptions> metricsOptions,
        ILogger<CrestAppsYesSqlSchema> logger)
    {
        _store = store;
        _storeOptions = storeOptions.Value;
        _metricsOptions = metricsOptions.Value;
        _logger = logger;
    }

    public async Task CreateAsync()
    {
        await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var schema = new SchemaBuilder(_store.Configuration, transaction);
        var options = _storeOptions;

        Func<Task>[] steps =
        [
            () => schema.CreateAIProviderConnectionIndexSchemaAsync(options),
            () => schema.CreateAIDeploymentIndexSchemaAsync(options),
            () => schema.CreateAIProfileIndexSchemaAsync(options),
            () => schema.CreateAIProfileTemplateIndexSchemaAsync(options),
            () => schema.CreateAIPersonaIndexSchemaAsync(options),
            () => schema.CreateAIChatSessionIndexSchemaAsync(options),
            () => schema.CreateAIChatSessionPromptIndexSchemaAsync(options),
            () => schema.CreateAIChatSessionMetricsSchemaAsync(options, _metricsOptions),
            () => schema.CreateAICompletionUsageIndexSchemaAsync(options),
            () => schema.CreateAIChatSessionExtractedDataIndexSchemaAsync(options),
            () => schema.CreateChatInteractionIndexSchemaAsync(options),
            () => schema.CreateChatInteractionPromptIndexSchemaAsync(options),
            () => schema.CreateAIDocumentIndexSchemaAsync(options),
            () => schema.CreateAIDocumentChunkIndexSchemaAsync(options),
            () => schema.CreateAIDataSourceIndexSchemaAsync(options),
            () => schema.CreateSearchIndexProfileIndexSchemaAsync(options),
            () => schema.CreateAIMemoryEntryIndexSchemaAsync(options),
            () => schema.CreateAIToolInstanceIndexSchemaAsync(options),
            () => schema.CreateA2AConnectionIndexSchemaAsync(options),
            () => schema.CreateMcpConnectionIndexSchemaAsync(options),
            () => schema.CreateMcpPromptIndexSchemaAsync(options),
            () => schema.CreateMcpResourceIndexSchemaAsync(options),
            () => schema.CreateWebCrawlerIndexSchemaAsync(options),
            () => schema.CreateWebCrawlStateIndexSchemaAsync(options),
            () => schema.CreateFileSourceIndexSchemaAsync(options),
            () => schema.CreateIngestionItemStateIndexSchemaAsync(options),
            () => schema.CreateKnowledgeObjectIndexSchemaAsync(options),
        ];

        foreach (var step in steps)
        {
            try
            {
                await step();
            }
            catch (Exception ex)
            {
                // The table usually exists already. Track a schema version in production instead.
                _logger.LogDebug(ex, "Skipped a CrestApps YesSql index table.");
            }
        }

        await transaction.CommitAsync();
    }
}
```

```csharp
builder.Services.AddSingleton<CrestAppsYesSqlSchema>();

var app = builder.Build();
await app.Services.GetRequiredService<CrestAppsYesSqlSchema>().CreateAsync();
```

Keep only the steps for the features you registered. The skip-on-error loop mirrors the SQLite sample; on PostgreSQL or SQL Server a failed statement aborts the transaction, so record a schema version and run each step once instead. For existing databases, the column-upgrade extensions `AddAIProfileIndexTypeColumnAsync`, `AddAIDocumentIndexExtensionColumnAsync` and `AddAIChatSessionMetricsCompletionCountColumnAsync` add later columns.

## Store and Index Inventory

| Method | Stores and index providers |
|---|---|
| `AddCoreAIServicesStoresYesSql()` | `YesSqlAIProfileStore`; `AIProviderConnection` and `AIDeployment` binding sources; `AIProfileIndexProvider`, `AIProviderConnectionIndexProvider`, `AIDeploymentIndexProvider` |
| `AddCoreAIProfileTemplateStoresYesSql()` | `AIProfileTemplate` catalog, `AIProfileTemplateIndexProvider` |
| `AddCoreAIPersonaStoresYesSql()` | `AIPersona` named catalog, `AIPersonaIndexProvider` |
| `AddCoreAIChatSessionStoresYesSql()` | runs the base, metrics, completion usage and extracted data methods below |
| `AddCoreAIChatSessionBaseStoresYesSql()` | `YesSqlAIChatSessionManager`, `YesSqlAIChatSessionStore`, `YesSqlAIChatSessionPromptStore`; session and prompt index providers |
| `AddCoreAIChatSessionMetricsStoresYesSql()` | `YesSqlAIChatSessionEventStore`, `AIChatSessionMetricsIndexProvider` |
| `AddCoreAICompletionUsageStoresYesSql()` | `YesSqlAICompletionUsageStore`, `AICompletionUsageIndexProvider` |
| `AddCoreAIChatSessionExtractedDataStoresYesSql()` | `YesSqlAIChatSessionExtractedDataStore`, extracted data recorder and index provider |
| `AddCoreAIChatInteractionStoresYesSql()` | `ChatInteraction` catalog, `YesSqlChatInteractionPromptStore`, both index providers |
| `AddCoreAIDocumentProcessingStoresYesSql()` | `YesSqlAIDocumentStore`, `YesSqlAIDocumentChunkStore`, both index providers |
| `AddCoreAIDataSourceStoresYesSql()` | `YesSqlAIDataSourceStore`, `AIDataSourceIndexProvider` |
| `AddCoreAIMemoryStoresYesSql()` | `YesSqlAIMemoryStore`, `AIMemoryEntryIndexProvider` |
| `AddCoreIndexingStoresYesSql()` | `YesSqlSearchIndexProfileStore`, `SearchIndexProfileIndexProvider` |
| `AddCoreAIToolInstanceStoresYesSql()` | `AIToolInstance` catalog, `AIToolInstanceIndexProvider` |
| `AddCoreAIA2AClientStoresYesSql()` | `A2AConnection` catalog, `A2AConnectionIndexProvider` |
| `AddCoreAIMcpClientStoresYesSql()` | `McpConnection` catalog, `McpConnectionIndexProvider` |
| `AddCoreAIMcpServerStoresYesSql()` | `McpPrompt` and `McpResource` catalogs and index providers |
| `AddCoreKnowledgeStoresYesSql()` | `YesSqlKnowledgeObjectStore`, `KnowledgeObjectIndexProvider` |
| `AddCoreIngestionItemStateStoreYesSql()` | `YesSqlIngestionItemStateStore`, `IngestionItemStateIndexProvider` |
| `AddCoreWebCrawlerStoresYesSql()` | `YesSqlWebCrawlerStore`, `YesSqlWebCrawlStateStore`, their index providers, plus knowledge and ingestion item state |
| `AddCoreFileSourceStoresYesSql()` | `YesSqlFileSourceStore`, `FileSourceIndexProvider`, plus knowledge and ingestion item state |

Each index type and its provider live in separate files under `Indexes/{Feature}` (namespace `CrestApps.Core.Data.YesSql.Indexes.{Feature}`), with a matching `*IndexSchemaBuilderExtensions` class. `INameAwareIndex`, `IDisplayTextAwareIndex` and `ISourceAwareIndex` mark the lookup columns `DocumentCatalog` filters on; `CatalogItemIndex` (with `ItemId`) is the shared base.

## Custom Models

```csharp
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using Microsoft.Extensions.Options;
using YesSql.Indexes;

namespace MyApp.Data;

public sealed class TicketCategoryIndex : CatalogItemIndex, INameAwareIndex
{
    public string Name { get; set; }
}

public sealed class TicketCategoryIndexProvider : IndexProvider<TicketCategory>
{
    public TicketCategoryIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    public override void Describe(DescribeContext<TicketCategory> context)
    {
        context.For<TicketCategoryIndex>()
            .Map(category => new TicketCategoryIndex
            {
                ItemId = category.ItemId,
                Name = category.Name,
            });
    }
}
```

```csharp
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IIndexProvider, TicketCategoryIndexProvider>());
builder.Services.AddYesSqlNamedDocumentCatalog<TicketCategory, TicketCategoryIndex>("AI");
```

Create the table with `schema.CreateMapIndexTableAsync<TicketCategoryIndex>(table => table.Column<string>("ItemId", c => c.WithLength(26)).Column<string>("Name", c => c.WithLength(255)), collection: "AI")`. The catalog, index provider and table must name the same collection, or writes and queries silently miss each other. The other helpers are `AddYesSqlDocumentCatalog`, `AddYesSqlSourceDocumentCatalog`, `AddYesSqlNamedSourceDocumentCatalog`, and the binding-source helpers `AddYesSqlNamedSourceBindingSource<TModel, TIndex>()` and `AddYesSqlNamedBindingSource<TModel, TIndex>()`; each has a parameterless overload that uses `DefaultCollectionName`.

## Paging

`PageAsync(page, pageSize, new QueryContext { Name = search, Source = source, Sorted = true })` filters on the concrete index's `Name` (or `DisplayText`) and `Source` columns and translates to SQL `OFFSET`/`LIMIT`.

## Upgrading from 1.x

- `DocumentCatalog.PageAsync` with a name or source filter used to throw `no such table: INameAwareIndex_Document`; it now builds the filter against the concrete index. Remove any workaround that avoided `QueryContext` filters.
- New index tables to create: `AIPersonaIndex`, `FileSourceIndex`, `IngestionItemStateIndex`, `KnowledgeObjectIndex`, `WebCrawlerIndex`, `WebCrawlStateIndex`. A host with web crawlers but no file sources still needs `IngestionItemStateIndex`.
- File sources are stored in their own `FileSourceIndex` records, not as web crawlers; a host that ran a preview build moves those records itself.

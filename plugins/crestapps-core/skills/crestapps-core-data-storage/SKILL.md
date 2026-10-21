---
name: crestapps-core-data-storage
description: Skill for choosing and registering CrestApps.Core 2.0 persistence. Covers picking Entity Framework Core (CrestApps.Core.Data.EntityCore) or YesSql (CrestApps.Core.Data.YesSql), registering the backing data store on the root builder, per-feature AddEntityCoreStores and AddYesSqlStores calls on the AI suite, personas, chat interactions, document processing, memory, tool instances, A2A, MCP and indexing builders, the service-collection store calls for web crawlers, file sources, knowledge objects and ingestion item state, what the null fallback stores mean, commit boundaries through IStoreCommitter, how configuration and database deployments merge, and writing a custom backend. Use it when a host needs durable profiles, sessions, documents, connections, deployments, or crawler and file-source state.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Data Storage

You are a CrestApps.Core expert. Help the host choose a backend and register exactly the stores each enabled feature needs.

## Guidelines

- Choose Entity Framework Core when the host already uses EF Core or wants any EF relational provider. Choose YesSql when the host wants a document store with collections and map indexes (as Orchard Core does).
- Register the backing store once on the root builder: `.AddEntityCoreSqliteDataStore(...)` / `.AddEntityCoreDataStore(...)`, or `.AddYesSqlDataStore(...)`. The per-feature `Add*Stores()` calls only select stores; they never create the `DbContext` or `IStore`.
- Call the store method on every feature builder you enable. One call does not cover other features.
- Without a store package, connections and deployments come only from configuration and `ISearchIndexProfileStore` falls back to a null store, but nothing registers `IAIProfileStore`, the chat session stores, or the `ChatInteraction` catalog. Register a store before resolving `IAIProfileManager`, chat sessions, or Chat Interactions.
- Every write path needs a commit boundary (`IStoreCommitter`); see `crestapps-core-foundations`.
- Create the schema before serving requests: `InitializeEntityCoreSchemaAsync()` for EF Core, or the `Create*IndexSchemaAsync` extensions for YesSql.

## Entity Framework Core

```csharp
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.A2A;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.OpenXml;
using CrestApps.Core.AI.Documents.Pdf;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.Data.EntityCore;

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddEntityCoreStores()
        .AddOpenAI()
        .AddChatInteractions(chat => chat.AddEntityCoreStores())
        .AddDocumentProcessing(documents => documents
            .AddEntityCoreStores()
            .AddOpenXml()
            .AddPdf())
        .AddAIMemory(memory => memory.AddEntityCoreStores())
        .AddPersonas(personas => personas.AddEntityCoreStores())
        .AddToolInstances(tools => tools.AddEntityCoreStores())
        .AddA2AClient(a2a => a2a.AddEntityCoreStores())
        .AddMcpClient(mcp => mcp.AddEntityCoreStores())
        .AddMcpServer(mcp => mcp.AddEntityCoreStores()))
    .AddIndexingServices(indexing => indexing.AddEntityCoreStores())
    .AddEntityCoreSqliteDataStore(
        $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "crestapps.db")}"));

// Features registered on IServiceCollection
builder.Services.AddCoreWebCrawlerStoresEntityCore();
builder.Services.AddCoreFileSourceStoresEntityCore();

var app = builder.Build();
await app.Services.InitializeEntityCoreSchemaAsync();
```

## YesSql

```csharp
using CrestApps.Core.Data.YesSql;
using YesSql.Provider.Sqlite;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddYesSqlStores()
        .AddOpenAI()
        .AddChatInteractions(chat => chat.AddYesSqlStores())
        .AddDocumentProcessing(documents => documents.AddYesSqlStores())
        .AddAIMemory(memory => memory.AddYesSqlStores())
        .AddPersonas(personas => personas.AddYesSqlStores())
        .AddToolInstances(tools => tools.AddYesSqlStores())
        .AddA2AClient(a2a => a2a.AddYesSqlStores())
        .AddMcpClient(mcp => mcp.AddYesSqlStores())
        .AddMcpServer(mcp => mcp.AddYesSqlStores()))
    .AddIndexingServices(indexing => indexing.AddYesSqlStores())
    .AddYesSqlDataStore(configuration => configuration
        .UseSqLite("Data Source=App_Data/crestapps.db;Cache=Shared")));

builder.Services.AddCoreWebCrawlerStoresYesSql();
builder.Services.AddCoreFileSourceStoresYesSql();
```

YesSql hosts must also create every index table for the stores they registered; see `crestapps-core-yessql-stores`.

## What Each Store Call Registers

| Feature | Call | Stores |
|---|---|---|
| AI suite | `CrestAppsAISuiteBuilder.Add*Stores()` | `IAIProfileStore` and the `AIProfile` catalogs, `AIProfileTemplate` catalog, writable `AIProviderConnection` and `AIDeployment` binding sources, `IAIChatSessionManager`, `IAIChatSessionStore`, `IAIChatSessionPromptStore`, `IAIChatSessionEventStore`, `IAICompletionUsageStore`, `IAIChatSessionExtractedDataStore` |
| Personas | `CrestAppsAIPersonasBuilder.Add*Stores()` | `AIPersona` named catalog |
| Chat Interactions | `CrestAppsChatInteractionsBuilder.Add*Stores()` | `ChatInteraction` catalog, `IChatInteractionPromptStore` |
| Document processing | `CrestAppsDocumentProcessingBuilder.Add*Stores()` | `IAIDocumentStore`, `IAIDocumentChunkStore`, `IAIDataSourceStore` |
| AI memory | `CrestAppsAIMemoryBuilder.Add*Stores()` | `IAIMemoryStore` |
| Tool instances | `CrestAppsAIToolInstancesBuilder.Add*Stores()` | `AIToolInstance` named-source catalog |
| A2A client | `CrestAppsA2AClientBuilder.Add*Stores()` | `A2AConnection` catalog |
| MCP client | `CrestAppsMcpClientBuilder.Add*Stores()` | `McpConnection` source catalog |
| MCP server | `CrestAppsMcpServerBuilder.Add*Stores()` | `McpPrompt` named catalog, `McpResource` source catalog |
| Indexing | `CrestAppsIndexingBuilder.Add*Stores()` | `ISearchIndexProfileStore` |
| Web crawlers | `AddCoreWebCrawlerStoresEntityCore()` / `AddCoreWebCrawlerStoresYesSql()` | `IWebCrawlerStore`, `IWebCrawlStateStore`, plus ingestion item state and knowledge stores |
| File sources | `AddCoreFileSourceStoresEntityCore()` / `AddCoreFileSourceStoresYesSql()` | `IFileSourceStore`, plus ingestion item state and knowledge stores |
| Knowledge objects | `AddCoreKnowledgeStoresEntityCore()` / `AddCoreKnowledgeStoresYesSql()` | `IKnowledgeObjectStore` |
| Ingestion item state | `AddCoreIngestionItemStateStoreEntityCore()` / `AddCoreIngestionItemStateStoreYesSql()` | `IIngestionItemStateStore` |

- `IKnowledgeObjectStore` has no fallback. File data sources, the knowledge figure endpoint, knowledge tool instances and MCP knowledge resources need it, so call the knowledge store method directly when the host has none of the crawler or file-source stores.
- The knowledge and ingestion-state calls are safe to repeat; the crawler and file-source calls include them.
- For finer control, every builder method maps to an `IServiceCollection` method such as `AddCoreAIServicesStoresEntityCore()` or `AddCoreAIChatSessionBaseStoresYesSql()`; the backend skills list them.

## Configuration and Database Records

`IAIDeploymentStore` and `IAIProviderConnectionStore` are merged views. The configuration binding sources (`CrestApps:AI:Deployments`, `CrestApps:AI:Connections`, `Order = 100`) are always present; the store packages add a writable database source at `Order = 0`. When both define the same name, the database record wins, and all writes go to the database. Resolve `INamedSourceCatalog<AIDeployment>` when you need only the database-backed records. Custom sources are covered in `crestapps-core-foundations`.

## Custom Backend

Implement the catalog interfaces (`ICatalog<T>` and, where the model supports it, `INamedCatalog<T>`, `ISourceCatalog<T>`, `INamedSourceCatalog<T>`) and the store interfaces the features resolve (for example `IAIChatSessionManager`, `IAIDocumentStore`), register them as scoped, and register a scoped `IStoreCommitter` when writes are staged. Register `AddCatalogManagers()` so managers and handlers run over your catalogs.

## Upgrading from 1.x

- New store calls in 2.0: personas (`AddPersonas(p => p.Add*Stores())`), web crawlers, file sources, knowledge objects and ingestion item state. File sources are their own records and are no longer stored as web crawlers; a host that ran a preview build moves those records itself.
- A YesSql host with web crawlers alone must also create the `IngestionItemStateIndex` table (`CreateIngestionItemStateIndexSchemaAsync`).
- Stored deployment records that still carry a legacy purpose need no migration; they are projected onto capabilities on read. Only a host that copied the purpose into its own column needs its own migration.
- Usage records gained new fields (`OperationType`, cached, reasoning and audio token counts, and others) inside the record document; existing records need no migration.

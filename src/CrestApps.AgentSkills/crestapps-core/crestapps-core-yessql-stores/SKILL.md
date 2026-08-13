---
name: crestapps-core-yessql-stores
description: Skill for configuring the YesSql persistence backend and per-feature stores in CrestApps.Core.
---

# CrestApps.Core YesSql Stores - Prompt Templates

## Configure YesSql Persistence

You are a CrestApps.Core expert. Generate accurate YesSql persistence guidance using `CrestApps.Core.Data.YesSql`.

### Rules

- Register `AddYesSqlDataStore(...)` on the root `CrestAppsCoreBuilder`. It calls `AddCoreYesSqlDataStore(...)`, creates the singleton `IStore`, initializes configured collections, registers `IIndexProvider` instances, creates scoped `ISession`, and registers `YesSqlStoreCommitter`.
- `AddYesSqlStores()` only selects stores for a feature. It does **not** create the `IStore`; without `AddYesSqlDataStore(...)`, YesSql stores cannot resolve their session.
- `YesSqlStoreCommitter` flushes the request-scoped `ISession` through `IStoreCommitter.CommitAsync()`. Add `AddCrestAppsStoreCommitterFilter()` to MVC, `StoreCommitterEndpointFilter` to Minimal API groups, or commit explicitly in a background scope.
- Configure collections with `YesSqlStoreOptions`: `DefaultCollectionName`, `AICollectionName` (default `"AI"`), `AIMemoryCollectionName` (default `"AIMemory"`), and `AIDocsCollectionName` (default `"AIDocs"`).

```csharp
builder.Services.AddControllersWithViews()
    .AddCrestAppsStoreCommitterFilter();

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddYesSqlStores()
        .AddChatInteractions(chat => chat.AddYesSqlStores())
        .AddDocumentProcessing(documents => documents.AddYesSqlStores())
        .AddAIMemory(memory => memory.AddYesSqlStores())
    )
    .AddIndexingServices(indexing => indexing.AddYesSqlStores())
    .AddYesSqlDataStore(configuration => configuration
        .UseSqLite("Data Source=App_Data/crestapps.db;Cache=Shared"))
);
```

## Per-Feature Registration

Use the matching builder extension only for enabled features:

| Builder | Store registration |
|---|---|
| `CrestAppsAISuiteBuilder` | `.AddYesSqlStores()` |
| `CrestAppsChatInteractionsBuilder` | `.AddYesSqlStores()` |
| `CrestAppsDocumentProcessingBuilder` | `.AddYesSqlStores()` |
| `CrestAppsAIMemoryBuilder` | `.AddYesSqlStores()` |
| `CrestAppsIndexingBuilder` | `.AddYesSqlStores()` |
| `CrestAppsA2AClientBuilder` | `.AddYesSqlStores()` |
| `CrestAppsMcpClientBuilder` or `CrestAppsMcpServerBuilder` | `.AddYesSqlStores()` |
| `CrestAppsAIToolInstancesBuilder` | `.AddYesSqlStores()` |

For lower-level composition, use `AddCoreAIServicesStoresYesSql`, `AddCoreAIToolInstanceStoresYesSql`, `AddCoreAIProfileTemplateStoresYesSql`, `AddCoreAIA2AClientStoresYesSql`, `AddCoreAIMcpClientStoresYesSql`, `AddCoreAIMcpServerStoresYesSql`, `AddCoreAIChatSessionStoresYesSql`, `AddCoreAIChatSessionBaseStoresYesSql`, `AddCoreAIChatSessionMetricsStoresYesSql`, `AddCoreAICompletionUsageStoresYesSql`, `AddCoreAIChatSessionExtractedDataStoresYesSql`, `AddCoreAIDocumentProcessingStoresYesSql`, `AddCoreAIDataSourceStoresYesSql`, `AddCoreAIMemoryStoresYesSql`, `AddCoreAIChatInteractionStoresYesSql`, or `AddCoreIndexingStoresYesSql`.

## Store and Index Inventory

`AddCoreAIServicesStoresYesSql()` registers `YesSqlAIProfileStore` and named-source bindings for provider connections and deployments. The feature-specific methods register `YesSqlAIChatSessionManager`, `YesSqlAIChatSessionStore`, `YesSqlAIChatSessionPromptStore`, `YesSqlAIChatSessionEventStore`, `YesSqlAIChatSessionExtractedDataStore`, `YesSqlAICompletionUsageStore`, `YesSqlChatInteractionPromptStore`, `YesSqlAIDocumentStore`, `YesSqlAIDocumentChunkStore`, `YesSqlAIDataSourceStore`, `YesSqlAIMemoryStore`, and `YesSqlSearchIndexProfileStore` as needed.

Catalog helpers are `DocumentCatalog<T, TIndex>`, `NamedDocumentCatalog<T, TIndex>`, `SourceDocumentCatalog<T, TIndex>`, and `NamedSourceDocumentCatalog<T, TIndex>`. Use the corresponding `AddYesSql*DocumentCatalog` or `AddYesSql*BindingSource` extension only when implementing a custom model and its index.

The built-in index families are `A2AConnection`, `AIProfile`, `AIProfileTemplate`, `AIProviderConnection`, `AIDeployment`, `AIChatSession`, `AIChatSessionPrompt`, `AIChatSessionMetrics`, `AIChatSessionExtractedData`, `AICompletionUsage`, `AIMemoryEntry`, `ChatInteraction`, `ChatInteractionPrompt`, `AIDataSource`, `AIDocument`, `AIDocumentChunk`, `SearchIndexProfile`, `AIToolInstance`, `McpConnection`, `McpPrompt`, and `McpResource`. Each has a matching `*Index` and `*IndexProvider` under `Indexes/`; `CatalogItemIndex` is their shared base where applicable.

`INameAwareIndex`, `IDisplayTextAwareIndex`, and `ISourceAwareIndex` are the shared index capabilities. `AIChatSessionMetricsIndexSchemaOptions` controls the chat-session metrics index schema. Register custom `IIndexProvider` implementations before the `IStore` is first resolved so `AddCoreYesSqlDataStore(...)` can initialize their collections and register them.

## Schema Initialization

The `IStore` is created and initialized when DI first resolves it. Unlike EntityCore, `CrestApps.Core.Data.YesSql` does not provide a package-level `InitializeYesSqlSchemaAsync()` extension. The sample hosts define that helper themselves. Custom hosts must provision their selected YesSql dialect and schema according to their own migration strategy before handling requests.

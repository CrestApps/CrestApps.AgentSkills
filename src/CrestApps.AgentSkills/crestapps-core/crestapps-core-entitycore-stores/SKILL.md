---
name: crestapps-core-entitycore-stores
description: Skill for configuring the Entity Framework Core persistence backend and per-feature stores in CrestApps.Core.
---

# CrestApps.Core EntityCore Stores - Prompt Templates

## Configure Entity Framework Core Persistence

You are a CrestApps.Core expert. Generate accurate Entity Framework Core persistence guidance using `CrestApps.Core.Data.EntityCore`.

### Rules

- Register `AddEntityCoreDataStore(...)` or `AddEntityCoreSqliteDataStore(...)` on the root `CrestAppsCoreBuilder`. They register `CrestAppsEntityDbContext` and `EntityCoreStoreCommitter`.
- `AddEntityCoreStores()` selects feature stores. It is separate from registering the `DbContext`.
- `EntityCoreStoreCommitter` calls `SaveChangesAsync()` only when `ChangeTracker.HasChanges()` is true. Use the MVC, Minimal API, SignalR, or explicit background-scope commit boundary.
- `EntityCoreDataStoreOptions.TablePrefix` defaults to `"CA_"`. `EnforceNamedSourceUniqueness` defaults to `false`; turn it on only after removing duplicates and applying an EF Core migration.
- Extend the model by implementing `ICrestAppsModelConfigurer`; do not subclass the sealed `CrestAppsEntityDbContext`.

```csharp
builder.Services.AddControllersWithViews()
    .AddCrestAppsStoreCommitterFilter();

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddEntityCoreStores()
        .AddChatInteractions(chat => chat.AddEntityCoreStores())
        .AddDocumentProcessing(documents => documents.AddEntityCoreStores())
        .AddAIMemory(memory => memory.AddEntityCoreStores())
    )
    .AddIndexingServices(indexing => indexing.AddEntityCoreStores())
    .AddEntityCoreSqliteDataStore(
        $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "crestapps.db")}")
);
```

For another EF provider, configure it directly:

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddEntityCoreDataStore(
        options => options.UseNpgsql(connectionString),
        store => store.TablePrefix = "App_")
);
```

## Per-Feature Registration

The builder extensions are available on `CrestAppsAISuiteBuilder`, `CrestAppsChatInteractionsBuilder`, `CrestAppsDocumentProcessingBuilder`, `CrestAppsAIMemoryBuilder`, `CrestAppsIndexingBuilder`, `CrestAppsA2AClientBuilder`, `CrestAppsMcpClientBuilder`, `CrestAppsMcpServerBuilder`, and `CrestAppsAIToolInstancesBuilder`.

For one-call registration of all built-in EntityCore stores, call `services.AddEntityCoreStores()`. Lower-level APIs include `AddCoreAIServicesStoresEntityCore`, `AddCoreAIProfileTemplateStoresEntityCore`, `AddCoreAIChatSessionStoresEntityCore`, `AddCoreAIDocumentProcessingStoresEntityCore`, `AddCoreAIDataSourceStoresEntityCore`, `AddCoreAIMemoryStoresEntityCore`, `AddCoreAIChatInteractionStoresEntityCore`, and `AddCoreIndexingStoresEntityCore`.

## Store Inventory

The backend implementations are `EntityCoreAIProfileStore`, `EntityCoreAIChatSessionManager`, `EntityCoreAIChatSessionStore`, `EntityCoreAIChatSessionPromptStore`, `EntityCoreAIChatSessionEventStore`, `EntityCoreAIChatSessionExtractedDataStore`, `EntityCoreAICompletionUsageStore`, `EntityCoreChatInteractionPromptStore`, `EntityCoreAIDocumentStore`, `EntityCoreAIDocumentChunkStore`, `EntityCoreAIDataSourceStore`, `EntityCoreAIMemoryStore`, and `EntityCoreSearchIndexProfileStore`.

Shared catalog implementations are `DocumentCatalog<T>`, `NamedDocumentCatalog<T>`, `SourceDocumentCatalog<T>`, and `NamedSourceDocumentCatalog<T>`. Use `AddDocumentCatalog`, `AddNamedDocumentCatalog`, `AddSourceDocumentCatalog`, `AddNamedSourceDocumentCatalog`, `AddEntityCoreNamedSourceBindingSource`, or `AddEntityCoreNamedBindingSource` when composing custom catalog bindings.

`CrestAppsEntityDbContext` exposes `Documents`, `CatalogRecords`, `AIChatSessionRecords`, `AIChatSessionEventRecords`, `AICompletionUsageRecords`, and `AIChatSessionExtractedDataRecords`. Apply migrations or call your host's initialization strategy before serving requests; `InitializeEntityCoreSchemaAsync()` in the sample host is not a generic package API.

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

For one-call registration of all built-in EntityCore stores, call `services.AddEntityCoreStores()`. Its lower-level selection methods are `AddCoreAIServicesStoresEntityCore`, `AddCoreAIToolInstanceStoresEntityCore`, `AddCoreAIProfileTemplateStoresEntityCore`, `AddCoreAIA2AClientStoresEntityCore`, `AddCoreAIMcpClientStoresEntityCore`, `AddCoreAIMcpServerStoresEntityCore`, `AddCoreAIChatSessionStoresEntityCore`, `AddCoreAIChatSessionExtractedDataStoresEntityCore`, `AddCoreAIDocumentProcessingStoresEntityCore`, `AddCoreAIDataSourceStoresEntityCore`, `AddCoreAIMemoryStoresEntityCore`, `AddCoreAIChatInteractionStoresEntityCore`, and `AddCoreIndexingStoresEntityCore`.

## Store Inventory

`AddCoreAIServicesStoresEntityCore()` registers `EntityCoreAIProfileStore` and EntityCore named-source bindings for provider connections and deployments. The feature-specific methods register `EntityCoreAIChatSessionManager`, `EntityCoreAIChatSessionStore`, `EntityCoreAIChatSessionPromptStore`, `EntityCoreAIChatSessionEventStore`, `EntityCoreAIChatSessionExtractedDataStore`, `EntityCoreAICompletionUsageStore`, `EntityCoreChatInteractionPromptStore`, `EntityCoreAIDocumentStore`, `EntityCoreAIDocumentChunkStore`, `EntityCoreAIDataSourceStore`, `EntityCoreAIMemoryStore`, and `EntityCoreSearchIndexProfileStore` as appropriate.

The implementation also uses `DocumentCatalog<T>`, `NamedDocumentCatalog<T>`, `SourceDocumentCatalog<T>`, and `NamedSourceDocumentCatalog<T>` for tool instances, profile templates, A2A connections, MCP connections, MCP prompts, MCP resources, and chat interactions. Use the matching catalog helper or `AddEntityCoreNamedSourceBindingSource` / `AddEntityCoreNamedBindingSource` only when composing a custom binding.

`CrestAppsEntityDbContext` exposes `Documents`, `CatalogRecords`, `AIChatSessionRecords`, `AIChatSessionEventRecords`, `AICompletionUsageRecords`, and `AIChatSessionExtractedDataRecords`. `InitializeEntityCoreSchemaAsync()` is a public `CrestApps.Core.Data.EntityCore` service-provider extension. It calls `EnsureCreatedAsync()` and performs the package's schema initialization work; use EF Core migrations instead when that is the host's schema-management strategy.

---
name: crestapps-core-entitycore-stores
description: Skill for the CrestApps.Core.Data.EntityCore persistence backend in CrestApps.Core 2.0. Covers AddEntityCoreSqliteDataStore and AddEntityCoreDataStore with any EF Core provider, EntityCoreDataStoreOptions (TablePrefix, EnforceNamedSourceUniqueness), CrestAppsEntityDbContext and its tables, EntityCoreStoreCommitter, per-feature AddEntityCoreStores on every feature builder including personas and tool instances, the service-collection store methods for web crawlers, file sources, knowledge objects and ingestion item state, generic document catalogs for custom models, binding sources, InitializeEntityCoreSchemaAsync, and extending the model with ICrestAppsModelConfigurer. Use it when a host persists CrestApps.Core data with Entity Framework Core or troubleshoots EF Core stores.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core EntityCore Stores

You are a CrestApps.Core expert. Generate accurate Entity Framework Core persistence code with `CrestApps.Core.Data.EntityCore`.

## Rules

- Register the data store on the root builder with `.AddEntityCoreSqliteDataStore(connectionString, tablePrefix: "CA_")` or `.AddEntityCoreDataStore(options => options.UseNpgsql(...), store => store.TablePrefix = "App_")`. They map to `AddCoreEntityCoreSqliteDataStore(...)` and `AddCoreEntityCoreDataStore(...)` on `IServiceCollection` and register `CrestAppsEntityDbContext` plus scoped `EntityCoreStoreCommitter`.
- `AddEntityCoreStores()` on a feature builder selects that feature's stores. It never registers the `DbContext`.
- `EntityCoreStoreCommitter.CommitAsync()` calls `SaveChangesAsync()` only when `ChangeTracker.HasChanges()` is true. Writes that never reach a commit boundary are lost when the scope ends.
- `EntityCoreDataStoreOptions.TablePrefix` defaults to `"CA_"`. `EnforceNamedSourceUniqueness` (default `false`) adds a unique index on entity type, source and name; enable it only after removing duplicates.
- `CrestAppsEntityDbContext` is sealed. Extend the model through `ICrestAppsModelConfigurer`.
- Call `await app.Services.InitializeEntityCoreSchemaAsync()` after `Build()` when the host lets the package manage the schema. It runs `EnsureCreatedAsync()`, upgrades the pre-1.0 layout, and adds optional tables and catalog columns introduced later. Use EF Core migrations instead when the host owns its schema.

## Registration

```csharp
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.Data.EntityCore;

builder.Services.AddControllersWithViews()
    .AddCrestAppsStoreCommitterFilter();

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddEntityCoreStores()
        .AddChatInteractions(chat => chat.AddEntityCoreStores())
        .AddDocumentProcessing(documents => documents.AddEntityCoreStores())
        .AddAIMemory(memory => memory.AddEntityCoreStores())
        .AddPersonas(personas => personas.AddEntityCoreStores())
        .AddToolInstances(tools => tools.AddEntityCoreStores()))
    .AddIndexingServices(indexing => indexing.AddEntityCoreStores())
    .AddEntityCoreDataStore(
        options => options.UseNpgsql(builder.Configuration.GetConnectionString("CrestApps")),
        store => store.TablePrefix = "App_"));

builder.Services.AddCoreWebCrawlerStoresEntityCore();
builder.Services.AddCoreFileSourceStoresEntityCore();
```

`AddEntityCoreStores()` also exists on `CrestAppsA2AClientBuilder`, `CrestAppsMcpClientBuilder` and `CrestAppsMcpServerBuilder`.

## Service-Collection Methods

| Method | Registers |
|---|---|
| `AddCoreAIServicesStoresEntityCore()` | `EntityCoreAIProfileStore` as `IAIProfileStore` and every `AIProfile` catalog interface; writable binding sources for `AIProviderConnection` and `AIDeployment` |
| `AddCoreAIProfileTemplateStoresEntityCore()` | `AIProfileTemplate` named-source catalog |
| `AddCoreAIPersonaStoresEntityCore()` | `AIPersona` named catalog |
| `AddCoreAIToolInstanceStoresEntityCore()` | `AIToolInstance` named-source catalog |
| `AddCoreAIA2AClientStoresEntityCore()` | `A2AConnection` catalog |
| `AddCoreAIMcpClientStoresEntityCore()` | `McpConnection` source catalog |
| `AddCoreAIMcpServerStoresEntityCore()` | `McpPrompt` named catalog, `McpResource` source catalog |
| `AddCoreAIChatSessionStoresEntityCore()` | `EntityCoreAIChatSessionManager`, `EntityCoreAIChatSessionStore`, `EntityCoreAIChatSessionPromptStore`, `EntityCoreAIChatSessionEventStore`, `EntityCoreAICompletionUsageStore`, and the extracted-data store |
| `AddCoreAIChatSessionExtractedDataStoresEntityCore()` | `EntityCoreAIChatSessionExtractedDataStore` and the extracted-data recorder |
| `AddCoreAIChatInteractionStoresEntityCore()` | `ChatInteraction` catalog, `EntityCoreChatInteractionPromptStore` |
| `AddCoreAIDocumentProcessingStoresEntityCore()` | `EntityCoreAIDocumentStore`, `EntityCoreAIDocumentChunkStore` |
| `AddCoreAIDataSourceStoresEntityCore()` | `EntityCoreAIDataSourceStore` |
| `AddCoreAIMemoryStoresEntityCore()` | `EntityCoreAIMemoryStore` |
| `AddCoreIndexingStoresEntityCore()` | `EntityCoreSearchIndexProfileStore` |
| `AddCoreKnowledgeStoresEntityCore()` | `EntityCoreKnowledgeObjectStore` as `IKnowledgeObjectStore` |
| `AddCoreIngestionItemStateStoreEntityCore()` | `EntityCoreIngestionItemStateStore` as `IIngestionItemStateStore` |
| `AddCoreWebCrawlerStoresEntityCore()` | `EntityCoreWebCrawlerStore`, `EntityCoreWebCrawlStateStore`, plus knowledge and ingestion item state |
| `AddCoreFileSourceStoresEntityCore()` | `EntityCoreFileSourceStore`, plus knowledge and ingestion item state |

`services.AddEntityCoreStores()` is a one-call shortcut that runs `AddCatalogManagers()` and the services, tool instance, profile template, A2A, MCP client, MCP server, chat session, document processing, data source, memory, chat interaction and indexing methods. It does **not** include personas, knowledge objects, ingestion item state, web crawlers or file sources; add those explicitly.

## Tables

`CrestAppsEntityDbContext` maps `Documents`, `CatalogRecords`, `AIChatSessions`, `AIChatSessionEvents`, `AICompletionUsage` and `AIChatSessionExtractedData`, each prefixed with `TablePrefix`. Catalog models are stored as JSON in `Documents` and indexed through `CatalogRecords` (entity type, item id, name, display text, source, and lookup columns). The context exposes them as `Documents`, `CatalogRecords`, `AIChatSessionRecords`, `AIChatSessionEventRecords`, `AICompletionUsageRecords` and `AIChatSessionExtractedDataRecords`.

## Custom Models

Generic catalogs in `CrestApps.Core.Data.EntityCore.Services` store any `CatalogItem` in the shared tables with no schema change:

```csharp
using CrestApps.Core;
using CrestApps.Core.Data.EntityCore.Services;

builder.Services.AddDocumentCatalog<Article, DocumentCatalog<Article>>();
builder.Services.AddNamedDocumentCatalog<TicketCategory, NamedDocumentCatalog<TicketCategory>>();
builder.Services.AddSourceDocumentCatalog<Connector, SourceDocumentCatalog<Connector>>();
builder.Services.AddNamedSourceDocumentCatalog<Widget, NamedSourceDocumentCatalog<Widget>>();
```

For a model that merges configuration and database entries, register the database side with `AddEntityCoreNamedSourceBindingSource<TModel>()` (named and source-aware) or `AddEntityCoreNamedBindingSource<TModel>()` (named only).

## Extend the Model

```csharp
using CrestApps.Core.Data.EntityCore;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Data;

public sealed class AuditEntry
{
    public int Id { get; set; }

    public string ItemId { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class AuditModelConfigurer : ICrestAppsModelConfigurer
{
    public void Configure(ModelBuilder modelBuilder, EntityCoreDataStoreOptions options)
    {
        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable($"{options.TablePrefix}AuditEntries");
            entity.HasKey(entry => entry.Id);
            entity.HasIndex(entry => entry.ItemId);
        });
    }
}
```

```csharp
builder.Services.AddSingleton<ICrestAppsModelConfigurer, AuditModelConfigurer>();
```

Configurers run inside `OnModelCreating` in registration order. Access the table through `dbContext.Set<AuditEntry>()`. `EnsureCreatedAsync()` creates tables only for a new database, so add tables to an existing database with a migration or your own script.

## Troubleshooting

- **Writes disappear**: no commit boundary ran. Add the MVC, hub or endpoint filter, or call `IStoreCommitter.CommitAsync()` in background scopes.
- **`IKnowledgeObjectStore` cannot be resolved**: call `AddCoreKnowledgeStoresEntityCore()` (or a web crawler or file source store method).
- **Duplicate names across sources**: clean them up before turning on `EnforceNamedSourceUniqueness`.

## Upgrading from 1.x

- New store methods: `AddCoreAIPersonaStoresEntityCore()`, `AddCoreKnowledgeStoresEntityCore()`, `AddCoreIngestionItemStateStoreEntityCore()`, `AddCoreWebCrawlerStoresEntityCore()`, `AddCoreFileSourceStoresEntityCore()`, and the persona builder overload.
- File sources are stored as their own entity type rather than as web crawlers; records written by a preview build that predates the split are not moved automatically.
- The knowledge store now filters by canonical identifier, root and type in the database.
- No deployment data migration is needed for the removed purpose; legacy values are projected onto capabilities when read.

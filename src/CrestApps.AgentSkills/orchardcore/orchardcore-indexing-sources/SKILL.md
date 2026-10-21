---
name: orchardcore-indexing-sources
description: Skill for registering Orchard Core indexing sources for search providers. Covers AddIndexingSource wrappers, AddLuceneIndexingSource, AddElasticsearchIndexingSource, AddAzureAISearchIndexingSource, OrchardCore.Contents integration, source metadata, options-gated registration, current index manager contracts, indexing task categories with IIndexingTaskManager, first-registration-wins keyed provider managers, end-to-end custom record indexing with NamedIndexingService, IDocumentIndexHandler, a background task and an IndexProfileHandlerBase for sync and reset, and how indexing sources differ from the data sources of data pipelines. Use this skill when requests mention Orchard Core Indexing Sources, AddElasticsearchIndexingSource, AddAzureAISearchIndexingSource, Register a new indexing source, indexing custom records, NamedIndexingService, or closely related Orchard Core implementation, setup, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Indexing Sources

An indexing source registers provider-specific index, document-index, and
index-name managers together with source metadata for the index-profile UI.
Use the provider's real wrapper when one exists.

## Elasticsearch Content Source

Orchard Core's Elasticsearch provider exposes
`AddElasticsearchIndexingSource`. Its content startup registers the source
with `IndexingConstants.ContentsIndexSource`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Handlers;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace MyModule;

[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    private readonly IStringLocalizer<ContentsStartup> _localizer;

    public ContentsStartup(IStringLocalizer<ContentsStartup> localizer)
    {
        _localizer = localizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<IndexingMigrations>();

        services
            .AddIndexProfileHandler<ElasticsearchContentIndexProfileHandler>()
            .AddElasticsearchIndexingSource(IndexingConstants.ContentsIndexSource, options =>
            {
                options.DisplayName = _localizer["Content in Elasticsearch"];
                options.Description = _localizer["Create an Elasticsearch index based on site contents."];
            });
    }
}
```

The generic primitive is
`AddIndexingSource<TIndexManager, TDocumentIndexManager, TIndexNameProvider>`.
The type parameters must implement `IIndexManager`,
`IDocumentIndexManager`, and `IIndexNameProvider`, respectively. The
registration uses keyed services by provider name, so a custom provider should
resolve its managers by that key.

Indexing tasks use a shared category string. Use
`IndexingConstants.ContentsIndexSource` for content records, or define a
stable category for another record source and use the same value when creating
tasks and reading them with `IIndexingTaskManager`. Categories are universal
across providers; they are not provider-specific index names.

```csharp
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

await indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(
    recordId,
    IndexingConstants.ContentsIndexSource,
    RecordIndexingTaskTypes.Update));
```

Content index field names are centralized in `ContentIndexingConstants` from
the `OrchardCore.Contents.Indexing` namespace. Use these constants instead of
duplicating names such as `Content.ContentItem.ContentType` or
`Content.ContentItem.FullText`.

Use its options-gated overload only when a source must be hidden until valid
provider configuration exists.

## Custom Providers

The following namespace and types are intentionally fictional placeholders
for a custom provider. They are not Orchard Core APIs and must be replaced by
the provider's own manager implementations:

```csharp
// Fictional Contoso provider example. S is an IStringLocalizer injected in the startup.
// DisplayName and Description are LocalizedString values.
services.AddIndexingSource<
    ContosoIndexManager,
    ContosoDocumentIndexManager,
    ContosoIndexNameProvider>(
    "Contoso",
    implementationType: "Products",
    options =>
    {
        options.DisplayName = S["Products in Contoso"];
        options.Description = S["Create a Contoso index for product records."];
    });
```

Do not treat `OrchardCore.OpenSearch` or `AddOpenSearchIndexingSource` as
built-in Orchard Core APIs; no such provider exists in Orchard Core 4.0.
The built-in wrappers are `AddLuceneIndexingSource`,
`AddElasticsearchIndexingSource` and `AddAzureAISearchIndexingSource`.
Pair a custom source with an
`IndexProfileHandlerBase` implementation only when it owns source-specific
defaults or mappings.

## Registration Rules

- `providerName` selects the keyed `IIndexManager`, `IDocumentIndexManager` and `IIndexNameProvider`; `implementationType` is the source category, stored as `IndexProfile.Type` and used as the indexing task category.
- The keyed managers are added with `TryAdd`, so the first registration for a provider name wins. To put a new record type into Lucene, Elasticsearch or Azure AI Search, call the provider wrapper with a new category (for example `AddLuceneIndexingSource("Products")`) and reuse the provider's managers. Implement your own managers only for a new provider name.
- The `CreateOrUpdateIndexProfile` step rejects a profile whose provider and type pair was never registered ("Unable to find a provider named ... with the type ...").

## Custom Record Source End to End

Indexing custom records (not content items) into an existing provider takes five pieces. The `IProductStore` and `Product` types below are your own.

1. Record tasks. Whenever a record changes, append a task to the log. `IIndexingTaskManager` queues tasks and writes them at the end of the shell scope, keeping one task per record and category.

```csharp
await _indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(
    product.Id,
    ProductIndexingService.Category,
    RecordIndexingTaskTypes.Update));
```

2. Indexing service. Derive from `NamedIndexingService` (namespace `OrchardCore.Indexing.Core`). The base class reads the task log for every profile of the category from each profile's last task id, under a distributed lock, in batches of `BatchSize` (100), runs every `IDocumentIndexHandler`, writes with `AddOrUpdateDocumentsAsync`, and advances the cursor. Return `null` for tasks to skip.

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;

public sealed class ProductIndexingService : NamedIndexingService
{
    public const string Category = "Products";

    private readonly IProductStore _productStore;

    public ProductIndexingService(
        IProductStore productStore,
        IIndexProfileStore indexProfileStore,
        IIndexingTaskManager indexingTaskManager,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        IServiceProvider serviceProvider,
        ILogger<ProductIndexingService> logger)
        : base(Category, indexProfileStore, indexingTaskManager, documentIndexHandlers, serviceProvider, logger)
    {
        _productStore = productStore;
    }

    protected override async Task<BuildDocumentIndexContext> GetBuildDocumentIndexAsync(IndexProfileEntryContext entry, RecordIndexingTask task)
    {
        if (task.Type != RecordIndexingTaskTypes.Update)
        {
            return null;
        }

        var product = await _productStore.FindByIdAsync(task.RecordId);

        if (product is null)
        {
            return null;
        }

        return new BuildDocumentIndexContext(
            new DocumentIndex(product.Id),
            product,
            [Category],
            entry.DocumentIndexManager.GetContentIndexSettings());
    }
}
```

3. Document handler. Map the record into the `DocumentIndex`; every handler sees every document, so check the record type.

```csharp
public sealed class ProductDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not Product product)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set("Products.Name", product.Name, DocumentIndexOptions.Store);
        context.DocumentIndex.Set("Products.Price", product.Price, DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
```

4. Processing trigger and profile lifecycle. Run the service from a background task, mirroring the built-in "Indexes Content Updater", and add an `IndexProfileHandlerBase` (namespace `OrchardCore.Indexing.Core.Handlers`) that syncs on `SynchronizedAsync` and rewinds the cursor on `ResetAsync`.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

[BackgroundTask(
    Title = "Products Index Updater",
    Schedule = "* * * * *",
    Description = "Indexes changed products.")]
public sealed class ProductIndexingBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ProductIndexingService>().ProcessRecordsForAllIndexesAsync();
}

public sealed class ProductIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ProductIndexingService _indexingService;

    public ProductIndexProfileHandler(IServiceProvider serviceProvider, ProductIndexingService indexingService)
    {
        _serviceProvider = serviceProvider;
        _indexingService = indexingService;
    }

    public override Task SynchronizedAsync(IndexProfileSynchronizedContext context)
        => context.IndexProfile.Type == ProductIndexingService.Category
            ? _indexingService.ProcessRecordsAsync([context.IndexProfile.Id])
            : Task.CompletedTask;

    public override async Task ResetAsync(IndexProfileResetContext context)
    {
        if (context.IndexProfile.Type != ProductIndexingService.Category)
        {
            return;
        }

        var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(context.IndexProfile.ProviderName);

        if (documentIndexManager is not null)
        {
            await documentIndexManager.SetLastTaskIdAsync(context.IndexProfile, 0);
        }
    }
}
```

5. Registration.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Lucene;
using OrchardCore.Modules;

[RequireFeatures("OrchardCore.Lucene")]
public sealed class ProductLuceneStartup : StartupBase
{
    private readonly IStringLocalizer S;

    public ProductLuceneStartup(IStringLocalizer<ProductLuceneStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ProductIndexingService>();
        services.AddScoped<IDocumentIndexHandler, ProductDocumentIndexHandler>();
        services.AddSingleton<IBackgroundTask, ProductIndexingBackgroundTask>();

        services
            .AddIndexProfileHandler<ProductIndexProfileHandler>()
            .AddLuceneIndexingSource(ProductIndexingService.Category, options =>
            {
                options.DisplayName = S["Products in Lucene"];
                options.Description = S["Create a Lucene index for product records."];
            });
    }
}
```

The worker path handles `Update` tasks only, like the content source. When a record is deleted, also remove it right away with `IDocumentIndexManager.DeleteDocumentsAsync(profile, [id])` for each profile of the category (`IIndexProfileStore.GetByTypeAsync(Category)`), the way content removal is handled.

## Indexing Sources vs Data Sources

Orchard Core 4.0 also has **data sources** (`IDataSource`, registered with
`services.AddDataSource<T>()` in the Data Pipelines Core feature). They are a
different extension point: a data source exposes tabular data sets that data
pipelines and reports read in batches with a user's permissions, while an
indexing source feeds documents into a search provider index from the
indexing task log. Use `orchardcore-data-sources` for the former.

## Provider Connection Settings

Provider connection settings that options-gated sources depend on are read
from `OrchardCore:Search:Elasticsearch` and `OrchardCore:Search:AzureAISearch`
since Orchard Core 4.0 (formerly `OrchardCore_Elasticsearch` and
`OrchardCore_AzureAISearch`). Azure AI Search options are refreshed through
`IOptionsMonitor<AzureAISearchDefaultOptions>` when the admin settings change.

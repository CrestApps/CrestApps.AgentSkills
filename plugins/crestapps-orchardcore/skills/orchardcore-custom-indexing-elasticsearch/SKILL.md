---
name: orchardcore-custom-indexing-elasticsearch
description: Skill for building a custom Elasticsearch index for arbitrary records in Orchard Core 4.0, following the CrestApps AI Memory pattern of CrestApps.OrchardCore 3.0. Covers a custom index profile type with AddElasticsearchIndexingSource, IndexProfileHandlerBase mappings with ElasticsearchIndexMetadata and dense vectors, an embedding deployment chosen from the embedding slot and stored in DataSourceIndexProfileMetadata, IDocumentIndexHandler mapping, a custom indexing service with the keyed IDocumentIndexManager and BuildDocumentIndexContext, deferred lifecycle indexing with ShellScope, re-sync through SynchronizedAsync, and a master index site setting read through IOptionsMonitor. Use it for custom Elasticsearch indexing of non-content data, vector search over custom records, or questions about CrestApps.OrchardCore.AI.Memory.Elasticsearch, OrchardCore.Indexing, OrchardCore.Elasticsearch, IIndexProfileStore, IndexProfileEmbeddingMetadataAccessor, and IAIClientFactory.CreateEmbeddingGeneratorAsync.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Custom Indexing for Elasticsearch

## Index arbitrary records into Elasticsearch

You are an Orchard Core expert. Generate code that indexes custom records (not content items) into an Elasticsearch index profile, optionally with embeddings for vector search. Model it on `CrestApps.OrchardCore.AI.Memory` (records, settings, indexing service) and `CrestApps.OrchardCore.AI.Memory.Elasticsearch` (provider registration and mappings).

### When to use it

- Records you store yourself: CRM or ERP rows, generated summaries, user-scoped facts, imported business data.
- Full-text, filtered, or vector search over them through Orchard Core's index profiles.
- For Orchard content items use the built-in content index instead (see `orchardcore-elasticsearch`). For retrieval-augmented chat over an index you already have, an AI data source is usually enough (see `orchardcore-ai-data-sources`).

### Architecture

1. A shared module owns the records, a provider-neutral index document, the master-index setting, and the indexing service.
2. A provider module registers the index source, the index-profile handler that shapes the Elasticsearch mapping, and the `IDocumentIndexHandler` that maps the neutral document.
3. Indexing runs from the record store's lifecycle, deferred to the end of the request, never from a single controller or tool.
4. One index profile type per record family; a site setting picks the active profile by name.

### Key pieces

| Piece | Namespace | Role |
|---|---|---|
| `AddElasticsearchIndexingSource(type, o => ...)` | `OrchardCore.Elasticsearch` | Adds the index type to **Search > Indexing** |
| `IndexProfileHandlerBase` | `OrchardCore.Indexing.Core.Handlers` | Shapes the profile on initialize, create, update, and reacts to `SynchronizedAsync` |
| `ElasticsearchIndexMetadata`, `ElasticsearchDefaultQueryMetadata` | `OrchardCore.Elasticsearch.Core.Models`, `OrchardCore.Elasticsearch.Models` | Mapping and default query fields |
| `IDocumentIndexHandler`, `BuildDocumentIndexContext` | `OrchardCore.Indexing` | Map a record into a `DocumentIndex` |
| keyed `IDocumentIndexManager` | `OrchardCore.Indexing` | `AddOrUpdateDocumentsAsync`, `DeleteDocumentsAsync` per provider |
| `IIndexProfileStore` | `OrchardCore.Indexing` | `FindByNameAsync`, `GetByTypeAsync` |
| `IndexProfileEmbeddingMetadataAccessor` | `CrestApps.OrchardCore.AI.Core` | Reads the embedding deployment stored in `DataSourceIndexProfileMetadata` |
| `IAIDeploymentManager`, `IAIClientFactory` | `CrestApps.Core.AI.Deployments`, `CrestApps.Core.AI.Clients` | Resolve the deployment and create the embedding generator |

### 1. Constants and the neutral document

```csharp
public static class CustomerInsightsConstants
{
    public const string IndexingTaskType = "CustomerInsights";

    public static class ColumnNames
    {
        public const string RecordId = "recordId";
        public const string CustomerId = "customerId";
        public const string Title = "title";
        public const string Content = "content";
        public const string UpdatedUtc = "updatedUtc";
        public const string Embedding = "embedding";
    }
}

// The stored record. Here it is a CrestApps catalog item (CrestApps.Core.Models), kept in its own store.
public sealed class CustomerInsight : CatalogItem
{
    public string CustomerId { get; set; }

    public string Title { get; set; }

    public string Content { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

// What the provider handlers receive.
public sealed class CustomerInsightIndexDocument
{
    public string RecordId { get; set; }

    public string CustomerId { get; set; }

    public string Title { get; set; }

    public string Content { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public float[] Embedding { get; set; }
}
```

### 2. Register the provider

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
        services.AddIndexProfileHandler<CustomerInsightElasticsearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, CustomerInsightElasticsearchDocumentIndexHandler>();

        services.AddElasticsearchIndexingSource(CustomerInsightsConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["Customer insights (Elasticsearch)"];
            o.Description = S["Create an Elasticsearch index for customer insight records."];
        });
    }
}
```

The provider module depends on your shared feature and on `OrchardCore.Elasticsearch`.

### 3. Shape the mapping

Measure the vector size from the selected embedding deployment, as the memory and data source handlers do, and fall back to 1536 when it cannot be measured.

```csharp
public sealed class CustomerInsightElasticsearchIndexProfileHandler : IndexProfileHandlerBase
{
    private const int DefaultDimensions = 1536;

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;

    public CustomerInsightElasticsearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory)
    {
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
    }

    public override async Task InitializingAsync(InitializingContext<IndexProfile> context)
    {
        await SetMappingAsync(context.Model);

        if (CanHandle(context.Model))
        {
            var query = context.Model.GetOrCreate<ElasticsearchDefaultQueryMetadata>();

            if (query.DefaultSearchFields is null || query.DefaultSearchFields.Length == 0)
            {
                query.DefaultSearchFields = [CustomerInsightsConstants.ColumnNames.Title, CustomerInsightsConstants.ColumnNames.Content];
                context.Model.Put(query);
            }
        }
    }

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    private async Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var metadata = indexProfile.GetOrCreate<ElasticsearchIndexMetadata>();
        metadata.IndexMappings ??= new ElasticsearchIndexMap();
        metadata.IndexMappings.Mapping ??= new TypeMapping();
        metadata.IndexMappings.Mapping.Properties ??= [];

        var properties = metadata.IndexMappings.Mapping.Properties;

        metadata.IndexMappings.KeyFieldName = CustomerInsightsConstants.ColumnNames.RecordId;
        properties[CustomerInsightsConstants.ColumnNames.RecordId] = new KeywordProperty();
        properties[CustomerInsightsConstants.ColumnNames.CustomerId] = new KeywordProperty();
        properties[CustomerInsightsConstants.ColumnNames.Title] = new TextProperty();
        properties[CustomerInsightsConstants.ColumnNames.Content] = new TextProperty();
        properties[CustomerInsightsConstants.ColumnNames.UpdatedUtc] = new DateProperty();
        properties[CustomerInsightsConstants.ColumnNames.Embedding] = new DenseVectorProperty
        {
            Dims = await GetEmbeddingDimensionsAsync(indexProfile),
            Index = true,
            Similarity = DenseVectorSimilarity.Cosine,
        };

        indexProfile.Put(metadata);
    }

    private async Task<int> GetEmbeddingDimensionsAsync(IndexProfile indexProfile)
    {
        var deploymentName = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile).GetEmbeddingDeploymentName();

        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return DefaultDimensions;
        }

        var deployment = await _deploymentManager.FindByNameAsync(deploymentName);

        if (deployment is null)
        {
            return DefaultDimensions;
        }

        var generator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsagePurposes.Indexing));

        if (generator is null)
        {
            return DefaultDimensions;
        }

        var embeddings = await generator.GenerateAsync(["Sample"]);

        return embeddings?.Count > 0 && embeddings[0].Vector.Length > 0
            ? embeddings[0].Vector.Length
            : DefaultDimensions;
    }

    private static bool CanHandle(IndexProfile indexProfile)
        => string.Equals(indexProfile.Type, CustomerInsightsConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(indexProfile.ProviderName, ElasticsearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase);
}
```

Wrap the dimension probe in a `try`/`catch` that logs and falls back in production code, as the CrestApps handlers do.

### 4. Map the neutral document

```csharp
public sealed class CustomerInsightElasticsearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not CustomerInsightIndexDocument record ||
            !context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var value) ||
            value is not IndexProfile indexProfile ||
            !string.Equals(indexProfile.ProviderName, ElasticsearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(CustomerInsightsConstants.ColumnNames.RecordId, record.RecordId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(CustomerInsightsConstants.ColumnNames.CustomerId, record.CustomerId, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(CustomerInsightsConstants.ColumnNames.Title, record.Title, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(CustomerInsightsConstants.ColumnNames.Content, record.Content, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(CustomerInsightsConstants.ColumnNames.UpdatedUtc, record.UpdatedUtc, DocumentIndexOptions.Store);
        context.DocumentIndex.Set(
            CustomerInsightsConstants.ColumnNames.Embedding,
            record.Embedding,
            record.Embedding?.Length ?? 0,
            DocumentIndexOptions.Store,
            new Dictionary<string, object> { ["VectorSearchConfiguration"] = "default" });

        return Task.CompletedTask;
    }
}
```

### 5. Choose the embedding deployment

Store the choice in `DataSourceIndexProfileMetadata.EmbeddingDeploymentName` so `IndexProfileEmbeddingMetadataAccessor` reads it. Add a `DisplayDriver<IndexProfile>` for your type, following `AIMemoryIndexProfileDisplayDriver`:

- List candidates with `IAIDeploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Embedding)`.
- On save, accept only a deployment whose `AIDeploymentMetadata` supports `AIDeploymentFeatureNames.TextEmbedding`.
- Lock the field once the index exists, because the vector size is fixed.
- Write it with `IndexProfileEmbeddingMetadataAccessor.StoreMetadata(indexProfile, metadata)` after `metadata.SetEmbeddingDeploymentName(deployment.Name)`.

### 6. The indexing service

```csharp
public sealed class CustomerInsightIndexingService
{
    private readonly IOptionsMonitor<CustomerInsightOptions> _options;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IDocumentIndexHandler> _documentIndexHandlers;
    private readonly ILogger _logger;

    public CustomerInsightIndexingService(
        IOptionsMonitor<CustomerInsightOptions> options,
        IIndexProfileStore indexProfileStore,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        IServiceProvider serviceProvider,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        ILogger<CustomerInsightIndexingService> logger)
    {
        _options = options;
        _indexProfileStore = indexProfileStore;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _serviceProvider = serviceProvider;
        _documentIndexHandlers = documentIndexHandlers;
        _logger = logger;
    }

    public async Task IndexAsync(CustomerInsight insight, CancellationToken cancellationToken = default)
    {
        var indexProfile = await GetActiveProfileAsync();

        if (indexProfile is null)
        {
            return;
        }

        var manager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

        if (manager is null)
        {
            return;
        }

        var document = await BuildDocumentAsync(insight, indexProfile, manager, cancellationToken);

        if (document is not null)
        {
            await manager.AddOrUpdateDocumentsAsync(indexProfile, [document]);
        }
    }

    public async Task DeleteAsync(IEnumerable<string> recordIds)
    {
        var indexProfile = await GetActiveProfileAsync();
        var manager = indexProfile is null ? null : _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

        if (manager is not null)
        {
            await manager.DeleteDocumentsAsync(indexProfile, recordIds);
        }
    }

    private async Task<IndexProfile> GetActiveProfileAsync()
    {
        var name = _options.CurrentValue.IndexProfileName;

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var profile = await _indexProfileStore.FindByNameAsync(name);

        return profile is not null && string.Equals(profile.Type, CustomerInsightsConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase)
            ? profile
            : null;
    }

    private async Task<DocumentIndex> BuildDocumentAsync(
        CustomerInsight insight,
        IndexProfile indexProfile,
        IDocumentIndexManager manager,
        CancellationToken cancellationToken)
    {
        var deploymentName = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile).GetEmbeddingDeploymentName();
        var deployment = string.IsNullOrWhiteSpace(deploymentName) ? null : await _deploymentManager.FindByNameAsync(deploymentName);

        if (deployment is null)
        {
            return null;
        }

        var generator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsagePurposes.Indexing));
        var embeddings = await generator.GenerateAsync([$"{insight.Title}{Environment.NewLine}{insight.Content}"], cancellationToken: cancellationToken);

        var record = new CustomerInsightIndexDocument
        {
            RecordId = insight.ItemId,
            CustomerId = insight.CustomerId,
            Title = insight.Title,
            Content = insight.Content,
            UpdatedUtc = insight.UpdatedUtc,
            Embedding = embeddings[0].Vector.ToArray(),
        };

        var documentIndex = new DocumentIndex(insight.ItemId);
        var buildContext = new BuildDocumentIndexContext(documentIndex, record, [insight.ItemId], manager.GetContentIndexSettings())
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                [nameof(IndexProfile)] = indexProfile,
            },
        };

        await _documentIndexHandlers.InvokeAsync((handler, ctx) => handler.BuildIndexAsync(ctx), buildContext, _logger);

        return documentIndex;
    }
}
```

Add a `SyncByIndexProfileIdsAsync` that loads `_indexProfileStore.GetByTypeAsync(CustomerInsightsConstants.IndexingTaskType)`, filters by id, and writes every record, for the re-sync in step 8.

### 7. Index from the store lifecycle

Collect changes in a scoped catalog handler and flush once, after the request commits:

```csharp
public sealed class CustomerInsightIndexingHandler : CatalogEntryHandlerBase<CustomerInsight>
{
    private readonly Dictionary<string, CustomerInsight> _upserts = [];
    private readonly HashSet<string> _deletes = [];
    private bool _scheduled;

    public override Task CreatedAsync(CreatedContext<CustomerInsight> context, CancellationToken cancellationToken = default)
        => QueueUpsert(context.Model);

    public override Task UpdatedAsync(UpdatedContext<CustomerInsight> context, CancellationToken cancellationToken = default)
        => QueueUpsert(context.Model);

    public override Task DeletedAsync(DeletedContext<CustomerInsight> context, CancellationToken cancellationToken = default)
    {
        _upserts.Remove(context.Model.ItemId);
        _deletes.Add(context.Model.ItemId);
        Schedule();

        return Task.CompletedTask;
    }

    private Task QueueUpsert(CustomerInsight insight)
    {
        _deletes.Remove(insight.ItemId);
        _upserts[insight.ItemId] = insight;
        Schedule();

        return Task.CompletedTask;
    }

    private void Schedule()
    {
        if (_scheduled)
        {
            return;
        }

        _scheduled = true;
        var upserts = _upserts;
        var deletes = _deletes;

        ShellScope.AddDeferredTask(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<CustomerInsightIndexingService>();

            foreach (var insight in upserts.Values)
            {
                await service.IndexAsync(insight);
            }

            if (deletes.Count > 0)
            {
                await service.DeleteAsync(deletes);
            }
        });
    }
}
```

Register it with `services.AddScoped<ICatalogEntryHandler<CustomerInsight>, CustomerInsightIndexingHandler>()` in the shared module. `CatalogEntryHandlerBase<T>` comes from CrestApps.Core (`CrestApps.Core.Handlers`); use the equivalent lifecycle hook of your own store if it is not a CrestApps catalog.

### 8. Re-sync when the profile is synchronized

```csharp
public sealed class CustomerInsightIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly CustomerInsightIndexingService _indexingService;

    public CustomerInsightIndexProfileHandler(CustomerInsightIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public override async Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (string.Equals(context.IndexProfile.Type, CustomerInsightsConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            await _indexingService.SyncByIndexProfileIdsAsync([context.IndexProfile.Id]);
        }
    }
}
```

### 9. The master-index setting

Project the site setting onto an options class and refresh it without a tenant release: register `IConfigureOptions<CustomerInsightOptions>` that reads `ISiteService.GetSettings<CustomerInsightSettings>()`, call `services.AddSignalOptionsChangeTokenSource<CustomerInsightOptions>()`, and call `IOptionsUpdateNotifier.RequestUpdate<CustomerInsightOptions>()` from the settings driver when the value changes. Read it with `IOptionsMonitor<CustomerInsightOptions>.CurrentValue`. Mechanics are in `orchardcore-site-settings`.

### Querying

For vector search, embed the query with the same deployment and run a k-nearest-neighbor query on `embedding`, always adding the filters that scope results (the memory service filters on `userId`; a per-customer index filters on `customerId`). Use `ElasticsearchMemoryVectorSearchService` in `CrestApps.OrchardCore.AI.Memory.Elasticsearch` as the reference implementation.

### Checklist

- Every write path goes through the store, so the deferred handler sees it.
- The key field (`KeyFieldName`) and the `DocumentIndex` id are the same stable record id.
- Index dimensions match the embedding deployment; a model change needs a new index profile and a re-sync.
- Mandatory scope filters are applied in the search service, not left to callers.
- The Azure AI Search variant of this pattern is `orchardcore-custom-indexing-azureai`.

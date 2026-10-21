---
name: orchardcore-custom-indexing-azureai
description: Skill for building a custom Azure AI Search index for arbitrary records in Orchard Core 4.0, following the CrestApps AI Memory pattern of CrestApps.OrchardCore 3.0. Covers a custom index profile type with AddAzureAISearchIndexingSource, IndexProfileHandlerBase mappings with AzureAISearchIndexMetadata, AzureAISearchIndexMap with IndexingKey and AzureFieldKey, vector fields with AzureAISearchIndexMapVectorInfo and VectorSearchMappings, normalizing managed mappings in LoadedAsync, an embedding deployment from the embedding slot stored in DataSourceIndexProfileMetadata, IDocumentIndexHandler mapping, a custom indexing service with the keyed IDocumentIndexManager, deferred lifecycle indexing, and re-sync through SynchronizedAsync. Use it for custom Azure AI Search indexing of non-content data, vector search over custom records, or questions about CrestApps.OrchardCore.AI.Memory.AzureAI, OrchardCore.Indexing, OrchardCore.AzureAI, IIndexProfileStore, IndexProfileEmbeddingMetadataAccessor, and VectorizedQuery.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Custom Indexing for Azure AI Search

## Index arbitrary records into Azure AI Search

You are an Orchard Core expert. Generate code that indexes custom records (not content items) into an Azure AI Search index profile, optionally with vectors. Model it on `CrestApps.OrchardCore.AI.Memory` (records, settings, indexing service) and `CrestApps.OrchardCore.AI.Memory.AzureAI` (provider registration and mappings).

### When to use it

- Records you store yourself (CRM rows, generated summaries, user-scoped facts, imported data) that need full-text, filtered, or vector search.
- For Orchard content items use the built-in content index (see `orchardcore-azure-ai-search`). For RAG over an index you already have, an AI data source is usually enough (see `orchardcore-ai-data-sources`).

### Architecture

The shared module (records, neutral document, master-index setting, indexing service, lifecycle handler, re-sync handler) is identical to the Elasticsearch variant; build it as shown in `orchardcore-custom-indexing-elasticsearch`. This skill covers the Azure AI Search provider module and what differs.

| Piece | Namespace | Role |
|---|---|---|
| `AddAzureAISearchIndexingSource(type, o => ...)` | `OrchardCore.AzureAI` | Adds the index type to **Search > Indexing** |
| `IndexProfileHandlerBase` | `OrchardCore.Indexing.Core.Handlers` | Shapes the profile on initialize, create, update, and load |
| `AzureAISearchIndexMetadata`, `AzureAISearchIndexMap`, `AzureAISearchIndexMapVectorInfo`, `VectorSearchMappings` | `OrchardCore.AzureAI.Models` | Field and vector configuration |
| `AzureAISearchConstants.ProviderName` | `OrchardCore.AzureAI` | The provider key (`AzureAISearch`) |
| `IDocumentIndexHandler`, keyed `IDocumentIndexManager` | `OrchardCore.Indexing` | Map and write documents |
| `IndexProfileEmbeddingMetadataAccessor` | `CrestApps.OrchardCore.AI.Core` | Reads the embedding deployment from `DataSourceIndexProfileMetadata` |

### 1. Register the provider

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
        services.AddIndexProfileHandler<CustomerInsightAzureAISearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, CustomerInsightAzureAISearchDocumentIndexHandler>();

        services.AddAzureAISearchIndexingSource(CustomerInsightsConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["Customer insights (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index for customer insight records."];
        });
    }
}
```

The provider module depends on your shared feature, `OrchardCore.Indexing`, and `OrchardCore.AzureAI`.

### 2. Shape the fields

Azure AI Search mappings need both keys on every map: `AzureFieldKey` names the Azure field and `IndexingKey` names the `DocumentIndex` entry it is filled from. Missing indexing keys make upserts fail with null-key mapping errors. A vector field needs a vector search profile.

```csharp
public sealed class CustomerInsightAzureAISearchIndexProfileHandler : IndexProfileHandlerBase
{
    private const int DefaultDimensions = 1536;

    private static readonly HashSet<string> _managedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        CustomerInsightsConstants.ColumnNames.RecordId,
        CustomerInsightsConstants.ColumnNames.CustomerId,
        CustomerInsightsConstants.ColumnNames.Title,
        CustomerInsightsConstants.ColumnNames.Content,
        CustomerInsightsConstants.ColumnNames.UpdatedUtc,
        CustomerInsightsConstants.ColumnNames.Embedding,
    };

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;

    public CustomerInsightAzureAISearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory)
    {
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task LoadedAsync(LoadedContext<IndexProfile> context)
    {
        if (CanHandle(context.Model))
        {
            var metadata = context.Model.GetOrCreate<AzureAISearchIndexMetadata>();
            Normalize(metadata, dimensions: null);
            context.Model.Put(metadata);
        }

        return Task.CompletedTask;
    }

    private async Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var metadata = indexProfile.GetOrCreate<AzureAISearchIndexMetadata>();

        Normalize(metadata, await GetEmbeddingDimensionsAsync(indexProfile));

        metadata.VectorSearchMappings = new VectorSearchMappings
        {
            Profiles =
            [
                new VectorSearchProfileMap
                {
                    Name = "default",
                    AlgorithmConfigurationName = "default-hnsw",
                },
            ],
            Algorithms =
            [
                new VectorSearchAlgorithmMap
                {
                    Name = "default-hnsw",
                    Kind = VectorSearchAlgorithmMap.HnswKind,
                },
            ],
        };

        indexProfile.Put(metadata);
    }

    // Rebuilds the managed fields once each and keeps any custom field the administrator added.
    private static void Normalize(AzureAISearchIndexMetadata metadata, int? dimensions)
    {
        var existing = new Dictionary<string, AzureAISearchIndexMap>(StringComparer.OrdinalIgnoreCase);
        var custom = new List<AzureAISearchIndexMap>();

        foreach (var map in metadata.IndexMappings)
        {
            map.IndexingKey ??= map.AzureFieldKey;
            var key = map.AzureFieldKey ?? map.IndexingKey;

            if (!_managedFields.Contains(key))
            {
                custom.Add(map);
            }
            else
            {
                existing.TryAdd(key, map);
            }
        }

        metadata.IndexMappings.Clear();
        metadata.IndexMappings.Add(Map(existing, CustomerInsightsConstants.ColumnNames.RecordId, DocumentIndex.Types.Text, m =>
        {
            m.IsKey = true;
            m.IsFilterable = true;
        }));
        metadata.IndexMappings.Add(Map(existing, CustomerInsightsConstants.ColumnNames.CustomerId, DocumentIndex.Types.Text, m => m.IsFilterable = true));
        metadata.IndexMappings.Add(Map(existing, CustomerInsightsConstants.ColumnNames.Title, DocumentIndex.Types.Text, m => m.IsSearchable = true));
        metadata.IndexMappings.Add(Map(existing, CustomerInsightsConstants.ColumnNames.Content, DocumentIndex.Types.Text, m => m.IsSearchable = true));
        metadata.IndexMappings.Add(Map(existing, CustomerInsightsConstants.ColumnNames.UpdatedUtc, DocumentIndex.Types.DateTime, m =>
        {
            m.IsFilterable = true;
            m.IsSortable = true;
        }));
        metadata.IndexMappings.Add(Map(existing, CustomerInsightsConstants.ColumnNames.Embedding, DocumentIndex.Types.Vector, m =>
        {
            var current = m.VectorInfo?.Dimensions;
            m.IsSearchable = true;
            m.VectorInfo = new AzureAISearchIndexMapVectorInfo
            {
                Dimensions = dimensions ?? current ?? DefaultDimensions,
                VectorSearchConfiguration = "default",
            };
        }));

        foreach (var map in custom)
        {
            metadata.IndexMappings.Add(map);
        }
    }

    private static AzureAISearchIndexMap Map(
        Dictionary<string, AzureAISearchIndexMap> existing,
        string key,
        DocumentIndex.Types type,
        Action<AzureAISearchIndexMap> configure)
    {
        if (!existing.TryGetValue(key, out var map))
        {
            map = new AzureAISearchIndexMap();
        }

        map.IndexingKey = key;
        map.AzureFieldKey = key;
        map.Type = type;
        map.IsKey = false;
        map.IsFilterable = false;
        map.IsSortable = false;
        map.IsSearchable = false;
        configure(map);

        return map;
    }

    private async Task<int> GetEmbeddingDimensionsAsync(IndexProfile indexProfile)
    {
        var deploymentName = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile).GetEmbeddingDeploymentName();
        var deployment = string.IsNullOrWhiteSpace(deploymentName) ? null : await _deploymentManager.FindByNameAsync(deploymentName);

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
           string.Equals(indexProfile.ProviderName, AzureAISearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase);
}
```

Normalizing in `LoadedAsync` keeps reopening and saving the profile from appending duplicate managed fields, which is the bug the CrestApps memory, documents, and data source handlers fixed. Wrap the dimension probe in a `try`/`catch` that logs and falls back in production code.

### 3. Map the neutral document

```csharp
public sealed class CustomerInsightAzureAISearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not CustomerInsightIndexDocument record ||
            !context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var value) ||
            value is not IndexProfile indexProfile ||
            !string.Equals(indexProfile.ProviderName, AzureAISearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase))
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

### 4. Shared module steps

Follow `orchardcore-custom-indexing-elasticsearch` for:

- the embedding deployment picker (`GetAllBySlotAsync(AIDeploymentSlotNames.Embedding)`, `AIDeploymentFeatureNames.TextEmbedding`, `IndexProfileEmbeddingMetadataAccessor.StoreMetadata`, locked once the index exists);
- the indexing service that resolves the active profile through `IOptionsMonitor<TOptions>`, embeds the record, builds a `BuildDocumentIndexContext` with the `IndexProfile` in `AdditionalProperties`, and calls the keyed `IDocumentIndexManager`;
- the catalog handler that flushes upserts and deletes in a `ShellScope` deferred task;
- the `SynchronizedAsync` handler that rebuilds a profile when it is synchronized from **Search > Indexing**.

### Querying

Embed the query with the index's deployment and send a `VectorizedQuery` on `embedding` with `KNearestNeighborsCount = topN`, plus an OData `Filter` that scopes the results (for example `customerId eq '...'`). Only fields declared `IsFilterable` can be filtered. `AzureAISearchMemoryVectorSearchService` in `CrestApps.OrchardCore.AI.Memory.AzureAI` is the reference, including its mandatory `userId` filter.

### Azure AI Search rules

- One `AzureAISearchIndexMap` per field, with matching `IndexingKey` and `AzureFieldKey`.
- Exactly one key field (`IsKey = true`), filled with the same stable id as the `DocumentIndex`.
- Vector fields use `DocumentIndex.Types.Vector`, `IsSearchable = true`, `VectorInfo.Dimensions`, and a `VectorSearchConfiguration` that names a profile in `VectorSearchMappings`.
- Mark every field you filter or sort on as `IsFilterable` or `IsSortable`; Azure rejects filters on undeclared fields.
- A new embedding model with different dimensions needs a new index profile and a full re-sync.

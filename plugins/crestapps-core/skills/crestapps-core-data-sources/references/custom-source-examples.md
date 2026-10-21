# Custom Data Source Examples

Companion to `crestapps-core-data-sources`. `ICatalogApiClient` and `CatalogItemDto` stand for the host's own client for an external system.

## Source Handler

```csharp
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;

public sealed class CustomCatalogAIDataSourceSourceHandler : IAIDataSourceSourceHandler
{
    public const string TypeName = "CustomCatalog";

    private readonly ICatalogApiClient _client;

    public CustomCatalogAIDataSourceSourceHandler(ICatalogApiClient client)
    {
        _client = client;
    }

    public string SourceType => TypeName;

    public ValueTask ValidateAsync(
        AIDataSource dataSource,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            result.Fail(new ValidationResult(
                "A knowledge base index profile is required.",
                [nameof(AIDataSource.AIKnowledgeBaseIndexProfileName)]));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<string> GetReferenceTypeAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
        => ValueTask.FromResult("CustomCatalogItem");

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        AIDataSource dataSource,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _client.GetAllAsync(cancellationToken))
        {
            yield return new(item.Id, ToSourceDocument(item));
        }
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        AIDataSource dataSource,
        IEnumerable<string> documentIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _client.GetByIdsAsync(documentIds, cancellationToken))
        {
            yield return new(item.Id, ToSourceDocument(item));
        }
    }

    private static SourceDocument ToSourceDocument(CatalogItemDto item)
    {
        return new SourceDocument
        {
            Title = item.Title,
            Content = item.Body,
            Fields = new Dictionary<string, object>
            {
                ["category"] = item.Category,
            },
        };
    }
}
```

`SourceDocument` has no constructor arguments; set `Title`, `Content`, optional `Fields` (they become filterable entries in the row's filter bag) and `IsPreChunked` when a row must stay one chunk.

## Registration

```csharp
using CrestApps.Core.AI;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;

builder.Services
    .AddCoreAIServices()
    .AddCoreAIDataSourceRag()
    .Configure<AIDataSourceSourceOptions>(options => options.AddOrUpdate(
        CustomCatalogAIDataSourceSourceHandler.TypeName,
        new LocalizedString("CustomCatalog", "Custom Catalog"),
        new LocalizedString("CustomCatalog Description", "Reads documents from the catalog service.")))
    .AddKeyedScoped<IAIDataSourceSourceHandler, CustomCatalogAIDataSourceSourceHandler>(
        CustomCatalogAIDataSourceSourceHandler.TypeName);
```

`AIDataSourceSourceOptions.AddOrUpdate` takes `LocalizedString` values for the display name and description; plain strings do not compile.

## Sync Bridge

```csharp
using CrestApps.Core.AI.DataSources;

public sealed class CustomCatalogSyncBridge
{
    private readonly IAIDataSourceChangeNotifier _changeNotifier;

    public CustomCatalogSyncBridge(IAIDataSourceChangeNotifier changeNotifier)
    {
        _changeNotifier = changeNotifier;
    }

    public ValueTask OnSavedAsync(string dataSourceId, string recordId, CancellationToken cancellationToken)
        => _changeNotifier.QueueDocumentsAddedOrUpdatedAsync(dataSourceId, [recordId], cancellationToken);

    public ValueTask OnDeletedAsync(string dataSourceId, string recordId, CancellationToken cancellationToken)
        => _changeNotifier.QueueDocumentsDeletedAsync(dataSourceId, [recordId], cancellationToken);

    public ValueTask OnReseedAsync(string dataSourceId, CancellationToken cancellationToken)
        => _changeNotifier.QueueFullSyncAsync(dataSourceId, cancellationToken);
}
```

| Source event | Call |
|---|---|
| records added or updated | `QueueDocumentsAddedOrUpdatedAsync` |
| records deleted | `QueueDocumentsDeletedAsync` |
| reseed, backfill, unknown drift | `QueueFullSyncAsync` |

The nightly `AIDataSourceAlignmentBackgroundService` still reconciles everything, but explicit notifications keep answers fresh between passes.

## Custom Vector Backend

A new backend registers six keyed services under one provider name, and its `ISearchDocumentManager` must notify every `ISearchDocumentHandler` after successful writes.

```csharp
const string providerName = "MyBackend";

builder.Services.AddKeyedScoped<IVectorSearchService, MyVectorSearchService>(providerName);
builder.Services.AddKeyedScoped<ISearchIndexManager, MySearchIndexManager>(providerName);
builder.Services.AddKeyedScoped<ISearchDocumentManager, MySearchDocumentManager>(providerName);
builder.Services.AddKeyedScoped<IDataSourceContentManager, MyDataSourceContentManager>(providerName);
builder.Services.AddKeyedScoped<IDataSourceDocumentReader, MyDataSourceDocumentReader>(providerName);
builder.Services.AddKeyedScoped<IODataFilterTranslator, MyODataFilterTranslator>(providerName);
```

Translate the typed columns (`contentType`, `rootId`, `parentId`, `page`) to real columns, and everything else to the row's filter bag. Implement `ISearchIndexManager.TryAddFieldsAsync` to add new schema fields in place.

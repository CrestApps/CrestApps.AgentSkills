# Indexing Source Examples

## Elasticsearch

```csharp
services
    .AddIndexProfileHandler<ElasticsearchContentIndexProfileHandler>()
    .AddElasticsearchIndexingSource(IndexingConstants.ContentsIndexSource, options =>
    {
        options.DisplayName = _localizer["Content in Elasticsearch"];
        options.Description = _localizer["Create an Elasticsearch index based on site contents."];
    });
```

## Generic Source Contracts

The generic registration requires sealed provider implementations of the
current source contracts. `S` is an `IStringLocalizer` injected in the startup
constructor; `DisplayName` and `Description` are `LocalizedString` values:

```csharp
services.AddIndexingSource<
    ContosoIndexManager,
    ContosoDocumentIndexManager,
    ContosoIndexNameProvider>(
    "Contoso",
    "Products",
    options =>
    {
        options.DisplayName = S["Products in Contoso"];
        options.Description = S["Create a Contoso index for product records."];
    });
```

`ContosoIndexManager` implements `IIndexManager`,
`ContosoDocumentIndexManager` implements `IDocumentIndexManager`, and
`ContosoIndexNameProvider` implements `IIndexNameProvider`. The managers are
registered as keyed services by the provider name.

## Universal Indexing Task Categories

Use one stable category for each record source. Content records use
`IndexingConstants.ContentsIndexSource`; a custom source can use its own
category and pass it to both `CreateIndexingTaskContext` and
`GetIndexingTasksAsync`.

```csharp
await indexingTaskManager.CreateTaskAsync(new CreateIndexingTaskContext(
    recordId,
    "Products",
    RecordIndexingTaskTypes.Update));

var tasks = await indexingTaskManager.GetIndexingTasksAsync(
    afterTaskId,
    count: 100,
    category: "Products");
```

## Fictional Custom Provider

The following is a conceptual Contoso provider, not an Orchard Core provider:

```csharp
services.AddIndexingSource<
    ContosoIndexManager,
    ContosoDocumentIndexManager,
    ContosoIndexNameProvider>(
    "Contoso",
    "Products",
    options => options.DisplayName = S["Products in Contoso"]);
```

Implement the three provider manager types and any related index-profile
handler in the Contoso integration before using this registration.

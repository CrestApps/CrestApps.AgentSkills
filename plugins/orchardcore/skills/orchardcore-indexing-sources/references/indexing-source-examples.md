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

## Fictional Custom Provider

The following is a conceptual Contoso provider, not an Orchard Core provider:

```csharp
services.AddIndexingSource<
    ContosoIndexManager,
    ContosoDocumentIndexManager,
    ContosoIndexNameProvider>(
    "Contoso",
    "Products",
    options => options.DisplayName = "Products in Contoso");
```

Implement the three provider manager types and any related index-profile
handler in the Contoso integration before using this registration.

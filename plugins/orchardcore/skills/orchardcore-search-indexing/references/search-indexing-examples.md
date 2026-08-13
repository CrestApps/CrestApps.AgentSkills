# Search & Indexing Examples

## Example 1: Lucene Search Index Profile Recipe

```json
{
  "steps": [
    {
      "name": "CreateOrUpdateIndexProfile",
      "indexes": [
        {
          "Name": "Search",
          "IndexName": "search",
          "ProviderName": "Lucene",
          "Type": "Content",
          "Properties": {
            "ContentIndexMetadata": {
              "IndexLatest": false,
              "IndexedContentTypes": [
                "Article",
                "BlogPost",
                "Page"
              ],
              "Culture": "any"
            },
            "LuceneIndexMetadata": {
              "AnalyzerName": "standardanalyzer",
              "StoreSourceData": false
            }
          }
        }
      ]
    }
  ]
}
```

## Example 2: Search Query Recipe

```json
{
  "steps": [
    {
      "name": "Queries",
      "Queries": [
        {
          "Source": "Lucene",
          "Name": "RecentArticles",
          "Index": "Search",
          "Template": "{\"query\":{\"bool\":{\"filter\":[{\"term\":{\"Content.ContentItem.ContentType\":\"Article\"}}]}},\"sort\":{\"Content.ContentItem.PublishedUtc\":{\"order\":\"desc\"}},\"size\":10}",
          "ReturnContentItems": true,
          "Schema": "[]"
        },
        {
          "Source": "Lucene",
          "Name": "SearchByKeyword",
          "Index": "Search",
          "Template": "{\"query\":{\"multi_match\":{\"query\":\"{{term}}\",\"fields\":[\"Content.ContentItem.FullText\"]}},\"size\":20}",
          "ReturnContentItems": true,
          "Schema": "[{\"name\":\"term\",\"type\":\"String\"}]"
        }
      ]
    }
  ]
}
```

## Example 3: Custom Index Handler

```csharp
using OrchardCore.Indexing;

public sealed class ProductPartIndexHandler : ContentPartIndexHandler<ProductPart>
{
    public override Task BuildIndexAsync(
        ProductPart part,
        BuildPartIndexContext context)
    {
        context.DocumentIndex.Set(
            $"{nameof(ProductPart)}.{nameof(ProductPart.ProductName)}",
            part.ProductName,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            $"{nameof(ProductPart)}.{nameof(ProductPart.Price)}",
            part.Price,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            $"{nameof(ProductPart)}.{nameof(ProductPart.SKU)}",
            part.SKU,
            DocumentIndexOptions.Store | DocumentIndexOptions.Keyword);

        return Task.CompletedTask;
    }
}
```

## Example 4: Custom Document Index Handler

`IDocumentIndexHandler` receives the source record through
`BuildDocumentIndexContext.Record`. `DocumentIndex.Set` supports structured
values and vectors in addition to scalar values.

```csharp
using System.Collections.Generic;
using OrchardCore.Indexing;

public sealed class ProductDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ProductRecord record)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(
            "Product.Attributes",
            record.Attributes,
            DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            "Product.Embedding",
            record.Embedding,
            record.Embedding.Length,
            DocumentIndexOptions.None);

        return Task.CompletedTask;
    }
}

public sealed class ProductRecord
{
    public Dictionary<string, object> Attributes { get; init; } = [];
    public float[] Embedding { get; init; } = [];
}
```

Register it with `services.AddScoped<IDocumentIndexHandler, ProductDocumentIndexHandler>()`.
Use `ContentIndexingConstants` from `OrchardCore.Contents.Indexing` for
built-in content index keys.

## Example 5: Using Search in Liquid

```liquid
{% assign results = Queries.RecentArticles | query %}
<div class="article-list">
    {% for item in results %}
        <article>
            <h2><a href="{{ item | display_url }}">{{ item.DisplayText }}</a></h2>
            <time datetime="{{ item.PublishedUtc | date: '%Y-%m-%d' }}">
                {{ item.PublishedUtc | date: "%B %d, %Y" }}
            </time>
        </article>
    {% endfor %}
</div>
```

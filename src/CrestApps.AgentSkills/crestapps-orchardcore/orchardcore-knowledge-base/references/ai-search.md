# Let AI Answer From Knowledge Base Articles

Knowledge Base articles are content items, so the AI data sources pipeline turns them into a retrieval (RAG) knowledge base without code. See `orchardcore-ai-data-sources` for the pipeline itself.

## Features

| Feature | ID | For |
|---|---|---|
| Knowledge Base | `CrestApps.OrchardCore.KnowledgeBase` | The articles |
| Elasticsearch | `OrchardCore.Elasticsearch` | The content index of the `KnowledgeArticle` type |
| AI Data Sources - Elasticsearch | `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | **AI Knowledge Base Index (Elasticsearch)** and the **Search Index Profile** data source (enables AI Data Sources and Indexing) |
| AI Data Sources - Azure AI Search | `CrestApps.OrchardCore.AI.DataSources.AzureAI` | The same on Azure AI Search |
| AI Chat | `CrestApps.OrchardCore.AI.Chat` | Admin chat and the **Artificial Intelligence Chat** widget |
| Widgets, Layers | `OrchardCore.Widgets`, `OrchardCore.Layers` | Placing the chat widget |

Lucene has no AI knowledge base index. The AI connection needs a chat deployment and an embedding deployment.

## Elasticsearch Connections

Orchard Core's content index and the CrestApps AI data sources read separate sections:

```json
{
  "OrchardCore": {
    "OrchardCore_Elasticsearch": {
      "ConnectionType": "SingleNodeConnectionPool",
      "Url": "http://localhost",
      "Ports": [ 9200 ],
      "AuthenticationType": "Basic",
      "Username": "elastic",
      "Password": "elasticsearch"
    },
    "CrestApps": {
      "Elasticsearch": {
        "Url": "http://localhost:9200",
        "AuthenticationType": "Basic",
        "Username": "elastic",
        "Password": "elasticsearch"
      }
    }
  }
}
```

## Indexes and Data Source

| Piece | Settings that matter |
|---|---|
| Content index (**Content in Elasticsearch**) | Content types: `KnowledgeArticle` only. **Index Latest version** off (drafts stay out). **Store Source Data** on: the document reader reads each hit's `_source`. |
| Article full text | The type's full-text settings (display text and body by default). Add `{{ Model.Content.KnowledgeArticlePart.Summary }}` as a custom full-text template to include the summary. |
| AI knowledge base index | Its embedding deployment, fixed when created. Holds the chunks and embeddings. |
| Data source (**Search Index Profile**) | Source: the content index. Destination: the knowledge base index. Content field `Content.ContentItem.FullText`, title field a display text field, key field empty (the document id, which is the content item id). |

Content events and index notifications queue a targeted re-sync, rebuilding the source index queues a full one, and a nightly alignment task repairs drift.

## Restricted Articles Stay Out

A knowledge base answers anyone allowed to query its profile, without the source's access checks. Before indexing, `DataSourceIndexingService` asks every `IIndexedDocumentVisibility` (namespace `CrestApps.OrchardCore.Indexing`) whether a document is for everyone. Content Access Control's `ContentAccessIndexedDocumentVisibility` says yes only for items with no restricting role picker or with `Anonymous` (no role picked), so articles for `Authenticated` or staff roles are left out and their chunks are deleted at the next sync. The check uses the document id, so the key field must stay the content item id.

A module that restricts other records registers its own check:

```csharp
services.AddIndexedDocumentVisibility<MyRecordVisibility>();
```

## Citations and Public Chat

- Content references have the type `Content` (`AIConstants.DataSourceReferenceTypes.Content`); `ContentItemAILinkGenerator` links each to the article's display route.
- The **Artificial Intelligence Chat** widget shows only to people allowed to query its profile. Give the `Anonymous` role the profile's own `QueryAIProfile_{profileName}` (**Query AI profile - *name***), never `QueryAnyAIProfile`. Visitor chats are rate limited and screened for prompt injection.

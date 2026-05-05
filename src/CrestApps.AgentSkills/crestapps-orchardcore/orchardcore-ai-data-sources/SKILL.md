---
name: orchardcore-ai-data-sources
description: Skill for configuring AI Data Sources in Orchard Core using the CrestApps AI Data Sources modules. Covers knowledge base indexing, RAG search, embedding generation, vector search backends (Azure AI Search and Elasticsearch), data source alignment, and linking data sources to AI profiles. Use this skill when requests mention Orchard Core AI Data Sources, knowledge base indexing, RAG search, data source alignment, embedding indexes, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI.DataSources, CrestApps.OrchardCore.AI.DataSources.AzureAI, CrestApps.OrchardCore.AI.DataSources.Elasticsearch, DataSourceIndexingService, AIDataSourceIndexingQueue, DataSourceAlignmentBackgroundTask. It also helps with creating data sources via recipe, linking data sources to AI profiles, configuring embedding deployments, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Data Sources - Prompt Templates

## Configure AI Data Sources

You are an Orchard Core expert. Generate code, configuration, and recipes for configuring AI data sources in Orchard Core using CrestApps modules to enable knowledge base indexing and RAG (Retrieval-Augmented Generation) search.

### Guidelines

- AI Data Sources provide knowledge base indexing and RAG search capabilities for AI profiles in Orchard Core.
- A data source maps a **source index** (e.g., Lucene, Elasticsearch, Azure AI Search content index) to an **AI knowledge base index** that stores chunked, embedded documents for vector search.
- The indexing pipeline reads documents from the source index, generates embeddings via a configured embedding deployment, chunks content, and writes vector documents into the knowledge base index.
- Supported vector search backends are Azure AI Search and Elasticsearch. Install the matching backend module for your environment.
- The `DataSourceAlignmentBackgroundTask` runs daily at 2 AM to keep knowledge base indexes aligned with their mapped data sources.
- Content item changes (create, update, publish, unpublish, remove) are automatically tracked and queued for re-indexing via `DataSourceContentHandler`.
- Data source configuration (source index, knowledge base index, field mappings) is locked after initial creation and cannot be changed.
- AI profiles reference data sources on the **Knowledge** tab, where you configure strictness, top-N documents, in-scope filtering, and OData filters.
- Strictness controls how closely results must match the query. Top-N documents limits how many retrieved documents are included in the AI context.
- Always secure API keys using user secrets or environment variables; never hardcode them.
- Install CrestApps packages in the web/startup project.

### Feature Overview

| Feature | Feature ID | Description |
|---------|-----------|-------------|
| AI Data Sources (Core) | `CrestApps.OrchardCore.AI.DataSources` | Core data source management, indexing pipeline, and RAG search |
| AI Data Sources - Azure AI Search | `CrestApps.OrchardCore.AI.DataSources.AzureAI` | Azure AI Search backend for embeddings, vector search, and indexing |
| AI Data Sources - Elasticsearch | `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | Elasticsearch backend for embeddings, vector search, and indexing |

### NuGet Packages

| Package | Description |
|---------|-------------|
| `CrestApps.OrchardCore.AI.DataSources` | Core data source management and RAG search |
| `CrestApps.OrchardCore.AI.DataSources.AzureAI` | Azure AI Search support for data source embeddings and vector search |
| `CrestApps.OrchardCore.AI.DataSources.Elasticsearch` | Elasticsearch support for data source embeddings and vector search |

Install the core package plus at least one backend package in your web/startup project.

### Enabling AI Data Sources with Azure AI Search

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.DataSources",
        "CrestApps.OrchardCore.AI.DataSources.AzureAI",
        "OrchardCore.AzureAI"
      ],
      "disable": []
    }
  ]
}
```

### Enabling AI Data Sources with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.DataSources",
        "CrestApps.OrchardCore.AI.DataSources.Elasticsearch",
        "OrchardCore.Elasticsearch"
      ],
      "disable": []
    }
  ]
}
```

## How Data Sources Work

### Architecture Overview

1. **Source Index** — An existing Orchard Core index profile (Lucene, Elasticsearch, or Azure AI Search) that contains the original content documents.
2. **AI Knowledge Base Index** — A vector-enabled index profile (Azure AI Search or Elasticsearch) that stores chunked, embedded documents for semantic search.
3. **Embedding Deployment** — An AI deployment of type `Embedding` (e.g., `text-embedding-3-large`) used to generate vector embeddings for document chunks.
4. **Data Source** — The configuration object that links a source index to a knowledge base index, specifying key, title, and content field mappings.

### Indexing Pipeline

When a data source is synchronized:

1. Documents are read from the source index via `IDataSourceDocumentReader`.
2. Content fields are extracted and chunked based on field mappings (key, title, content).
3. Embeddings are generated for each chunk using the configured embedding deployment.
4. Embedded chunks are written to the knowledge base index via `IDataSourceContentManager`.

### RAG Search Flow

When an AI profile with a linked data source receives a query:

1. The query is embedded using the same embedding deployment.
2. A vector (KNN) search is executed against the knowledge base index.
3. Top-N matching documents are retrieved, filtered by strictness and optional OData filters.
4. Retrieved context is injected into the AI prompt for grounded, knowledge-based responses.

### Automatic Alignment

- **Content Changes** — `DataSourceContentHandler` tracks content item lifecycle events (created, updated, published, unpublished, removed) and queues affected documents for re-indexing or removal.
- **Source Index Sync** — When a source index is synchronized, all data sources using that source index are automatically re-indexed.
- **Knowledge Base Index Sync** — When a knowledge base index is synchronized, all data sources mapped to it are re-indexed.
- **Background Alignment** — `DataSourceAlignmentBackgroundTask` runs daily at 2 AM (`0 2 * * *`) and calls `SyncAllAsync` to align all knowledge base indexes.

### Key Services

| Service | Description |
|---------|-------------|
| `DataSourceIndexingService` | Core indexing service that orchestrates document reading, embedding, and writing |
| `IAIDataSourceIndexingQueue` | Request-scoped queue that collects work items and processes them after the HTTP response completes |
| `IAIDataSourceIndexingService` | Interface for sync, delete, and document management operations |
| `DataSourceAlignmentBackgroundTask` | Daily background task for full alignment of all data sources |
| `IDataSourceContentManager` | Provider-specific service for vector search and document management |
| `IDataSourceDocumentReader` | Provider-specific service for reading documents from the knowledge base index |

## Creating Data Sources

### Creating a Data Source via Admin UI

1. Navigate to **Artificial Intelligence → Data Sources**.
2. Click **Create**.
3. Enter a **Display Name** for the data source.
4. Select the **Source Index Profile** (the existing content index to read from).
5. Select the **AI Knowledge Base Index Profile** (the vector-enabled index to write to).
6. Map the **Key Field**, **Title Field**, and **Content Field** from the source index.
7. Click **Save**.

After creation, the source index, knowledge base index, and field mappings are locked and cannot be changed.

### Creating a Data Source via Recipe

```json
{
  "steps": [
    {
      "name": "AIDataSource",
      "DataSources": [
        {
          "ItemId": "my-data-source-id",
          "DisplayText": "My Knowledge Base",
          "SourceIndexProfileName": "content-index",
          "AIKnowledgeBaseIndexProfileName": "kb-vector-index",
          "KeyFieldName": "ContentItemId",
          "TitleFieldName": "Content.ContentItem.DisplayText",
          "ContentFieldName": "Content.ContentItem.BodyPart.Body"
        }
      ]
    }
  ]
}
```

### Triggering a Manual Re-Index

After creating or modifying source content, you can trigger a manual re-index:

1. Navigate to **Artificial Intelligence → Data Sources**.
2. Find the target data source in the list.
3. Click **Sync Index** to queue a full re-indexing of that data source.

## Linking Data Sources to AI Profiles

### Configuring a Profile via Admin UI

1. Navigate to **Artificial Intelligence → Profiles** and edit the target AI profile.
2. Go to the **Knowledge** tab.
3. Select a **Data Source** from the dropdown.
4. Configure retrieval parameters:
   - **Strictness** — How closely results must match (1–5).
   - **Top N Documents** — Maximum number of documents to retrieve (1–20).
   - **In Scope** — When enabled, restricts AI responses to only the retrieved context.
   - **Filter** — Optional OData filter expression for additional result filtering.
5. Click **Save**.

### Configuring a Profile via Recipe

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "knowledge-assistant",
          "DisplayText": "Knowledge Assistant",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ChatDeploymentName": "gpt-4o",
          "UtilityDeploymentName": "gpt-4o-mini",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful assistant. Answer questions using the provided knowledge base context."
            },
            "DataSourceMetadata": {
              "DataSourceId": "my-data-source-id"
            },
            "AIDataSourceRagMetadata": {
              "Strictness": 3,
              "TopNDocuments": 5,
              "IsInScope": true,
              "Filter": null
            }
          }
        }
      ]
    }
  ]
}
```

## Configuring the Knowledge Base Index

### Embedding Deployment

Each knowledge base index profile requires an embedding deployment. When creating or editing a knowledge base index profile of type `DataSourceConstants.IndexingTaskType`, you select the embedding deployment from available deployments that support `AIDeploymentType.Embedding`.

### Configuring Default RAG Settings

Default strictness and top-N documents can be configured in site settings:

1. Navigate to **Configuration → Settings → AI**.
2. Find the **Data Sources** section.
3. Set **Default Strictness** (1–5) and **Default Top N Documents** (1–20).
4. Click **Save**.

These defaults are applied when a profile does not specify its own values.

### Azure AI Search Index Fields

When using Azure AI Search as the knowledge base backend, the index profile handler automatically configures these fields:

| Field | Type | Properties |
|-------|------|------------|
| `ChunkId` | Text | Key, Filterable |
| `ReferenceId` | Text | — |
| `DataSourceId` | Text | Filterable |
| `ReferenceType` | Text | Filterable |
| `ChunkIndex` | Integer | — |
| `Title` | Text | Searchable |
| `Content` | Text | Searchable |
| `Timestamp` | DateTime | Filterable, Sortable |
| `Embedding` | Vector | Dimensions from embedding deployment |

### Elasticsearch Index Fields

When using Elasticsearch as the knowledge base backend, the index profile handler configures these mappings:

| Field | Mapping Type | Notes |
|-------|-------------|-------|
| `ChunkId` | Keyword | Key field |
| `ReferenceId` | Keyword | — |
| `DataSourceId` | Keyword | — |
| `ReferenceType` | Keyword | — |
| `ChunkIndex` | Integer | — |
| `Title` | Text | — |
| `Content` | Text | Default search field |
| `Timestamp` | Date | — |
| `Embedding` | DenseVector | Cosine similarity |
| `Filters` | Object | Dynamic mapping |

## Deployment Support

Data sources can be included in Orchard Core deployment plans:

1. Navigate to **Configuration → Import/Export → Deployment Plans**.
2. Add an **AI Data Source** step.
3. Choose **Include All** or select specific data sources.
4. Execute the deployment plan.

### Security Best Practices

- Only users with the `ManageAIDataSources` permission (default role Administrator) can create, edit, or delete data sources.
- Secure embedding API keys using user secrets or environment variables.
- Use OData filters on AI profiles to restrict which knowledge base documents are accessible per profile.
- Avoid using `AuthenticationType: "None"` for any connected AI services in production environments.
- Review data source field mappings carefully before creation — they cannot be changed after the initial save.

---
name: orchardcore-ai-documents-azureai
description: Skill for indexing CrestApps Orchard Core AI Documents with Azure AI Search. Covers Azure AI Search index profiles, managed chunk mappings, embeddings, vector retrieval, and scoped RAG results. Use this skill when requests mention AI Documents Azure AI Search, AIDocumentAzureAISearchIndexProfileHandler, AzureAISearchVectorSearchService, document vector indexes, or Azure AI Search document retrieval. Strong matches include work with CrestApps.OrchardCore.AI.Documents.AzureAI, IVectorSearchService, IDocumentIndexHandler, AddAzureAISearchIndexingSource, AzureAISearchIndexMetadata, and AIConstants.AIDocumentsIndexingTaskType.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Documents Azure AI Search

## Configure Azure AI Search document retrieval

You are an Orchard Core expert. Configure Azure AI Search as the vector indexing and
retrieval backend for CrestApps AI Documents. Preserve the provider-managed mappings
and configure an embedding deployment before creating an index.

### Guidelines

- Enable the exact feature ID `CrestApps.OrchardCore.AI.Documents.AzureAI`.
- Its manifest depends on the AI Documents feature through `ChatInteractionsConstants.Feature.ChatDocuments`, `OrchardCore.Indexing`, and `OrchardCore.AzureAI`. The base AI Documents capability is enabled by dependency.
- Configure an Azure AI Search connection and an embedding deployment before creating the document index.
- Create the index at **Search → Indexing** with source **AI Documents (Azure AI Search)**.
- The module registers its vector search service keyed by `AzureAISearchConstants.ProviderName`; do not resolve an unkeyed replacement when extending the pipeline.
- Select the appropriate document context feature such as Chat Interactions, Profiles, or Chat Sessions in addition to this backend.
- Install `CrestApps.OrchardCore.AI.Documents.AzureAI` in the web or startup project and protect Azure credentials through secret configuration.

### Feature registration

| Registration | Purpose |
|---|---|
| `AIDocumentAzureAISearchIndexProfileHandler` | Establishes and normalizes provider-specific Azure AI Search mappings. |
| `AIDocumentAzureAISearchDocumentIndexHandler` | Builds the provider document from document chunk records. |
| `AzureAISearchVectorSearchService` | Performs filtered vector similarity search for RAG. |
| `AddAzureAISearchIndexingSource` | Adds **AI Documents (Azure AI Search)** to Search → Indexing. |

### Enable document indexing

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.AzureAI",
        "OrchardCore.AzureAI"
      ],
      "disable": []
    }
  ]
}
```

Enable the PDF or OpenXml features separately when those file formats must be extracted.
They are optional processors and not dependencies of this index provider.

### Create the index profile

1. Configure an Azure AI Search connection in the Orchard Core Azure AI Search feature.
2. Make an embedding deployment available from a configured AI provider.
3. Go to **Search → Indexing** and create **AI Documents (Azure AI Search)**.
4. Select the connection and embedding deployment for the profile.
5. Configure the active AI Documents context to use that document index profile.
6. Upload a document and verify chunks appear in the Azure AI Search index.

The embedding deployment determines vector dimensions. Treat it as stable after data
exists; a model dimension change requires a compatible new index and reindexing.

### Managed Azure AI Search mappings

`AIDocumentAzureAISearchIndexProfileHandler` owns these fields:

| Field | Mapping |
|---|---|
| `ChunkId` | Text, key, and filterable |
| `DocumentId` | Text and filterable |
| `Content` | Text and searchable |
| `FileName` | Text and filterable |
| `ReferenceId` | Text and filterable |
| `ReferenceType` | Text and filterable |
| `ChunkIndex` | Integer |
| `Embedding` | Vector, searchable, HNSW profile `default` |

The handler computes embedding dimensions from the selected deployment. It configures
the `default` vector profile to use `default-hnsw`, preserves custom mappings, and
normalizes duplicate managed mappings when an existing profile is loaded or saved.
Do not manually add a second managed mapping to work around an index issue.

### Retrieval behavior

`AzureAISearchVectorSearchService` converts the prompt embedding to a
`VectorizedQuery` against the `Embedding` field. Every search is constrained by:

- `ReferenceId`
- `ReferenceType`

It selects the chunk ID, document ID, content, file name, and chunk index, orders the
returned chunks by descending Azure score, and returns at most the requested `topN`.
These reference filters prevent document chunks from unrelated chat, profile, or
session contexts from entering the RAG prompt.

### Query flow

1. The current user prompt is embedded with the deployment associated with the index.
2. AI Documents resolves the Azure-keyed `IVectorSearchService`.
3. The service runs k-nearest-neighbor vector search with the current reference ID and type filters.
4. Relevant chunks and source file metadata are supplied as private context to the model.
5. Azure request failures are logged and return no chunks rather than exposing stale or unscoped results.

### Configuration example

Azure AI Search connection details belong in secure Orchard Core Azure AI Search
configuration. Then use the index profile UI to select a connection and embedding
deployment; avoid hard-coding an index name into application code.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Documents.Profiles",
        "CrestApps.OrchardCore.AI.Documents.AzureAI"
      ],
      "disable": []
    }
  ]
}
```

This example enables profile-scoped documents plus the backend. It intentionally does
not require Azure Blob Storage because file storage and vector indexing are independent.

### Troubleshooting

| Symptom | Check |
|---|---|
| No AI Documents index source | Confirm `OrchardCore.Indexing`, `OrchardCore.AzureAI`, and the exact backend feature are enabled. |
| Index creation cannot select embeddings | Configure an embedding-capable deployment, not only a chat deployment. |
| Search returns no chunks | Verify indexed chunks exist, the vector dimensions match the profile, and the reference ID and type match the requesting context. |
| Managed fields duplicate after editing | Reopen and save the profile. The handler normalizes duplicate managed mappings while retaining custom fields. |
| Azure request failure | Check endpoint, credential, network access, index name, and Azure Search service logs. |
| Documents upload but do not answer questions | Confirm an active document index profile and a valid context feature, then reprocess or reindex existing documents. |

### Operational and security notes

- Keep API keys and endpoints out of recipes and repository configuration.
- Restrict Azure AI Search network and role access to the application identity.
- Reindex when changing vector dimensions or moving data to a new embedding deployment.
- Keep custom Azure mappings distinct from the eight provider-managed fields.

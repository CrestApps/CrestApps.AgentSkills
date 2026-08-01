---
name: orchardcore-ai-documents-elasticsearch
description: Skill for indexing CrestApps Orchard Core AI Documents with Elasticsearch. Covers Elasticsearch index profiles, dense vector chunk mappings, k-nearest-neighbor retrieval, default search fields, and scoped RAG results. Use this skill when requests mention AI Documents Elasticsearch, AIDocumentElasticsearchIndexProfileHandler, ElasticsearchVectorSearchService, document vector indexing, or Elasticsearch document retrieval. Strong matches include work with CrestApps.OrchardCore.AI.Documents.Elasticsearch, IVectorSearchService, IDocumentIndexHandler, AddElasticsearchIndexingSource, ElasticsearchIndexMetadata, and AIConstants.AIDocumentsIndexingTaskType.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Documents Elasticsearch

## Configure Elasticsearch document retrieval

You are an Orchard Core expert. Configure Elasticsearch as the vector indexing and
retrieval backend for CrestApps AI Documents. Use the provider-created index profile,
embedding dimensions, and reference-scoped retrieval rather than hand-written queries.

### Guidelines

- Enable the exact feature ID `CrestApps.OrchardCore.AI.Documents.Elasticsearch`.
- Its manifest depends on AI Documents through `ChatInteractionsConstants.Feature.ChatDocuments` and on `OrchardCore.Elasticsearch`; the base document feature is enabled by dependency.
- Create the index at **Search → Indexing** using **AI Documents (Elasticsearch)**.
- Configure an embedding-capable deployment before creating or reindexing the profile.
- The service is registered as a keyed `IVectorSearchService` using `ElasticsearchConstants.ProviderName`.
- Keep document storage separate from indexing. Azure Blob Storage is optional and does not replace this provider.
- Install the package in the web or startup project and keep Elasticsearch connection secrets outside recipes and source control.

### Feature registration

| Registration | Purpose |
|---|---|
| `AIDocumentElasticsearchIndexProfileHandler` | Defines chunk fields, dense vectors, and default textual fields. |
| `AIDocumentElasticsearchDocumentIndexHandler` | Maps document chunk records into Elasticsearch documents. |
| `ElasticsearchVectorSearchService` | Runs filtered k-nearest-neighbor chunk retrieval. |
| `AddElasticsearchIndexingSource` | Adds **AI Documents (Elasticsearch)** to Search → Indexing. |

### Enable document indexing

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.Elasticsearch",
        "OrchardCore.Elasticsearch"
      ],
      "disable": []
    }
  ]
}
```

Enable `CrestApps.OrchardCore.AI.Documents.Pdf` or
`CrestApps.OrchardCore.AI.Documents.OpenXml` separately for optional extraction
support. These processor features are not required by Elasticsearch itself.

### Create and use an index profile

1. Configure the Orchard Core Elasticsearch connection and ensure the application can create and query indexes.
2. Configure an embedding deployment with dimensions supported by the target Elasticsearch version.
3. In **Search → Indexing**, create **AI Documents (Elasticsearch)**.
4. Select the desired embedding deployment.
5. Associate the profile with the enabled chat, profile, or session document context.
6. Upload or reindex documents and test a query that is unique to an uploaded source.

Changing embeddings after data has been indexed can make existing vectors incompatible.
Create a compatible new index and reindex rather than mixing different dimensions.

### Provider-managed mappings

`AIDocumentElasticsearchIndexProfileHandler` configures:

| Field | Elasticsearch mapping |
|---|---|
| `ChunkId` | Keyword and index key field |
| `DocumentId` | Keyword |
| `Content` | Text |
| `FileName` | Keyword |
| `ReferenceId` | Keyword |
| `ReferenceType` | Keyword |
| `ChunkIndex` | Integer |
| `Embedding` | Indexed `dense_vector` with cosine similarity |

The dense vector dimensions come from the profile's selected embedding deployment.
When no default Elasticsearch query fields exist, the handler sets `Content` as the
default textual search field. Keep `ChunkId` stable so indexing updates and deletes
target the correct document.

### Vector retrieval behavior

`ElasticsearchVectorSearchService` performs k-nearest-neighbor search over
`Embedding` in the selected index. It requests `K = topN` and `NumCandidates = topN * 10`
and applies a Boolean filter requiring both:

- `ReferenceId` equals the active document context ID
- `ReferenceType` equals the active context type

The service reads content, chunk index, document ID, and file name from each hit,
sorts by score descending, and returns no more than `topN` chunks. Invalid Elasticsearch
responses and exceptions are logged and yield an empty result set.

### RAG request sequence

1. AI Documents generates an embedding for the current prompt.
2. It resolves `ElasticsearchVectorSearchService` with the Elasticsearch provider key.
3. The service searches the profile's `IndexFullName` and applies the two context filters.
4. The best chunk text and file metadata become model context.
5. The model answers using retrieved content without leaking chunks from a different reference.

### Example feature recipe

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Documents.ChatSessions",
        "CrestApps.OrchardCore.AI.Documents.Elasticsearch",
        "CrestApps.OrchardCore.AI.Documents.Pdf"
      ],
      "disable": []
    }
  ]
}
```

This enables session document upload, Elasticsearch retrieval, and PDF extraction.
Configure the index profile and embedding deployment in the admin UI afterward.

### Troubleshooting

| Symptom | Check |
|---|---|
| Index source is absent | Confirm `OrchardCore.Elasticsearch` and the exact document backend feature are enabled. |
| Index mapping rejects vectors | Ensure the selected embedding dimensions match the generated `dense_vector` mapping and target Elasticsearch capabilities. |
| RAG produces no sources | Verify indexing completed, `Content` is non-empty, and the document context selects this profile. |
| Expected chunks are missing | Verify `ReferenceId` and `ReferenceType`; the service deliberately filters both. |
| Search errors appear in logs | Check endpoint, authentication, TLS, index availability, and application network access. |
| Text search behavior is unexpected | Inspect the profile default query metadata; this handler defaults it to `Content` only when no fields are already selected. |

### Operational and security notes

- Use TLS and least-privilege Elasticsearch credentials.
- Do not remove context filters from custom vector search code.
- Reindex after intentional embedding model or dimension changes.
- Preserve the provider-managed keys and types when adding custom mappings.
- Monitor index health and failed indexing work before diagnosing retrieval quality.

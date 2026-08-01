---
name: orchardcore-ai-memory-elasticsearch
description: Skill for indexing CrestApps Orchard Core AI Memory with Elasticsearch. Covers memory field mappings, dense vectors, k-nearest-neighbor retrieval, default query fields, user isolation, and Elasticsearch operations. Use this skill when requests mention AI Memory Elasticsearch, AIMemoryElasticsearchIndexProfileHandler, ElasticsearchMemoryVectorSearchService, memory vector indexes, or Elasticsearch user-memory retrieval. Strong matches include work with CrestApps.OrchardCore.AI.Memory.Elasticsearch, IMemoryVectorSearchService, AIMemoryElasticsearchDocumentIndexHandler, AddElasticsearchIndexingSource, ElasticsearchIndexMetadata, and MemoryConstants.IndexingTaskType.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Memory Elasticsearch

## Configure Elasticsearch memory indexing

You are an Orchard Core expert. Configure Elasticsearch as the persistent AI Memory
vector backend while maintaining user isolation, stable provider mappings, and
embedding compatibility.

### Guidelines

- Enable the exact feature ID `CrestApps.OrchardCore.AI.Memory.Elasticsearch`.
- Its manifest depends on `CrestApps.OrchardCore.AI.Memory` and `OrchardCore.Elasticsearch`; the base memory feature is enabled by dependency.
- Create **AI Memory (Elasticsearch)** from **Search → Indexing**, select an embedding deployment, and choose it in **Settings → Artificial Intelligence → Memory**.
- The provider is registered as keyed `IMemoryVectorSearchService` using `ElasticsearchConstants.ProviderName`.
- Vector similarity never replaces authorization. Every retrieval must remain filtered to the current authenticated `userId`.
- Install this package in the web or startup project and secure the Elasticsearch connection with production secret configuration.
- Store only durable non-sensitive preferences and facts; do not persist credentials, tokens, financial data, or private keys as AI memory.

### Provider registrations

| Registration | Purpose |
|---|---|
| `AIMemoryElasticsearchIndexProfileHandler` | Defines memory mappings, vector dimensions, and default search fields. |
| `AIMemoryElasticsearchDocumentIndexHandler` | Maps persisted memory records to Elasticsearch documents. |
| `ElasticsearchMemoryVectorSearchService` | Runs user-filtered k-nearest-neighbor queries. |
| `AddElasticsearchIndexingSource` | Adds **AI Memory (Elasticsearch)** at Search → Indexing. |

### Enable the backend

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Memory.Elasticsearch",
        "OrchardCore.Elasticsearch"
      ],
      "disable": []
    }
  ]
}
```

The core AI Memory feature comes from the provider dependency. Use the Azure AI Search
provider instead when that is the selected index service; do not configure both as
masters unless the application intentionally manages separate indexes and migration.

### Configure the profile

1. Configure Orchard Core Elasticsearch connectivity and permissions.
2. Configure an embedding deployment compatible with Elasticsearch dense vectors.
3. Create **AI Memory (Elasticsearch)** under **Search → Indexing**.
4. Select the embedding deployment and save.
5. Select the profile in the AI Memory site setting.
6. Enable user memory for the desired AI profile or interaction, then test as an authenticated user.

The profile's embedding deployment defines dense-vector dimensions. If dimensions
change, create a new compatible index and reindex instead of mixing incompatible vectors.

### Managed mappings

| Field | Elasticsearch mapping |
|---|---|
| `memoryId` | Keyword and index key |
| `userId` | Keyword |
| `name` | Text |
| `description` | Text |
| `content` | Text |
| `updatedUtc` | Date |
| `embedding` | Indexed `dense_vector` using cosine similarity |

The handler uses selected embedding dimensions for `embedding`. When the profile has
no default Elasticsearch query fields, it assigns `name`, `description`, and `content`.
Do not change the key field away from `memoryId`; indexing updates and deletions depend
on the stable identifier.

### Vector retrieval behavior

`ElasticsearchMemoryVectorSearchService` searches the profile `IndexFullName` with:

- `embedding` as the k-nearest-neighbor field
- `K = topN`
- `NumCandidates = topN * 10`
- a `userId` term filter for the current authenticated user

It maps memory ID, name, description, content, updated time, and score from each hit.
Results without content are discarded, then remaining results are ordered by score and
limited to `topN`. Invalid responses and exceptions are logged and produce an empty
result set.

### Retrieval sequence

1. The current authenticated user submits a memory-aware request.
2. The query is embedded with the memory profile deployment.
3. The Elasticsearch keyed memory service executes filtered k-nearest-neighbor search.
4. Relevant user-specific records become private retrieval context or tool results.
5. The application never exposes hits from another user's `userId`.

### Troubleshooting

| Symptom | Check |
|---|---|
| AI Memory index source is absent | Enable `OrchardCore.Elasticsearch` and the exact provider feature. |
| Index rejects a mapping | Verify Elasticsearch version support and the selected embedding vector dimensions. |
| No memories are returned | Verify an authenticated identity, selected master profile, saved records, and index health. |
| Search error is logged | Inspect Elasticsearch endpoint, TLS, credentials, index availability, and network access. |
| Results cross user boundaries | Stop and correct the query. Preserve the mandatory `userId` term filter. |
| Textual memory queries behave unexpectedly | Review default query metadata; the handler assigns Name, Description, and Content only when no defaults exist. |

### Security and operations

- Use TLS, least-privilege credentials, and restricted network access for Elasticsearch.
- Validate user isolation with two distinct accounts before production rollout.
- Reindex after an intentional embedding deployment or vector-dimension change.
- Keep users informed about memory retention and support their removal requests.
- Do not bypass the shared memory store lifecycle with direct index writes unless the application also preserves indexing consistency.

### Profile maintenance

The provider handler initializes, creates, and updates Elasticsearch index profiles.
It sets mapping metadata only for profiles it can handle:

1. Confirm the profile provider is Elasticsearch.
2. Confirm the selected embedding deployment is still available and dimension-compatible.
3. Save the profile through **Search → Indexing** so managed properties remain valid.
4. Preserve `memoryId` as the key and the `userId` keyword mapping for filtering.
5. Reindex through the shared memory lifecycle after mapping or embedding changes.

Add custom fields only with new names. Do not replace the `dense_vector` field,
change its cosine similarity setting, or remove `userId` from an index that backs
shared authenticated-user memory.

### Index migration checklist

- Create a new index profile for an embedding deployment with changed dimensions.
- Create and validate the target Elasticsearch index before selecting it as master.
- Reindex memory records using the normal indexing service rather than direct bulk writes.
- Test semantic search, save, update, removal, and self-service memory clearing.
- Monitor index health and failed indexing work before diagnosing semantic retrieval.

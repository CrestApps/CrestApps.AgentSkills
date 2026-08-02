---
name: orchardcore-ai-memory-azureai
description: Skill for indexing CrestApps Orchard Core AI Memory with Azure AI Search. Covers managed memory index mappings, embedding dimensions, user-scoped vector retrieval, index profiles, and Azure AI Search operations. Use this skill when requests mention AI Memory Azure AI Search, AIMemoryAzureAISearchIndexProfileHandler, AzureAISearchMemoryVectorSearchService, memory vector indexes, or Azure user-memory retrieval. Strong matches include work with CrestApps.OrchardCore.AI.Memory.AzureAI, IMemoryVectorSearchService, AIMemoryAzureAISearchDocumentIndexHandler, AddAzureAISearchIndexingSource, AzureAISearchIndexMetadata, and MemoryConstants.IndexingTaskType.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Memory Azure AI Search

## Configure Azure AI Search memory indexing

You are an Orchard Core expert. Configure Azure AI Search for persistent user-memory
vector indexing and semantic retrieval. Maintain the strict authenticated-user filter
and use the provider-managed profile mappings.

### Guidelines

- Enable the exact feature ID `CrestApps.OrchardCore.AI.Memory.AzureAI`.
- Its manifest depends on `CrestApps.OrchardCore.AI.Memory`, `OrchardCore.Indexing`, and `OrchardCore.AzureAI`; the base AI Memory feature is enabled by dependency.
- Create **AI Memory (Azure AI Search)** from **Search → Indexing**, select its embedding deployment, then choose it in **Settings → Artificial Intelligence → Memory**.
- Memory remains authenticated-user scoped even though all users can occupy a shared external index.
- The backend registers `AzureAISearchMemoryVectorSearchService` as a keyed `IMemoryVectorSearchService` using `AzureAISearchConstants.ProviderName`.
- Install the package in the web or startup project and store Azure endpoint and credential configuration securely.
- Do not use memory to retain secrets, tokens, private keys, payment data, or other sensitive user data.

### Provider registrations

| Registration | Purpose |
|---|---|
| `AIMemoryAzureAISearchIndexProfileHandler` | Creates, updates, and normalizes Azure Search memory mappings. |
| `AIMemoryAzureAISearchDocumentIndexHandler` | Builds Azure index documents from persisted memory records. |
| `AzureAISearchMemoryVectorSearchService` | Retrieves semantically similar memories for one user. |
| `AddAzureAISearchIndexingSource` | Adds **AI Memory (Azure AI Search)** as an index source. |

### Enable the backend

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Memory.AzureAI",
        "OrchardCore.AzureAI"
      ],
      "disable": []
    }
  ]
}
```

The base memory feature is dependency-only. Do not treat it as an independent
standalone backend; enable this provider or the Elasticsearch provider alongside it.

### Configure the memory index

1. Configure the Orchard Core Azure AI Search connection.
2. Configure an embedding-capable AI deployment.
3. Create **AI Memory (Azure AI Search)** in **Search → Indexing**.
4. Select the embedding deployment and save the profile.
5. Select that profile as the Memory **Index profile** site setting.
6. Enable user memory for a supported profile or interaction and test with an authenticated user.

The selected embedding dimensions drive the vector mapping. Changing model dimensions
after memory data exists requires a new compatible index and a planned reindex.

### Managed Azure mappings

| Field | Mapping |
|---|---|
| `memoryId` | Text, key, filterable |
| `userId` | Text, filterable |
| `name` | Text, searchable, filterable |
| `description` | Text, searchable |
| `content` | Text, searchable |
| `updatedUtc` | DateTime, filterable, sortable |
| `embedding` | Searchable vector using the `default` HNSW profile |

The index-profile handler keeps a `default` vector profile and `default-hnsw`
algorithm configuration. It calculates dimensions from the selected deployment,
retains user-defined custom fields, and normalizes duplicate managed mappings when a
profile is loaded or saved.

### User-scoped vector retrieval

`AzureAISearchMemoryVectorSearchService` sends a `VectorizedQuery` against
`embedding` and requires:

```text
userId eq '<current authenticated user ID>'
```

It selects memory ID, name, description, content, and updated time; drops results
without content; orders by score descending; and takes at most `topN`. Azure request
failures and unexpected errors are logged and return no results.

Never remove the `userId` filter from a custom implementation. Semantic similarity
alone is not authorization and could expose memory belonging to another user.

### Memory retrieval flow

1. An authenticated user asks a question.
2. The application embeds the query using the profile deployment.
3. The Azure-keyed memory search service performs filtered vector search.
4. Matching personal memory can be injected as private context or returned by the
   memory tool.
5. The model responds without treating retrieved memory as a public cross-user corpus.

### Troubleshooting

| Symptom | Check |
|---|---|
| Memory index source is absent | Enable `OrchardCore.Indexing`, `OrchardCore.AzureAI`, and the exact Azure AI Memory feature. |
| No embedding deployment is selectable | Configure an embedding deployment rather than only a chat deployment. |
| Memory tool returns nothing | Confirm an authenticated identity, an active memory index profile, saved memory records, and a valid embedding configuration. |
| Index edit duplicates mappings | Reopen and save it. The handler normalizes managed duplicates while retaining custom fields. |
| Search fails | Check Azure endpoint, credentials, network rules, index availability, and vector dimensions. |
| Memories appear cross-user | Treat this as an authorization defect. Verify the active user ID and preserve the mandatory `userId` filter. |

### Security and operations

- Restrict Azure AI Search access by network and application identity.
- Keep connection strings and keys in secret providers.
- Reindex deliberately when changing embeddings or moving to another master index.
- Retain only durable, non-sensitive memory and honor user deletion requests.
- Test search with separate authenticated users to verify isolation.

### Profile maintenance

The handler runs when an Azure AI Search memory profile is initialized, created,
updated, and loaded. Reopen a profile through the supported admin experience instead
of manually editing provider metadata:

1. Confirm the profile provider is Azure AI Search.
2. Verify the intended embedding deployment remains available.
3. Save the profile so its managed fields and vector settings are normalized.
4. Reindex only after changes that affect vectors, mappings, or the target index.
5. Retest retrieval with two authenticated users after every integration change.

Custom fields are retained, but they must not reuse the seven managed memory field
names. Keep custom data and memory authorization rules separate from the mandatory
`userId` filter.

### Index migration checklist

- Create a new profile when a new embedding model has different dimensions.
- Select the new profile only after its index is created and validated.
- Reindex persistent records through the AI Memory indexing lifecycle.
- Verify old and new profiles do not receive concurrent writes beyond the planned
  transition period.

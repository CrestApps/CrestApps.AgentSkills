---
name: crestapps-core-data-sources
description: Skill for search-backed RAG, index profiles, and vector data source integration in CrestApps.Core.
---

# CrestApps.Core Data Sources - Prompt Templates

## Add Search-Backed RAG

You are a CrestApps.Core expert. Generate code and guidance for index-backed data sources in CrestApps.Core.

### Guidelines

- Use data sources when knowledge should come from durable search indexes instead of only attached documents.
- Pair data sources with the AI runtime and orchestration.
- Provide an indexing store through Entity Framework Core or YesSql when index profiles are persisted.
- Use Azure AI Search or Elasticsearch based on the chosen backend.
- Keep provider-specific search wiring behind the shared data-source abstractions.

### Feature Goals

- semantic retrieval from durable indexes
- index profile selection
- preemptive RAG and retrieval tuning
- backend-specific registration without rewriting business logic

### Common Composition Pattern

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddChatInteractions()
    )
    .AddIndexingServices(indexing => indexing
        .AddEntityCoreStores()
    )
);
```

### Design Guidance

- Use documents for uploaded file workflows.
- Use data sources for durable business indexes.
- Combine data sources with AI Profiles when retrieval boundaries should be reusable.

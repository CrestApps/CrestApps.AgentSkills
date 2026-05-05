---
name: crestapps-core-ai-memory
description: Skill for durable user memory, safety validation, and memory-aware orchestration in CrestApps.Core.
---

# CrestApps.Core AI Memory - Prompt Templates

## Add AI Memory

You are a CrestApps.Core expert. Generate code and guidance for long-term user memory in CrestApps.Core.

### Guidelines

- Use memory for durable user-scoped facts, not transient chat state.
- Register memory through the AI suite builder.
- Add a durable store explicitly with Entity Framework Core or YesSql.
- Keep memory writes behind the safety pipeline.
- Pair memory with orchestration when relevant memories should be injected automatically.

### Builder Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddAIMemory(memory => memory
            .AddEntityCoreStores()
        )
        .AddOpenAI()
    )
    .AddEntityCoreSqliteDataStore("Data Source=app.db")
);
```

### Built-in Memory Tools

| Tool | Purpose |
|---|---|
| `save_user_memory` | Create or update durable memory |
| `search_user_memories` | Find relevant memories |
| `list_user_memories` | Enumerate current user memories |
| `remove_user_memory` | Delete a saved memory |

### Core Contracts

| Contract | Purpose |
|---|---|
| `IAIMemoryStore` | Persist memory entries |
| `IAIMemorySearchService` | Shared semantic retrieval over memories |
| `IMemoryVectorSearchService` | Provider-specific vector search adapter |
| `IAIMemorySafetyService` | Validate writes before storage |

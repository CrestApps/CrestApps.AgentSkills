---
name: crestapps-core-ai-memory
description: Skill for durable, user-scoped AI memory in CrestApps.Core. Covers AddAIMemory and AddCoreAIMemory, Entity Framework Core and YesSql memory stores, the AIMemory index profile and the AddAIMemory option on the Elasticsearch, Azure AI Search and PostgreSQL backends, AIMemoryOptions IndexProfileName and TopN, enabling memory per AI profile with MemoryMetadata and for chat interactions with ChatInteractionMemoryOptions, preemptive memory retrieval through GeneralAIOptions, the save_user_memory, search_user_memories, list_user_memories and remove_user_memory tools, IAIMemorySafetyService validation, and custom IMemoryVectorSearchService implementations.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Memory

Memory stores long-lived facts about the authenticated user (preferences, projects, roles) and recalls them in later conversations. It is not for transient chat state.

## Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddAIMemory(memory => memory
            .AddEntityCoreStores()))   // or .AddYesSqlStores()
    .AddIndexingServices(indexing => indexing
        .AddEntityCoreStores()
        .AddElasticsearch(
            builder.Configuration.GetSection("CrestApps:Elasticsearch"),
            elasticsearch => elasticsearch.AddAIMemory()))
    .AddEntityCoreSqliteDataStore("Data Source=app.db"));

builder.Services.Configure<AIMemoryOptions>(options =>
{
    options.IndexProfileName = "user-memories"; // an index profile of type AIMemory
    options.TopN = 5;
});
```

- `AddAIMemory(...)` calls `AddCoreAIMemory()`, which registers `IAIMemorySafetyService`, `IAIMemorySearchService`, `AIMemoryIndexingService`, `AIMemoryOrchestrationHandler`, `AIMemoryPreemptiveRagHandler`, the options and the four memory tools.
- The store builder call registers `IAIMemoryStore` (and `ICatalog<AIMemoryEntry>`). YesSql hosts also create the index table with `CreateAIMemoryEntryIndexSchemaAsync(...)`.
- A backend's `AddAIMemory()` registers the `AIMemory` index-profile source, its profile handler and the keyed `IMemoryVectorSearchService`. Without it, memory is stored but never searched semantically.
- `AIMemoryOptions.IndexProfileName` must name an existing index profile of type `AIMemory`; otherwise indexing logs a warning and skips.

## Turning Memory On

| Surface | Switch |
|---|---|
| AI profile | `MemoryMetadata.EnableUserMemory = true` on the profile (`profile.Put(new MemoryMetadata { EnableUserMemory = true })`) |
| Chat interactions | `ChatInteractionMemoryOptions.EnableUserMemory` (default `true`) |
| Preemptive recall | `GeneralAIOptions.EnablePreemptiveMemoryRetrieval` (default `true`) |

Memory runs only for an authenticated user. The user id is read from `ClaimTypes.NameIdentifier`, falling back to `ClaimTypes.Name`.

## Tools

| Tool | Use |
|---|---|
| `save_user_memory` | create or update a memory by name |
| `search_user_memories` | semantic search over the user's memories |
| `list_user_memories` | list what is already known |
| `remove_user_memory` | forget a memory |

## Contracts

| Contract | Role |
|---|---|
| `IAIMemoryStore` | `ICatalog<AIMemoryEntry>` plus `CountByUserAsync`, `FindByUserAndNameAsync`, `GetByUserAsync` |
| `IAIMemorySearchService` | shared semantic retrieval |
| `IMemoryVectorSearchService` | provider-keyed vector search adapter |
| `IAIMemorySafetyService` | `TryValidate(name, description, content, out errorMessage)` before a write |

`AIMemoryEntry` holds `UserId`, `Name`, `Description`, `Content`, `CreatedUtc` and `UpdatedUtc`.

## Custom Safety Rules

```csharp
using CrestApps.Core.AI.Memory;

public sealed class StrictMemorySafetyService : IAIMemorySafetyService
{
    private static readonly string[] _blockedWords = ["password", "api key", "secret"];

    public bool TryValidate(string name, string description, string content, out string errorMessage)
    {
        var text = $"{name} {description} {content}";

        foreach (var word in _blockedWords)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Memories cannot contain credentials or secrets.";

                return false;
            }
        }

        errorMessage = null;

        return true;
    }
}

builder.Services.AddScoped<IAIMemorySafetyService, StrictMemorySafetyService>();
```

The default registration is a `TryAdd`, so register your implementation before or after `AddAIMemory(...)`; a plain `AddScoped` registered later wins for single resolution.

## Custom Vector Backend

Implement `IMemoryVectorSearchService.SearchAsync(SearchIndexProfile indexProfile, float[] embedding, string userId, int topN, CancellationToken)` and register it keyed by the provider name of the memory index profile. Always filter by `userId` so one user can never retrieve another user's memories.

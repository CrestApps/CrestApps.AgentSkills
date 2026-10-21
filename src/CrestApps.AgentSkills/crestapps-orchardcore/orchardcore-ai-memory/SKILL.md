---
name: orchardcore-ai-memory
description: Skill for persistent, user-scoped AI Memory in Orchard Core 4.0 with CrestApps.OrchardCore 3.0. Covers the memory index with its embedding deployment from the embedding slot, the Memory settings (AIMemorySettings, AIMemoryOptions and ChatInteractionMemoryOptions read through IOptionsMonitor), enabling memory on AI profiles, profile templates and chat interactions through MemoryMetadata, the save, search, list and remove memory tools, preemptive memory retrieval, clearing memory with ClearAIMemory and ClearAIMemoryForOthers, and the deferred indexing pipeline. Use it for CrestApps.OrchardCore.AI.Memory, CrestApps.OrchardCore.AI.Memory.AzureAI, CrestApps.OrchardCore.AI.Memory.Elasticsearch, AIMemoryEntry, IAIMemoryStore, AIMemoryIndexingService, IMemoryVectorSearchService, MemoryConstants, AlterMemoryMetadata, and EnablePreemptiveMemoryRetrieval.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Memory

## Configure persistent user memory

You are an Orchard Core expert. Generate configuration, recipes, and code that let the AI remember durable, non-sensitive facts about the signed-in user across conversations.

### Guidelines

- Memory works only for authenticated users. Every read and write is filtered by the user's `ClaimTypes.NameIdentifier`; anonymous users get no memory tools.
- `CrestApps.OrchardCore.AI.Memory` is `EnabledByDependencyOnly`; it depends on `CrestApps.OrchardCore.AI.Chat.Core` and `OrchardCore.Indexing`. Enable a provider feature and it comes along.
- Each memory is a record in the tenant store (`AIMemoryEntry`) plus one document in the configured **master memory index** (an index profile of type `AIMemory`).
- The memory index names an embedding deployment from the `embedding` slot, so the deployment must declare `textEmbedding`. It is locked once the index exists.
- Memory is for preferences, projects, recurring topics, and stable background. Never store passwords, keys, tokens, card or Social Security numbers, private keys, or connection strings; the save tool rejects obvious sensitive patterns.
- Read the options through `IOptionsMonitor<AIMemoryOptions>` and `IOptionsMonitor<ChatInteractionMemoryOptions>`; saving the Memory settings no longer releases the tenant.
- Install packages in the web/startup project.

### Features

| Feature | Feature ID | Depends on |
|---|---|---|
| AI Memory | `CrestApps.OrchardCore.AI.Memory` | `CrestApps.OrchardCore.AI.Chat.Core`, `OrchardCore.Indexing` (dependency only) |
| AI Memory indexing using Azure AI Search | `CrestApps.OrchardCore.AI.Memory.AzureAI` | AI Memory, `OrchardCore.Indexing`, `OrchardCore.AzureAI` |
| AI Memory indexing using Elasticsearch | `CrestApps.OrchardCore.AI.Memory.Elasticsearch` | AI Memory, `OrchardCore.Elasticsearch` |

Provider details are in `orchardcore-ai-memory-azureai` and `orchardcore-ai-memory-elasticsearch`.

### Enable memory with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Elasticsearch",
        "CrestApps.OrchardCore.AI.Memory.Elasticsearch"
      ],
      "disable": []
    }
  ]
}
```

Use `OrchardCore.AzureAI` and `CrestApps.OrchardCore.AI.Memory.AzureAI` for Azure AI Search.

### Setup

1. Create an embedding deployment that declares `textEmbedding` (see `orchardcore-ai-model-capabilities`).
2. Go to **Search > Indexing** and add **AI Memory (Elasticsearch)** or **AI Memory (Azure AI Search)**, then pick the embedding deployment.
3. In the **Memory** section of **Settings > Artificial Intelligence**, select the **Index profile** and the **Default top N** (1 to 20, default 5).
4. Turn on **Enable user memory** on the profiles that should remember (off by default).
5. Optionally turn off **Enable preemptive memory retrieval** in the **General** section to keep the tools but skip the upfront memory search.

### Settings and options

| Setting | Stored as | Read through | Guarded by |
|---|---|---|---|
| Memory index profile and top N | `AIMemorySettings` (`IndexProfileName`, `TopN`) | `IOptionsMonitor<AIMemoryOptions>` | `ManageAIProfiles` |
| Chat interaction memory | site `MemoryMetadata` (`EnableUserMemory`) | `IOptionsMonitor<ChatInteractionMemoryOptions>` | `ManageChatInteractionSettings` |
| Preemptive memory retrieval | `GeneralAISettings.EnablePreemptiveMemoryRetrieval` (default `true`) | `IOptionsMonitor<GeneralAIOptions>` | AI settings |

```json
{
  "steps": [
    {
      "name": "Settings",
      "AIMemorySettings": {
        "IndexProfileName": "AIMemory",
        "TopN": 5
      },
      "MemoryMetadata": {
        "EnableUserMemory": true
      }
    }
  ]
}
```

The chat interaction setting is added only when `CrestApps.OrchardCore.AI.Chat.Interactions` is enabled; the feature migration turns it on for a new tenant. Memory stays inactive until a valid memory index profile is selected.

### Enable memory on a profile

Profiles and profile-source templates store the switch in `MemoryMetadata` (**Enable user memory** on the **Knowledge** tab):

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "personal-assistant",
          "DisplayText": "Personal Assistant",
          "Type": "Chat",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful assistant. Remember durable user preferences."
            },
            "MemoryMetadata": {
              "EnableUserMemory": true
            }
          }
        }
      ]
    }
  ]
}
```

In code, use the extensions in `CrestApps.Core.AI.Memory`:

```csharp
public sealed class PersonalAssistantMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public PersonalAssistantMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync();

        profile.Name = "personal-assistant";
        profile.DisplayText = "Personal Assistant";
        profile.Type = AIProfileType.Chat;
        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "You are a helpful assistant. Remember durable user preferences.",
        });
        profile.AlterMemoryMetadata(memory => memory.EnableUserMemory = true);

        var result = await _profileManager.ValidateAsync(profile);

        if (result.Succeeded)
        {
            await _profileManager.CreateAsync(profile);
        }

        return 1;
    }
}
```

`profile.GetMemoryMetadata()` reads the switch, and `template.WithMemoryMetadata(...)` sets it on an `AIProfileTemplate`. Legacy `AIProfileMemorySettings` and `ChatInteractionMemorySettings` keys are migrated to `MemoryMetadata` automatically.

### Memory tools

| Tool | Purpose |
|---|---|
| `save_user_memory` | Create or update a named memory |
| `search_user_memories` | Semantic search over the user's memories |
| `list_user_memories` | List the user's memories |
| `remove_user_memory` | Forget a memory |

They are system tools, added to a request only when memory is enabled for that profile or interaction and the user is signed in. The orchestration prompt tells the model to save durable facts in the same turn before promising to remember them, and to search or list memory before saying it does not know.

Each memory has a stable `name` (for example `preferred_name`), a `description` (`The user's preferred name.`), and the `content`. The embedding is built from the name and description, because the raw content alone is often not meaningful for retrieval.

### Indexing pipeline

1. `AIMemoryEntryHandler` collects created, updated, and deleted entries and adds one `ShellScope` deferred task per request.
2. `AIMemoryIndexingService` loads the index named by `AIMemoryOptions.IndexProfileName`, embeds the memory with the index's deployment (`UseUsageLabels(purpose: AIUsagePurposes.Indexing)`), and builds an `AIMemoryEntryIndexDocument`.
3. The provider `IDocumentIndexHandler` maps it to `memoryId`, `userId`, `name`, `description`, `content`, `updatedUtc`, and `embedding`, and the keyed `IDocumentIndexManager` writes it.
4. Synchronizing the memory index from **Search > Indexing** rebuilds every memory into it (`SyncByIndexProfileIdsAsync`).
5. Retrieval uses the keyed `IMemoryVectorSearchService`, always filtered to the current user.

Because indexing hangs off the store lifecycle, memories written by admin actions, tools, or custom code stay in sync. This pattern is the template for `orchardcore-custom-indexing-elasticsearch` and `orchardcore-custom-indexing-azureai`.

### Clearing memory

With `OrchardCore.Users` enabled, the user editor gets an **AI Memory** section with a **Danger zone** and **Clear saved AI memory** button, shown only when the viewer is authorized and the user has memories. It uses the standard admin confirmation dialog, then removes the records and their index documents.

| Permission | Meaning |
|---|---|
| `ClearAIMemory` | Clear your own memory. Grant it to the roles whose users may clear their memory. |
| `ClearAIMemoryForOthers` | Clear another user's memory; implies `ClearAIMemory`. |

Both are given to Administrator by default. The UI and the clear endpoint check `ClearAIMemory` with the target user id as the resource; `AIMemoryAuthorizationHandler` then requires `ClearAIMemory` for your own id and `ClearAIMemoryForOthers` for anyone else. Use the same check in custom code:

```csharp
var canClear = await _authorizationService.AuthorizeAsync(User, AIPermissions.ClearAIMemory, (object)targetUserId);
```

### Upgrading from 2.x

- Replace `IOptions<AIMemoryOptions>` and `IOptions<ChatInteractionMemoryOptions>` with `IOptionsMonitor<T>.CurrentValue`; settings changes are now pushed without a tenant release.
- The memory index embedding picker lists deployments by the `embedding` slot. Declare `textEmbedding` on new embedding deployments; legacy `Purpose: "Embedding"` records are projected automatically.

### Troubleshooting

| Symptom | Check |
|---|---|
| No memory tools | The user is anonymous, the profile has **Enable user memory** off, or no memory index is selected. |
| Nothing is remembered between sessions | The memory index has no embedding deployment, or the provider feature is disabled. |
| Memory not injected up front | **Enable preemptive memory retrieval** is off; the tools still work. |
| Clear button missing | The viewer lacks `ClearAIMemory` (own memory) or `ClearAIMemoryForOthers` (another user), or the user has no memories. |
| Wrong vector size after a model change | Create a new memory index with the new deployment, select it, and synchronize it. |

---
name: orchardcore-ai-memory
description: Skill for configuring persistent, user-scoped AI Memory in Orchard Core using the CrestApps AI Memory module. Covers memory indexing backends (Azure AI Search and Elasticsearch), memory tools, preemptive memory retrieval, and per-profile memory settings. Use this skill when requests mention Orchard Core AI Memory, Persistent User Memory, Memory Indexing, AI Memory Settings, Enable User Memory on a Profile, Memory Index Profiles, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI.Memory, CrestApps.OrchardCore.AI.Memory.AzureAI, CrestApps.OrchardCore.AI.Memory.Elasticsearch, DefaultAIMemoryStore, AIMemoryIndexingService, IAIMemoryStore, IMemoryVectorSearchService.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Memory - Prompt Templates

## Configure AI Memory

You are an Orchard Core expert. Generate code, configuration, and recipes for adding persistent, user-scoped AI memory to an Orchard Core application using CrestApps modules.

### Guidelines

- AI Memory provides persistent, user-scoped memory so the AI can remember durable, non-sensitive preferences and background details for authenticated users across multiple conversations.
- Memory is only available to authenticated users. Anonymous users do not receive memory tools and cannot search, list, or save memories.
- All memory reads and writes are filtered by the current authenticated user's `ClaimTypes.NameIdentifier`, ensuring user isolation.
- The core feature (`CrestApps.OrchardCore.AI.Memory`) is enabled by dependency when a provider module is enabled.
- You must enable at least one indexing provider (Azure AI Search or Elasticsearch) alongside the memory feature.
- Each memory entry has a stable `name`, a semantic `description`, and `content`. Use short stable names so the system can update and locate the same memory later (e.g., `preferred_name`).
- Memory should store durable, non-sensitive information such as response style preferences, active projects, recurring topics, or long-lived workflow preferences.
- Never store secrets, API keys, tokens, credit card numbers, Social Security numbers, private keys, or connection strings in memory.
- Memory indexing is triggered from deferred catalog entry handlers, keeping the tenant store and external index in sync automatically.
- Install CrestApps packages in the web/startup project.

### Feature Overview

| Feature | Feature ID | Description |
|---------|-----------|-------------|
| AI Memory (Core) | `CrestApps.OrchardCore.AI.Memory` | Core memory storage, tools, and user-scoped persistence |
| AI Memory (Azure AI Search) | `CrestApps.OrchardCore.AI.Memory.AzureAI` | Azure AI Search indexing and vector search for memory |
| AI Memory (Elasticsearch) | `CrestApps.OrchardCore.AI.Memory.Elasticsearch` | Elasticsearch indexing and vector search for memory |

### Built-In Memory Tools

When user memory is enabled for a profile and the user is authenticated, four system tools are automatically available:

| Tool | Description |
|------|-------------|
| Save User Memory | Create or update a named memory entry for the current user |
| Search User Memories | Semantic vector search across the current user's saved memories |
| List User Memories | Enumerate the current user's existing memories |
| Remove User Memory | Remove a saved memory entry when it should be forgotten |

The orchestration prompt instructs the model to call Save User Memory in the same turn before claiming it will remember durable facts. Memory tools are only force-included for requests where user memory is enabled for the current authenticated user.

### How Memory Works

1. The user shares a durable preference or fact during a conversation.
2. The AI calls the **Save User Memory** tool with a stable `name`, a semantic `description`, and the `content`.
3. The entry is persisted in the tenant store and indexed into the configured master memory index with an embedding vector.
4. On subsequent conversations, if preemptive retrieval is enabled, matching memories are injected into the system prompt as private background context before the model answers.
5. The **Search User Memories** tool remains available for follow-up lookups when the initial memory context is not enough.

### Enabling AI Memory with Azure AI Search

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Memory.AzureAI"
      ],
      "disable": []
    }
  ]
}
```

### Enabling AI Memory with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Memory.Elasticsearch"
      ],
      "disable": []
    }
  ]
}
```

### Creating a Memory Index

1. Enable the corresponding Orchard Core search feature for your provider.
2. Navigate to **Search → Indexing**.
3. Create a new index using either **AI Memory (Azure AI Search)** or **AI Memory (Elasticsearch)**.
4. Choose the embedding connection that should be used for memory indexing and search.

### Configuring Global Memory Settings

Navigate to **Settings → Artificial Intelligence → Memory** and configure:

- **Index profile** — the master memory index used for storing memories.
- **Default top N** — the default number of matching memories returned by searches.

Preemptive memory retrieval is controlled separately under **Settings → Artificial Intelligence → General** through **Enable Preemptive Memory Retrieval**. This lets you keep user memory tools enabled while turning off the upfront memory injection step for the tenant.

### Enabling User Memory on an AI Profile

AI Profiles expose **Enable User Memory** in the **Interactions** card of the profile editor.

- Default is **disabled**
- Scope is per profile

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "assistant-with-memory",
          "DisplayText": "Personal Assistant",
          "WelcomeMessage": "Hello! I remember your preferences.",
          "Type": "Chat",
          "TitleType": "InitialPrompt",
          "ChatDeploymentName": "gpt-4o",
          "UtilityDeploymentName": "gpt-4o-mini",
          "Properties": {
            "AIProfileMetadata": {
              "SystemMessage": "You are a helpful assistant with persistent memory. Remember user preferences and apply them across sessions.",
              "Temperature": 0.3,
              "MaxTokens": 4096,
              "PastMessagesCount": 10
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

### Enabling User Memory on an AI Profile in Code

```csharp
public sealed class MemoryProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _profileManager;

    public MemoryProfileMigrations(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _profileManager.NewAsync();

        profile.Name = "assistant-with-memory";
        profile.DisplayText = "Personal Assistant";
        profile.Type = AIProfileType.Chat;

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "You are a helpful assistant with persistent memory.",
            Temperature = 0.3f,
            MaxTokens = 4096,
            PastMessagesCount = 10,
        });

        profile.WithMemoryMetadata(new MemoryMetadata
        {
            EnableUserMemory = true,
        });

        await _profileManager.SaveAsync(profile);

        return 1;
    }
}
```

### Enabling User Memory for Chat Interactions

Chat Interactions add a site setting under **Settings → Artificial Intelligence → Chat Interactions**:

- **Enable User Memory** — default is **enabled**
- Memory retrieval and indexing only become active after a valid memory index profile is configured

### Key Services

| Service | Description |
|---------|-------------|
| `IAIMemoryStore` | Memory record persistence abstraction (implemented by `DefaultAIMemoryStore` using YesSql) |
| `AIMemoryIndexingService` | Generates embeddings and indexes memory entries via `IDocumentIndexManager` |
| `IMemoryVectorSearchService` | Provider-specific vector search (keyed by provider name) |
| `AIMemoryOptionsConfiguration` | Reads `AIMemorySettings` from site settings into `AIMemoryOptions` |

### Memory Index Document Fields

| Field | Type | Description |
|-------|------|-------------|
| `MemoryId` | Keyword / Filterable | Unique memory entry identifier |
| `UserId` | Keyword / Filterable | Owning user identifier |
| `Name` | Text / Searchable | Stable memory name for lookup and update |
| `Description` | Text / Searchable | Semantic description used for search embeddings |
| `Content` | Text / Searchable | The memory content value |
| `UpdatedUtc` | Date / Sortable | Last update timestamp |
| `Embedding` | Vector | Embedding vector for semantic search |

### Clearing User Memory

Users can clear their own saved AI memory from their user profile editor. The clear option is only shown when a user is editing their own profile. A confirmation checkbox is required before memory is removed. Clearing memory removes stored records and deletes indexed documents from the configured master memory index.

### NuGet Packages

| Package | Description |
|---------|-------------|
| `CrestApps.OrchardCore.AI.Memory` | Core AI memory module |
| `CrestApps.OrchardCore.AI.Memory.AzureAI` | Azure AI Search indexing provider for AI memory |
| `CrestApps.OrchardCore.AI.Memory.Elasticsearch` | Elasticsearch indexing provider for AI memory |

Install in the web/startup project:

```shell
dotnet add package CrestApps.OrchardCore.AI.Memory.AzureAI
```

or

```shell
dotnet add package CrestApps.OrchardCore.AI.Memory.Elasticsearch
```

### Security Best Practices

- AI Memory is only available to authenticated users; anonymous users never receive memory tools.
- All memory queries are scoped by `userId`, ensuring strict user isolation.
- The built-in save tool rejects obvious sensitive patterns such as passwords and API keys.
- Store embedding service credentials using `dotnet user-secrets` during development and environment variables or Azure Key Vault in production.
- After starting to store production memory data, avoid changing the configured master index unless you plan a full re-index.

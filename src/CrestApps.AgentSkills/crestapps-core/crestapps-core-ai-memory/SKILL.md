---
name: crestapps-core-ai-memory
description: Skill for AI memory in CrestApps.Core. Covers AddAIMemory, AddCoreAIMemory and AddCoreAIMemoryIndexing, Entity Framework Core and YesSql memory stores, the AIMemory index profile on the Elasticsearch, Azure AI Search and PostgreSQL backends, AIMemoryOptions, the store fallback when no index is usable, background memory indexing, the three kinds of memory (session memory with AIConversationMemory summaries and search_conversation, past conversations with search_past_conversations and list_recent_conversations, durable user memory) switched by MemoryMetadata and ChatInteractionMemoryOptions, preemptive memory retrieval and RunsWhenPreemptiveRagIsDisabled, owner-only chat sessions, the user memory tools, IAIMemorySafetyService validation, and custom IMemoryVectorSearchService implementations.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core AI Memory

Three kinds of memory share one registration:

| Kind | Remembers | Switch | Covers |
|---|---|---|---|
| Session memory | the earlier part of a long conversation | `MemoryMetadata.EnableSessionMemory`; `ChatInteractionMemoryOptions.EnableSessionMemory` (default `true`) | the conversation itself, including anonymous visitors |
| Past conversations | the user's earlier conversations with the same profile | `MemoryMetadata.EnablePastConversations` | signed-in users only |
| User memory | durable facts (preferences, projects, roles) | `MemoryMetadata.EnableUserMemory`; `ChatInteractionMemoryOptions.EnableUserMemory` (default `true`) | signed-in users only |

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

- `AddAIMemory(...)` calls `AddCoreAIMemory()` and `AddCoreAIMemoryIndexing()`. `AddCoreAIMemory()` registers `IAIMemorySafetyService`, `IAIMemorySearchService`, `ITextTokenizer`, `AIMemoryOrchestrationHandler`, `ConversationMemoryOrchestrationHandler`, `AIMemoryPreemptiveRagHandler`, the options, the four user-memory tools and the three conversation tools.
- `AddCoreAIMemoryIndexing()` registers the `ICatalogEntryHandler<AIMemoryEntry>` that queues every saved or deleted memory and a hosted background service that writes it to the index, so a save never waits on an embedding call and a failed embedding does not fail the save. A host with its own indexing handler calls `services.AddCoreAIMemory()` alone, or memories are indexed twice.
- The store builder call registers `IAIMemoryStore` (and `ICatalog<AIMemoryEntry>`). YesSql hosts also create the index table with `CreateAIMemoryEntryIndexSchemaAsync(...)`.
- A backend's `AddAIMemory()` registers the `AIMemory` index-profile source, its profile handler and the keyed `IMemoryVectorSearchService`. Indexing and search use the same embedding deployment, read from the index profile's property or its metadata.
- `AIMemoryOptions.IndexProfileName` names an index profile of type `AIMemory`. It is optional: see the fallback below.

## Turning Memory On

```csharp
using CrestApps.Core.AI.Models;

profile.Put(new MemoryMetadata
{
    EnableUserMemory = true,
    EnableSessionMemory = true,
    EnablePastConversations = true,
});
```

- Preemptive recall follows `GeneralAIOptions.EnablePreemptiveMemoryRetrieval` (default `true`) on its own: turning off site-wide preemptive RAG no longer stops memory retrieval. A custom `IPreemptiveRagHandler` with its own switch returns `true` from `RunsWhenPreemptiveRagIsDisabled` (default `false`) to get the same behavior.
- User memory and past conversations run only for an authenticated user. The user id is read from `ClaimTypes.NameIdentifier`, falling back to `ClaimTypes.Name`.

## Search Without an Index

`IAIMemorySearchService` falls back to the memory store when no index profile is configured, the configured one is missing, is not an `AIMemory` profile, has no vector-search provider or no resolvable embedding deployment (one warning per profile name), or the index holds nothing for the user. The fallback reads up to 200 of the user's memories and ranks them by the share of query terms found in name, description and content, then by most recently updated.

## Session Memory

With a positive past-messages count, messages that fall outside the window are no longer simply dropped:

- Once six or more have fallen out unsummarized, the utility deployment folds them into a running summary stored on the `AIChatSession` or `ChatInteraction` as `AIConversationMemory` (`Summary`, `SummarizedMessageCount`, `UpdatedUtc`), about every three turns. Usage purpose `AIUsagePurposes.ConversationSummarization`.
- Messages not yet summarized are shown verbatim, and up to three older turns that share terms with the current message are recalled word for word, all in an "Earlier in This Conversation" system block.
- With tools enabled the model can call `search_conversation`, which reads only the older messages placed on the current `AIInvocationScope`, never a store.
- Clearing the history restarts the summary; a failed summary keeps the previous one. Conversation privacy redacts or strips the summary with the transcript.

## Past Conversations

`search_past_conversations` and `list_recent_conversations` search the signed-in user's own conversations with the same profile, newest first, matching title, stored summary and transcript. The store query filters with `AIChatSessionQueryContext.UserId`, and every result is checked again against the user and profile, so a store that ignores the filter leaks nothing and logs a warning. They are not offered to anonymous visitors. Tool purposes: `AIToolPurposes.ConversationMemory` and `AIToolPurposes.PastConversations`.

## Session Ownership

`AIChatHubCore` calls `AuthorizeSessionAsync` before it loads, continues, rates or acts on a session by id. By default only the signed-in user who started it, or the same visitor, gets in; another id is reported as not found, and a message sent to it starts a new session. Override it to let staff open other users' conversations (see `crestapps-core-chat-interactions`).

## Tools

| Tool | Use |
|---|---|
| `save_user_memory` | create or update a memory by name |
| `search_user_memories` | semantic search over the user's memories (store fallback without an index) |
| `list_user_memories` | list what is already known |
| `remove_user_memory` | forget a memory |
| `search_conversation` | search the earlier part of the current conversation (session memory) |
| `search_past_conversations` | search the user's earlier conversations with the profile |
| `list_recent_conversations` | list the user's most recent earlier conversations with the profile |

The conversation tools are offered only when their feature is on and there is something to read.

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

## Upgrading Within 2.0 Previews

- **Memory indexing moved into the framework.** The `ICatalogEntryHandler<AIMemoryEntry>` that indexed memories lived in the sample hosts. A host that calls `AddAIMemory()` and still registers its own indexing handler removes it, or each memory is indexed twice. A host that calls `AddCoreAIMemory()` alone (as the Orchard Core module does) is unaffected.
- `AIMemorySearchService` takes an `IAIMemoryStore` and an `ITextTokenizer` for its store fallback; code that constructs it directly passes both.
- Retrieved memories now reach the prompt: the retrieved-memory block used to render anonymous objects that Fluid could not read, so every entry was a blank `Content:` line.
- The chat hub now opens a session only for its owner; hosts whose staff open other users' sessions must override `AuthorizeSessionAsync`.

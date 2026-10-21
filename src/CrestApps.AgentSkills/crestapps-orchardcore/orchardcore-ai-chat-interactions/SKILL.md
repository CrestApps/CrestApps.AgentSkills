---
name: orchardcore-ai-chat-interactions
description: Skill for configuring AI Chat Interactions in Orchard Core with CrestApps modules. Covers the CrestApps.OrchardCore.AI.Chat.Interactions feature for ad-hoc chats without predefined profiles, per-interaction chat, utility and conversation deployments, tools, agents, tool instances, personas and prompt templates, the Knowledge panel with data sources, OData filters and uploads through CrestApps.OrchardCore.AI.Documents.ChatInteractions, the site-wide ChatInteractionChatModeSettings, the generate_image and generate_chart system tools, Chat with Preset Settings cloning, the IChatInteractionSettingsHandler pipeline, ChatInteractionHub methods, the AIChatInteractions admin list, permissions, and upgrading from deployment purposes. Use this skill when requests mention Orchard Core Chat Interactions, ad-hoc AI chat, chat playground, or chat interaction settings. Strong matches include ChatInteraction, ChatInteractionHub, IChatInteractionSettingsHandler, ListChatInteractions, and EditChatInteractions.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Interactions - Prompt Templates

## Configure AI Chat Interactions

You are an Orchard Core expert. Generate code, configuration, and recipes for ad-hoc AI chat interactions, with optional document upload and retrieval, using the CrestApps modules.

### Guidelines

- `CrestApps.OrchardCore.AI.Chat.Interactions` gives chat without a predefined AI profile. It depends on `OrchardCore.SignalR`, `OrchardCore.Liquid`, `CrestApps.OrchardCore.Resources`, `CrestApps.OrchardCore.AI` and `CrestApps.OrchardCore.AI.Chat.Core`.
- An interaction picks its own chat and utility deployments. Left empty, the connection default and then **Default Chat Deployment** in **Settings → Artificial Intelligence → Default Deployments** apply. Dropdowns are grouped by connection.
- The chat deployment picker lists text-capable deployments only. A speech-to-speech model goes in the **Conversation deployment** picker. See `orchardcore-ai-realtime-voice`.
- **New Chat** creates and saves the interaction immediately. Settings changes are saved over SignalR (`SaveSettings`) through the `IChatInteractionSettingsHandler` pipeline, so orchestrator-specific fields need no change to the hub or `chat-interaction.js`.
- The settings panel covers model parameters, the orchestrator, tools, agents, MCP and A2A connections, tool instances, prompt templates and an **AI persona** (with `CrestApps.OrchardCore.AI.Personas`).
- Image and chart generation are the orchestration system tools `generate_image` and `generate_chart`, not interaction intents. `generate_image` needs a deployment declaring `imageOutput` in the `image` slot.
- Chat mode is a **site** setting for interactions (`ChatInteractionChatModeSettings`). An interaction has no chat mode of its own.
- Install CrestApps packages in the web/startup project. Never put API keys in recipes.
- All recipe JSON uses the root `{ "steps": [...] }` format and C# classes are `sealed`, except view models.

### Enable Chat Interactions

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Permissions and Admin List

| Permission | Allows |
|---|---|
| `ListChatInteractions` | Open **Artificial Intelligence → Chat Interactions** |
| `ListChatInteractionsForOthers` | See other users' interactions |
| `EditChatInteractions` / `EditOwnChatInteractions` | Change interaction settings; changing the site chat mode needs `EditChatInteractions` |
| `DeleteChatInteraction` / `DeleteOwnChatInteraction` | Delete interactions |
| `ManageChatInteractionSettings` | Chat interaction memory settings |

The list renders through Orchard Core's `AdminList` shape as `AIChatInteractions` (rows `ChatInteraction_SummaryAdmin`; grid columns `Name`, `Modified`, `Actions`; bulk **Delete**). Add a column with an `IAdminListColumnsProvider` for `AIChatInteractions`; see `orchardcore-admin-lists`. AI Profile Builder sessions are kept out of this list; see `orchardcore-ai-profile-builder`.

### Knowledge Panel

- With `CrestApps.OrchardCore.AI.DataSources` enabled, pick a data source and set strictness, retrieved document count, scope restriction and an OData filter. An invalid filter rejects the save with *"Invalid filter value. It must be a valid OData filter."*
- The `search_data_sources` tool the model calls uses the same filter, strictness and document count as preemptive retrieval.
- A filter is honored on Elasticsearch knowledge bases and ignored, with a warning, on Azure AI Search ones. See `orchardcore-ai-data-sources`.
- **Document retrieval mode** (`Chunk` or `Hierarchical`) appears with the AI Documents features; blank uses the site default.

### Document Upload and Retrieval

`CrestApps.OrchardCore.AI.Documents.ChatInteractions` adds uploads to the **Knowledge** panel. Pair it with a vector store module.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.Elasticsearch",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

1. Create an embedding deployment that declares `textEmbedding`.
2. In **Search → Indexing**, add an **AI Documents** index that uses it.
3. In **Settings → Artificial Intelligence**, select that index in the document settings, choose a retrieval mode, and enable **Allow document uploads in chat interactions** (and image uploads, which also need a vision deployment).
4. Open an interaction, go to **Knowledge**, and upload a file.

Uploads pass through Orchard Core's file creation pipeline first, so enabling `OrchardCore.Antivirus` scans them. Extraction, tabular data and the PowerPoint, Word and PDF agents belong to `orchardcore-ai-documents` and `orchardcore-ai-documents-extractors`.

### Image and Chart Generation

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
          {
            "Name": "gpt-4o",
            "ClientName": "OpenAI",
            "ConnectionName": "openai",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "textGeneration", "toolCalling", "streaming" ]
              }
            }
          },
          {
            "Name": "dall-e-3",
            "ClientName": "OpenAI",
            "ConnectionName": "openai",
            "Properties": {
              "AIDeploymentMetadata": {
                "Features": [ "imageOutput" ]
              }
            }
          }
        ]
      }
    }
  }
}
```

Select `dall-e-3` as **Default image deployment** in **Settings → Artificial Intelligence**, or on the interaction. `generate_chart` returns a Chart.js configuration and needs no image deployment. See `orchardcore-ai-model-capabilities` for slots.

### Chat Mode and Voice

| Site setting (`ChatInteractionChatModeSettings`) | Meaning |
|---|---|
| `ChatMode` | `TextInput` (default), `AudioInput` or `Conversation` for every interaction |
| `EnableTextToSpeechPlayback` | Adds a read-aloud button to each answer (needs a text-to-speech deployment) |

Set them in the **Chat Interactions** section of **Settings → Artificial Intelligence**. In Conversation mode, naming a **Conversation deployment** on an interaction (`ChatInteraction.ConversationDeploymentName`) is how that interaction asks to speak; the per-interaction **Voice** picker lists the voices of whichever model resolves. A site can no longer force realtime globally through this setting.

### Chat with Preset Settings

Cloning an interaction copies its data source, filter, strictness, document count, scope restriction, tool instances, model parameters and prompt templates. Data that belongs to one user, such as a Copilot session's access tokens, is not copied.

### Extend the Settings Pipeline

Implement `IChatInteractionSettingsHandler` to persist extra fields that the settings panel posts, the way the Claude and Copilot orchestrators store `ClaudeSessionMetadata` and `CopilotSessionMetadata`. Render the fields with a `DisplayDriver<ChatInteraction>`.

### ChatInteractionHub Methods

| Method | Purpose |
|---|---|
| `LoadInteraction` | Load an interaction and join its group |
| `SendMessage` | Stream a reply to a typed prompt |
| `SaveSettings` | Persist interaction settings, including agents, tools and persona |
| `ClearHistory` | Clear the interaction's messages |
| `SendAudioStream` | Stream audio for dictation |
| `StartConversation` / `StopConversation` | Speech-to-text plus text-to-speech conversation |
| `StartRealtimeConversation` / `StartRealtimeWebRtc` | Realtime voice over WebSocket or WebRTC |
| `GetRealtimeIceServers`, `AddRealtimeIceCandidate`, `UpdateRealtimeSettings`, `UpdateRealtimeSpeechSpeed` | Realtime session control |
| `SynthesizeSpeech` | Read an answer aloud |
| `HandleNotificationAction` | Run a notification action, see `orchardcore-ai-chat-notifications` |

### Configuration

Large tabular documents are processed in batches configured under `CrestApps:AI:ChatInteractions:BatchProcessing` (`RowBatchSize` default `25`, `MaxConcurrentBatches` `3`, `MaxRowsPerDocument` `1000`, `BatchTimeoutSeconds` `300`). User memory for interactions is covered by `orchardcore-ai-memory`.

### Upgrading from 2.x

- Replace `"Purpose": "Chat"`, `"Image"` or `"Embedding"` on deployments with `Properties.AIDeploymentMetadata.Features` (`textGeneration`, `imageOutput`, `textEmbedding`). Legacy purposes still import.
- A realtime model no longer goes in the chat deployment; name it as the interaction's conversation deployment.
- The list uses the `AdminList` shape. A whole-row theme override only applies to the List layout; customize the Grid with `SummaryAdminGrid` placement or `AdminListCell-AIChatInteractions-{Column}.cshtml`.

See [ai-chat-interactions-examples.md](references/ai-chat-interactions-examples.md) for complete recipes.

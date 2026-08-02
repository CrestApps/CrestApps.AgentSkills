---
name: orchardcore-ai-chat-interactions
description: Skill for configuring AI Chat Interactions in Orchard Core with CrestApps modules. Covers ad-hoc chat sessions, document upload with RAG support, deployment settings, system tools for image and chart generation, and SignalR chat methods. Use it for CrestApps.OrchardCore.AI.Chat.Interactions, CrestApps.OrchardCore.AI.Documents.ChatInteractions, current AI data sources, and related Orchard Core setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Interactions - Prompt Templates

## Configure AI Chat Interactions

You are an Orchard Core expert. Generate code, configuration, and recipes for adding ad-hoc AI chat interactions with document upload and RAG to an Orchard Core application using CrestApps modules.

### Guidelines
- The AI Chat Interactions module (`CrestApps.OrchardCore.AI.Chat.Interactions`) provides ad-hoc chat without predefined AI profiles.
- Users can configure temperature, TopP, max tokens, frequency/presence penalties, and past messages count per session.
- The current interaction flow is deployment-driven: users select chat and utility deployments per interaction, or rely on the configured site-level default deployments when explicit deployments are not set.
- Users can select agents from the Capabilities tab to enhance interaction capabilities. Agent selection is saved via the SignalR hub.
- The Capabilities tab is organized: MCP Connections first, then Agents, then Tools.
- All chat messages are persisted and sessions can be resumed later.
- The AI Documents modules add document upload with RAG (Retrieval Augmented Generation) support.
- Document-aware chat interactions should use `CrestApps.OrchardCore.AI.Documents.ChatInteractions` plus a current data-source module such as `CrestApps.OrchardCore.AI.DataSources.AzureAI` or `CrestApps.OrchardCore.AI.DataSources.Elasticsearch`.
- Image and chart generation are orchestration system tools (`generate_image` and `generate_chart`), not interaction intents. Image generation requires a deployment with the `Image` purpose.
- Install CrestApps packages in the web/startup project.
- Always secure API keys using user secrets or environment variables.

### Enabling AI Chat Interactions

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Getting Started

1. Enable the `AI Chat Interactions` feature in the Orchard Core admin under **Configuration → Features**.
2. Navigate to **Artificial Intelligence → Chat Interactions**.
3. Click **+ New Chat** and select the chat and utility deployments you want to use, or rely on the site-level default deployments.
4. Configure chat settings (temperature, tools, orchestrator, documents) and start chatting.

Chat interactions are authored as ad-hoc sessions rather than predefined AI profiles. In current guidance, the interaction chooses deployments directly and does not require a profile `Source` in authoring recipes or prompts.

### Configuring Image Generation

To enable image generation, create a deployment with `Purpose: Image`. The orchestration system tool `generate_image` resolves that deployment. The `generate_chart` system tool creates a Chart.js configuration and does not require an image deployment.

**Via Admin UI:** Navigate to **Artificial Intelligence → Deployments** and create an Image deployment (e.g., `dall-e-3`), then optionally set it as a default Image deployment.

**Via appsettings.json:**

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
          { "Name": "gpt-4o", "ClientName": "OpenAI", "ConnectionName": "default", "Purpose": "Chat" },
          { "Name": "dall-e-3", "ClientName": "OpenAI", "ConnectionName": "default", "Purpose": "Image" }
        ]
      }
    }
  }
}
```

### Enabling Document Upload and RAG

The AI Documents for Chat Interactions feature (`CrestApps.OrchardCore.AI.Documents.ChatInteractions`) adds document upload and document-aware prompt processing. Pair it with the current data-source feature for the backend you are using.

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.DataSources.AzureAI",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

Or for Elasticsearch:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.DataSources.Elasticsearch",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Setting Up Document Indexing

1. Enable the current data-source feature for Elasticsearch or Azure AI Search.
2. Navigate to **Search → Indexing** and create a new index (e.g., "ChatDocuments").
3. Navigate to **Settings → Artificial Intelligence → Chat Interactions** and select the new index as the default document index.
4. Enable the `AI Documents for Chat Interactions` feature.

### Configuring Embedding Model for Documents

Documents require an embedding deployment for RAG. Define it in `appsettings.json` with `Purpose: Embedding`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "Deployments": [
          { "Name": "gpt-4o", "ClientName": "OpenAI", "ConnectionName": "default", "Purpose": "Chat" },
          { "Name": "text-embedding-3-small", "ClientName": "OpenAI", "ConnectionName": "default", "Purpose": "Embedding" },
          { "Name": "gpt-4o-mini", "ClientName": "OpenAI", "ConnectionName": "default", "Purpose": "Utility" }
        ]
      }
    }
  }
}
```

For file-format, tabular-data, and custom document-processing guidance, use the AI Documents and AI Documents Extractors features. They own extraction and document-specific tools; Chat Interactions only supplies the chat context and upload UI.

### Enabling PDF and Office Document Support

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml"
      ],
      "disable": []
    }
  ]
}
```

### Document Upload API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/ai/chat-interactions/upload-document` | POST | Upload one or more documents |
| `/ai/chat-interactions/remove-document` | POST | Remove a document |

### Chat Mode in Chat Interactions

Chat interactions support the same `ChatMode` options as AI profiles, but configured at the site level via `ChatInteractionChatModeSettings` (under **Settings → Artificial Intelligence → Chat Interactions**):

| Mode | Description | Requirements |
|------|-------------|--------------|
| `TextInput` | Standard text-only chat (default) | None |
| `AudioInput` | Adds microphone button for speech-to-text dictation | `DefaultSpeechToTextDeploymentName` configured |
| `Conversation` | Two-way voice conversation | Both `DefaultSpeechToTextDeploymentName` and `DefaultTextToSpeechDeploymentName` configured |

Unlike AI profiles (configured per profile), chat interactions use a **single site-wide setting** that applies to all chat interaction sessions.

### SignalR Hub Methods (ChatInteractionHub)

| Method | Description |
|--------|-------------|
| `SendMessage` | Sends a text message |
| `LoadInteraction` | Loads an interaction and joins its SignalR group |
| `SaveSettings` | Persists interaction settings, including selected agents and tools |
| `SendAudioStream` | Streams audio chunks for speech-to-text transcription |
| `StartConversation` | Starts a full two-way voice conversation |
| `SynthesizeSpeech` | Converts text to speech audio |
| `ClearHistory` | Clears chat history for a session |
| `HandleNotificationAction` | Dispatches a notification action |
| `StopConversation` | Cancels the active conversation |

### Voice Configuration

When conversation mode is enabled, voices are populated from the configured TTS deployment. Voices are grouped by language in dropdown menus and sorted alphabetically. Each `SpeechVoice` includes `Id`, `Name`, `Language`, `Gender`, and `VoiceSampleUrl`.

### Conversation Mode Behavior

In conversation mode:
1. User clicks the headset button → persistent audio stream opens
2. Microphone, send button, and textarea are hidden/disabled
3. User speaks → audio streams to server via SignalR → STT transcribes → text appears as user message
4. Transcript is automatically sent to AI orchestrator → AI response text streams to message list AND audio streams back
5. User can interrupt by speaking → cancels current AI response → processes new prompt
6. User clicks headset again → ends conversation, restores normal UI

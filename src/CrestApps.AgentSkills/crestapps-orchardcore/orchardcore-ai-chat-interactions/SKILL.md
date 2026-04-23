---
name: orchardcore-ai-chat-interactions
description: Skill for configuring AI Chat Interactions in Orchard Core using the CrestApps module. Covers ad-hoc chat sessions, prompt routing with intent detection, document upload with RAG support, image and chart generation, and custom processing strategies. Use this skill when requests mention Orchard Core AI Chat Interactions, Configure AI Chat Interactions, Enabling AI Chat Interactions, Getting Started, Built-in Intents, Configuring Image Generation, or closely related Orchard Core implementation, setup, extension, or troubleshooting work. Strong matches include work with CrestApps.OrchardCore.AI, CrestApps.OrchardCore.OpenAI, CrestApps.OrchardCore.AI.DataSources.AzureAI, CrestApps.OrchardCore.AI.DataSources.Elasticsearch, IServiceCollection, ChatInteractionChatModeSettings, GenerateImage. It also helps with ai chat interactions examples, Built-in Intents, Configuring Image Generation, Configuring Intent Detection Model, plus the code patterns, admin flows, recipe steps, and referenced examples captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Chat Interactions - Prompt Templates

## Configure AI Chat Interactions

You are an Orchard Core expert. Generate code, configuration, and recipes for adding ad-hoc AI chat interactions with document upload, RAG, and intent-based prompt routing to an Orchard Core application using CrestApps modules.

### Guidelines

- The AI Chat Interactions module (`CrestApps.OrchardCore.AI.Chat.Interactions`) provides ad-hoc chat without predefined AI profiles.
- Users can configure temperature, TopP, max tokens, frequency/presence penalties, and past messages count per session.
- The current interaction flow is deployment-driven: users select chat and utility deployments per interaction, or rely on the configured site-level default deployments when explicit deployments are not set.
- Users can select agents from the Capabilities tab to enhance interaction capabilities. Agent selection is saved via the SignalR hub.
- The Capabilities tab is organized: MCP Connections first, then Agents, then Tools.
- All chat messages are persisted and sessions can be resumed later.
- Prompt routing uses intent detection to classify user prompts and route them to specialized processing strategies.
- Intent detection can use a dedicated lightweight AI model or fall back to keyword-based detection.
- The AI Documents modules add document upload with RAG (Retrieval Augmented Generation) support.
- Document-aware chat interactions should use `CrestApps.OrchardCore.AI.Documents.ChatInteractions` plus a current data-source module such as `CrestApps.OrchardCore.AI.DataSources.AzureAI` or `CrestApps.OrchardCore.AI.DataSources.Elasticsearch`.
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

### Built-in Intents

The AI Chat Interactions module ships with default intents for image and chart generation:

| Intent | Description | Example Prompts |
|--------|-------------|-----------------|
| `GenerateImage` | Generate an image from a text description | "Generate an image of a sunset", "Create a picture of a cat" |
| `GenerateImageWithHistory` | Generate an image using conversation context | "Based on the above, draw a diagram" |
| `GenerateChart` | Generate a chart or graph specification | "Create a bar chart of sales data", "Draw a pie chart" |

### Configuring Image Generation

To enable image generation, create a typed deployment with `Type: Image`, or define an Image deployment in `appsettings.json`.

**Via Admin UI:** Navigate to **Artificial Intelligence → Deployments** and create an Image deployment (e.g., `dall-e-3`), then optionally set it as a default Image deployment.

**Via appsettings.json:**

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "Deployments": [
                { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true },
                { "Name": "dall-e-3", "Type": "Image", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

### Configuring Intent Detection Model

Use a lightweight model for intent classification to optimize costs:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "Deployments": [
                { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true },
                { "Name": "gpt-4o-mini", "Type": "Utility", "IsDefault": true },
                { "Name": "dall-e-3", "Type": "Image", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

If no Utility deployment is configured, the system retries deployment resolution using the Chat type as a last resort before falling back to keyword-based intent detection.

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

Documents require an embedding model for RAG. Create a typed deployment with `Type: Embedding`, or define one in `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "Connections": {
            "default": {
              "Deployments": [
                { "Name": "gpt-4o", "Type": "Chat", "IsDefault": true },
                { "Name": "text-embedding-3-small", "Type": "Embedding", "IsDefault": true },
                { "Name": "gpt-4o-mini", "Type": "Utility", "IsDefault": true },
                { "Name": "dall-e-3", "Type": "Image", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

### Supported Document Formats

| Format | Extension | Required Feature |
|--------|-----------|------------------|
| PDF | .pdf | `CrestApps.OrchardCore.AI.Chat.Interactions.Pdf` |
| Word | .docx | `CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml` |
| Excel | .xlsx | `CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml` |
| PowerPoint | .pptx | `CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml` |
| Text | .txt | Built-in |
| CSV | .csv | Built-in |
| Markdown | .md | Built-in |
| JSON | .json | Built-in |
| XML | .xml | Built-in |
| HTML | .html, .htm | Built-in |
| YAML | .yml, .yaml | Built-in |

Legacy Office formats (.doc, .xls, .ppt) are not supported. Convert them to newer formats.

### Document Intent Types

When documents are uploaded, the intent detector routes prompts to specialized strategies:

| Intent | Description | Example Prompts |
|--------|-------------|-----------------|
| `DocumentQnA` | Question answering using RAG | "What does this document say about X?" |
| `SummarizeDocument` | Document summarization | "Summarize this document" |
| `AnalyzeTabularData` | CSV/Excel data analysis | "Calculate the total sales" |
| `ExtractStructuredData` | Structured data extraction | "Extract all email addresses" |
| `CompareDocuments` | Multi-document comparison | "Compare these two documents" |
| `TransformFormat` | Content reformatting | "Convert to bullet points" |
| `GeneralChatWithReference` | General chat using document context | Default fallback |

### Adding a Custom Processing Strategy

Register a custom intent and strategy to extend prompt routing:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPromptProcessingIntent(
            "TranslateDocument",
            "The user wants to translate the document content to another language.");

        services.AddPromptProcessingStrategy<TranslateDocumentStrategy>();
    }
}
```

### Enabling PDF and Office Document Support

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI.Chat.Interactions.Pdf",
        "CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml"
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
| `TextOnly` | Standard text-only chat (default) | None |
| `AudioInput` | Adds microphone button for speech-to-text dictation | `DefaultSpeechToTextDeploymentName` configured |
| `Conversation` | Two-way voice conversation | Both `DefaultSpeechToTextDeploymentName` and `DefaultTextToSpeechDeploymentName` configured |

Unlike AI profiles (configured per profile), chat interactions use a **single site-wide setting** that applies to all chat interaction sessions.

### SignalR Hub Methods (ChatInteractionHub)

| Method | Description |
|--------|-------------|
| `SendMessage` | Sends a text message |
| `SendAudioStream` | Streams audio chunks for speech-to-text transcription |
| `StartConversation` | Starts a full two-way voice conversation |
| `SynthesizeSpeech` | Converts text to speech audio |
| `UpdateAgents` | Updates agent selection for a session |
| `ClearHistory` | Clears chat history for a session |

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

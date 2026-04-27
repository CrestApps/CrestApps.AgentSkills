---
name: orchardcore-ai-documents
description: Skill for configuring AI Documents in Orchard Core with CrestApps modules. Covers document upload, text extraction, chunking, vector indexing, and Retrieval-Augmented Generation (RAG) for AI chat. Supports PDF, OpenXml (docx, xlsx, pptx), and plain-text formats with file-system or Azure Blob storage and Azure AI Search or Elasticsearch indexing backends. Use it for CrestApps.OrchardCore.AI.Documents, AI document processing, document upload flow, RAG pipelines, vector search, embedding configuration, and related Orchard Core setup, extension, or troubleshooting work.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Documents - Prompt Templates

## Configure AI Documents

You are an Orchard Core expert. Generate code, configuration, and recipes for adding document processing, storage, indexing, and Retrieval-Augmented Generation (RAG) capabilities to an Orchard Core application using CrestApps AI Documents modules.

### Guidelines

- The AI Documents modules provide document upload, text extraction, chunking, vector indexing, and RAG search for AI conversations.
- The base feature `CrestApps.OrchardCore.AI.Documents` is `EnabledByDependencyOnly` and is activated automatically when you enable one of the higher-level features.
- Document processing follows the pipeline Upload → Extract Text → Chunk → Embed → Index → Vector Search.
- An embedding deployment (e.g., `text-embedding-3-small`) must be configured for chunking and indexing to work.
- Each indexing backend (Azure AI Search or Elasticsearch) registers a keyed `IVectorSearchService` implementation.
- Install all CrestApps NuGet packages in the web/startup project.
- Always secure connection strings and API keys using user secrets or environment variables.

### Feature IDs

| Feature ID | Module | Description |
|------------|--------|-------------|
| `CrestApps.OrchardCore.AI.Documents` | AI Documents | Foundation for document processing, text extraction, and RAG (enabled by dependency only) |
| `CrestApps.OrchardCore.AI.Documents.ChatInteractions` | AI Documents | Document upload and RAG for AI Chat Interactions |
| `CrestApps.OrchardCore.AI.Documents.Profiles` | AI Documents | Document upload and RAG for AI Profiles |
| `CrestApps.OrchardCore.AI.Documents.ChatSessions` | AI Documents | Document upload and RAG for AI Chat Sessions and Widgets |
| `CrestApps.OrchardCore.AI.Documents.Azure` | AI Documents - Azure Blob Storage | Stores uploaded documents in Azure Blob Storage |
| `CrestApps.OrchardCore.AI.Documents.AzureAI` | AI Documents - Azure AI Search | Indexes document chunks in Azure AI Search |
| `CrestApps.OrchardCore.AI.Documents.Elasticsearch` | AI Documents - Elasticsearch | Indexes document chunks in Elasticsearch |
| `CrestApps.OrchardCore.AI.Documents.Pdf` | AI Documents (PDF) | Adds PDF file support |
| `CrestApps.OrchardCore.AI.Documents.OpenXml` | AI Documents (OpenXml) | Adds OpenXml file support (docx, xlsx, pptx) |

### NuGet Packages

| Package | Purpose |
|---------|---------|
| `CrestApps.OrchardCore.AI.Documents` | Core document processing, stores, and display drivers |
| `CrestApps.OrchardCore.AI.Documents.Azure` | Azure Blob Storage backend for document files |
| `CrestApps.OrchardCore.AI.Documents.AzureAI` | Azure AI Search indexing and vector search |
| `CrestApps.OrchardCore.AI.Documents.Elasticsearch` | Elasticsearch indexing and vector search |
| `CrestApps.OrchardCore.AI.Documents.Pdf` | PDF text extraction |
| `CrestApps.OrchardCore.AI.Documents.OpenXml` | OpenXml text extraction (docx, xlsx, pptx) |

### Enabling AI Documents for Chat Interactions with Azure AI Search

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Chat.Interactions",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.AzureAI",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Enabling AI Documents for Chat Interactions with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
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

### Enabling AI Documents for Profiles

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Documents.Profiles",
        "CrestApps.OrchardCore.AI.Documents.AzureAI",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Enabling AI Documents for Chat Sessions

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Documents.ChatSessions",
        "CrestApps.OrchardCore.AI.Documents.AzureAI",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml",
        "CrestApps.OrchardCore.OpenAI"
      ],
      "disable": []
    }
  ]
}
```

### Document Upload Flow

The document processing pipeline follows these steps:

1. **Upload** - User uploads a file via the chat interaction, profile, or chat session UI. The file is saved using `IDocumentFileStore` (file system by default, or Azure Blob if configured).
2. **Extract Text** - The document processor extracts plain text. PDF and OpenXml formats require their respective feature modules.
3. **Chunk** - Extracted text is split into smaller chunks stored via `IAIDocumentChunkStore`.
4. **Embed** - Each chunk is sent to the configured embedding deployment (e.g., `text-embedding-3-small`) to generate vector embeddings.
5. **Index** - Chunks with embeddings are indexed into the configured search backend (Azure AI Search or Elasticsearch) via `AIDocumentsIndexingService`.
6. **Search** - At query time, the user prompt is embedded and a vector similarity search retrieves relevant chunks to augment the AI response (RAG).

### Supported Document Formats

| Format | Extensions | Required Feature |
|--------|------------|------------------|
| PDF | .pdf | `CrestApps.OrchardCore.AI.Documents.Pdf` |
| Word | .docx | `CrestApps.OrchardCore.AI.Documents.OpenXml` |
| Excel | .xlsx | `CrestApps.OrchardCore.AI.Documents.OpenXml` |
| PowerPoint | .pptx | `CrestApps.OrchardCore.AI.Documents.OpenXml` |
| Plain Text | .txt | Built-in |
| CSV | .csv | Built-in |
| Markdown | .md | Built-in |
| JSON | .json | Built-in |
| XML | .xml | Built-in |
| HTML | .html, .htm | Built-in |
| YAML | .yml, .yaml | Built-in |

Legacy Office formats (.doc, .xls, .ppt) are not supported. Convert them to their modern equivalents first.

### Storage Backends

#### File System (Default)

By default, uploaded documents are stored on the local file system under `{WebRootPath}/{TenantName}/AIDocuments`. No additional configuration is required.

#### Azure Blob Storage

Enable the `CrestApps.OrchardCore.AI.Documents.Azure` feature and configure `appsettings.json`:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "AzureDocuments": {
          "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
          "ContainerName": "ai-documents",
          "CreateContainer": true
        }
      }
    }
  }
}
```

The Azure module replaces `IDocumentFileStore` with a `BlobFileStore`-backed implementation. The `AIDocumentBlobContainerTenantEvents` handler manages container lifecycle for multi-tenant setups.

### Indexing Backends

#### Azure AI Search

Enable `CrestApps.OrchardCore.AI.Documents.AzureAI`. This registers:

- `AIDocumentAzureAISearchDocumentIndexHandler` as an `IDocumentIndexHandler`
- `AzureAISearchVectorSearchService` as a keyed `IVectorSearchService`
- An indexing source named "AI Documents (Azure AI Search)" under `Search → Indexing`

#### Elasticsearch

Enable `CrestApps.OrchardCore.AI.Documents.Elasticsearch`. This registers:

- `AIDocumentElasticsearchDocumentIndexHandler` as an `IDocumentIndexHandler`
- `ElasticsearchVectorSearchService` as a keyed `IVectorSearchService`
- An indexing source named "AI Documents (Elasticsearch)" under `Search → Indexing`

### Setting Up Document Indexing

1. Enable one of the indexing backend features (`CrestApps.OrchardCore.AI.Documents.AzureAI` or `CrestApps.OrchardCore.AI.Documents.Elasticsearch`).
2. Navigate to **Search → Indexing** in the admin and create a new index selecting the "AI Documents" source.
3. Configure an embedding deployment (see below).
4. Enable the desired document context feature (`ChatInteractions`, `Profiles`, or `ChatSessions`).

### Configuring the Embedding Model

Document indexing requires an embedding deployment. Create one via admin UI or define it in `appsettings.json`:

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
                { "Name": "text-embedding-3-small", "Type": "Embedding", "IsDefault": true }
              ]
            }
          }
        }
      }
    }
  }
}
```

### Integration with Chat Sessions, Profiles, and Interactions

#### Chat Interactions

The `CrestApps.OrchardCore.AI.Documents.ChatInteractions` feature adds document upload endpoints and display drivers to the Chat Interactions UI. Users can upload documents directly in an ad-hoc chat session, and the system indexes them for RAG queries.

- Upload endpoint registered via `AddUploadChatInteractionDocumentEndpoint`
- Remove endpoint registered via `AddRemoveChatInteractionDocumentEndpoint`
- Display driver `ChatInteractionDocumentsDisplayDriver` renders the upload UI

#### Profiles

The `CrestApps.OrchardCore.AI.Documents.Profiles` feature lets administrators attach documents to AI Profiles and Profile Templates. Documents uploaded at the profile level are available to all chat sessions that use that profile.

- `AIProfileDocumentsDisplayDriver` adds a documents tab to the profile editor
- `AIProfileTemplateDocumentsDisplayDriver` adds a documents tab to profile templates

#### Chat Sessions

The `CrestApps.OrchardCore.AI.Documents.ChatSessions` feature adds per-session document upload for AI Chat Sessions and Widgets. When a chat session is deleted, the `AIChatSessionDocumentCleanupHandler` automatically removes associated documents.

- Upload endpoint registered via `AddUploadChatSessionDocumentEndpoint`
- Remove endpoint registered via `AddRemoveChatSessionDocumentEndpoint`
- `AIProfileSessionDocumentsDisplayDriver` adds documents to session configuration

### Key Services

| Service | Description |
|---------|-------------|
| `IAIDocumentStore` | Manages document metadata (CRUD operations) |
| `IAIDocumentChunkStore` | Manages document text chunks and their embeddings |
| `IDocumentFileStore` | File storage abstraction (file system or Azure Blob) |
| `IVectorSearchService` | Vector similarity search (Azure AI Search or Elasticsearch, keyed by provider) |
| `AIDocumentsIndexingService` | Orchestrates indexing of document chunks into search backends |
| `IDocumentIndexHandler` | Builds index documents from chunk data for a specific provider |
| `IAIChatDocumentEventHandler` | Handles document lifecycle events |
| `IAIChatSessionHandler` | Session lifecycle (includes cleanup of documents on session delete) |

### Custom Document Event Handler

Implement `IAIChatDocumentEventHandler` to react to document lifecycle events:

```csharp
public sealed class MyDocumentEventHandler : IAIChatDocumentEventHandler
{
    public Task DocumentUploadedAsync(AIDocumentEventContext context)
    {
        // Runs after a document is uploaded and processed.
        return Task.CompletedTask;
    }

    public Task DocumentRemovedAsync(AIDocumentEventContext context)
    {
        // Runs after a document is removed.
        return Task.CompletedTask;
    }
}
```

Register the handler in your module startup:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAIChatDocumentEventHandler, MyDocumentEventHandler>();
    }
}
```

### Security

Document upload and removal endpoints are protected by authorization handlers:

- `OrchardChatInteractionDocumentAuthorizationHandler` controls access for chat interaction documents.
- `OrchardAIChatSessionDocumentAuthorizationHandler` controls access for chat session documents.

These handlers are registered automatically when the base AI Documents feature is enabled.

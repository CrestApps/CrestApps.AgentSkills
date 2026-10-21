---
name: orchardcore-ai-documents
description: Skill for CrestApps AI Documents in Orchard Core 4.0 (CrestApps.OrchardCore 3.0). Covers document upload, text and figure extraction, chunking, embedding and RAG for chat interactions, AI profiles, profile templates, chat sessions and widgets, the AI Documents index with its embedding deployment, InteractionDocumentSettings and DocumentsMetadata, Chunk and Hierarchical retrieval, the largest indexable document, figure descriptions with the vision slot, upload scanning through Orchard Core FileCreationService and ClamAV, file storage, document cleanup, the PowerPoint, Word and PDF agents, and IAIChatDocumentEventHandler. Use it for CrestApps.OrchardCore.AI.Documents, CrestApps.OrchardCore.AI.Documents.ChatInteractions, CrestApps.OrchardCore.AI.Documents.Profiles, CrestApps.OrchardCore.AI.Documents.ChatSessions, IAIDocumentStore, IDocumentFileStore, IVectorSearchService, IUploadedFileScanner, OrchardUploadedFileScanner, and AIProfileSessionDocumentsMetadata.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core AI Documents

## Configure document upload and RAG

You are an Orchard Core expert. Generate configuration, recipes, and code that let users and administrators attach documents to CrestApps AI chat surfaces, and let the model retrieve from them.

### Guidelines

- The base feature `CrestApps.OrchardCore.AI.Documents` is `EnabledByDependencyOnly`. It depends on `CrestApps.OrchardCore.AI.Chat.Interactions` and `OrchardCore.Indexing`, and comes along with any of the sub-features below.
- Embeddable documents follow upload, scan, extract, chunk, embed, index, and vector search. Tabular files (`.csv`, `.tsv`, `.xlsx`, `.xls`) are non-embeddable; they are loaded into the conversation's tabular workspace instead.
- Indexing needs an **AI Documents** index profile from `CrestApps.OrchardCore.AI.Documents.Elasticsearch` or `CrestApps.OrchardCore.AI.Documents.AzureAI` (see `orchardcore-ai-documents-elasticsearch` and `orchardcore-ai-documents-azureai`).
- The AI Documents index names its embedding deployment. The picker lists deployments in the `embedding` slot, so the deployment must declare `textEmbedding`. It is locked once the index exists.
- Pick the active index once. Changing it later loses access to documents of existing sessions.
- PDF and Office formats need the extractor features (see `orchardcore-ai-documents-extractors`). Azure Blob Storage is optional (see `orchardcore-ai-documents-azure-storage`).
- Every upload is scanned before it is stored. Enable `OrchardCore.Antivirus` to add ClamAV.
- Install packages in the web/startup project.

### Features

| Feature | Feature ID | Depends on |
|---|---|---|
| AI Documents | `CrestApps.OrchardCore.AI.Documents` | `CrestApps.OrchardCore.AI.Chat.Interactions`, `OrchardCore.Indexing` (dependency only) |
| AI Documents for Chat Interactions | `CrestApps.OrchardCore.AI.Documents.ChatInteractions` | AI Documents |
| AI Documents for Profiles | `CrestApps.OrchardCore.AI.Documents.Profiles` | AI Documents, `CrestApps.OrchardCore.AI.Chat.Core` |
| AI Documents for Chat Sessions | `CrestApps.OrchardCore.AI.Documents.ChatSessions` | AI Documents, `CrestApps.OrchardCore.AI.Chat` |
| AI Documents - Azure Blob Storage | `CrestApps.OrchardCore.AI.Documents.Azure` | AI Documents |
| AI Documents indexing using Azure AI Search | `CrestApps.OrchardCore.AI.Documents.AzureAI` | AI Documents, `OrchardCore.AzureAI` |
| AI Documents indexing using Elasticsearch | `CrestApps.OrchardCore.AI.Documents.Elasticsearch` | AI Documents, `OrchardCore.Elasticsearch` |
| AI Documents (PDF) | `CrestApps.OrchardCore.AI.Documents.Pdf` | none |
| AI Documents (OpenXml) | `CrestApps.OrchardCore.AI.Documents.OpenXml` | none |

### Enable documents everywhere with Elasticsearch

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Elasticsearch",
        "CrestApps.OrchardCore.AI.Documents.Elasticsearch",
        "CrestApps.OrchardCore.AI.Documents.ChatInteractions",
        "CrestApps.OrchardCore.AI.Documents.Profiles",
        "CrestApps.OrchardCore.AI.Documents.ChatSessions",
        "CrestApps.OrchardCore.AI.Documents.Pdf",
        "CrestApps.OrchardCore.AI.Documents.OpenXml"
      ],
      "disable": []
    }
  ]
}
```

Swap in `OrchardCore.AzureAI` and `CrestApps.OrchardCore.AI.Documents.AzureAI` for Azure AI Search, and keep only the sub-features you need.

### Setup

1. Create an embedding deployment that declares `textEmbedding` (see `orchardcore-ai-model-capabilities`).
2. Go to **Search > Indexing**, add **AI Documents (Elasticsearch)** or **AI Documents (Azure AI Search)**, and pick the embedding deployment.
3. In **Settings > Artificial Intelligence**, open the documents section and select that **Index profile**.
4. Upload on the **Knowledge** tab of a chat interaction or AI profile, or in a chat session when the profile allows it.

### Site settings

The documents section is stored as `InteractionDocumentSettings` and guarded by `ManageChatInteractionSettings`:

| Field | Property | Default |
|---|---|---|
| **Index profile** | `IndexProfileName` | none |
| Top matching chunks | `TopN` | 3 |
| **Document retrieval mode** | `RetrievalMode` (`Chunk` or `Hierarchical`) | `Chunk` |
| **Allow document uploads in chat interactions** | `AllowDocumentUploads` | `true` |
| **Allow image uploads in chat interactions** | `AllowImageUploads` (needs a vision deployment) | `false` |
| **Largest document that can be indexed** | `MaxIndexableCharacters` (characters of extracted text, `0` means no limit) | 50000 |
| **Describe figures in uploaded documents** | `DescribeFiguresInUploads` (needs a deployment in the `vision` slot) | `true` |

```json
{
  "steps": [
    {
      "name": "Settings",
      "InteractionDocumentSettings": {
        "IndexProfileName": "AIDocuments",
        "TopN": 3,
        "RetrievalMode": "Chunk",
        "AllowDocumentUploads": true,
        "AllowImageUploads": false,
        "MaxIndexableCharacters": 50000,
        "DescribeFiguresInUploads": true
      }
    }
  ]
}
```

`Chunk` keeps chunk-level context. `Hierarchical` matches on chunks, then injects the full text of the matched documents. A document over the indexable limit is refused at upload instead of being stored unsearchable. Saving these settings still releases the tenant, because their readers in CrestApps.Core still use `IOptions<InteractionDocumentOptions>`.

### Profile documents

AI Documents for Profiles adds an upload area to the **Knowledge** tab of AI profiles and profile-source templates. Profile documents are background knowledge shared by every session of the profile; files are uploaded, extracted, embedded, and removed atomically when the profile is saved. Only embeddable extensions are accepted.

The profile stores its settings in `DocumentsMetadata`:

| Property | Meaning |
|---|---|
| `Documents` | The attached documents (`ChatDocumentInfo`) |
| `DocumentTopN` | Chunks to include, default 3 |
| `RetrievalMode` | `Chunk` or `Hierarchical`, blank uses the site default |
| `MaxIndexableCharacters` | Blank uses the site limit |
| `DescribeFiguresInUploads` | Blank follows the site setting |

A profile created from a template gets its own copies of the template's documents through `IAIProfileTemplateApplicationHandler`, and the copies are indexed.

### Session documents

AI Documents for Chat Sessions adds attachment to admin and front-end chat sessions and widgets. It is off per profile until you enable it on the **Knowledge** tab (`AIProfileSessionDocumentsMetadata`):

```json
{
  "steps": [
    {
      "name": "AIProfile",
      "profiles": [
        {
          "Name": "support-chat",
          "DisplayText": "Support Chat",
          "Type": "Chat",
          "Properties": {
            "AIProfileSessionDocumentsMetadata": {
              "AllowSessionDocuments": true,
              "AllowSessionImageUploads": false
            }
          }
        }
      ]
    }
  ]
}
```

The **Artificial Intelligence Chat** widget follows the settings of the profile it uses. Image uploads also require a vision deployment.

### Upload scanning

`OrchardUploadedFileScanner` implements CrestApps.Core's `IUploadedFileScanner` and routes every upload from chat interactions, profiles, sessions, and widgets through Orchard Core's `FileCreationService` before storage. Each registered `IFileEventHandler` can reject the file, including the ClamAV handler of `OrchardCore.Antivirus`. Rejections and scan failures fail closed and are logged at `Warning`. Add your own policy with an `IFileEventHandler` (see `orchardcore-file-upload-security` and `orchardcore-antivirus`).

### Storage and cleanup

- Files go through `IDocumentFileStore`, by default `wwwroot/<tenant>/AIDocuments`.
- Deleting a chat interaction removes its chunks and `AIDocument` records; deleting a chat session removes its documents in a deferred task (`AIChatSessionDocumentCleanupHandler`); removing a profile document cleans its chunks on save.
- Download endpoints are mapped by the base feature. An answer that cites a figure writes `[fig:N]`, which chat surfaces render as an image served by the figure endpoint; the same rule as the document download decides who can see it.

### Document agents

| Agent | Comes with | Does |
|---|---|---|
| PowerPoint | AI Documents (OpenXml); PDF export also needs AI Documents (PDF) | Creates, edits, designs, reviews, previews, and exports `.pptx` decks |
| Word | AI Documents (OpenXml) | Writes, edits, reviews, previews, and exports `.docx` documents |
| PDF | AI Documents (PDF) | Creates, converts, edits, previews, and analyses PDFs |

They are system agents, always available to the primary model and not listed in agent pickers. Previews return as images that the chat UI stacks into a carousel.

### React to document events

```csharp
public sealed class AuditDocumentEventHandler : IAIChatDocumentEventHandler
{
    public Task UploadedAsync(AIChatDocumentUploadContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemovedAsync(AIChatDocumentRemoveContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAIChatDocumentEventHandler, AuditDocumentEventHandler>();
    }
}
```

### Key services

| Service | Role |
|---|---|
| `IAIDocumentStore` | Document records for every reference type (`ReferenceId`, `ReferenceType`) |
| `IDocumentFileStore` | File storage, replaced by the Azure Blob feature |
| `IVectorSearchService` | Keyed by provider name, filtered by reference |
| `AIDocumentsIndexingService` | Writes chat interaction chunks to the index |
| `IAIChatDocumentEventHandler` | Upload and removal events |
| `OrchardChatInteractionDocumentAuthorizationHandler`, `OrchardAIChatSessionDocumentAuthorizationHandler`, `OrchardAIProfileDocumentAuthorizationHandler` | Decide who may upload, remove, and download |

The shared pipeline (readers, chunking, figure processing) is described in `crestapps-core-document-processing`.

### Upgrading from 2.x

- Declare `textEmbedding` on embedding deployments instead of `"Purpose": "Embedding"`. Legacy records are projected automatically.
- Uploads are now scanned through `FileCreationService`; a custom `IFileEventHandler` that rejects files now also blocks AI document uploads.
- The profile editor's **Apply Templates** card is gone. Start from a template with the **New AI Profile** picker, which now copies and indexes the template's documents.
- Code that reads the documents settings must keep using `InteractionDocumentOptions` from CrestApps.Core; this settings screen still releases the shell on save.

### Troubleshooting

| Symptom | Check |
|---|---|
| **Index not configured** warning | Select an index profile in **Settings > Artificial Intelligence**. |
| No **AI Documents** index type | Enable `CrestApps.OrchardCore.AI.Documents.Elasticsearch` or `.AzureAI`. |
| **Vector Search Service Not Available** | The selected index's provider feature is not enabled. |
| Upload refused | The scan rejected it, the type has no reader, or the extracted text is over the indexable limit. |
| No attach button in a session | Enable **Allow session document uploads** or **Allow session image uploads** on the profile. |
| Figures never described | No deployment fills the `vision` slot, or figure description is turned off. |

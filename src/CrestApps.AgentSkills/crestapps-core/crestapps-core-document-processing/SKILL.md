---
name: crestapps-core-document-processing
description: Skill for uploaded document ingestion, document RAG, readers, and document downloads in CrestApps.Core.
---

# CrestApps.Core Document Processing - Prompt Templates

## Add Document Processing

You are a CrestApps.Core expert. Generate code and guidance for document ingestion and document RAG in CrestApps.Core.

### Guidelines

- Use `CrestApps.Core.AI.Documents` for the document pipeline.
- Add OpenXml and Pdf support explicitly because they are opt-in packages.
- Add Markdown normalization explicitly when Markdig-backed normalization is needed.
- Provide stores on the document-processing builder.
- Use `AddReferenceDownloads()` plus `AddDownloadAIDocumentEndpoint()` when citations should become downloadable links.

### Builder Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMarkdown()
        .AddChatInteractions()
        .AddDocumentProcessing(documentProcessing => documentProcessing
            .AddEntityCoreStores()
            .AddOpenXml()
            .AddPdf()
            .AddReferenceDownloads()
        )
        .AddOpenAI()
    )
    .AddEntityCoreSqliteDataStore("Data Source=app.db")
);

app.AddChatApiEndpoints()
    .AddDownloadAIDocumentEndpoint();
```

### Built-in Reader Coverage

| Reader | Formats |
|---|---|
| Plain text reader | `.txt`, `.md`, `.json`, `.xml`, `.html`, `.htm`, `.log`, `.yaml`, `.yml`, `.csv` |
| OpenXml reader | `.docx`, `.pptx`, `.xlsx` |
| Pdf reader | `.pdf` |

### Built-in Document Tools

- `SearchDocumentsTool`
- `ReadDocumentTool`
- `ReadTabularDataTool`

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
- `AddChatApiEndpoints()` is internal MVC and Blazor sample-host composition. Custom hosts should map `AddDownloadAIDocumentEndpoint()` directly and add their own chat and upload endpoints as needed.

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

app.AddDownloadAIDocumentEndpoint();
```

`AddDownloadAIDocumentEndpoint()` maps `GET ai/documents/{documentId}/download` and authorizes access to the referenced document. It is the shared document endpoint; the sample hosts' chat endpoints are not required to register it.

### Built-in Reader Coverage

| Reader | Formats |
|---|---|
| Plain text reader | `.txt`, `.md`, `.json`, `.xml`, `.html`, `.htm`, `.log`, `.yaml`, `.yml`, `.csv` |
| OpenXml reader | `.docx`, `.pptx`, `.xlsx` |
| Pdf reader | `.pdf` |

### Built-in Document Tools

**Document retrieval**

- `SearchDocumentsTool` — semantic/keyword search across ingested documents.
- `ReadDocumentTool` — reads the full text of an ingested document.
- `GetDocumentMetadataTool` — returns stored metadata for a document.
- `InspectImageTool` — inspects/describes an image document.

**Tabular data (spreadsheets/CSV)**

- `ListTabularDataTool` — lists available tables/sheets in a tabular document.
- `QueryTabularDataTool` — queries rows from a tabular document.
- `ExecuteTabularCommandTool` — runs a command/transformation over tabular data.
- `FillEmptyTabularCellsTool` — fills empty cells in tabular data.
- `ExportTabularDataTool` — exports tabular data to a file.

The built-in Tabular Data Agent delegates to these tools for analysis,
calculations, filtering, aggregation, transformations, and exports over
uploaded tabular files. See `crestapps-core-ai-agents` for the
`IAIProfileProvider` pattern used by this code-defined system agent.

**File generation**

- `GenerateFileTool` — generates a downloadable file from AI output.

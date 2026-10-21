---
name: crestapps-core-document-processing
description: Skill for CrestApps.Core 2.0 document processing and the separate ingestion path. Covers AddDocumentProcessing with OpenXml, Pdf, Word and reference downloads, AddCoreAIDocumentIngestion with AddPlainTextReader, AddFigureProcessing, AddFigureBackfill, AddPdf and AddDocumentIntelligence, the IAIDocumentIngestionPipeline and AIDocumentIngestionProcessor extension points, custom IngestionDocumentReader registration, PDF layout analysis with PdfLayoutOptions, figure captions, salience and vision transcription, upload limits through InteractionDocumentOptions and DocumentsMetadata, the document and figure download endpoints, the built-in document and tabular tools, and upgrading 1.x hosts whose types moved to CrestApps.Core.AI.Ingestion.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Document Processing

Use this skill when a host lets users upload documents into chat sessions, chat interactions or AI profiles, or when it needs to read files into an AI knowledge base without the chat stack. For the Presentation, Word and PDF agents see `crestapps-core-document-agents`. For scheduled folder and file-server intake see `crestapps-core-file-sources`.

## Two packages, two jobs

| Package | Holds | Register with |
|---|---|---|
| `CrestApps.Core.AI.Ingestion` | readers, the processor pipeline, `IDocumentFileStore`, `IKnowledgeIngestionService` | `AddCoreAIDocumentIngestion(...)` |
| `CrestApps.Core.AI.Ingestion.Pdf` | the PdfPig PDF reader and `PdfLayoutOptions` | `AddPdf()` on the ingestion builder |
| `CrestApps.Core.AI.Documents` | uploads, tabular workspaces, generated files, the chat tools, the Tabular Data Agent | `AddDocumentProcessing(...)` / `AddCoreAIDocumentProcessing()` |
| `CrestApps.Core.AI.Documents.OpenXml` | `.docx`, `.xlsx`, `.pptx`, `.potx` reader, Office writers, Presentation Agent | `AddOpenXml()` |
| `CrestApps.Core.AI.Documents.Pdf` | PDF reader plus PDF writer and PDF Agent | `AddPdf()` on the document-processing builder |
| `CrestApps.Core.AI.Documents.Word` | Word Agent | `AddWord()` |
| `CrestApps.Core.Azure.DocumentIntelligence` | Azure AI Document Intelligence reader | `AddDocumentIntelligence(...)` on the ingestion builder |

`AddCoreAIDocumentProcessing()` calls `AddCoreAIDocumentIngestion(i => i.AddPlainTextReader().AddFigureProcessing().AddFigureBackfill())` for you, so a chat host does not register the ingestion half separately.

## Chat Document Processing

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMarkdown()
        .AddChatInteractions(chatInteractions => chatInteractions
            .AddEntityCoreStores())
        .AddDocumentProcessing(documentProcessing => documentProcessing
            .AddEntityCoreStores()
            .AddOpenXml()
            .AddPdf()
            .AddWord()
            .AddReferenceDownloads())
        .AddOpenAI())
    .AddEntityCoreSqliteDataStore("Data Source=app.db"));

var app = builder.Build();

app.AddDownloadAIDocumentEndpoint()
    .AddDownloadAIDocumentFigureEndpoint()
    .AddUploadChatInteractionDocumentEndpoint()
    .AddRemoveChatInteractionDocumentEndpoint()
    .AddUploadChatSessionDocumentEndpoint()
    .AddRemoveChatSessionDocumentEndpoint();
```

- Use `AddYesSqlStores()` instead of `AddEntityCoreStores()` on the document-processing builder for YesSql hosts. Both register the document, chunk and data source stores.
- `AddReferenceDownloads()` registers the citation link resolvers (`DocumentAIReferenceLinkResolver` and the `File` data source `FileReferenceLinkResolver`).
- The endpoint extensions live in `CrestApps.Core.AI.Documents.Endpoints`.

| Endpoint extension | Route |
|---|---|
| `AddDownloadAIDocumentEndpoint()` | `GET ai/documents/{documentId}/download` |
| `AddDownloadAIDocumentFigureEndpoint()` | `GET ai/documents/{documentId}/figures/{figureId}` |
| `AddDownloadKnowledgeFigureEndpoint()` | `GET ai/knowledge/{dataSourceId}/figures/{canonicalId}` |

Download and figure endpoints authorize through the resource-based `AIChatDocumentOperations.ManageDocuments` requirement (chat interaction, chat session and AI profile documents). Knowledge figures use `AIKnowledgeOperations.ViewFigures` against the owning `AIDataSource`. Register an `AuthorizationHandler<OperationAuthorizationRequirement, TResource>` that grants them.

`AddChatApiEndpoints()` is internal composition in the MVC and Blazor sample hosts. Custom hosts map the endpoint extensions above directly.

## Ingestion Without Chat

A host that only reads files into a knowledge base registers the ingestion half on its own. None of the upload, tabular or chat tool services come with it.

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIDocumentIngestion(ingestion => ingestion
        .AddPlainTextReader()
        .AddFigureProcessing()
        .AddFigureBackfill()
        .AddPdf());
```

| Builder call | Registers | Leave out when |
|---|---|---|
| always | pipeline, reader resolver, `IDocumentFileStore`, `IImageAnalysisService`, `IKnowledgeIngestionService`, the `File` source handler | never |
| `AddPlainTextReader()` | `.txt`, `.csv`, `.md`, `.json`, `.xml`, `.html`, `.htm`, `.log`, `.yaml`, `.yml` | those types never arrive |
| `AddFigureProcessing()` | `FigureCaptionProcessor`, `FigureSalienceProcessor`, `FigureDescriptionProcessor` | you want no vision calls |
| `AddFigureBackfill()` | `IFigureDescriptionBackfillService` and its hosted job | you schedule backfill yourself |
| `AddPdf()` | PDF reader only (no writer, no agent) | you read no PDFs |
| `AddDocumentIntelligence(...)` | Azure layout reader wrapped in `FallbackIngestionDocumentReader` | no Azure endpoint |

Office files are read by `CrestApps.Core.AI.Documents.OpenXml`, which depends on document processing, so a host ingesting `.docx`, `.xlsx` or `.pptx` still registers `AddDocumentProcessing(...).AddOpenXml()`.

Keyed readers resolve to the last registration. When a host uses both chat document processing and file sources, call `AddDocumentProcessing()` before `AddCoreFileSources()` so the HTML reader registered by file sources wins `.html` over the plain-text reader.

## Custom Readers and Processors

```csharp
builder.Services.AddCoreAIIngestionDocumentReader<RtfIngestionDocumentReader>(".rtf");
builder.Services.AddCoreAIIngestionDocumentProcessor<EmailRedactionProcessor>();
```

- `AddCoreAIIngestionDocumentReader<T>(params ExtractorExtension[])` registers a keyed singleton per extension and per inferred media type, and adds the extension to `ChatDocumentsOptions`. Use `new ExtractorExtension(".tsv", embeddable: false, isTabular: true)` for tabular formats.
- `AddCoreAIIngestionDocumentProcessor<T>()` registers a scoped processor. Processors run in registration order, so register yours before `AddDocumentProcessing()` to run ahead of the built-in figure processors.
- A processor must never fail an ingest. When it cannot do its work, log once and return the document unchanged.

```csharp
using System.Text.RegularExpressions;
using CrestApps.Core.AI.Ingestion;
using Microsoft.Extensions.DataIngestion;

public sealed partial class EmailRedactionProcessor : AIDocumentIngestionProcessor
{
    public override Task<IngestionDocument> ProcessAsync(
        IngestionDocument document,
        DocumentIngestionContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var section in document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var paragraph in section.Elements.OfType<IngestionDocumentParagraph>())
            {
                if (!string.IsNullOrEmpty(paragraph.Text))
                {
                    paragraph.Text = EmailPattern().Replace(paragraph.Text, "[email]");
                }
            }
        }

        return Task.FromResult(document);
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.]+")]
    private static partial Regex EmailPattern();
}
```

`DocumentIngestionContext` carries `FigureMode`, `VisionDeploymentName`, `UtilityDeploymentName`, `MaxFigureDescriptionsPerDocument` (default 25), `Language`, `DescribeFiguresInline` and `DataSourceId`. The single-argument `ProcessAsync` is sealed and runs with `DocumentIngestionContext.Default`.

See [references/ingestion-pipeline.md](references/ingestion-pipeline.md) for a custom reader, reader resolution, PDF layout analysis, captions, salience, transcription and Azure Document Intelligence.

## Upload Limits

The framework reads upload settings from `InteractionDocumentOptions` (the sample hosts map their `InteractionDocumentSettings` site settings onto it).

```csharp
builder.Services.Configure<InteractionDocumentOptions>(options =>
{
    options.MaxIndexableCharacters = 50000;   // 0 means no limit
    options.DescribeFiguresInUploads = true;  // false keeps figures but skips vision transcription
    options.TopN = 3;
    options.AllowImageUploads = true;
});
```

`ChatDocumentsOptions` lists the allowed, embeddable and tabular extensions, but its `Add` methods are internal. An extension becomes uploadable by registering a reader for it with `AddCoreAIIngestionDocumentReader<T>(...)`. Use the public members such as `MaxVisionImageBytesPerFile`, `MaxVisionInputBytesPerRequest`, `AnalyzeImagesAtUpload` and the `GetAllowedFileExtensions(...)` and `IsAllowedFileExtension(...)` extensions to drive upload UI and server validation.

- A document whose extracted text exceeds `MaxIndexableCharacters` is refused at upload with its measured size and the limit. The measuring pass runs before ingestion with figure description off.
- `DescribeFiguresInUploads` maps to `FigureProcessingMode.Auto` or `Off`. Description resolves the `AIDeploymentSlotNames.Vision` slot, never the utility deployment, and does nothing when no vision deployment exists.
- Per-profile overrides live on `DocumentsMetadata.MaxIndexableCharacters` (`int?`) and `DocumentsMetadata.DescribeFiguresInUploads` (`bool?`), where `null` means use the site value. AI templates carry the same fields.

## Storage

Uploaded files, figure images and agent workspaces go through `IDocumentFileStore` (namespace `CrestApps.Core.AI.Ingestion`). The default is `FileSystemFileStore` under `DocumentFileSystemFileStoreOptions.BasePath`. Replace it with your own singleton for blob storage.

```csharp
builder.Services.AddSingleton<IDocumentFileStore, BlobDocumentFileStore>();
```

## Built-in Tools

| Tool | Name | Notes |
|---|---|---|
| `SearchDocumentsTool` | `search_documents` | vector search over attached documents |
| `ReadDocumentTool` | `read_document` | full text of one document |
| `GetDocumentMetadataTool` | `get_document_metadata` | metadata, tabular columns, and the figures a document holds |
| `ViewDocumentFigureTool` | `view_document_figure` | caption, page, link and markdown image for a figure, optional vision question |
| `InspectImageTool` | `inspect_image` | detailed look at an uploaded image |
| `GenerateFileTool` | `generate_file` | writes a downloadable file through `IGeneratedFileWriter` |

The hidden Tabular Data Agent (`tabular-data-agent`) tools are `list_tabular_data`, `query_tabular_data`, `preview_tabular_data`, `execute_tabular_command`, `fill_empty_tabular_cells`, `format_tabular_data`, `export_tabular_data` and `compare_tabular_data`. Worksheets load as separate tables, rollup rows go to a sibling `<table>_rollups` table, and `TabularPreviewOptions` caps the SVG preview.

Tool results that implement `IAIToolContentProvider` reach a chat model as text, while the MCP server keeps the rich content blocks.

## Upgrading from 1.x

- Change `using` directives for types that moved to `CrestApps.Core.AI.Ingestion`: `IDocumentFileStore`, `FileSystemFileStore`, `DocumentFileSystemFileStoreOptions`, `IImageAnalysisService`, `ChatDocumentsOptions` and `ExtractorExtension`. `AddCoreAIIngestionDocumentReader<T>` also moved there from `CrestApps.Core.AI.Documents`.
- The PdfPig reader moved from `CrestApps.Core.AI.Documents.Pdf` to `CrestApps.Core.AI.Ingestion.Pdf`. `AddPdf()` on the document-processing builder still registers reader and writer.
- `DefaultAIDocumentProcessingService` now takes `IAIDocumentIngestionPipeline` and `IDocumentFileStore`. Replace any custom subclass constructor accordingly.
- Derive processors from `AIDocumentIngestionProcessor` to receive `DocumentIngestionContext`.
- Deployment `Purpose` is gone. Vision transcription resolves the `vision` slot and needs a deployment that declares image input.
- Stored documents need no migration.
